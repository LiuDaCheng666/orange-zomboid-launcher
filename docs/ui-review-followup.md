# Final independent UI/contract follow-up

Reviewed current UI and host contract integration on 2026-10-13. This follow-up supersedes the unresolved findings in `ui-review.md`. The review made no production source, index or Git changes.

## Verdict

**No remaining Critical or Important finding in the reviewed UI/contract scope. Ready for final packaging verification.** A fresh `node Orange3DTool/Web/full-ui-tests.cjs` run completed with **21/21 test groups passed**.

The former Important reminder and thumbnail findings are resolved. The global reminder dialog (`Web/app.js:132`) is polled on every route (`app.js:163`), shown once per elapsed local-day threshold, deferred while another dialog is open, and dismissed explicitly. The suite verifies a reminder on another page without losing a dirty configuration. Original thumbnail reads use viewport observation and at most two requests in flight; the long-save-list test passes. Ordered lists now display actual one-based positions after reordering.

## Requested contract checks

- **Unavailable Steam subscription entries remain truthful.** `SteamLauncherServices.cs:50` preserves the subscribed ID and actual item-state flags, supplies an explicit inaccessible title/explanation, and returns `updated:null`. An additional synthetic probe using that exact response shape displayed `工坊条目不可访问 · 999`, the permission/deletion explanation, and `修改时间未知`. It displayed no subscription-card download button. A subsequent failed `steamDetails` response showed retry and still exposed no download button; zero `steamDownload` requests were sent. There were no page errors.
- **Real Steam update seconds are handled.** Native `ParseUgc` returns Unix seconds (`SteamLauncherServices.cs:47`), and `shortDate` converts numeric second values (`Web/app.js:78`). The fresh suite includes the numeric 2026 timestamp case and excludes the erroneous 1970 rendering. Null timestamps remain unknown.
- **Native action spelling and source guards match.** `ModernHostPolicy.cs:7` allowlists the new read/write names. Its validator requires the exact local HTTPS host/index document, no user-info or nondefault port, a bounded envelope and an object payload. `ModernController.cs:45` onward dispatches the agreed mod, thumbnail, favorites, Steam and playtime names; `serverHall` maps to native `hall`, and Steam suffix actions map to `connection`, `subscriptions`, `search` and `details`. `steamDownload` retains explicit confirmation and the native guard before mutation. No route-name/action mismatch was found.
- **Task/playtime snapshots do not cancel queued mutations.** `ModernHost.cs:38` uses `ExecuteSnapshot` for these two actions, and only explicit cancellation uses the dispatcher's generation-changing bypass. `ModernDispatchQueue.cs:7` runs snapshots independently. A fresh in-memory probe held an ordinary read, queued a mutation, fetched both task and playtime snapshots, then released the read: the queued mutation returned `saved` rather than cancellation.

The fresh suite also covers escaped native HTML, confirmed downloads without automatic activation, returned profile collection-stamp binding, server dialog opening stamps, profile transfers preserving newer manual edits, task history/unknown progress, both accepted themes and eleven routes at 1100×760 plus narrow windows. Automatic Memory reads, original local covers and measured-time display remain intact.

## Minor

`Web/app.js:99` still has `maxlength="120"` on `#mod-profile-name`, confirmed through the live synthetic DOM probe. Native profile names remain bounded to 100. Root has been notified to align the field limit; this does not lose drafts or bypass native validation and is not a release-blocking Important finding.

## Limits

All browser checks used the real HTML/CSS/JS with synthetic native responses; screenshots are confined to canonical `artifacts`. The dispatch-queue probe compiled source in memory. This review did not download or subscribe to Steam items, launch a game, mutate personal files, run server logins or claim actual multiplayer success. Root owns embedded WebView2 live-read captures and package/installer verification.
