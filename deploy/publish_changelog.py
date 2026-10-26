"""Synchronize published release notes into the existing live changelog only."""
import argparse
from datetime import datetime, timedelta, timezone
import html
import json
from pathlib import Path
import re
import sys
import uuid
from urllib.request import Request, urlopen

from publish_software import BASE, SftpRemote, command, sha, validate_current, version_tuple


class ChangelogRemote(SftpRemote):
    def replace_checked(self, source, target, expected_hash, current, version=None):
        """Serialize our publishers; check live bytes on the server at activation."""
        if (source, target, True) not in self.allowed_renames or target != current + '/changes.html':
            raise ValueError('Replacement is outside the changelog whitelist')
        self.regular(source); self.regular(target)
        # The lock and candidates live outside the served website. Unlike a
        # client-side read followed by SFTP rename, validation and replacement
        # happen in one SSH operation, under a common publication lock.
        script = '''import fcntl,hashlib,json,os,stat
source,target,expected,current,version=PARAMETERS
lock=os.open('/var/www/ozl/test-runs/.changelog-publish.lock',os.O_CREAT|os.O_RDWR|os.O_NOFOLLOW,0o600)
try:
 fcntl.flock(lock,fcntl.LOCK_EX)
 if not stat.S_ISREG(os.fstat(lock).st_mode): raise RuntimeError('Invalid publication lock')
 if os.path.realpath('/var/www/ozl/current')!=current: raise RuntimeError('Physical current changed')
 for path in (source,target):
  if not stat.S_ISREG(os.lstat(path).st_mode): raise RuntimeError('Unexpected changelog file type')
 if version is not None:
  with open(current+'/release.json','rb') as f: live=json.load(f)
  if live.get('version')!=version: raise RuntimeError('Software version changed')
 with open(target,'rb') as f: digest=hashlib.sha256(f.read()).hexdigest()
 if digest!=expected: raise RuntimeError('Concurrent changelog edit preserved')
 os.replace(source,target)
 print(json.dumps({'replaced':True}))
finally:
 os.close(lock)
'''.replace('PARAMETERS', repr((source, target, expected_hash, current, version)))
        command(self.connection, script)


def render(original, changelog, releases, version):
    """Insert missing newer cards; preserve the live document outside those cards."""
    target = version_tuple(version)
    text = original.decode('utf-8')
    cards = list(re.finditer(r'<section class="change-entry">.*?</section>', text, re.S))
    if not cards:
        raise ValueError('Live changelog card structure was not recognized')
    existing = []
    for card in cards:
        headings = re.findall(r'<h2>([0-9]+\.[0-9]+\.[0-9]+)</h2>', card[0])
        if len(headings) != 1:
            raise ValueError('Expected one version per live card')
        existing.append(version_tuple(headings[0]))
    if existing != sorted(set(existing), reverse=True) or target < existing[0]:
        raise ValueError('Refusing duplicate, unordered, or downgraded changelog')
    if text.count('当前稳定版') != 1 or '当前稳定版' not in cards[0][0]:
        raise ValueError('Expected exactly one current stable badge on the newest card')
    notes = {}
    for match in re.finditer(r'^## (\d+\.\d+\.\d+) — (\d{4}-\d{2}-\d{2})\s*\n(.*?)(?=^## |\Z)', changelog, re.M | re.S):
        bullets = re.findall(r'^- (.+)$', match[3], re.M)
        notes[version_tuple(match[1])] = bullets
    published = {}
    for release in releases:
        tag = release.get('tag_name', '')
        if release.get('draft') or release.get('prerelease') or not re.fullmatch(r'v\d+\.\d+\.\d+', tag):
            continue
        key = version_tuple(tag[1:])
        if existing[-1] < key <= target:
            stamp = datetime.fromisoformat(release['published_at'].replace('Z', '+00:00'))
            published[key] = stamp.astimezone(timezone(timedelta(hours=8))).date()
    if target not in published:
        raise ValueError('Current version must already be a public stable release')
    missing = sorted(set(published) - set(existing), reverse=True)
    if not missing:
        if existing[0] != target:
            raise ValueError('Current stable version is missing')
        return original
    if any(key <= existing[0] for key in missing):
        raise ValueError('A gap in older live cards needs explicit reconciliation')
    newline = '\r\n' if '\r\n' in text else '\n'
    additions = []
    for key in missing:
        if not notes.get(key):
            raise ValueError('Missing release notes for ' + '.'.join(map(str, key)))
        number = '.'.join(map(str, key)); date = published[key]
        lines = ['<section class="change-entry">', '<div class="change-version">',
                 '<span class="pill">' + ('当前稳定版' if key == target else '历史版本') + '</span>',
                 '<h2>' + number + '</h2>',
                 '<p class="muted"><time datetime="' + date.isoformat() + '">' +
                 f'{date.year} 年 {date.month:02} 月 {date.day:02} 日</time></p>', '</div>',
                 '<div class="change-copy">', '<ul>']
        lines += ['<li>' + html.escape(bullet.strip()) + '</li>' for bullet in notes[key]]
        lines += ['</ul>', '<p><a href="/#download">前往当前版本下载区 →</a></p>', '</div>', '</section>']
        additions.append(newline.join(lines))
    old = text[cards[0].start():].replace('当前稳定版', '历史版本', 1)
    return (text[:cards[0].start()] + newline.join(additions) + newline + old).encode('utf-8')


