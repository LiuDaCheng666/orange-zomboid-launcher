# Task 6 report — client-only server Workshop preparation

Implementation commit: `7d9a30f` (`Add client-only server Workshop manifests and background preparation`). Started from task BASE `7c5f4d2`; controller-owned documentation commits `7f87d1a` and `e1e8145` landed independently and were preserved. No publishing, deployment, release metadata, version, signing, security settings, Java authorization, or server-side changes.

## Delivered contracts

- Native actions: `serverWorkshopGet {id}`, `serverWorkshopImport {id,stamp,text,confirmed:true}`, `serverWorkshopPrepare {id,stamp,confirmed:true}`, `serverWorkshopStatus {}`, `serverWorkshopCancel {jobId}`. Host/controller enforce bounded exact field sets; saved opaque server ID resolves public endpoint without decrypting credentials.
- Catalog exposes `id,endpoint,stamp,source,capturedUtc,items:[{id,title,localPresent}],summary`. Sources are `none`, `imported`, `connection`. Known local titles reuse the existing display catalog; no online item-detail fanout. Local directory presence is labeled only as local presence.
- Imports accept `WorkshopItems=...` or comma/semicolon/LF/CRLF positive decimal Steam ID lists, preserve unique first-seen order, retain IDs as strings, and reject malformed/empty/zero/out-of-ulong-range inputs, over 65536 characters, or over 2000 unique IDs. Errors do not echo raw input. No Mods/default/config/save writes or Steam calls occur on import.
- Endpoint-hashed records are atomically stored in `Data/server-workshop`. Stamps bind the endpoint, saved-server store revision and manifest revision. Old manifests cannot be used after endpoint or import changes. No complete list is inferred from public server descriptions or ModID order.
- Preparation returns immediately, uses the existing injected Steam service `download` boundary and existing controller SDK semaphore, and never enables mods or launches. The existing service remains responsible for App108600/subscription/download/install proof; the coordinator also checks returned ID identity. The item delegate is replaced only by controlled fixtures in tests.
- One job per controller; duplicate starts of the same manifest return the same job. Each job owns a separate `LauncherJournal` instance on the existing shared locked store. Settings/server edits remain available. The engine/endpoint/ID list/stamp are fixed at start. Launch and disposal cancel/drain only the owned preparation worker before continuing existing launch handling. Restart preflight blocks active preparation.
- Progress snapshots/event `serverWorkshopProgress` contain `protocol:1,jobId,revision,serverId,endpoint,manifestStamp,state,totalItems,completedItems,currentItem,progress,message,items`. Success counts require service proof; real percent/byte text is forwarded and unknown percent remains -1. Results distinguish completed/partial/failed/cancelled; durable per-job start/final snapshots recover abandoned jobs as interrupted. Interrupted snapshots are last durable checkpoints, not a claim that no additional Steam activity occurred before interruption; retries reverify all items.
- Saved server cards and the selected home-server target expose a compact nonmodal panel. Source/time and item/local-present counts are visible. Import is explicit; subscribing requires one batch confirmation. The drawer updates only its progress region, preserves ordinary drafts and navigation, rejects old revisions/retired jobs/delayed status responses, and never auto-launches. Existing group contact and three core cards remain intact.

## Capture format, conditions and limits

Existing owned `OrangeLaunchBridge.lua` conditionally observes `Events.OnServerWorkshopItems` without replacing the vanilla callback or confirming anything. Only `Required` during this launcher's pending server connection is accepted. It reads the original Java list using `size()` and `get(i-1)`.

Fixed cache file: `User/Lua/OrangeLauncherWorkshop.txt`, written through the original `getFileWriter` API:

```text
ORANGE_WORKSHOP_1
<existing request nonce>
<host>:<port>
<count>
<string Workshop ID 1>
<string Workshop ID N>
END
```

No username/password/ticket is included. Lua writes only complete, nonempty, bounded, unique positive string-ID records (2000 items / 65536 bytes). C# performs a bounded stream read and validates protocol, nonce, endpoint, count, IDs and END marker. Partial/wrong-nonce/wrong-endpoint/zero records do not replace prior data. Missing APIs/event or writer failures never prevent original connection. No-event does not mean vanilla or clear the previous record.

`ProfileSessionRecord.CaptureEndpoint` is optional for old-journal compatibility. The existing request nonce and journal nonce are now the same; previously they were generated separately. Recovery imports a valid associated capture before deleting the session journal. Capture read/write errors are diagnostic and do not block session recovery. The catalog is the last recorded/imported list, not a guarantee that today's server configuration matches. Current-game Required-event behavior is taken from the controller's already documented read-only protocol investigation; no real connection was run for this task.

## RED/GREEN evidence

Native command throughout:

```powershell
./Build-Tests.ps1 -Entry PZ3D.ServerWorkshopTests -Output artifacts/server-workshop-tests.exe
./artifacts/server-workshop-tests.exe
```

Observed RED failures, each exit 1, before its corresponding implementation/fix:

