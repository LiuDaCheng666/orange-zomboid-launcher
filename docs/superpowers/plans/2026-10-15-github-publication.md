# GitHub 1.2.1 publication plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans for implementation work. This is an operational publication of existing reviewed functionality: the controller owns authorized remote writes and release verification; an independent read-only agent audits publication inputs. If runtime changes become necessary, stop publication and use the implementation/test/review workflow first.

**Goal:** Publish current source, stable1.2.1 installer/ZIP and all signed update resources so existing trusted launchers can check, download, and safely restart.

**Architecture:** Keep the existing RSA trust root,18-file allowlist, GitHub latest-release endpoint and transactional updater. Build a new public candidate after updating bundled release instructions, preserving the previously accepted local candidate. Verify uploaded draft assets before making the release latest; verify the actual public network transport separately from isolated native restart lifecycle tests.

**Tech Stack:** Git/GitHub CLI, C#/.NET Framework4.8, WinForms/WebView2, existing signed release tooling, isolated fixture roots.

## Global constraints

- User explicitly authorizes source push and stable1.2.1 publication; previous local-only instructions are historical for previous tasks.
- Keep version1.2.1, existing public trust bytes and signed18-file allowlist; never read/print/copy/regenerate the official private key.
- Preserve dist/1.2.1-installer-verified and accepted1.2.0/1.1.1 artifacts; use fresh dist/1.2.1-github-release for public bytes.
- No real Steam/game/save/pagefile/registry changes, automatic game launch, Java authorization or third-party payload publication.
- Git pushes must fast-forward; no force pushes, overwritten tags, clobbered release assets, or unrelated history changes.
- The controller performs all GitHub mutations. Tests/downloads/staging use isolated fixtures, not real launcher/game installations.

## Task 1 Publication preparation

- [ ] Read current updater/feed/resource contract and verify remote main is an ancestor, no existing v1.2.1 release/tag, and no tracked private credentials. Independent audit writes artifacts/github-release-121/preflight-review.md only.
- [ ] Update README download/update instructions, current1.2.1 CHANGELOG text and current usage instructions; preserve historical verification records. Add Chinese release notes in artifacts/github-release-121/release-notes.md, clearly distinguishing first install, updating signed core builds and legacy singleEXE migration.
- [ ] Use an external fixture-only Framework reflection probe of actual accepted assemblies: before publication, old1.2.0 default CoreUpdateService must return Unavailable; no transport or trust injection. After publication it must return Available1.2.1 and stage verified downloads without altering baseline files. Current1.2.1 must return Latest. This probe does not run actual game or modify user profiles.

## Task 2 Fresh release gates and build

- [ ] Run archived coordinator-regressions.ps1 with a fresh publication prefix (14native/11browser), Test-CorePackageBuild.ps1 -ExpectedVersion1.2.1, Test-LegacySetupBuild.ps1 -ExpectedVersion1.2.1 and Test-CoreNativeUpdate.ps1 with a fresh short confined fixture. Native restart fixtures use temporary keys, separately from actual public-feed verification.
- [ ] Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.2.1-github-release. The existing signer consumes the path; no agent reads the key contents.
- [ ] Adapt the archived full formal verifier only for the new candidate-directory prefix. Verify RSA signature,18file hashes/lengths,20ZIP entries, three core EXEs and setup VERSION metadata, current source/assets/guide identity, actual setup extraction/scoped uninstall and all historical protected hashes. Freeze source/payload bytes before push.

## Task 3 Authorized source and release publication

- [ ] Commit only intended source/document changes. Fetch origin main, verify it is still an ancestor, then git push origin HEAD:refs/heads/main (normal fast-forward). Keep current local feature checkout; no branch deletion or worktree creation.
- [ ] Create annotated v1.2.1 tag at the exact release-source commit and push that tag without force. Create a GitHub draft using gh release create v1.2.1 --verify-tag --draft with Chinese notes and exactly the candidate's22top-level assets:18content-addressed .bin files, installerEXE, fullZIP, update-manifest.json and update-manifest.sig. Do not upload core/, private resources or fixture outputs.
- [ ] Read the draft asset API; check the full name set, lengths, uploaded state and server SHA256 digests. If digest unavailable, download into a new fixture and compare exact hashes. Publish with gh release edit v1.2.1 --draft=false --prerelease=false --latest only after all22match.

## Task 4 Public acceptance and handoff

- [ ] Verify anonymous public latest API reports stable v1.2.1. Run actual old-version check/download/stage probe and current-version Latest probe through the production transport. Download public installer/ZIP and compare exact hashes with the frozen public candidate.
- [ ] Recheck source remote/tag identities and public assets, preserve logs/fixtures. Record source commit, GitHub URL, hashes, timestamps and precise network-versus-native-test boundaries in docs/releases/1.2.1-github-release.md; push documentation-only closeout without moving v1.2.1 tag or rebuilding published bytes.
- [ ] Tell players to use 顶部“启动器更新”→检查→下载→保存编辑并退出游戏→确认重启. Legacy builds without an embedded trust root need one manual install; do not promise unsupported legacy self-update.
