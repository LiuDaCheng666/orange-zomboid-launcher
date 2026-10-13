# Independent Web UI and host integration review

Reviewed current `Web/app.js`, `app.css`, `index.html`, `full-ui-tests.cjs`, `ModernHost.cs`, `ModernHostPolicy.cs`, `ModernDispatchQueue.cs`, and related native DTO producers against `docs/integration-contract.md` on 2026-10-13. UI/host source review was read-only; this reviewer changed only this report within that review scope and ran synthetic fixtures/probes. Separately authorized mod-backend review corrections are documented in `mod-workspace-report.md`.

## Assessment

No Critical issue found. Ready with the Important fix below. The completed reviewed browser baseline passed **18/18 groups**. The owner's newer 21-group suite is currently in a TDD RED cycle for the global reminder modal and explicit ordered-list positions (16/21 passed on this reviewer's latest run); final GREEN remains for the owner/root to verify. Several issues identified early in review were fixed by their owners while this review was running; those verified corrections are listed separately, rather than reported as outstanding bugs.

## Important

1. **The requested due-reminder modal is missing, and reminder polling runs only on Home.** `Orange3DTool/Web/app.js:132` renders a due reminder as an inline Home card; `app.js:159` polls playtime only when the current route is Home. The contract explicitly requires a modal once per due threshold. A synthetic `reminderDue:true` response produced zero open dialogs. While viewing components, saves or another route, a due threshold will not be polled or announced until Home is visited. Add a global modal keyed to the current local day/elapsed threshold, keep the current explicit native dismissal, and exercise it on another route and during task polling. The newly fixed threshold key already prevents repeated announcements for the same dismissed threshold.

## Minor

1. **The profile name field allows names the native API will reject.** `Orange3DTool/Web/app.js:99` uses `maxlength="120"`, while `ModConfigurations.cs:23` accepts at most 100 characters. Match the input limit to the native bound and handle the suffix added by profile copying. Current native validation is safe and preserves the draft on refusal; this is avoidable UI friction.

## Corrections verified during review

- **Snapshot polling no longer cancels queued mutations.** The initial `ModernHost.cs:38` passed task/playtime reads through the dispatcher's cancellation bypass. An in-memory probe demonstrated that one task snapshot cancelled a queued mutation. Current host integration uses `ModernDispatchQueue.ExecuteSnapshot` (`ModernDispatchQueue.cs:7`), which does not advance the cancellation generation; the same queued mutation now finishes as `saved`.
- **Actual Steam timestamps render correctly.** Native `SteamLauncherServices.ParseUgc` emits Unix seconds. The previous `shortDate` treated numeric values as milliseconds, which the ISO-string fixtures missed. Current `app.js:78` converts seconds; a probe with `1700000000` displays `2023/11/15 06:13` in the configured local timezone. The suite now covers this real field shape.
- **Stale hall results are ignored.** Current `app.js:138` uses a hall request sequence. Releasing the newest reply before an older reply leaves the newest hall result visible, with the newest search text intact.
- **Native help titles are escaped.** Current `app.js:26` escapes button labels. A native component-title probe containing an injected action button produces zero injected buttons. Existing source links, descriptions, account names, task titles/events and catalog content use text/escaping; external workshop preview URLs are not rendered as remote images.
- **Reminder dismissal is bound to the elapsed threshold.** Current `app.js:131` computes a day/interval/time bucket. After dismissing the first threshold, changing native `todaySeconds` to the next bucket while `reminderDue` remains true displays the next reminder. It no longer depends on observing an intermediate false state.
- **Pending list refreshes show indeterminate progress.** The newly added hall-refresh case initially failed while its implementation was in flight. Current `readPending` (`app.js:94`) reports a hall read even when the previous result remains visible; the final browser suite passes that case.
- **Late profile transfers cannot replace newer save edits.** Current test coverage includes manual save editing while a profile read is pending, alongside returned collection-stamp binding and the server dialog's opening-stamp preservation.
- **Thumbnail work is now visible and bounded.** The initial 80-save probe queued 80 reads despite only four visible cards. The owner subsequently added viewport observation (`app.js:107`) and a two-request queue (`app.js:106`). The same probe now issues only two initial requests for those 80 rows, preserving exact-byte rendering and avoiding hidden-card read backlogs.

## Verified requirements

- The profile and sort UI consumes the agreed native fields (`profiles`, `activeMods`, `stamp`, `mods`, `warnings`, `errors`, `changed`). Sorting is a preview before applying to a draft, and profile-to-save transfer does not send `saveSave` implicitly.
- The first visit to Memory sends `memoryGet` and fills the current heap value automatically. A native-shaped synthetic response with `currentGiB:6` populated the number input with `6`; no fabricated default is required.
- Ordered enabled rows retain original local cover rendering, title, author, version and hard-dependency information. Cover bytes are constrained to local base64 image formats; workshop remote preview URLs use a neutral icon unless a local catalog cover is known.
- Favorites import and workshop subscribe/download require explicit confirmation. Hall selection seeds a server credential draft, and passwords stay hidden; no automatic launch, download or activation is performed by browsing.
- Task snapshots remain accessible during active work through the independent host snapshot path. History distinguishes completed/partial/failed/cancelled/interrupted results and retains unknown progress instead of manufacturing a percentage.
- Eleven routes remain visible with no horizontal main-panel overflow in both accepted themes at 1100×760, 733×507 and 500×760. Inspected synthetic screenshots for dark Home/Servers and light Mods show compact complete navigation and clearly separated server metric labels/values.
- Host source/origin validation, request-size/action allowlists, disabled host objects/new windows/permissions, and local-only resource filtering remain in place. The Web CSP blocks remote connections and arbitrary script sources.

## Evidence and limits

`node Orange3DTool/Web/full-ui-tests.cjs` completed with `18/18 full UI test groups passed` before the owner added the global-modal/ordinal tests. The subsequent expanded suite returned `16/21` during its expected implementation cycle: four missing reminder-modal controls and one missing explicit ordinal element; its new viewport-thumbnail case passed. This writes only synthetic screenshots to `artifacts/full-ui-shots`. Additional browser probes used the same synthetic bridge to inspect actual numeric timestamp rendering, reversed hall replies, native-title HTML, reminder thresholds, automatic memory reads and an 80-save thumbnail list. The dispatch-queue probes compiled source in memory and performed no native game/account/file mutations.

These browser tests use the real HTML/CSS/JS with a synthetic native transport. They do not establish real Steam queries, multiplayer success, real game-process timing or the embedded WebView2 runtime's final capture behavior; root owns native live-read validation and final packaging. No Git operations or production-source edits were made for this review.
