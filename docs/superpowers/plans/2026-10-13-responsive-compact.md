# Responsive Compact Launcher Implementation Plan

> **For agentic workers:** Use executing-plans with independent parallel domains and review checkpoints. Steps use checkbox syntax.

**Goal:** Deliver the approved compact component cards, mod list/detail workspace, streamed hall and responsive exact-file approval flow.

**Architecture:** Steam hall batches cross worker stdout and native WebView events, scoped by requestId. Network reads have an independent cancellable controller lane; local mutations retain serialization. UI uses semantic statuses and local row pending state, never auto-grants authorization.

**Tech Stack:** .NET Framework 4.8 C#, native Steam flat API, WebView2, vanilla JS/CSS, Playwright.

## Global Constraints

- Canonical source E:/Projects/OrangeLauncher; feature branch in place, preserve main and all unrelated files.
- Do not launch a real game, modify real authorization, subscriptions, saves, or mod configuration while testing.
- Original author covers only, no new AI imagery.
- Green is configured/allowed, red is update/rejected/error, gray is absent/unknown; configured is not verified loaded.
- All file modifications remain guarded and serialized. Do not change author-wide Java trust.
- Tests use isolated synthetic directories; unknown progress remains indeterminate.

## Task 1: Stream public hall

Files: SteamLauncherNative.cs, SteamLauncherServices.cs, SteamLauncherServicesTests.cs.
Interface: add optional Action<object> hallBatch parameter to Execute/Transport, emit bounded nonce-validated stdout `{kind:"hallBatch",nonce,servers:[...]}`; current final DTO unchanged.

- [ ] Test an incremental transport event delivered while final worker remains pending; reject mismatched nonce and malformed row.
```csharp
bool completed=false,receivedBeforeFinal=false;
// Synthetic worker writes one valid batch then waits for acknowledgement before final response.
// Assert callback sees one literal server and !completed; actual transport is exercised.
```
- [ ] Run the failing synthetic service tests with Build-Tests.ps1 and --self-test.
- [ ] Poll responsive native rows during the existing bounded 25s loop, deduplicate IDs, send first immediately then bounded batches; never wait for completion to emit first row.
- [ ] Preserve cancel/release/shutdown and final validated response; run service tests and independent read-only real hall probe if available.

## Task 2: Local/native responsiveness

Files: ModernHost.cs, ModernController.cs, ModernDispatchQueue.cs, ModernControllerTests.cs.
Interface: dedicated ExecuteNetwork(action,payload); NativeNetworkReads allowlist of Steam reads; controller Event callback forwards `{event:"hallBatch",data:{requestId,servers}}`. Network progress uses its own event, not mutation task state.

- [ ] Test local reads/mutations complete while a network request is pending; mutation queue remains serial and polling does not cancel queued writes.
```csharp
var queue=new ModernDispatchQueue();
// Hold network delegate, execute local snapshot + queued mutation, assert both finish before release.
```
- [ ] Run RED tests; implement bounded independent network lane, cancel old hall on refresh, cancel all on close/cancel; refuse directory changes while network references old engine.
- [ ] Forward valid hallBatch with original client requestId; run controller/dispatch/host tests.

## Task 3: Approved compact frontend and approval flow

Files: Web/app.js, Web/app.css, new Web/compact-ui-tests.cjs, relevant existing UI tests.
Interfaces: serverHall payload requestId string; hallBatch event merges only current requestId. Java save returns authoritative new List DTO; UI publishes it before refreshing status.

- [ ] Add browser RED regressions for incremental hall, local sort/filter, already-allowed no-op, fast authoritative approval feedback, row-specific busy, compact visible counts, master/detail draft preservation.
```javascript
await page.evaluate(()=>window.chrome.webview.emit({event:'hallBatch',data:{requestId:window.orange.state.hallRequestId,servers:[{id:'hall:1',name:'first',players:4,maxPlayers:32,ping:22}]}}));
// Assert first server visible before pending final request resolves.
```
- [ ] Implement compact component original cover layout, consistent status colors, mod master/detail list, shorter home stats and dense secondary routes.
- [ ] Implement Java pending-first grouping, collapsible allowed list, disabled repeated decisions, row busy, no status refresh before applying successful approval DTO; preserve confirmation hash and safety.
- [ ] Stream hall rows while pending; client sort players/ping/name, empty/password/player/latency filters; keep old generations ignored and error/cancel honest.
- [ ] Run all UI tests, capture 11 pages in dark/light plus populated workflows; inspect screenshots and adjust only failures/approved density requirements.

## Task 4: Review and delivery

- [ ] Independent source review against spec; correct important issues and re-review.
- [ ] Fresh full C# and browser regression; private Build-Setup.ps1 -PrivatePresets, packed self-test and --verify-setup.
- [ ] Copy verified outputs to dist/current, record hashes and measured evidence in docs; retain source branch. No third-party public binary release.
