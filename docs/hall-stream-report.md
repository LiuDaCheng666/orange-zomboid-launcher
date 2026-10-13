# Hall stream evidence — 2026-10-13

Scope: Task 1 only, on `fix/compact-responsive-ui`. Changed `SteamLauncherServices.cs`, `SteamLauncherNative.cs`, `SteamLauncherServicesTests.cs`; added `SteamLauncherNativeHallTests.cs`. Controller, host and Web integration are owned separately.

## Cause and behavior

The original native Hall loop pumped for completion or its 25-second deadline before reading any server details. `ServerSink.responded` discarded its indices, and parent stdout accepted only progress events. Consequently, valid rows already available during the query could not reach the interface.

The worker now reads responded indices each pump, with a 250 ms fallback sweep. It sends the first valid matching row immediately, then batches of at most 16 rows at 250 ms or the batch-size threshold. IDs are deduplicated, streaming is capped at 1,000 IDs, and the final partial batch still flushes after the cap. The final scan retains the original index order, 1,000-row limit, DTO fields and warning messages.

`Execute(engine, action, payload, progress, cancel, Action<object> hallBatch = null)` and `Transport(..., cancel, Action<object> hallBatch = null)` preserve existing callers. The callback receives `{servers:[...]}`. Worker stdout uses `{kind:"hallBatch",nonce,servers:[...]}`; the parent accepts only Hall events with the exact string nonce, validates all rows before delivery, and drops extra row fields including `requestId`. Client correlation remains the controller's responsibility.

Each batch is limited to 16 rows, 32 KiB of serialized characters and 1,000 total streamed rows. The 32 KiB line limit accommodates six-character JSON escaping of the native 64-byte name and description buffers. Row validation checks canonical IPv4, matching endpoint ID, integer bounds, text bounds, and boolean flags.

Original native cancel/release `finally`, worker disposal/shutdown, response nonce validation, owned request cleanup and subprocess timeout/cancellation remain. Cancellation is also checked immediately after a batch callback. An eventual failure still reaches the caller after any valid partial batches.

## RED / GREEN

Run from `E:/Projects/OrangeLauncher`:

```powershell
./Build-Tests.ps1 -Entry PZ3D.SteamLauncherServicesTests -Output E:/Projects/OrangeLauncher/artifacts/hall-stream.exe
& E:/Projects/OrangeLauncher/artifacts/hall-stream.exe --self-test
```

Observed RED failures before implementing each boundary: optional callback absent; actual worker transport had no first event before final response; native polling helper absent; the last partial batch waited for completion after reaching the cap; maximum escaped native text exceeded the old 8 KiB line bound; callback cancellation could be missed when the final snapshot became empty.

Final GREEN: compile exit 0, test exit 0, **57 PASS checks**. The actual subprocess fixture emits a server, then waits for acknowledgement; its callback confirms no final `response.json` exists before acknowledging. Additional tests reject a wrong nonce, missing port, inconsistent ID, fractional players, oversized batch, and a Hall event in a search request. Native synthetic tests cover early rows, responded retry, bounded batches, deduplication, query filtering, final ordering, cancellation and partial/empty timeout behavior. Existing Steam parsing, confirmation and transport regressions also pass.

`git diff --check -- Orange3DTool/SteamLauncherServices.cs Orange3DTool/SteamLauncherNative.cs Orange3DTool/SteamLauncherServicesTests.cs` exited 0.

## Limits

Tests use synthetic rows and isolated fixture directories; they make no Steam connection, account mutation, subscription, download or game launch. They prove the production polling logic and real parent/child transport ordering, but not current Steam network latency or native ABI callback timing. A real read-only timed Hall probe was not run in this task. UI generation handling and network-lane integration require the separately owned controller/Web verification.
