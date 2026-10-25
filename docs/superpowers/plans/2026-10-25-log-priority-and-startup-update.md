# Log priority and startup update correction

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement task-by-task.

**Goal:** Correct the approved log analyzer so players see meaningful faults from console.txt first, and fix startup update discovery cancelled by passive page reads.

**Architecture:** Preserve original evidence, general MOD/frame parsing and signed updater. Add `GameLogIssue.attention` (`primary`, `review`, `background`) to distinguish useful fault evidence from technical noise. UI consumes attention with a conservative fallback for older reports. Own server remains primary, GitHub fallback. Corrections implement direct user feedback; no additional design approval or foreground operation is required.

**Constraints:** Work in current managed checkout. Preserve all unrelated website/deploy changes. No Computer Use or foreground control; only background launch if useful. No raw player logs uploaded/committed. No mod-name allowlists or blanket suppression of ERROR. Public versions immutable; next release 1.2.19 only after tests and review. Fixed taskbar AUMID remains `OrangeZomboidLauncher.Launcher`.

**User copy correction:** Remove generic disclaimers and operational-principle prose from product screens and exported analysis. Keep concise results, related MOD/script locations, translated messages, targeted advice and actual dates/read-limit facts. Unknown remains an outcome label; no extra health disclaimer paragraph.

### Task 1: Startup discovery and understandable connection label (root)

Files: Web/app.js, new Web/startup-update-ui-tests.cjs.

- Reproduce initial #help or opening help before initial status: passive coreUpdateStatus increments manual generation and causes checkStartupUpdate to skip.
- Make status reads passive: never change manual generation/download consent; startup checks once when real status ready. Manual check/download/cancel retain ownership and stale-response safety. Preserve preview offline behavior.
- Tests: normal home, initial help, help before status reply, only one startup check through refreshes, manual actions win races, failures never block launch or auto-download, preview no network.
- Explain header connection as `本机服务已连接`, never a web/game server/update status.

### Task 2: Evidence-based attention, grouping and console-first local intake

Owned files: GameLogModels.cs, GameLogAnalyzer.cs, GameLogAnalyzerTests.cs, GameLogAnalysisService.cs, GameLogAnalysisServiceTests.cs. Tests may add files but no other production files without coordinator agreement.

- Add public string `attention` to GameLogIssue, exactly `primary`, `review`, `background`. Keep original severity/evidence/count; not all ERROR implies functional failure. Unknown with useful MOD/script/exception context can be primary; unknown without useful cause is review, with a concrete evidence-based title rather than repeated `未分类的日志提示`.
- Explicit world/JVM/native/required resource blocking, connection/checksum/dependency failures and actionable Lua/Java errors remain primary. Nonfatal ImportedSkeleton.collectBoneFrames / missing bone index technical asset diagnostics get concrete Chinese meaning and background priority, not an unsupported harmlessness guarantee. Similar resource diagnostics group by family/resource/mod context, without grouping unrelated core faults or hiding critical escalation. No mod-name allowlist.
- Budget priority keeps primary faults when noisy resources fill the current 80 groups. Summary/state must not claim gameplay failure only from background/review records. Retain all accepted evidence in exported grouped report; explicitly explain caps.
- Local default reads console.txt first; supplement only matching same-run DebugLog/connections. Missing console may fall back to newest DebugLog; do not mix stale console and unrelated newer DebugLog into one ambiguous report. Preserve read-only/privacy/limits/cancellation/deduplication behavior.
- RED/GREEN tests include screenshot bone record, hundreds of variants then real Lua MOD/line and late fatal, arbitrary renamed MOD, nonfatal vs fatal asset distinction, unknown review, useful unknown frame, console-first pairing. Run current parser/service/old diagnostics tests. Write report at artifacts/log-priority/task-2-report.md, include real local console before/after counts and reviewed examples; no raw logs committed. Do not edit Web/app.js, deploy, VERSION or commit broad changes.

### Task 3: Core-first, compact report UI

Owned files: Web/app.js log functions only, Web/app.css log rules only, Web/log-analysis-ui-tests.cjs (coordinate root startup edits before beginning).

- Consume issue.attention; older reports conservatively derive attention from type/severity/impact/frames. Default expanded content shows primary faults ranked blocking then actionable runtime, grouped with MOD and script/line visible early. No repeated full-screen generic cards.
- Review/background records stay accessible in compact collapsed sections with counts, original evidence and export; do not label them all as core errors. Missing primary evidence uses a concise no-definite-fault result. Technical attempt/source IDs remain in details; friendly ordinal labels in main content.
- Summary focuses primary fault categories and affected MODs, states if entered world or failure supported, and distinguishes raw ERROR counts from actionable issues. Intake explicitly says console.txt primary for local mode. Do not expose meaningless `未分类` template wall.
- Preserve hostile text escaping, native file picker/cancel/export, source provenance, incomplete warnings, arbitrary MOD support, small viewport/themes. Tests: 500 background/review + genuine core fault defaults to compact useful core view; expansion/raw/export still available, all-background input no red alarm wall, unknowns remain review, 820x600 no overflow.
- Write report and actual screenshots under artifacts/log-priority, no website changes.

### Task 4: Review, real corpus and release (root)

- Review changed output on local console and selected real player logs, then 705-input corpus as needed for classification/state/cap changes; old known critical scenarios remain detected and noise no longer crowds core faults. Capture before/after counts with caveats.
- Independent code review and meaningful UI/native regressions; keep baseline unrelated failures separate.
- Build new 1.2.19 with unchanged branding AUMID, installer path selection, pinned update identity; verify production analyzer and startup discovery integration, signed immutable artifacts. Publish only software through pinned-key helper, own server first then GitHub; verify public downloads, old production client Ready and protected website equality. Report tested result with limitations.
