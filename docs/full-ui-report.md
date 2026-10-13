# Full launcher Web UI implementation report

Implemented against `docs/integration-contract.md` and the approved full-launcher plan. Production edits are limited to `Orange3DTool/Web/index.html`, `app.js`, and `app.css`. Added `full-ui-tests.cjs` and `full-ui-capture.cjs`; no controller, host, backend, build, or Git changes were made by the UI worker.

## Delivered behavior

- Eleven routes: home, components, mods, workshop, saves, servers, shaders, settings, memory, maintenance, help. Compact navigation remains visible at 1100×760 and 733×507; narrow 500×760 also has no horizontal overflow. Existing dark/gold and light/blue themes and original author component covers remain intact.
- Named mod configurations support creating, copying, naming, editing ordered rich rows, snapshot saves/deletion, and explicit default activation. Rows show local author cover, title, ID, author, version, hard dependencies, and soft ordering constraints. Sort is a preview; dependency errors disable applying it. A profile can be copied into an unsaved per-save draft without writing the save.
- Saves and personal servers show original native thumbnails. A viewport observer reads at most two thumbnails concurrently; positive image cache is bounded to 48 and negative results to 512. Route generations prevent old requests from injecting into another page. Unknown or ambiguous pictures use a neutral placeholder.
- Servers have personal/game-favorites/public-hall tabs, explicit import, add/edit credential forms, and existing join actions. Hall items open a personal draft for role credentials. Metric badges separate count, capacity, and ping values with theme-aware high contrast. Passwords remain empty password inputs on edit; no plaintext credential response is expected or displayed.
- Steam connection uses the existing signed-in client, with subscribed items, keyword/B42 keyword search, pagination, details/dependency preview, and explicit subscription/download confirmation. Download does not activate mods. Remote preview URLs are not rendered under the existing CSP; known local author covers or a neutral icon are used. Native numeric Unix timestamps are normalized to seconds before display.
- Task center is globally accessible, including while a mutation runs. It shows durable native history, status, timestamps, messages and events. Unknown progress is indeterminate. Partial progress stays measured; an invalid failed/partial 100 value is shown as unknown rather than inventing 99. Completed, failed, partial, cancelled and interrupted states remain distinct.
- Home shows actual native today/total/session playtime, configurable reminder interval, and a dismissible inline reminder. A due reminder also appears as a launcher DOM dialog from other pages; no native window-focus action is invoked. Local day, interval and elapsed threshold identify each reminder, so repeated polling does not reopen it and a later threshold works even if the intermediate false snapshot was missed. Explicit dismissal sends only `{dismissReminder:true}`.
- Reads show indeterminate loading, errors and retry. Passive status/task/playtime polling preserves drafts and passwords. List navigation starts at the top; selecting a save/profile brings its editor into view. Search and hall reads reject late replies. Server and mod-profile writes preserve the snapshot corresponding to the bytes shown in their editor. A delayed profile copy cannot overwrite newer manual save edits.

## Verification evidence

All checks below ran against the current Web files. Browser tests use a synthetic WebView bridge; they do not execute real downloads, launches, file writes, account actions, server logins, or Steam mutations.

| Command | Result |
| --- | --- |
| `node --check Orange3DTool/Web/app.js` | exit 0 |
| `node Orange3DTool/Web/full-ui-tests.cjs` | 21/21 groups passed |
| `node Orange3DTool/Web/full-ui-capture.cjs` | 24 workflow screenshots; download confirmation cancelled |
| `node Orange3DTool/Web/extensions-ui-tests.cjs` | passed |
| `node Orange3DTool/Web/java-cover-ui-tests.cjs` | passed |
| `node Orange3DTool/Web/inherited-layout-ui-tests.cjs` | passed |

RED was observed before the new workflows were implemented: initial fixture suite was 0/6 due to missing routes, mod profiles, profile-to-save copy, server metrics, workshop and playtime. Additional regressions were reproduced before their fixes: opening server/profile snapshot replacement, delayed profile-copy overwriting manual edits, fabricated failure percentage, lost next reminder, preserved scroll after navigation, native seconds rendered as 1970, stale hall replies, raw help title HTML, missing refresh progress, 80 eager thumbnail reads, missing global reminder and zero-based CSS list markers. Each subsequently passed.

The suite covers eleven routes × two themes × three viewport sizes, untrusted native metadata, credentials kept out of card/notice text, confirmation packet boundaries, partial/unknown/terminal task states, retries, stale responses and snapshots, global reminder repetition, bounded thumbnail reads, and real displayed order numbers. Playwright resolves the normal installed package first, with the available bundled Windows runtime as fallback; `ORANGE_BROWSER_CHANNEL` can select the browser channel.

Forty-six **synthetic fixture** screenshots are under `artifacts/full-ui-shots`: 22 route/theme screenshots plus 24 editor, sorting, server, workshop, download confirmation, playtime/reminder and task-center screenshots. They are layout evidence, not proof of actual account/server/download success. Representative dark/light screenshots were visually inspected, including rich profile editor, separated server metrics, task history and the reminder.

Two older fixtures intentionally retain obsolete assertions: `profile-ui-tests.cjs` expects nine routes, and `ui-tests.cjs` expects only `status` at startup. The complete UI has eleven routes and a local `playtimeGet` home read. The UI worker did not edit existing test scripts; the integration owner should update those two expectations and provide the new read DTOs in their fixtures.

## Native integration boundary

No endpoint or DTO changes are requested. All new actions and payloads use the integration contract. Native write guards, snapshot refusal, secret storage, Steam app validation, journal sanitation and real process timing remain native responsibilities. Actual Steam connectivity, public hall availability, real original-thumbnail matching, multiplayer entry and launched-game outcomes were not claimed by fixture tests.
