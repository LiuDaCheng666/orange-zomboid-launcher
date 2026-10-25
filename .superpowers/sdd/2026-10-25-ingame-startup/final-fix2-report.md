# Final native retention fix F2 — 2026-10-25

Base: `6b7cb5ad04f72ebcdd325a3bf1e08d5ef2296b30`. Scope: startup reducer/component, the analyzer's distinct startup entry, focused component tests, this report. All verification was silent/background. No real game, Steam, UI automation, Java source, packaging/version/release, website, or deployment changes.

## RED evidence

Added `SaturatedEligibility` before changing production code. Initial fixture fills native history with 24 distinct heap errors at t0, appends a lower-priority exception at t+31, and checks both retention and the actual eight-line issue exchange through the observer.

```powershell
./Build-Tests.ps1 -Entry PZ3D.GameStartupComponentTests -Output artifacts/startup-component-saturation-red.exe
./artifacts/startup-component-saturation-red.exe
```

Build passed; test exited 1 with `FAIL: expired saturated heap history cannot evict a fresh core exception`. Logs: `artifacts/startup-native-verification/saturation-red-build.log`, `saturation-red.log`.

After the 24-entry fix passed, added the exact 80-stale-group raw-window fixture requested by root. No extra lines were fed after the new exception, so chunk eviction cannot rescue the candidate.

```powershell
./Build-Tests.ps1 -Entry PZ3D.GameStartupComponentTests -Output artifacts/startup-component-saturation80-red.exe
./artifacts/startup-component-saturation80-red.exe
```

Build passed; 24-entry cases passed; test exited 1 at the same retention assertion for mode 3 (80 expired heap groups). Logs: `saturation80-red-build.log`, `saturation80-red.log`.

## Fix and scope

The native 24-entry retention budget now ranks by the exact same actionable/time/menu eligibility predicate used by IPC selection, then existing priority, then actual occurrence order. It therefore removes stale or pre-menu history before a fresh actionable candidate. Active high-priority findings retain their existing precedence.

A separate internal `AnalyzeStartupText` entry supplies original observation eligibility to the analyzer's existing 80-group retention budget. This preserves the complete parser input and block/frame/continuation context. Startup retention prefers eligible observations and uses actual block position to resolve equal-priority capacity ties. `AnalyzeText` and imported-file `Analyze` supply no startup policy: their priorities, 80-group cap, equal-priority behavior, classification, export ordering, and JSON remain unchanged. Native history stays capped at 24; raw input remains bounded at 160 lines/64 KiB with the existing overlap. Original observation timestamps and ordinals are never refreshed by retention or parsing.

Seven deterministic scenarios now cover expired, verified-menu-ineligible, and active saturation at both 24 and 80 groups, plus a genuinely newer equal-priority observation at the 80-group cap. Tests verify the retained snapshot and real IPC payload. Prior rolling-window, expiry, recurrence, ownership/recovery, and background-observer tests remain in the affected suite.

## GREEN verification

```powershell
./Build-Tests.ps1 -Entry PZ3D.GameStartupComponentTests -Output artifacts/startup-component-saturation-green.exe
./artifacts/startup-component-saturation-green.exe
./Build-Tests.ps1 -Entry PZ3D.GameStartupSuiteTests -Output artifacts/startup-native-verification/startup-saturation-suite.exe
# Copy EXE plus the two WebView2 managed DLLs to a fresh short temp directory.
<short-temp>/startup-tests.exe --headless
./Build-Tests.ps1 -Entry PZ3D.GameLogAnalyzerTests -Output artifacts/startup-native-verification/saturation-analyzer.exe
./artifacts/startup-native-verification/saturation-analyzer.exe
git diff --check -- Orange3DTool/GameLogAnalyzer.cs Orange3DTool/GameStartupMonitor.cs Orange3DTool/GameStartupComponent.cs Orange3DTool/GameStartupComponentTests.cs
```

- Focused component suite: exit 0, **218 PASS assertions**, `ALL OWNED STARTUP COMPONENT TESTS PASSED`. Logs `saturation-green-build.log`, `saturation-green.log`.
- Analyzer suite: exit 0, **186 assertions, 0 failed groups**. Logs `saturation-analyzer-build.log`, `saturation-analyzer-green.log`. This checks unchanged general analyzer classification/grouping/priority/export/bounds behavior after adding the startup-specific retention policy.
- Headless integrated suite: exit 0, **747 PASS assertions**, `ALL GAME STARTUP AND RELATED REGRESSIONS PASSED`. Logs `saturation-suite-build.log`, `saturation-suite-green.log`. Run justified because reducer chunk-boundary collection and observation retention changed; includes existing bounded-input/overlap, lifecycle, bridge, preflight, profiles, transport, memory, and controller regressions while skipping visible presenter/window tests.
- Scoped diff whitespace check: exit 0.

## Self-review and handoff

The startup-specific policy is absent from general analyzer calls, and all public report/export priority ordering remains the original priority ordering. Eligibility is evaluated before each retention cap can discard a live candidate, using actual original timestamps and absolute block positions bound by the reducer. Complete parser context stays present, avoiding artificial new occurrences caused by slicing an old exception's continuation away from its start. Capacity tie recency is enabled only for startup. Both existing caps and the raw overlap are unchanged. The saturated active cases verify that a lower-priority exception still cannot displace 24/80 current heap failures. Existing original-ordinal deduplication and recurrence tests pass in the focused and integrated runs.

Only the four explicit C# paths plus this report are committed. Unrelated dirty files remain untouched. Root must rebuild candidate 3 from the fix commit and complete the scoped review; this report makes no foreground acceptance or release claim.
