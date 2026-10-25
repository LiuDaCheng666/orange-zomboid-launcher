# Final native observation selection fix — 2026-10-25

Base: `02a96e01443e308015ba16522ba6a4f8f40a211e`. Scope is native/model code and focused tests only. No Java, build script, website, deployment, release, or version changes. Testing stayed silent/background; no UI automation, real game, Steam launch, or release acceptance claim.

## Review reproduction and RED

Added behavioral fixtures before implementation in `GameStartupComponentTests.IssueSelection`.

Commands:

```powershell
./Build-Tests.ps1 -Entry PZ3D.GameStartupComponentTests -Output artifacts/startup-observation-red.exe
./artifacts/startup-observation-red.exe
```

Build succeeded; test exited 1 at `FAIL: new core heap failure outranks an older lower-priority exception`. Logs: `artifacts/startup-native-verification/observation-red-build.log`, `observation-red.log`.

After the root requested the old-high-priority/later-operation guard, added `IssueEligibility` before implementing eligibility. Commands:

```powershell
./Build-Tests.ps1 -Entry PZ3D.GameStartupComponentTests -Output artifacts/startup-component-eligibility-red.exe
./artifacts/startup-component-eligibility-red.exe
```

Build succeeded; test exited 1 at `FAIL: expired observations clear IPC while remaining in diagnostic history`. Logs: `eligibility-red-build.log`, `eligibility-red.log` in the same verification directory.

## Implementation

- Kept existing analyzer impact/type priority values and reused them in the reducer and IPC selector. Priority wins; true occurrence position breaks ties.
- Added internal, nonserialized observation metadata. The analyzer records the actual block start, updates that position when identical blocks merge, and provides its stable grouping key without relative attempt IDs. The reducer translates relative lines into a monotonically increasing absolute input-line ordinal.
- Retained original occurrence timestamps in a bounded queue alongside the raw log window. Parsing, shifting the window, and polling do not refresh those timestamps. Richer attribution may update an existing occurrence without manufacturing a newer occurrence position.
- IPC identity uses selected payload plus actual occurrence ordinal, allowing an identical error to be exported again after genuine recurrence even after its old raw block has been evicted. Retained history alone cannot cause periodic exports.
- Kept diagnostic history while restricting IPC selection to observations within 30 seconds of their original observation, and observations after the first verified nonce-bound menu handoff. Expiry emits one clear packet; an expired heap issue cannot monopolize selection over fresh lower-priority core failures. The menu floor prevents boot history from blocking later-world findings. No unverified localized log phrase invents an operation boundary.

## GREEN verification

```powershell
./Build-Tests.ps1 -Entry PZ3D.GameStartupSuiteTests -Output artifacts/startup-native-verification/startup-observation-suite.exe
# Copy EXE and its two WebView2 managed DLLs to a fresh short temp directory.
<short-temp>/startup-tests.exe --headless
./Build-Tests.ps1 -Entry PZ3D.GameLogAnalyzerTests -Output artifacts/startup-native-verification/observation-analyzer.exe
./artifacts/startup-native-verification/observation-analyzer.exe
git diff --check -- Orange3DTool/GameLogAnalyzer.cs Orange3DTool/GameLogModels.cs Orange3DTool/GameStartupModels.cs Orange3DTool/GameStartupMonitor.cs Orange3DTool/GameStartupComponent.cs Orange3DTool/GameStartupComponentTests.cs
```

- Final headless integrated run: exit 0, **729 PASS assertions**, `ALL GAME STARTUP AND RELATED REGRESSIONS PASSED`. Includes component, presentation/lifecycle, monitor, bridge, profiles, transport, preflight, memory policy, and controller regressions; skips visible presenter/window tests. `observation-suite-build.log` and `observation-suite-green.log`.
- Final analyzer suite: exit 0, **186 assertions, 0 failed groups**. `observation-analyzer-build.log` and `observation-analyzer-green.log`.
- Focused component test previously passed after the initial selection fix and again after eligibility implementation. The final integrated suite includes the final focused tests plus all existing component tests.
- Scoped diff whitespace check: exit 0.
- During integrated verification, the timestamped `Line` overload caused the legacy reflection harness to report `AmbiguousMatchException`; renamed the timestamped entry to `ObservedLine`, preserving the single original `Line` method. Final integrated run above is after that correction.

Focused assertions verify: old low-priority exception -> new heap; subsequent lower-priority noise cannot replace/refresh heap; equal-priority new occurrence wins; merged repeated blocks carry true newest occurrence; internal fields absent from JSON; shifted relative-window lines do not refresh the packet; old evicted history does not acquire freshness; identical new recurrence receives fresh IPC; expiry clears once; fresh lower-priority later-operation errors beat expired heap history; verified menu handoff excludes boot history while retaining diagnostics. IPC checks use actual file payload and modification times.

## Self-review and remaining acceptance

All observation timestamps originate from line arrival, all ordering ordinals from actual block starts, and the queues/history retain existing bounds. Existing analyzer report priority values, exported JSON schema, issue IPC format, agent authentication, ownership/recovery, and Buddy policy remain unchanged. The native observer has no trusted direct signal for every Java instrumentation world transition; it therefore uses bounded recent observations and the verified menu floor, while Java continues to enforce its per-world packet floor. No fake periodic refresh is introduced. Root still needs to rebuild the candidate with this commit and run the final scoped review; foreground acceptance and release are outside this fixwave.
