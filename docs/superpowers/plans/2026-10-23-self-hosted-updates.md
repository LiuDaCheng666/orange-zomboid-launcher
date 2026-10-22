# OZL Self-Hosted Updates Implementation Plan

> **For agentic workers:** Use executing-plans inline and test-driven-development; independent read-only review before release.

**Goal:** Bind the approved domain with valid HTTPS, then release a launcher that checks/downloads from orangezl.com first and GitHub second.

**Architecture:** Preserve the signed GitHub-format manifest and all existing transaction checks. A source-selection helper obtains the mirrored envelope and maps validated versioned GitHub asset URLs to identical own-server files. Only network/availability failure falls back; bad signature/hash never becomes a success through fallback. Existing dependency-injection constructors keep their single-source test behavior.

**Tech Stack:** Existing C#/.NET Framework updater, native tests, Caddy, Aliyun DNS UI, existing signed release tools.

## Constraints

- Work in `E:/Projects/OrangeLauncher` without copying a new checkout. Preserve unrelated user changes.
- Add only `@` and `www` A records for `orangezl.com` to `101.201.143.163`; keep existing `pz` unchanged. No cloud purchases, IAM changes or security-group changes.
- HTTPS must use a normally validated public certificate; never disable certificate validation.
- Keep raw manifests/signatures, exact owned paths, bounded lengths, SHA-256, current embedded trust key, rollback and one-confirmation restart.
- Never upload signing private keys, server credentials, user logs or client configuration.
- Old versions learn the new transport through one ordinary GitHub update; do not claim DNS changes alter old binaries.

## 1. Domain and TLS

- [ ] Verify/add records in Aliyun UI, record public DNS answers, preserve pz.
- [ ] Back up Caddy config, validate the domain template as service user, reload, verify actual HTTPS and www/IP redirects.
- [ ] Fetch signed metadata/assets via HTTPS and check hashes; verify desktop/mobile website.

## 2. Source selection and download fallback

Files: new `Orange3DTool/CoreReleaseSources.cs`, `Orange3DTool/CoreReleaseSourcesTests.cs`; modify `CoreUpdateService.cs`, `CoreReleaseProtocol.cs` (URL-validation visibility only), `CoreUpdateServiceTests.cs` (run new tests).

Interfaces:
- `CoreReleaseSources.Lookup(CancellationToken, Func<string,CancellationToken,Stream>, Func<CancellationToken,byte[][]>, string key, string current)` returns original raw manifest/signature. Read own latest manifest, parse only bounded version metadata for the immutable signature URL, then authenticate it. If authenticated own version is older than current, consult GitHub instead of reporting a replay as latest.
- `CoreReleaseSources.AssetUrls(string signedUrl)` returns own-server mapping followed by the original canonical GitHub URL; rejects unsafe input using the existing URL rules.
- Existing 4-argument `CoreUpdateService` constructor preserves injected single-source behavior; a 5-argument variant accepts source candidates. Production uses own-server-first candidates.
- `CoreReleaseNetwork.Allowed` accepts exact own HTTPS metadata/versioned paths in addition to the existing GitHub hosts; own requests use a shorter timeout.
- Per-file download attempts reset partial bytes/hash/progress before GitHub fallback after a transport exception. Cancellation and integrity exceptions do not fall back. Cache only fully verified bytes.

- [ ] Write tests for own success/no GitHub call, offline/missing fallback, old authenticated own feed, invalid signature refusal, unsafe URL refusal, own allowlist and mid-stream retry from byte zero.
- [ ] Build native service test entry and run RED against the previous implementation.
- [ ] Implement the helper and scoped service changes. Keep all update envelope/transaction validation.
- [ ] Run new tests, existing network/fallback/service tests and full native/Web regressions.

## 3. Versioned release and mirror

Files: `VERSION`, `CHANGELOG.md`, `README.md`, `deploy/README.md`, versioned release/security validation records.

- [ ] Select next unused patch version (expected 1.2.14), record source/HTTPS/transport changes.
- [ ] Build signed core ZIP and installer in a fresh ignored output directory; scan actual deliverables with Defender and record result/hash/definitions. If a file is flagged, preserve evidence and stop that delivery for analysis, not a rebuild to evade detection.
- [ ] Verify packages/install/uninstall and independent code review. Do not launch the user's game or change its files.
- [ ] Publish source plus signed stable release assets to the existing GitHub repository; prepare and deploy exact mirrored assets to the own server. Latest pointers advance only after complete validation, with historical immutable releases retained.
- [ ] Isolated old-version default update discovers/downloads the new release; new version default update contacts HTTPS own source and reaches Latest/Ready as appropriate. Record actual source/bytes and no changes to the original install.
- [ ] Re-test HTTPS download headers/bytes, website version display and GitHub fallback. Merge/push verified source using the user's integration choice.