- `FAIL catalog available` (reflection test against missing implementation); GREEN 23 checks after implementation. Initial test serializer cast scaffolding was corrected from object[] to IList after the serializer returned ArrayList.
- `FAIL background coordinator available`; GREEN 40 checks after controlled download/progress/partial/cancel fixtures and coordinator.
- `FAIL active preparation blocks launcher restart`; GREEN 45 checks after restart gate.
- `FAIL job snapshots survive launcher restart`; GREEN 50 checks after durable interrupted-job support.
- `FAIL known local metadata shown without Steam query`; GREEN 51 checks after existing catalog metadata reuse.
- Windows CRLF list fixture failed in `ServerWorkshopCatalog.Parse` with `工坊 ID 格式无效。`; GREEN 54 after treating CRLF as one separator.
- Additional endpoint-isolation, simultaneous-journal, explicit-launch-drain, session recovery and I/O diagnostic regression assertions were added to the same fixture suite; final GREEN: **`PASS ServerWorkshopTests 60 checks`**, exit 0. These supplemental assertions are regression coverage, not separately claimed RED cycles.

Lua command:

```powershell
py -3.12 Orange3DTool/launch-bridge-lua-tests.py
```

RED: `AssertionError: complete Java string list captured with nonce and endpoint`, exit 1, against the original production asset. GREEN exit 0: **7 PASS groups**, including bounded Required observation, malformed/zero/oversized/no-event/writer-error cases and all prior connect, one-shot, expiry, single-player and Buddy-order fixtures. The intentionally incompatible Buddy fixture prints its expected fallback diagnostic.

Web command:

```powershell
node Orange3DTool/Web/server-workshop-ui-tests.cjs
```

RED: both initial groups timed out waiting for the missing `[data-server-workshop="demo"]` control (0/2). GREEN 2/2 after drawer implementation. Further RED cycles caught `retired event` replacing a cancelled task, and `old status` replacing a newly confirmed job; corresponding revision/job/status-ticket fixes brought GREEN to 3/3. The controller's requested count/local-presence copy produced a RED expected-count assertion and then GREEN **3/3 server workshop UI groups passed**, exit 0. A first wiring attempt registered a second fixture event listener; it was corrected to the existing bridge event dispatch. An initial test tried filling the deliberately collapsed import details; the fixture was corrected to open that actual control first.

## Aggregate verification and visual QA

- Full native build/run command:

  ```powershell
  ./Build-Tests.ps1 -Output artifacts/task6-regression.exe
  ./artifacts/task6-regression.exe --self-test *> artifacts/task6-self-test.log
  ```

  **Exit 0; 1739 lines beginning PASS.** Log `artifacts/task6-self-test.log`. Includes ServerLaunchPolicy, LaunchTransport, LaunchProfiles, Chinese/3D retention, Buddy ordering, existing Steam proof/service fixtures and all native self-tests. Final full run was before the isolated CRLF separator correction and final supplemental test assertions; focused final-source native suite passes 60. Controller final integration rerun is separate.
- `node Orange3DTool/Web/full-ui-tests.cjs *> artifacts/task6-web-tests.log`: **37/37 full UI test groups passed**, exit 0. Includes product refresh/contact/core cards and new server Workshop tests. After the final count-copy adjustment, focused UI 3/3 and capture suite were rerun; no unrelated behavior changed.
- `node --test tests/website.test.cjs`: **9 PASS, 0 fail, 1 live-header test skipped**, exit 0. Local headless website verification only.
- `git diff --check`: exit 0, no whitespace errors.
- `node Orange3DTool/Web/server-workshop-capture.cjs`: **PASS 12 server manifest/import/progress captures; dark/light at1440/768; 2 website images exported.** All twelve use actual application DOM plus sanitized native fixtures, no native app/game foreground. The source/time labels and count/local-present summary were recaptured after controller feedback. Capture-only Available version is now consistent at 1.2.14; ordinary fixture defaults remain unchanged.

Visual paths:

- `artifacts/server-workshop-ui/{dark,light}-manifest-{1440,768}.png`
- `artifacts/server-workshop-ui/{dark,light}-import-{1440,768}.png`
- `artifacts/server-workshop-ui/{dark,light}-progress-{1440,768}.png`
- `website/assets/launcher-servers-dark.png`, `website/assets/launcher-servers-light.png`

Inspected dark 1440 progress, light 768 import, light 1440 progress and dark 768 import directly. Controller also inspected dark progress1440/light import768/dark manifest768. Text remained readable, item list contained, and drawer nonmodal. Corrected textarea theme styling and narrow-window top offset during QA. Website copy is concise about recorded/imported preparation and conditional complete-list availability; existing 1.2.13 download metadata/URLs remain untouched.

## Self-review and limits

Self-review checked the catalog/parser, session nonce association, unchanged original Lua connection, bounded capture read, source/endpoint isolation, owned worker lifecycle, journal ownership, actual Steam proof reuse, UI late-event races, compact copy and preserved existing screens. Fixed restart exclusion, durable interruption, stale events/status, and CRLF parsing with focused failing fixtures. No known blocking issue remains from this review; controller independent review is pending.

Two implementation slices were briefly dispatched before the controller clarified the active no-parallel-implementation workflow. Both were immediately interrupted before producing changes; all shipped work above was implemented and verified sequentially by this task agent.

No real subscription/download, game/server/player configuration mutation, foreground browser/game test, Java API addition, real successful connection claim, publication or deployment occurred. Conditional Required observation and Steam success are covered at real-Lua/fake-service boundaries, not end-to-end against a live game/server. The existing 18-file release protocol is unchanged: C# builds automatically and Lua remains in the existing owned bridge resource.
