# Official Release Feed Fallback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development for Task 1 implementation/review. The root coordinator owns the operational publication in Task 2. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Recover signed release checks from GitHub API HTTP 403/429 using only the official latest-download aliases, then publish 1.2.1.

**Architecture:** Keep release verification, file downloads and restart transactions unchanged. Add a narrow transport seam around the existing network lookup and a bounded fallback for the two approved HTTP statuses. Exercise parsing and verification with real production code and real loopback HTTP errors.

**Tech Stack:** C#/.NET Framework, PowerShell, existing CoreFixture tests, GitHub Releases.

## Global Constraints

- User-approved spec: `docs/superpowers/specs/2026-10-15-release-feed-fallback-design.md`.
- Only API HTTP 403/429 triggers fallback. Never weaken embedded trust, HTTPS hosts, redirect limits, cancellation, bounds, platform/path/version/hash rules or restart health checks.
- API and fallback 404 mean unavailable; malformed responses, other errors and invalid/mixed signatures fail closed.
- No game, Steam, user authorization, private-key disclosure or historical-package mutation.
- Reuse the existing feature checkout; delegate one implementation task at a time and use read-only reviews. Only the root coordinator publishes remotely.

### Task 1: Rate-limit fallback with regression coverage

**Files:**
- Modify: `Orange3DTool/CoreUpdateService.cs` (network lookup only).
- Create: `Orange3DTool/CoreReleaseNetworkTests.cs` (fixture transport and behavioral tests).
- Modify: `Orange3DTool/CoreUpdateServiceTests.cs` (run network tests with existing service suite).
- Modify: `CHANGELOG.md`, `使用说明.md`, `docs/core-release.md` (describe the actual behavior and legacy boundary).

**Interfaces:**
- Consumes: `CoreReleaseNetwork.Open(string,CancellationToken)`, existing signed CoreFixture manifests and `CoreUpdateService` verification.
- Produces: existing `Lookup(CancellationToken)` plus internal `Lookup(CancellationToken,Func<string,CancellationToken,Stream>)`; output remains `byte[][]` raw manifest/signature or null.

- [ ] Run existing regression baseline and retain logs. Existing real signed 1.2.0 probe already reproduced `Error: HTTP 403`; inspect response headers/body to confirm exhausted anonymous quota.
- [ ] Introduce the transport overload without changing branches, replacing direct `Open(...)` calls with the supplied transport. Add a normal API fixture using literal `{"draft":false,"prerelease":false,"tag_name":"v9.8.7"}` and signed 9.8.7 envelope. Assert fixed-tag downloads and production verification.
- [ ] Add a real loopback HTTP 403 exception fixture and a test expecting lookup of exactly API, official latest manifest alias and official latest signature alias. Run it against unchanged branches and record failure with HTTP 403 (not a compiler error).
- [ ] Implement only approved fallback branches. Dispose caught HTTP responses; check cancellation before alias requests. Read the same bounded manifest/signature sizes. Catch alias 404 as unavailable, propagate other failures.

```csharp
// Network routing only; CoreUpdateService.Check still verifies returned bytes.
catch (WebException e) {
    var response = e.Response as HttpWebResponse;
    if (response == null) throw;
    int code = (int)response.StatusCode;
    response.Dispose();
    if (code == 404) return null;
    if (code != 403 && code != 429) throw;
    return LatestEnvelope(token, open);
}
```

- [ ] Cover 429, API 404/500/no-response errors, malformed API, canceled lookup, alias 404/oversize, corrupted signature and mismatched signed envelopes. Assert service refuses unauthenticated offers and baseline files remain unchanged.
- [ ] Run the focused suite, full existing native/browser gates, package/legacy builders and native update lifecycle; obtain independent read-only review and resolve concrete findings.
- [ ] Commit only reviewed implementation/tests/docs; preserve unrelated working changes until publication preparation.

### Task 2: Rebuild and publish the signed stable release

**Files:**
- Existing publication runbook: `docs/superpowers/plans/2026-10-15-github-publication.md`.
- Create: `docs/releases/1.2.1-github-release.md` after live verification.
- Generated ignored output: `dist/1.2.1-github-release/`, `artifacts/github-release-121/`.

**Interfaces:**
- Consumes: reviewed 1.2.1 source, existing signing key by path only, unchanged formal public key.
- Produces: ordinary fast-forward main push, immutable v1.2.1 tag, stable latest Release containing installer, ZIP, signed envelope and all declared `.bin` files.

- [ ] Follow publication runbook: fresh build, complete cryptographic/file-set checks, protected historical hashes, fresh remote ancestry, source commit and normal FF push/tag.
- [ ] Upload all exact assets to a draft; verify remote names, lengths and digests before publishing as latest.
- [ ] Use actual signed 1.2.1 default network lookup to verify live fallback when quota is still exhausted. Verify older signed client check/download when API permits; explicitly report if the old client itself still requires quota reset/manual installation.
- [ ] Keep live network check/staging evidence separate from isolated native restart/rollback health evidence; do not claim real game testing.
- [ ] Save source/tag/asset hashes and verification boundaries in the release record, push documentation closeout without moving the release tag, then provide public download links.

## 执行收尾

Task 1 已实现、RED/GREEN 验证并通过独立任务／最终发布合同复核；Task 2 已发布稳定 latest。14 原生／11 浏览器门禁、打包入口、五例原生重启生命周期、正式签名和全部远端资源核验完成。旧 API 默认客户端仍受限的情况按规格披露，不伪装其成功。实际提交、产物哈希、异常保留与验证边界见 `docs/releases/1.2.1-github-release.md`。
