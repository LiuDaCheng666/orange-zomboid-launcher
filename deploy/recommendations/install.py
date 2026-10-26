#!/usr/bin/env python3
"""Fixed-path first installation. Never edits/reloads a web configuration."""
import argparse
import glob
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import time
import urllib.request
import uuid

CODE = "/opt/ozl-recommendations"
PRIVATE = "/var/lib/ozl-recommendations"
PUBLIC = "/var/lib/ozl-recommendations-public"
FEED = PUBLIC + "/feed.json"
UNIT = "/etc/systemd/system/ozl-recommendations.service"
CURRENT = "/var/www/ozl/current"
SERVICE = "ozl-recommendations"
BUNDLE = ("server.py", "install.py", "ozl-recommendations.service", "static/admin.html", "static/admin.css", "static/admin.js")


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def atomic(path, content, mode):
    if path.is_symlink():
        raise ValueError("Refusing symbolic-link destination")
    temporary = path.with_name(".recommendations-" + uuid.uuid4().hex)
    try:
        with os.fdopen(os.open(str(temporary), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "wb") as stream:
            stream.write(content)
            stream.flush()
            os.chmod(str(temporary), mode)
            os.fsync(stream.fileno())
        os.replace(str(temporary), str(path))
        if os.name != "nt":
            directory = os.open(str(path.parent), os.O_RDONLY | os.O_DIRECTORY)
            try:
                os.fsync(directory)
            finally:
                os.close(directory)
    finally:
        if temporary.exists():
            temporary.unlink()


def identity(path):
    value = path.stat()
    return [value.st_dev, value.st_ino]


class Installer:
    def __init__(self, root="/", run=None):
        self.root = Path(root).resolve()
        self.run = run or (lambda args: subprocess.run(args, check=True, timeout=30))

    def path(self, absolute):
        return self.root / absolute.lstrip("/")

    def effective_nginx(self):
        loaded = {}
        def read(absolute):
            if absolute in loaded:
                return
            if len(loaded) >= 128:
                raise ValueError("Nginx include tree exceeds audit budget")
            path = self.path(absolute)
            payload = path.read_bytes()
            if len(payload) > 1048576:
                raise ValueError("Nginx configuration exceeds audit budget")
            source = re.sub(r"#[^\n]*", "", payload.decode("utf-8"))
            loaded[absolute] = source
            for include in re.findall(r"\binclude\s+([^;]+);", source):
                include = include.strip().strip("\"'")
                if not include.startswith("/"):
                    raise ValueError("Relative nginx include requires explicit review")
                for item in sorted(glob.glob(str(self.path(include)))):
                    relative = "/" + str(Path(item).relative_to(self.root)).replace("\\", "/")
                    read(relative)
        read("/etc/ozl-http-guard/nginx.conf")
        source = "\n".join(loaded.values())
        for setting in re.findall(r"\bdisable_symlinks\s+([^;]+);", source):
            if setting.strip() != "off":
                raise ValueError("Nginx disables symlink traversal; no configuration change is permitted")
        if "root /var/www/ozl/current;" not in source or not re.search(r"try_files\s+\$uri\s+\$uri/\s+=404\s*;", source):
            raise ValueError("Audited static current/try_files route is absent")
        endpoint = "/api/recommended-servers"
        for modifier, route in re.findall(r"\blocation\s+(?:(=|\^~|~\*?)\s+)?([^\s{]+)\s*\{", source):
            route = route.strip("\"'")
            if modifier.startswith("~"):
                raise ValueError("Regex nginx locations require explicit route review")
            if route != "/" and ((modifier == "=" and route == endpoint) or (modifier != "=" and endpoint.startswith(route))):
                raise ValueError("Existing nginx location intercepts the new feed")
        users = set(re.findall(r"\buser\s+([a-zA-Z_][a-zA-Z0-9_-]*)(?:\s+[^;]+)?;", source))
        if len(users) != 1:
            raise ValueError("Cannot establish nginx worker account")
        return loaded, users.pop()

    def protected(self):
        current = self.path(CURRENT)
        resolved = current.resolve(strict=True)
        result = {"current": str(resolved), "currentLink": os.readlink(str(current)) if current.is_symlink() else None,
                  "currentIdentity": identity(resolved), "files": {}}
        directories = [("site", resolved), ("releases", self.path("/var/www/ozl/releases")),
                       ("caddy", self.path("/etc/caddy")), ("guard", self.path("/etc/ozl-http-guard")),
                       ("nginx", self.path("/etc/nginx"))]
        for label, directory in directories:
            if not directory.exists():
                continue
            for folder, dirs, files in os.walk(str(directory), followlinks=False):
                for name in sorted(dirs + files):
                    path = Path(folder) / name
                    relative = str(path.relative_to(directory)).replace("\\", "/")
                    if label == "site" and relative == "api/recommended-servers" and path.is_symlink() and os.readlink(str(path)) == FEED:
                        continue  # The single explicitly owned additive route.
                    if path.is_symlink():
                        result["files"][label + "/" + relative] = "symlink:" + os.readlink(str(path))
                    elif path.is_file():
                        result["files"][label + "/" + relative] = file_sha(path)
        web = self.path("/var/www/ozl")
        for path in web.iterdir():
            if path.is_file() and not path.is_symlink():
                result["files"]["web-root/" + path.name] = file_sha(path)
        configs, _ = self.effective_nginx()
        for absolute in configs:
            result["files"]["nginx-include:" + absolute] = file_sha(self.path(absolute))
        return result

    def audit(self):
        protected = self.protected()
        _, worker = self.effective_nginx()
        return {"readOnly": True, "python": sys.version.split()[0], "current": protected["current"],
                "protectedSha256": sha(json.dumps(protected, sort_keys=True).encode("utf-8")), "nginxWorker": worker,
                "newPathsAbsent": all(not self.path(path).exists() and not self.path(path).is_symlink() for path in (CODE, PRIVATE, PUBLIC, UNIT)),
                "routeAbsent": not (Path(protected["current"]) / "api/recommended-servers").exists() and not (Path(protected["current"]) / "api/recommended-servers").is_symlink()}

    def preflight(self, expected_current, expected_hash):
        audit = self.audit()
        if audit["current"] != expected_current or audit["protectedSha256"] != expected_hash:
            raise ValueError("Current site/protected files changed since the explicit audit")
        if not audit["newPathsAbsent"] or not audit["routeAbsent"]:
            raise ValueError("Recommendation paths/route already exist; explicit migration required")
        api = Path(audit["current"]) / "api"
        if api.is_symlink() or (api.exists() and not api.is_dir()):
            raise ValueError("Existing api parent is not an ordinary directory")
        return audit

    def account(self):
        import pwd
        try:
            pwd.getpwnam(SERVICE)
        except KeyError:
            self.run(["useradd", "--system", "--user-group", "--no-create-home", "--home-dir", PRIVATE, "--shell", "/sbin/nologin", SERVICE])
        else:
            raise ValueError("Service account already exists; explicit adoption required")
        account = pwd.getpwnam(SERVICE)
        if account.pw_uid == 0:
            raise ValueError("Service account must be unprivileged")
        return account.pw_uid, account.pw_gid

    def save_state(self, state):
        atomic(self.path(CODE + "/install-state.json"), json.dumps(state, indent=2, sort_keys=True).encode("utf-8"), 0o600)

    def install(self, bundle, expected_current, expected_hash):
        if sys.version_info < (3, 6):
            raise ValueError("Python 3.6 or later required")
        audit = self.preflight(expected_current, expected_hash)
        # A bind probe reserves no listener after this block; systemd remains authoritative.
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 18083))
        content = {}
        for name in BUNDLE:
            payload = (Path(bundle) / name).read_bytes()
            if len(payload) > 1048576:
                raise ValueError("Bundle file exceeds deployment budget")
            content[name] = payload
        uid, gid = self.account()
        self.preflight(expected_current, expected_hash)
        code = self.path(CODE)
        code.mkdir(mode=0o755)
        code.chmod(0o755)
        state = {"version": 1, "phase": "prepared", "current": expected_current, "currentIdentity": identity(Path(expected_current)),
                 "protectedSha256": expected_hash, "files": {}, "directories": {CODE: identity(code)}, "apiCreated": False,
                 "route": str(Path(expected_current) / "api/recommended-servers")}
        self.save_state(state)
        try:
            for absolute, mode in ((PRIVATE, 0o700), (PUBLIC, 0o755)):
                path = self.path(absolute)
                path.mkdir(mode=mode)
                path.chmod(mode)
                os.chown(str(path), uid, gid)
                state["directories"][absolute] = identity(path)
                self.save_state(state)
            (code / "static").mkdir(mode=0o755)
            (code / "static").chmod(0o755)
            for name, payload in content.items():
                target = self.path(UNIT) if name == "ozl-recommendations.service" else code / name
                if target.exists() or target.is_symlink():
                    raise ValueError("Prepared installation destination was occupied")
                state["files"][str(target)] = sha(payload)
                self.save_state(state)
                atomic(target, payload, 0o644)
            self.run(["runuser", "-u", SERVICE, "--", "/usr/bin/python3", CODE + "/server.py", "--private-dir", PRIVATE, "--setup-only"])
            self.run(["systemctl", "daemon-reload"])
            self.run(["systemctl", "enable", SERVICE])
            self.run(["systemctl", "start", SERVICE])
            self.wait_health()
            self.run(["runuser", "-u", audit["nginxWorker"], "--", "test", "-r", FEED])
            if self.audit()["protectedSha256"] != expected_hash:
                raise ValueError("Protected files changed before adding feed route")
            api = Path(expected_current) / "api"
            if not api.exists():
                api.mkdir(mode=0o755)
                api.chmod(0o755)
                state["apiCreated"] = True
            if api.is_symlink() or not api.is_dir():
                raise ValueError("API parent changed before route creation")
            state["apiIdentity"] = identity(api)
            self.save_state(state)
            os.symlink(FEED, state["route"])
            self.run(["runuser", "-u", audit["nginxWorker"], "--", "test", "-r", state["route"]])
            for port in (18080, 18081):
                with urllib.request.urlopen("http://127.0.0.1:" + str(port) + "/api/recommended-servers", timeout=5) as response:
                    payload = response.read(1048577)
                    if response.status != 200 or len(payload) > 1048576 or json.loads(payload.decode("utf-8"))["schema"] != 1:
                        raise ValueError("Existing static route did not serve the public feed")
            if self.audit()["protectedSha256"] != expected_hash:
                raise ValueError("Protected site/configuration changed during installation")
            state["phase"] = "installed"
            self.save_state(state)
            return {"status": "installed", "protectedUnchanged": True, "publicUrl": "https://orangezl.com/api/recommended-servers", "privateCredentialsFile": PRIVATE + "/initial-credentials.json"}
        except BaseException:
            # Preserve data and the ownership manifest for explicit inspected rollback.
            state["phase"] = "interrupted"
            self.save_state(state)
            raise

    def wait_health(self):
        deadline = time.monotonic() + 10
        while True:
            try:
                with urllib.request.urlopen("http://127.0.0.1:18083/api/recommended-servers", timeout=1) as response:
                    if response.status == 200 and json.loads(response.read(1048577).decode("utf-8"))["schema"] == 1:
                        return
            except (OSError, ValueError):
                pass
            if time.monotonic() >= deadline:
                raise RuntimeError("Private recommendation service readiness timed out")
            time.sleep(0.2)

    def rollback(self):
        code = self.path(CODE)
        manifest = code / "install-state.json"
        state = json.loads(manifest.read_text(encoding="utf-8"))
        if state.get("version") != 1 or state["directories"].get(CODE) != identity(code) or code.is_symlink():
            raise ValueError("Owned installation identity changed")
        allowed = {str(self.path(UNIT))} | {str(code / name) for name in BUNDLE if name != "ozl-recommendations.service"}
        if not set(state["files"]).issubset(allowed):
            raise ValueError("Rollback manifest contains foreign paths")
        for name, expected in state["files"].items():
            path = Path(name)
            if path.is_symlink() or (path.exists() and file_sha(path) != expected):
                raise ValueError("Installed source/unit changed; rollback preserved it")
        route = Path(state["route"])
        if str(route) != str(Path(state["current"]) / "api/recommended-servers") or identity(Path(state["current"])) != state["currentIdentity"]:
            raise ValueError("Owned site identity changed")
        if route.exists() or route.is_symlink():
            if not route.is_symlink() or os.readlink(str(route)) != FEED or identity(route.parent) != state.get("apiIdentity"):
                raise ValueError("Feed route changed; rollback preserved it")
        unit = self.path(UNIT)
        if unit.exists():
            self.run(["systemctl", "stop", SERVICE])
            self.run(["systemctl", "disable", SERVICE])
        if route.is_symlink():
            route.unlink()
        if state.get("apiCreated") and route.parent.exists() and identity(route.parent) == state.get("apiIdentity") and not list(route.parent.iterdir()):
            route.parent.rmdir()
        for name in state["files"]:
            path = Path(name)
            if path.exists():
                path.unlink()
        if (code / "static").exists() and not list((code / "static").iterdir()):
            (code / "static").rmdir()
        state["phase"] = "rolled-back"
        self.save_state(state)
        self.run(["systemctl", "daemon-reload"])
        # Private DB/credentials, public snapshots, account and root-owned manifest
        # remain recoverable. No recursive delete or foreign-file cleanup.
        return {"status": "rolledBack", "privateDataRetained": PRIVATE, "publicSnapshotRetained": FEED}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit", action="store_true")
    parser.add_argument("--rollback", action="store_true")
    parser.add_argument("--expected-current")
    parser.add_argument("--expected-protected-sha256")
    args = parser.parse_args()
    tool = Installer()
    if args.audit:
        result = tool.audit()
    elif args.rollback:
        result = tool.rollback()
    else:
        if not args.expected_current or not re.fullmatch(r"[a-f0-9]{64}", args.expected_protected_sha256 or ""):
            parser.error("Install requires the exact audited current path and protected SHA256")
        result = tool.install(Path(__file__).resolve().parent, args.expected_current, args.expected_protected_sha256)
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
