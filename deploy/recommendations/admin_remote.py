"""Private administrator access through the project's pinned-key SSH transport."""
import argparse
import base64
import json
import os
from pathlib import Path
import select
import socketserver
import subprocess
import sys
import webbrowser

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from ssh_connection import connect

URL = "http://127.0.0.1:18083/admin"
REMOTE_CREDENTIALS = "/var/lib/ozl-recommendations/initial-credentials.json"


def is_reparse(path):
    if os.name == "nt":
        import ctypes
        attributes = ctypes.windll.kernel32.GetFileAttributesW(str(path))
        return attributes != -1 and attributes != 0xffffffff and bool(attributes & 0x400)
    return path.is_symlink()


def check_path(path):
    if any(is_reparse(part) for part in [path] + list(path.parents)):
        raise ValueError("Private credential paths must not contain links or reparse points")


def windows_acl_script(path, directory):
    # Only a destination path is encoded, never credentials. Build a fresh DACL,
    # rather than retaining preexisting explicit grants from an existing folder.
    encoded = base64.b64encode(str(path).encode("utf-8")).decode("ascii")
    kind = "Directory" if directory else "File"
    inheritance = "[System.Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'" if directory else "[System.Security.AccessControl.InheritanceFlags]::None"
    return ("$ErrorActionPreference='Stop';"
            "$target=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + encoded + "'));"
            "$owner=[Security.Principal.WindowsIdentity]::GetCurrent().User;"
            "$security=[System.Security.AccessControl." + kind + "Security]::new();"
            "$security.SetOwner($owner);$security.SetAccessRuleProtection($true,$false);"
            "$rule=[System.Security.AccessControl.FileSystemAccessRule]::new($owner,[System.Security.AccessControl.FileSystemRights]::FullControl," + inheritance + ",[System.Security.AccessControl.PropagationFlags]::None,[System.Security.AccessControl.AccessControlType]::Allow);"
            "$security.AddAccessRule($rule);[System.IO." + kind + "]::SetAccessControl($target,$security);")


def restrict_acl(path, directory):
    if os.name == "nt":
        script = base64.b64encode(windows_acl_script(path, directory).encode("utf-16-le")).decode("ascii")
        subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-EncodedCommand", script], check=True,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0), timeout=15)
    else:
        path.chmod(0o700 if directory else 0o600)


def secure_directory(path):
    check_path(path)
    path.mkdir(mode=0o700, parents=True, exist_ok=True)
    check_path(path)
    restrict_acl(path, True)


def save_credentials(value, directory=None):
    repository = Path(__file__).resolve().parents[2]
    destination = Path(directory) if directory is not None else Path(os.environ.get("LOCALAPPDATA", str(Path.home() / ".local/share"))) / "OrangeLauncher/recommendations-production"
    destination = Path(os.path.abspath(str(destination)))
    check_path(destination)
    resolved = destination.resolve()
    if resolved == repository or repository in resolved.parents:
        raise ValueError("Production credentials must stay outside the repository")
    if set(value) != {"username", "password"} or value["username"] != "admin" or not isinstance(value["password"], str) or not 32 <= len(value["password"]) <= 256 or any(ord(char) < 32 for char in value["password"]):
        raise ValueError("Initial credential file has an unexpected format")
    secure_directory(destination)
    target = destination / "initial-credentials.json"
    check_path(target)
    if target.exists():
        if json.loads(target.read_text(encoding="utf-8")) != value:
            raise ValueError("Different local production credentials already exist; preserved")
        restrict_acl(target, False)
        return target
    with os.fdopen(os.open(str(target), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "w", encoding="utf-8") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.flush()
        os.fsync(stream.fileno())
    restrict_acl(target, False)
    return target


def fetch_credentials(transport):
    import paramiko
    with paramiko.SFTPClient.from_transport(transport) as sftp:
        with sftp.open(REMOTE_CREDENTIALS, "rb") as source:
            payload = source.read(8193)
    if len(payload) > 8192:
        raise ValueError("Initial credential file is unexpectedly large")
    return save_credentials(json.loads(payload.decode("utf-8")))


def forward(transport, client, address):
    channel = None
    try:
        channel = transport.open_channel("direct-tcpip", ("127.0.0.1", 18083), address, timeout=15)
        if channel is None:
            return
        while True:
            ready, _, _ = select.select([client, channel], [], [], 1)
            for source, target in ((client, channel), (channel, client)):
                if source in ready:
                    data = source.recv(65536)
                    if not data:
                        return
                    target.sendall(data)
    except (OSError, EOFError):
        pass
    finally:
        if channel is not None:
            channel.close()
        client.close()


class Handler(socketserver.BaseRequestHandler):
    def handle(self):
        forward(self.server.transport, self.request, self.client_address)


class Tunnel(socketserver.ThreadingTCPServer):
    daemon_threads = True
    allow_reuse_address = False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--credentials", action="store_true", help="Download initial credentials to an owner-only local file; print path only")
    parser.add_argument("--open", action="store_true", help="Open administrator browser after the fixed local tunnel is listening")
    args = parser.parse_args()
    # Reserve the fixed loopback name before authenticating; never hijack another service.
    with Tunnel(("127.0.0.1", 18083), Handler) as tunnel:
        transport = connect()
        try:
            tunnel.transport = transport
            transport.set_keepalive(30)
            if args.credentials:
                print("Private credentials file: " + str(fetch_credentials(transport)), flush=True)
            print("Private admin: " + URL, flush=True)
            if args.open:
                webbrowser.open(URL)
            try:
                tunnel.serve_forever(poll_interval=0.5)
            except KeyboardInterrupt:
                pass
        finally:
            transport.close()


if __name__ == "__main__":
    main()
