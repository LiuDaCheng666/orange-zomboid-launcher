# Compact responsive frontend report

Implemented Task 3 from the approved `2026-10-13-responsive-compact` design and plan in `E:/Projects/OrangeLauncher`, on `fix/compact-responsive-ui`. Frontend changes are limited to `Orange3DTool/Web/app.js`, `app.css`, the new `compact-ui-tests.cjs`, and this report.

## Result

- Components remain cards. Small canonical original-author covers retain their complete aspect ratio. Names, version, configured state, update state, and installation controls remain visible; full description, provenance, fingerprint, compatibility, and maintenance actions expand on demand. Green means configured, red means update/reapply, and gray means absent or unverified. Every configured card still states that this game session's loading is unverified.
- Both save and profile mod catalogs use dense selectable rows plus a description/source/dependency detail panel. Selection and filtering do not replace names, ordered drafts, save snapshots, or unsaved edits. Existing dependency sorting, explicit save confirmation, conflict handling, and lazy bounded cover transport remain intact.
- Home has a configured-component count, shorter navigation summaries and playtime panel, hardware summary, and the explicit Java-review entry within the tested 1100×760 desktop viewport.
- Java review puts undecided files first and focuses the first decision control after reads finish. Allowed files are green and collapsed, with no repeated Allow action. Full paths and hashes remain expandable and copyable. Explicit per-file confirmations are queued serially, each subsequent request uses the preceding authoritative snapshot, and only the submitted hash shows pending state. Successful `javaSave` data is published before a background status refresh; a stale concurrent `javaList` cannot undo it. Errors leave the file undecided.
- Hall requests carry a unique `requestId`. Current-generation batches render before the final promise completes and deduplicate by server ID. Name filtering, player/ping/name sorting, empty/password filters, minimum players, and maximum ping operate immediately on received rows. Unknown ping sorts last. Final replies reconcile the list; stale/cancelled generations cannot append. Cancellation sends `{scope:"hall", requestId:<captured-current-id>}` and does not request blanket mutation cancellation.
- Global task state disables unsafe native writes while preserving navigation, reads, local filtering, and editable drafts. `serverQuery` uses the independent native read lane and no longer starts a global mutation lock. Editors opened while a mutation is already pending also apply the write lock. While an editor's own save is pending, only its fields and draft-replacement controls lock and its Save label reads “保存中”; other editor drafts remain editable. The owning lock remains until the write's receipt and required reread finish, preventing post-submit changes from being cleared by completion.

## Regression evidence

The initial browser RED run returned **1/7**: no hall generation/filter/cancel support, repeated allowed-file grants, globally locked local filters, and only two complete component cards at 1920×1080. Additional RED runs demonstrated the stale Java-list overwrite, invisible initial Java decisions, home Java entry below the desktop viewport, newly opened server editor missing its write lock, and missing cancellation generation. Each was fixed before its corresponding GREEN rerun.

| Command from `E:/Projects/OrangeLauncher` | Result |
| --- | --- |
| `node Orange3DTool/Web/compact-ui-tests.cjs` | 19/19 groups passed |
| `node Orange3DTool/Web/full-ui-tests.cjs` | 21/21 groups passed |
| `node Orange3DTool/Web/ui-tests.cjs` | passed; core transport, dirty editing, busy/progress/error, route/theme/width checks |
| `node Orange3DTool/Web/profile-ui-tests.cjs` | passed; ordered save editing, escaped metadata, secrets and snapshot CRUD |
| `node Orange3DTool/Web/java-cover-ui-tests.cjs` | passed; explicit exact-hash approvals, changed hashes, running locks, bounded lazy covers |
| `node Orange3DTool/Web/inherited-layout-ui-tests.cjs` | passed; inherited combined confirmation, long-string overflow, short dates and Java entry |
| `node Orange3DTool/Web/extensions-ui-tests.cjs` | passed; optional selection, source/reapply evidence, metadata-only update check, safe server rich text |
| `node --check Orange3DTool/Web/app.js` | passed |
| `node --check Orange3DTool/Web/compact-ui-tests.cjs` | passed |
| `git diff --check -- Orange3DTool/Web/app.js Orange3DTool/Web/app.css Orange3DTool/Web/compact-ui-tests.cjs docs/compact-ui-report.md` | passed |

All browser testing uses a synthetic WebView bridge. No real game was launched, no real permission or game configuration was written, no subscription/download was performed, and no account state was changed. Native service/controller checks and packaging are owned by the coordinating worker and are not claimed by this frontend report.

## Independent review correction: own-save draft protection

The final source review found that permitting draft edits during a global mutation also permitted edits during that draft's own pending save; the subsequent authoritative completion cleared those later edits. Seven added regressions initially returned RED (**11/18**) for profile, save-mod order, memory, settings, reticle, server-error recovery, and profile-error recovery. The matching reminder-setting case also failed RED before its correction.

An action-specific lock now covers the complete write/status/reread transaction for profile, save, memory, settings, reticle, server, and reminder editors. Profile open/copy/new/reread and save open/reread/profile-load are included because they replace the same editor context; top-level routes and unrelated editor roles remain usable. Locks survive rerendering and release in `finally`, preserving the draft after an error. Readonly settings stay readonly after unlock; server passwords remain only in their masked form controls. Server close/Escape is deferred during its pending write. The delayed-write fixtures verify new authoritative profile/save/memory/settings snapshots, stored reticle/reminder values, same-role context protection, other-role edit preservation, and both error paths. Frontend-only correction; no native guard changes.

## Captures and visual inspection

Absolute screenshot directory: `E:/Projects/OrangeLauncher/artifacts/compact-ui-shots/`.

- `dark-components-populated.png`, `light-components-populated.png`: 1920×1080, all 12 complete component cards visible with original covers and semantic status colors.
- `dark-mods-populated.png`, `light-mods-populated.png`: 1920×1080, 13 complete rows visible in the focused list; description, source, dependencies and selected state remain in the adjacent detail panel.
- `dark-home-populated.png`, `light-home-populated.png`: 1100×760, component count, playtime, hardware, and Java-review control visible.
- `dark-java-populated.png`, `light-java-populated.png`: 1100×760, two undecided files show their full evidence and decision controls while the green allowed group is collapsed.
- `dark-hall-streaming.png`, `light-hall-streaming.png`: 1100×760, nine synthetic rows received while the final query remains pending, truthful discovered count, indeterminate read progress, local filters and cancellation.
- `dark-all-routes-review.png`, `light-all-routes-review.png`: contact sheets of all 11 source-asset routes, inspected for giant empty cards, clipping, overflow and disproportionate spacing.

Original all-route screenshots are under `E:/Projects/OrangeLauncher/artifacts/full-ui-shots/{dark,light}-{home,components,mods,workshop,saves,servers,shaders,settings,memory,maintenance,help}.png`. The full suite also checks all routes at 1100×760, 733×507, and 500×760 in both themes, including horizontal overflow and visible navigation. Empty source fields no longer reserve description-sized space. Captures are layout evidence from synthetic data, not proof of live account, permission, download, or server success.
