"""Pinned SSH operator. audit/check execute anonymous source without remote writes."""
import argparse
import json
from pathlib import Path
import re
import select
import shlex
import sys
import time
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from ssh_connection import connect

BUNDLE = ("server.py", "install.py", "ozl-recommendations.service", "static/admin.html", "static/admin.css", "static/admin.js", "update.py")


def update_source(action, expected_current=None, expected_hash=None, expected_installed=None, update_id=None):
    if action not in ("audit", "check", "rollback"):
        raise ValueError("Unknown update operation")
    folder = Path(__file__).resolve().parent
    sources = {name: (folder / name).read_text(encoding="utf-8") for name in ("install.py", "update.py", "server.py")}
    operation = "result=tool.audit()"
    if action == "check":
        operation = "result=tool.preflight_update(" + ",".join(repr(item) for item in (expected_current, expected_hash, expected_installed)) + ")"
    elif action == "rollback":
        if not re.fullmatch(r"[a-f0-9]{32}", update_id or ""):
            raise ValueError("Expected the exact update receipt ID")
        operation = "result=tool.rollback_update(" + repr(update_id) + ")"
    return ("import json,sys,types\n"
            "sources=json.loads(" + repr(json.dumps(sources)) + ")\n"
            "for name,source in sources.items(): compile(source,name,'exec')\n"
            "installer=types.ModuleType('install');installer.__file__='/opt/ozl-recommendations/install.py'\n"
            "exec(compile(sources['install.py'],'reviewed-install.py','exec'),installer.__dict__);sys.modules['install']=installer\n"
            "namespace={'__name__':'recommendations_reviewed_update','__file__':'/opt/ozl-recommendations/update.py'}\n"
            "exec(compile(sources['update.py'],'reviewed-update.py','exec'),namespace)\n"
            "service={'__name__':'recommendations_readonly','__file__':'/opt/ozl-recommendations/server.py'}\n"
            "exec(compile(sources['server.py'],'reviewed-server.py','exec'),service)\n"
            "tool=namespace['Updater']()\n" + operation + "\n"
            "result['runtimeImportVerified']=True;print(json.dumps(result,sort_keys=True),flush=True)\n")


def readonly_source(expected_current=None, expected_hash=None, check=False):
    folder = Path(__file__).resolve().parent
    sources = {name: (folder / name).read_text(encoding="utf-8") for name in ("install.py", "server.py")}
    return ("import json,sys\n"
            "sources=json.loads(" + repr(json.dumps(sources)) + ")\n"
            "for name,source in sources.items(): compile(source,name,'exec')\n"
            "installer={'__name__':'recommendations_readonly','__file__':'/opt/ozl-recommendations/install.py'}\n"
            "exec(compile(sources['install.py'],'install.py','exec'),installer)\n"
            "tool=installer['Installer']()\n"
            + ("tool.preflight(" + repr(expected_current) + "," + repr(expected_hash) + ")\n" if check else "")
            + "service={'__name__':'recommendations_readonly','__file__':'/opt/ozl-recommendations/server.py'}\n"
            "exec(compile(sources['server.py'],'server.py','exec'),service)\n"
            "result=tool.audit();result['runtimeImportVerified']=True;print(json.dumps(result,sort_keys=True),flush=True)\n")


def rollback_source():
    source = (Path(__file__).resolve().parent / "install.py").read_text(encoding="utf-8")
    return ("import json\nnamespace={'__name__':'recommendations_owned_rollback'}\n"
            "exec(compile(" + repr(source) + ",'reviewed-install.py','exec'),namespace)\n"
            "print(json.dumps(namespace['Installer']().rollback(),sort_keys=True),flush=True)\n")


def session(transport, command, source=None):
    channel = transport.open_session(timeout=15)
    try:
        channel.settimeout(15)
        channel.exec_command(" ".join(shlex.quote(value) for value in command))
        if source is not None:
            channel.sendall(source.encode("utf-8"))
        channel.shutdown_write()
        deadline = time.monotonic() + 180
        while True:
            if channel.recv_ready():
                sys.stdout.buffer.write(channel.recv(65536))
                sys.stdout.buffer.flush()
            if channel.recv_stderr_ready():
                sys.stderr.buffer.write(channel.recv_stderr(65536))
                sys.stderr.buffer.flush()
            if channel.exit_status_ready() and not channel.recv_ready() and not channel.recv_stderr_ready():
                return channel.recv_exit_status()
            if time.monotonic() >= deadline:
                raise TimeoutError("Recommendation operator timed out; inspect the retained operation before retrying")
            select.select([channel], [], [], 0.25)
    finally:
        channel.close()


