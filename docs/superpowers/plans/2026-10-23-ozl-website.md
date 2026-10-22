# OZL Website and Mirror Implementation Plan

> **For agentic workers:** Use executing-plans inline, task by task. Do not create additional checkouts or delegate implementation; use a separate read-only reviewer before merge.

**Goal:** Deploy the approved OZL product/download site and a byte-verified public release mirror on the specified server.

**Architecture:** Buildless static website served by Caddy. A separate Python release preparation tool verifies the public signed manifest and assets before producing a deployment tree. SSH deployment pins the approved host fingerprint and reads credentials only from the external handover file. Updating the launcher's default source is a separate rollout after domain HTTPS is operational.

**Tech Stack:** HTML/CSS/JavaScript, Node test runner with headless Playwright, Python 3 standard library plus the existing Paramiko dependency, Caddy.

## Global Constraints

- Work only in `E:/Projects/OrangeLauncher`; retain original launcher functions and Git history.
- Reuse original OZL logo and cover; no PCL assets, analytics, login, fabricated statistics or security certification.
- Domain `orangezl.com`; initial acceptance uses `http://101.201.143.163/`.
- Preserve all signed bytes and SHA-256 checks. Never serve trusted updater content over HTTP.
- No passwords, private keys, handover originals or private client data in published files or Git.
- Do not change BT-Panel, cloud firewall, SSH configuration or paid cloud settings.

## Task 1: Product website

Files: `website/index.html`, `website/site.css`, `website/site.js`, `website/release.json`, `website/assets/`, `tests/website.test.cjs`.

Interfaces: the page fetches `release.json` containing version, date, setup/portable URL, byte length and SHA-256. Download URLs are same-origin versioned paths or the exact official GitHub repository release path. Failures preserve GitHub fallback links. `site.js` uses text nodes for metadata and release notes.

- [x] Write browser tests: desktop/mobile have no horizontal overflow; primary downloads resolve to explicit versioned files; source and ICP links are present; expandable help works with keyboard; failed metadata fetch keeps a usable fallback; malicious metadata cannot inject markup or redirect downloads to another origin.
- [x] Run `node --test tests/website.test.cjs` and verify missing site behavior fails.
- [x] Implement a responsive light editorial layout: concise left hero, large right original cover, two download choices, feature rows, three-step guide, compact changelog and FAQ. Orange/charcoal with restrained shadows, meaningful typography and comfortable whitespace. Reused the cover directly rather than showing a fabricated launcher screenshot.
- [x] Run browser tests, inspect desktop/mobile screenshots and fix actual layout issues.

## Task 2: Verified release preparation

Files: `deploy/prepare_release.py`, `tests/test_prepare_release.py`.

Interfaces: `prepare_release(release_json, source_dir, destination)` verifies release metadata, manifest RSA/SHA-256 signature using the repository public key, allowed release asset names, version, length and SHA-256; emits immutable `releases/vVERSION/`, latest raw signed manifest/signature, and website display metadata. Inputs are previously downloaded public GitHub assets; no credentials accepted.

- [x] Write unit tests with a small fixture key, signed manifest and real temporary files: good assets are copied unchanged; wrong signature, altered length/hash, missing asset, wrong-version URL and traversal names fail without advancing latest.
- [x] Run Python unittest and confirm missing preparation behavior fails.
- [x] Implement validation and stage the release to a new directory; write latest only after the full release passes. Do not execute downloaded files.
- [x] Run tests and prepare the actual v1.2.13 public assets; compare installer, ZIP, manifest/signature and all content-addressed assets with public release hashes.

## Task 3: Deployment and maintenance

Files: `deploy/deploy_site.py`, `deploy/Caddyfile.ip`, `deploy/Caddyfile.domain`, `deploy/README.md`, `docs/workspace.md`, `docs/security/2026-10-23-defender-review.md`.

Interfaces: deployment reads external handover credentials, validates the approved SSH fingerprint before authentication, accepts a local prepared site tree, uploads a unique release, verifies remote hashes, then switches `/var/www/ozl/current`. It never changes `/www/server/panel`. Runtime credentials are not saved.

- [x] Test deployment-tree validation locally: allow only the exact public static file set, reject unexpected/secret files and missing index; reject symlinks before traversal.
- [x] Implement and test Caddy config with static root, no directory listing, attachment headers for downloads, short cache for metadata, immutable cache for versioned files and common content security headers.
- [x] Install Caddy from its documented official signed RHEL8 COPR repository, retaining original config. Validate as service user before starting/reloading. Upload site plus verified public release mirror; switch only after remote verification.
- [x] Confirm HTTP status, static assets/MIME, byte length and download SHA-256 from the public IP; verify no panel port/firewall changes. Record exact deployment, rollback path and pending DNS/HTTPS step.
- [x] Record the real Microsoft submission ID and status, local scan result/definitions, source/CI/sample hashes and the limitation that the historical quarantined local binary hash is unavailable.
- [x] Run `git diff --check`, website and deployment tests. User chose local main merge; commit and merge during finish. Do not publish a new launcher version before its separate updater migration and HTTPS validation.

## Separate rollout

After domain resolution and TLS validation, implement and verify self-host-first transport with GitHub fallback in a separate updater plan. Preserve original signed manifest validation; only map verified same-repository versioned resource URLs to the exact own-server mirror paths. Publish one new numbered launcher release after native update regression and real HTTPS download verification.
