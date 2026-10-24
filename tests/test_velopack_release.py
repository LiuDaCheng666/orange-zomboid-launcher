import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import unittest

import test_prepare_release as legacy_tests

SCRIPT = legacy_tests.SCRIPT

sys.path.insert(0, str(SCRIPT.parent))
from prepare_release import load_json


class VelopackReleaseTests(legacy_tests.PrepareReleaseTests):
    def setUp(self):
        super().setUp()
        self.legacy = self.source
        self.source = self.root / 'velopack'; self.source.mkdir()
        self.version = '1.2.15'
        self.package = 'OrangeZomboidLauncher-1.2.15-full.nupkg'
        payload = b'isolated package bytes'
        self.blobs = {
            self.package: payload,
            'OrangeZomboidLauncher-win-Setup.exe': b'isolated installer never executed',
            'OrangeZomboidLauncher-win-Portable.zip': b'isolated portable bytes',
            'assets.win.json': json.dumps([dict(RelativeFileName=n, Type=t) for n,t in [(self.package,'Full'),('OrangeZomboidLauncher-win-Setup.exe','Installer'),('OrangeZomboidLauncher-win-Portable.zip','Portable')]]).encode(),
            'releases.win.json': json.dumps(dict(Assets=[dict(PackageId='OrangeZomboidLauncher', Version=self.version, Type='Full', FileName=self.package, SHA1=hashlib.sha1(payload).hexdigest().upper(), SHA256=hashlib.sha256(payload).hexdigest().upper(), Size=len(payload))])).encode(),
            'RELEASES': f'{hashlib.sha1(payload).hexdigest().upper()} {self.package} {len(payload)}'.encode(),
        }
        self.feed = dict(schema=1,appId='OrangeZomboidLauncher',channel='win',version=self.version,fileName=self.package,length=len(payload),sha256=hashlib.sha256(payload).hexdigest().upper(),notes='- Velopack fixture')
        self.write_feed()
        self.write_inventory()

    def signed(self, data):
        from cryptography.hazmat.primitives.asymmetric import padding
        from cryptography.hazmat.primitives import hashes
        return self.key.sign(data, padding.PKCS1v15(), hashes.SHA256())

    def write_feed(self):
        raw=json.dumps(self.feed).encode()
        self.blobs['velopack-manifest.json']=raw
        self.blobs['velopack-manifest.sig']=self.signed(raw)

    def write_inventory(self):
        inventory=dict(schema=1,distribution='velopack',appId='OrangeZomboidLauncher',channel='win',version=self.version,date='2026-10-25',files=[dict(name=n,length=len(b),sha256=hashlib.sha256(b).hexdigest().upper()) for n,b in self.blobs.items()])
        raw=json.dumps(inventory).encode()
        for n,b in self.blobs.items(): (self.source/n).write_bytes(b)
        (self.source/'velopack-artifacts.json').write_bytes(raw)
        (self.source/'velopack-artifacts.sig').write_bytes(self.signed(raw))

    def prepare(self):
        from prepare_release import prepare_velopack_release
        return prepare_velopack_release(self.source,self.output,self.public.read_bytes(),self.legacy)

    def test_local_authenticated_candidate_preserves_legacy_and_needs_no_github(self):
        result=self.prepare()
        self.assertEqual(result['distribution'],'velopack')
        self.assertEqual(result['setup']['url'],'/releases/v1.2.15/OrangeZomboidLauncher-win-Setup.exe')
        self.assertEqual((self.output/'updates/latest/update-manifest.json').read_bytes(),(self.legacy/'update-manifest.json').read_bytes())
        self.assertEqual((self.output/'updates/velopack/latest/velopack-manifest.json').read_bytes(),self.blobs['velopack-manifest.json'])
        self.assertEqual(len(list((self.output/'releases/v1.2.15').iterdir())),10)
        with self.assertRaises(ValueError): self.prepare()

    def test_tampered_or_unsigned_candidate_is_rejected(self):
        (self.source/self.package).write_bytes(b'tampered')
        with self.assertRaises(ValueError): self.prepare()
        self.write_inventory()
        (self.source/'velopack-artifacts.sig').write_bytes(b'X'*256)
        with self.assertRaises(ValueError): self.prepare()

    def test_extra_secret_and_custom_helper_are_rejected(self):
        for name in ['secret.xml','橙子更新助手.exe']:
            (self.source/name).write_bytes(b'never upload')
            with self.assertRaises(ValueError): self.prepare()
            (self.source/name).unlink()

    def test_signed_wrong_feed_hash_version_and_foreign_field_are_rejected(self):
        for field,value in [('sha256','0'*64),('version','1.2.16'),('url','https://evil.example/package'),('schema',True)]:
            old=dict(self.feed); self.feed[field]=value
            self.write_feed(); self.write_inventory()
            with self.assertRaises(ValueError): self.prepare()
            self.feed=old

    def test_bad_feed_signature_even_with_valid_inventory_is_rejected(self):
        self.blobs['velopack-manifest.sig']=b'X'*256; self.write_inventory()
        with self.assertRaises(ValueError): self.prepare()

    def test_signed_framework_index_mismatch_is_rejected(self):
        index=json.loads(self.blobs['releases.win.json'])
        index['Assets'][0]['SHA256']='0'*64
        self.blobs['releases.win.json']=json.dumps(index).encode(); self.write_inventory()
        with self.assertRaises(ValueError): self.prepare()

    def test_boolean_inventory_schema_is_rejected(self):
        inventory=json.loads((self.source/'velopack-artifacts.json').read_bytes())
        inventory['schema']=True
        raw=json.dumps(inventory).encode()
        (self.source/'velopack-artifacts.json').write_bytes(raw)
        (self.source/'velopack-artifacts.sig').write_bytes(self.signed(raw))
        with self.assertRaises(ValueError): self.prepare()

    def test_legacy_signature_and_origin_must_be_authentic(self):
        self.manifest['files'][0]['url']='https://evil.example/file'
        # Sign only the preserved legacy fixture.
        raw=json.dumps(self.manifest).encode()
        (self.legacy/'update-manifest.json').write_bytes(raw)
        (self.legacy/'update-manifest.sig').write_bytes(self.signed(raw))
        with self.assertRaises(ValueError): self.prepare()

    def test_deploy_reauthenticates_candidate_and_uploads_both_feeds(self):
        self.prepare()
        import deploy_site
        checked=self.root/'checked'
        deploy_site.prepare_velopack_mirror(self.version,self.output,checked,self.public.read_bytes())
        paths=set(deploy_site.mutable_feed_files(checked))
        self.assertEqual(paths,{'updates/latest/update-manifest.json','updates/latest/update-manifest.sig','updates/velopack/latest/velopack-manifest.json','updates/velopack/latest/velopack-manifest.sig','updates/velopack/latest/releases.win.json'})
        (self.output/'releases/v1.2.15/private.xml').write_text('secret')
        with self.assertRaises(ValueError): deploy_site.prepare_velopack_mirror(self.version,self.output,self.root/'rejected',self.public.read_bytes())


# Base legacy tests remain in their own module.
for name in list(legacy_tests.PrepareReleaseTests.__dict__):
    if name.startswith('test_'):
        setattr(VelopackReleaseTests, name, None)

if __name__ == '__main__': unittest.main()