def upload(transport):
    import paramiko
    staging = "/root/ozl-recommendations-" + uuid.uuid4().hex
    with paramiko.SFTPClient.from_transport(transport) as sftp:
        sftp.mkdir(staging, mode=0o700)
        sftp.mkdir(staging + "/static", mode=0o700)
        for name in BUNDLE:
            destination = staging + "/" + name
            sftp.put(str(Path(__file__).parent / name), destination)
            sftp.chmod(destination, 0o600)
    return staging


def cleanup_upload(transport, staging):
    if not re.fullmatch(r"/root/ozl-recommendations-[a-f0-9]{32}", staging):
        raise ValueError("Unexpected staging directory")
    import paramiko
    with paramiko.SFTPClient.from_transport(transport) as sftp:
        for name in BUNDLE:
            sftp.remove(staging + "/" + name)
        sftp.rmdir(staging + "/static")
        sftp.rmdir(staging)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("audit", "check", "install", "rollback", "update-audit", "check-update", "update", "rollback-update"))
    parser.add_argument("--expected-current")
    parser.add_argument("--expected-protected-sha256")
    parser.add_argument("--expected-installed-sha256")
    parser.add_argument("--update-id")
    args = parser.parse_args()
    if args.command in ("check", "install", "check-update", "update"):
        if not re.fullmatch(r"/var/www/ozl/sites/[a-zA-Z0-9_.-]+", args.expected_current or "") or not re.fullmatch(r"[a-f0-9]{64}", args.expected_protected_sha256 or ""):
            parser.error("check/install requires the exact audited current site and protected SHA256")
    if args.command in ("check-update", "update") and not re.fullmatch(r"[a-f0-9]{64}", args.expected_installed_sha256 or ""):
        parser.error("check-update/update requires the exact audited installed SHA256")
    if args.command == "rollback-update" and not re.fullmatch(r"[a-f0-9]{32}", args.update_id or ""):
        parser.error("rollback-update requires the exact retained update ID")
    transport = connect()
    try:
        if args.command in ("audit", "check"):
            return session(transport, ["/usr/bin/python3", "-B", "-u", "-"], readonly_source(args.expected_current, args.expected_protected_sha256, args.command == "check"))
        if args.command == "rollback":
            return session(transport, ["/usr/bin/python3", "-B", "-u", "-"], rollback_source())
        if args.command in ("update-audit", "check-update", "rollback-update"):
            action = {"update-audit": "audit", "check-update": "check", "rollback-update": "rollback"}[args.command]
            return session(transport, ["/usr/bin/python3", "-B", "-u", "-"], update_source(action, args.expected_current, args.expected_protected_sha256, args.expected_installed_sha256, args.update_id))
        # Recheck anonymous memory before creating the one owned upload directory.
        checking = (update_source("check", args.expected_current, args.expected_protected_sha256, args.expected_installed_sha256)
                    if args.command == "update" else readonly_source(args.expected_current, args.expected_protected_sha256, True))
        checked = session(transport, ["/usr/bin/python3", "-B", "-u", "-"], checking)
        if checked:
            return checked
        staging = upload(transport)
        command = ["/usr/bin/python3", "-B", staging + ("/update.py" if args.command == "update" else "/install.py"), "--expected-current", args.expected_current,
                   "--expected-protected-sha256", args.expected_protected_sha256]
        if args.command == "update":
            command += ["--expected-installed-sha256", args.expected_installed_sha256]
        status = session(transport, command)
        if status == 0:
            try:
                cleanup_upload(transport, staging)
            except OSError:
                print("Operation succeeded; upload directory retained: " + staging, file=sys.stderr)
        else:
            print("Operation interrupted; retained reviewed operator source: " + staging, file=sys.stderr)
            print("Inspect /opt/ozl-recommendations/install-state.json and update-backups before an explicit rollback.", file=sys.stderr)
        return status
    finally:
        transport.close()


if __name__ == "__main__":
    raise SystemExit(main())