def public_releases():
    result = []
    for page in range(1, 101):
        request = Request('https://api.github.com/repos/LiuDaCheng666/orange-zomboid-launcher/releases?per_page=100&page=' + str(page),
                          headers={'User-Agent':'OZL-changelog-publisher', 'Accept':'application/vnd.github+json'})
        with urlopen(request, timeout=30) as response:
            batch = json.load(response)
        if not isinstance(batch, list): raise ValueError('Invalid public release response')
        result.extend(batch)
        if len(batch) < 100: return result
    raise ValueError('Public release pagination exceeded the bounded history range')


def http_health(origin, content):
    request = Request(origin.rstrip('/') + '/changes?verify=' + uuid.uuid4().hex,
                      headers={'User-Agent':'OZL-changelog-verifier', 'Cache-Control':'no-cache'})
    with urlopen(request, timeout=30) as response:
        if response.status != 200 or response.read() != content:
            raise RuntimeError('Public changelog differs from the verified candidate')


def publish(changelog, releases, version, expected_current, evidence, *, remote_factory=ChangelogRemote, health_check=None):
    expected_current = validate_current(expected_current); version_tuple(version)
    if health_check is None:
        raise ValueError('A public health check is required')
    target = expected_current + '/changes.html'
    run = BASE + '/test-runs/changelog-' + uuid.uuid4().hex
    staged, backup, rollback = [run + '/' + name for name in ('candidate.html', 'before.html', 'rollback.html')]
    receipt = dict(status='failed', version=version, current=expected_current, target=target, backup=backup)
    remote = remote_factory(); attempted = False
    try:
        if remote.resolve_current() != expected_current:
            raise ValueError('Physical current changed')
        before = remote.snapshot([target]); receipt['protected_before'] = before
        if json.loads(remote.read(expected_current + '/release.json'))['version'] != version:
            raise ValueError('Changelog version must match the live software release')
        old = remote.read(target)
        candidate = render(old, changelog, releases, version)
        receipt.update(before_sha256=sha(old), after_sha256=sha(candidate))
        if old == candidate:
            health_check(candidate); receipt['status'] = 'unchanged'; return receipt
        remote.configure([staged, backup, rollback], [run], [(staged,target,True), (rollback,target,True)])
        remote.mkdir(run)
        remote.write(backup, old); remote.write(staged, candidate)
        expected = {backup:sha(old), staged:sha(candidate)}
        if remote.hashes(expected) != expected:
            raise RuntimeError('Staged changelog hash verification failed')
        if remote.snapshot([target]) != before or remote.resolve_current() != expected_current:
            raise RuntimeError('Protected files or current changed before activation')
        if remote.read(target) != old:
            raise RuntimeError('Changelog changed concurrently; refusing to overwrite')
        attempted = True
        remote.replace_checked(staged, target, sha(old), expected_current, version)
        health_check(candidate)
        after = remote.snapshot([target]); receipt['protected_after'] = after
        if remote.read(target) != candidate or after != before or remote.resolve_current() != expected_current:
            raise RuntimeError('Concurrent changes detected after activation')
        receipt['status'] = 'published'; return receipt
    except BaseException as error:
        receipt['error'] = str(error)
        if attempted:
            try:
                current = remote.read(target)
                if current != old:
                    if current != candidate:
                        raise RuntimeError('Newer changelog edit preserved; rollback refused')
                    remote.write(rollback, old)
                    remote.replace_checked(rollback, target, sha(candidate), expected_current)
                    if remote.read(target) != old:
                        raise RuntimeError('Rollback verification failed')
                receipt['status'] = 'rolled-back'
            except BaseException as rollback_error:
                receipt['rollback_error'] = str(rollback_error)
        raise
    finally:
        receipt['finished_at'] = datetime.now(timezone.utc).isoformat()
        try:
            evidence = Path(evidence); evidence.parent.mkdir(parents=True, exist_ok=True)
            evidence.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding='utf-8')
        finally:
            remote.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', required=True)
    parser.add_argument('--expected-current', required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--changelog', type=Path, default=Path(__file__).resolve().parents[1] / 'CHANGELOG.md')
    parser.add_argument('--published-releases', type=Path)
    args = parser.parse_args()
    try:
        releases = json.loads(args.published_releases.read_text(encoding='utf-8')) if args.published_releases else public_releases()
        result = publish(args.changelog.read_text(encoding='utf-8-sig'), releases, args.version,
                         args.expected_current, args.evidence,
                         health_check=lambda content:http_health('https://orangezl.com', content))
        print(json.dumps({key:result[key] for key in ('status','version','target','backup')}))
        return 0
    except Exception as error:
        print('Changelog publication failed: ' + str(error), file=sys.stderr); return 1


if __name__ == '__main__': sys.exit(main())
