"""Prepare an immutable mirror from public GitHub release assets, without executing them."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile
from urllib.parse import unquote
import xml.etree.ElementTree as ET

REPOSITORY = 'https://github.com/LiuDaCheng666/orange-zomboid-launcher'
OWNED = {'橙子启动器.exe', '橙子更新助手.exe', '卸载橙子启动器.exe', 'VERSION', 'CHANGELOG.md', '使用说明.txt', 'release-public-key.xml', 'Web/index.html', 'Web/app.js', 'Web/app.css', 'Web/home.png', 'Web/orange-app.ico', 'Web/WebView2-LICENSE.txt', 'Web/Microsoft.Web.WebView2.Core.dll', 'Web/Microsoft.Web.WebView2.WinForms.dll', 'Web/WebView2Loader.dll', 'Assets/OrangeShootPresentation.jar', 'Assets/OrangeLaunchBridge.lua'}
MAX_FILE = 64 * 1024 * 1024
MAX_TOTAL = 160 * 1024 * 1024

def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key.casefold() in {name.casefold() for name in result}:
            raise ValueError('Duplicate JSON field')
        result[key] = value
    return result

def load_json(raw):
    return json.loads(raw, object_pairs_hook=unique_object)

def digest(content):
    return hashlib.sha256(content).hexdigest().upper()

def verify_signature(raw, signature, public_key):
    if not 0 < len(raw) <= 1048576:
        raise ValueError('Invalid manifest length')
    root = ET.fromstring(public_key)
    if root.tag != 'RSAKeyValue' or {c.tag for c in root} != {'Modulus', 'Exponent'} or len(root) != 2:
        raise ValueError('Expected a public RSA key only')
    n = int.from_bytes(base64.b64decode(root.findtext('Modulus'), validate=True), 'big')
    e = int.from_bytes(base64.b64decode(root.findtext('Exponent'), validate=True), 'big')
    size = (n.bit_length() + 7) // 8
    if not 2048 <= n.bit_length() <= 8192 or e < 3 or e % 2 == 0 or len(signature) != size:
        raise ValueError('Invalid signature or key size')
    number = int.from_bytes(signature, 'big')
    if number >= n:
        raise ValueError('Invalid RSA signature')
    decoded = pow(number, e, n).to_bytes(size, 'big')
    info = bytes.fromhex('3031300d060960864801650304020105000420') + hashlib.sha256(raw).digest()
    expected = b'\x00\x01' + b'\xff' * (size-len(info)-3) + b'\x00' + info
    if decoded != expected:
        raise ValueError('Manifest signature verification failed')

def prepare_release(release, source, destination, public_key):
    source, destination = Path(source).resolve(), Path(destination).resolve()
    if destination.exists():
        raise ValueError('Output already exists; immutable release trees are never overwritten')
    if release.get('draft') is not False or release.get('prerelease') is not False:
        raise ValueError('Only public stable releases may be mirrored')
    tag = release.get('tag_name', '')
    if not re.fullmatch(r'v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', tag):
        raise ValueError('Invalid stable release tag')
    version, prefix = tag[1:], f'{REPOSITORY}/releases/download/{tag}/'
    setup, portable = f'orange-launcher-setup-{tag}.exe', f'orange-launcher-{tag}-win-x64.zip'
    assets, blobs, proof = {}, {}, []
    total = 0
    items = release.get('assets')
    if not isinstance(items, list) or not 4 <= len(items) <= 36:
        raise ValueError('Invalid asset count')
    for item in items:
        name = item.get('name', '')
        if not re.fullmatch(r'[a-zA-Z0-9][a-zA-Z0-9._-]{0,180}', name) or name in assets or '..' in name:
            raise ValueError('Unsafe or duplicate asset name')
        if item.get('browser_download_url') != prefix + name:
            raise ValueError('Asset must belong to this repository and version')
        file = source / name
        size = item.get('size')
        if type(size) is not int or not 0 < size <= MAX_FILE or file.is_symlink() or not file.is_file() or file.stat().st_size != size:
            raise ValueError('Missing asset or invalid length: ' + name)
        total += size
        if total > MAX_TOTAL:
            raise ValueError('Release too large')
        content = file.read_bytes()
        actual = digest(content)
        if item.get('digest', '').lower() != 'sha256:' + actual.lower():
            raise ValueError('GitHub asset SHA-256 mismatch: ' + name)
        assets[name], blobs[name] = item, content
        proof.append(dict(name=name, length=size, sha256=actual))
    if not {'update-manifest.json', 'update-manifest.sig', setup, portable}.issubset(blobs):
        raise ValueError('Required public delivery files are missing')
    raw, signature = blobs['update-manifest.json'], blobs['update-manifest.sig']
    verify_signature(raw, signature, public_key)
    manifest = load_json(raw)
    if manifest.get('schema') != 1 or manifest.get('version') != version or manifest.get('platform') != 'win-x64':
        raise ValueError('Manifest metadata does not match release')
    if not re.fullmatch(r'\d+\.\d+\.\d+', manifest.get('minimumUpdaterVersion', '')):
        raise ValueError('Invalid minimum updater version')
    files = manifest.get('files')
    if not isinstance(files, list) or len(files) != len(OWNED) or {f.get('path') for f in files} != OWNED:
        raise ValueError('Manifest must contain exactly the launcher-owned paths')
    expected_names = {'update-manifest.json', 'update-manifest.sig', setup, portable}
    for file in files:
        url = file.get('url', '')
        if not url.startswith(prefix):
            raise ValueError('Wrong-version or foreign resource URL')
        name = unquote(url[len(prefix):])
        if url != prefix + name or name not in blobs:
            raise ValueError('Missing or unsafe signed resource URL')
        expected_names.add(name)
        if type(file.get('length')) is not int or file['length'] != len(blobs[name]) or file.get('sha256', '').upper() != digest(blobs[name]):
            raise ValueError('Signed resource integrity mismatch: ' + name)
    if set(blobs) != expected_names:
        raise ValueError('Unexpected release attachments')
    published = release.get('published_at', '')
    if not re.fullmatch(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z', published):
        raise ValueError('Missing publication timestamp')
    notes = manifest.get('notes', '')
    if not isinstance(notes, str):
        raise ValueError('Invalid release notes')
    section = re.split(r'(?m)^## ', notes)[1:2]
    bullets = [line[2:].strip()[:1000] for line in (section[0] if section else notes).splitlines() if line.startswith('- ')][:8]
    display = dict(version=version, date=published[:10], notes=bullets)
    for kind, name in [('setup', setup), ('portable', portable)]:
        display[kind] = dict(url=f'/releases/{tag}/{name}', github=prefix+name, length=len(blobs[name]), sha256=digest(blobs[name]))
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix='.ozl-mirror-', dir=destination.parent)).resolve()
    try:
        immutable = staging / 'releases' / tag; immutable.mkdir(parents=True)
        for name, content in blobs.items():
            (immutable / name).write_bytes(content)
        latest = staging / 'updates/latest'; latest.mkdir(parents=True)
        (latest / 'update-manifest.json').write_bytes(raw)
        (latest / 'update-manifest.sig').write_bytes(signature)
        (staging / 'release.json').write_text(json.dumps(display, ensure_ascii=False, indent=2), encoding='utf-8')
        (staging / 'release-proof.json').write_text(json.dumps(dict(version=version, assets=proof), indent=2), encoding='utf-8')
        staging.rename(destination)
    finally:
        if staging.exists() and staging.parent == destination.parent and staging.name.startswith('.ozl-mirror-'):
            shutil.rmtree(staging)
    return display

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--distribution', choices=['legacy', 'velopack'], default='legacy')
    parser.add_argument('--release-metadata', type=Path)
    parser.add_argument('--preserved-legacy', type=Path, help='Authenticated old latest JSON/signature directory; required for Velopack')
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--public-key', type=Path, default=Path(__file__).resolve().parents[1] / 'release-public-key.xml')
    args = parser.parse_args()
    try:
        if args.distribution == 'velopack':
            if args.release_metadata or not args.preserved_legacy:
                raise ValueError('Velopack requires --preserved-legacy and uses its signed local inventory, not GitHub metadata')
            data = prepare_velopack_release(args.source, args.output, args.public_key.read_bytes(), args.preserved_legacy)
        else:
            if not args.release_metadata or args.preserved_legacy:
                raise ValueError('Legacy mode requires --release-metadata')
            data = prepare_release(load_json(args.release_metadata.read_bytes()), args.source, args.output, args.public_key.read_bytes())
    except (ValueError, OSError, ET.ParseError) as error:
        print('Preparation failed: ' + str(error), file=sys.stderr); return 1
    print(json.dumps(dict(version=data['version'], output=str(args.output.resolve()), verified=True)))
    return 0

def velopack_names(version):
    return {f'OrangeZomboidLauncher-{version}-full.nupkg', 'OrangeZomboidLauncher-win-Setup.exe',
            'OrangeZomboidLauncher-win-Portable.zip', 'releases.win.json', 'assets.win.json', 'RELEASES',
            'velopack-manifest.json', 'velopack-manifest.sig'}


def preserved_legacy(source, public_key, new_version):
    source = Path(source)
    raw = (source / 'update-manifest.json').read_bytes()
    signature = (source / 'update-manifest.sig').read_bytes()
    verify_signature(raw, signature, public_key)
    manifest = load_json(raw)
    version = manifest.get('version', '')
    if (manifest.get('schema') != 1 or manifest.get('platform') != 'win-x64'
            or not re.fullmatch(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', version)
            or tuple(map(int, version.split('.'))) >= tuple(map(int, new_version.split('.')))):
        raise ValueError('Invalid preserved legacy metadata')
    files = manifest.get('files')
    if not isinstance(files, list) or len(files) != len(OWNED) or {f.get('path') for f in files} != OWNED:
        raise ValueError('Preserved legacy inventory must be exact')
    prefix = f'{REPOSITORY}/releases/download/v{version}/'
    for file in files:
        sha = file.get('sha256', '')
        if (not re.fullmatch('[A-Fa-f0-9]{64}', sha) or type(file.get('length')) is not int
                or not 0 < file['length'] <= MAX_FILE or file.get('url') != prefix + sha.lower() + '.bin'):
            raise ValueError('Invalid preserved legacy resource origin or integrity')
    return raw, signature


def prepare_velopack_release(source, destination, public_key, legacy):
    """Authenticate a local candidate with the existing RSA identity; no public release prerequisite."""
    source, destination = Path(source).resolve(), Path(destination).resolve()
    if destination.exists():
        raise ValueError('Output already exists; immutable release trees are never overwritten')
    raw = (source / 'velopack-artifacts.json').read_bytes()
    signature = (source / 'velopack-artifacts.sig').read_bytes()
    verify_signature(raw, signature, public_key)
    inventory = load_json(raw)
    if set(inventory) != {'schema','distribution','appId','channel','version','date','files'}:
        raise ValueError('Invalid artifact inventory fields')
    version = inventory.get('version', '')
    if (type(inventory.get('schema')) is not int or inventory.get('schema') != 1 or inventory.get('distribution') != 'velopack'
            or inventory.get('appId') != 'OrangeZomboidLauncher' or inventory.get('channel') != 'win'
            or not re.fullmatch(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', version)
            or not re.fullmatch(r'\d{4}-\d{2}-\d{2}', inventory.get('date',''))):
        raise ValueError('Invalid artifact identity')
    expected = velopack_names(version)
    files = inventory.get('files')
    if not isinstance(files,list) or len(files) != len(expected) or {f.get('name') for f in files} != expected:
        raise ValueError('Artifact inventory must contain exactly the approved delivery files')
    extras = {'velopack-artifacts.json','velopack-artifacts.sig'}
    if {f.name for f in source.iterdir()} != expected | extras or any(f.is_symlink() or not f.is_file() for f in source.iterdir()):
        raise ValueError('Unexpected candidate file or directory')
    blobs = {}; total = 0
    for file in files:
        if set(file) != {'name','length','sha256'} or type(file['length']) is not int or not 0 < file['length'] <= 1024**3:
            raise ValueError('Invalid artifact record')
        name = file['name']; path = source/name
        if path.stat().st_size != file['length']:
            raise ValueError('Artifact length mismatch: '+name)
        total += file['length']
        if total > 2*1024**3: raise ValueError('Candidate too large')
        content = path.read_bytes()
        if not isinstance(file['sha256'],str) or file['sha256'].upper() != digest(content):
            raise ValueError('Artifact hash mismatch: '+name)
        blobs[name] = content
    feedraw, feedsig = blobs['velopack-manifest.json'], blobs['velopack-manifest.sig']
    verify_signature(feedraw, feedsig, public_key)
    feed = load_json(feedraw)
    package = f'OrangeZomboidLauncher-{version}-full.nupkg'; payload = blobs[package]
    if (set(feed) != {'schema','appId','channel','version','fileName','length','sha256','notes'}
            or type(feed['schema']) is not int or feed['schema'] != 1 or feed['appId'] != 'OrangeZomboidLauncher' or feed['channel'] != 'win'
            or feed['version'] != version or feed['fileName'] != package or type(feed['length']) is not int
            or feed['length'] != len(payload) or feed['sha256'].upper() != digest(payload) or not isinstance(feed['notes'],str)):
        raise ValueError('Signed feed does not match candidate')
    index = load_json(blobs['releases.win.json'])
    entries = index.get('Assets')
    if not isinstance(entries,list) or len(entries) != 1:
        raise ValueError('Framework index must contain one full package')
    entry=entries[0]
    if (entry.get('PackageId') != 'OrangeZomboidLauncher' or entry.get('Version') != version or entry.get('Type') != 'Full'
            or entry.get('FileName') != package or entry.get('Size') != len(payload)
            or entry.get('SHA256','').upper() != digest(payload) or entry.get('SHA1','').upper() != hashlib.sha1(payload).hexdigest().upper()):
        raise ValueError('Framework release index mismatch')
    assets = load_json(blobs['assets.win.json'])
    required_assets = {(package,'Full'),('OrangeZomboidLauncher-win-Setup.exe','Installer'),('OrangeZomboidLauncher-win-Portable.zip','Portable')}
    if not isinstance(assets,list) or len(assets)!=3 or {(a.get('RelativeFileName'),a.get('Type')) for a in assets} != required_assets:
        raise ValueError('Framework asset index mismatch')
    if blobs['RELEASES'].decode('utf-8-sig').strip().upper() != f'{hashlib.sha1(payload).hexdigest()} {package} {len(payload)}'.upper():
        raise ValueError('Framework RELEASES mismatch')
    oldraw, oldsig = preserved_legacy(legacy, public_key, version)
    notes = re.split(r'(?m)^## ',feed['notes'])[1:2]
    bullets=[line[2:].strip()[:1000] for line in (notes[0] if notes else feed['notes']).splitlines() if line.startswith('- ')][:8]
    display=dict(distribution='velopack',version=version,date=inventory['date'],notes=bullets)
    for kind,name in [('setup','OrangeZomboidLauncher-win-Setup.exe'),('portable','OrangeZomboidLauncher-win-Portable.zip')]:
        display[kind]=dict(url=f'/releases/v{version}/{name}',github=f'{REPOSITORY}/releases/download/v{version}/{name}',length=len(blobs[name]),sha256=digest(blobs[name]))
    blobs.update({'velopack-artifacts.json':raw,'velopack-artifacts.sig':signature})
    destination.parent.mkdir(parents=True,exist_ok=True)
    staging=Path(tempfile.mkdtemp(prefix='.ozl-mirror-',dir=destination.parent))
    try:
        immutable=staging/'releases'/('v'+version); immutable.mkdir(parents=True)
        for name,content in blobs.items(): (immutable/name).write_bytes(content)
        for lane,contents in [('updates/latest',{'update-manifest.json':oldraw,'update-manifest.sig':oldsig}),('updates/velopack/latest',{'velopack-manifest.json':feedraw,'velopack-manifest.sig':feedsig,'releases.win.json':blobs['releases.win.json']})]:
            folder=staging/lane; folder.mkdir(parents=True)
            for name,content in contents.items(): (folder/name).write_bytes(content)
        (staging/'release.json').write_text(json.dumps(display,ensure_ascii=False,indent=2),encoding='utf-8')
        (staging/'release-proof.json').write_text(json.dumps(inventory,indent=2),encoding='utf-8')
        staging.rename(destination)
    finally:
        if staging.exists(): shutil.rmtree(staging)
    return display


if __name__ == '__main__':
    sys.exit(main())
