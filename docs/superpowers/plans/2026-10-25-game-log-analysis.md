# Game log analysis implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Execute the approved scope continuously, with tests and scoped review before publication.

**Goal:** Ship independent local/imported log analysis and Chinese issue explanations in both the analysis page and the full-screen startup view, validated against real logs and published without touching the website.

**Architecture:** A generic streaming C# analyzer consumes named, session-scoped readers and extracts exception blocks, mod frames, attempts and outcomes. A cancellable native service owns bounded filesystem/ZIP access; a separate WebView route renders reports and uses native pickers. Startup monitoring reuses the analyzer with a small rolling buffer.

**Tech Stack:** C# 5 / .NET Framework 4.8, WinForms, existing WebView2 HTML/CSS/JavaScript, System.IO.Compression, existing pinned Velopack and RSA release signatures.

## Global constraints

- Approved research: `E:/Projects/OrangeLauncher/docs/quality/2026-10-25-game-log-analysis-research.md`; latest user explicitly requests generic mod support, an independent navigation item, Chinese explanation plus original evidence, startup issues at bottom right, real-corpus testing and release.
- Worktree: `C:/Users/MC233/.codex/worktrees/velopack-updates/OrangeLauncher`, branch `codex/velopack-updates`. Preserve unrelated dirty website/deploy/HTTP protection changes.
- No mod-name allowlist, no per-example special cases. Use generic Lua/Java exception syntax and structured game state/network evidence. Unknown errors remain visible as unclassified observations.
- Imported players' logs never establish this machine's installed mods or authorize modifications. All analysis is local/read-only. Raw player logs never enter Git or publication.
- Source limits: 64 MiB/file, 256 MiB/batch, 128 files, directory depth 4, ZIP 512 entries / 256 MiB advertised total / 200x large-entry ratio; no extraction, executable content, reparse traversal, arbitrary UI-supplied paths or external network.
- Empty bridge payloads; native picker supplies paths internally. Background analysis supports status/progress/cancel and cannot block launch or mutate game state.
- Existing `GameDiagnostics` behavior/tests remain compatible; help/maintenance link to the new route.
- Release new unused version 1.2.17 (verify availability before activation); do not overwrite 1.2.16. Primary own server first, GitHub identical-byte fallback second. Use `deploy/ssh_connection.py` pinned-key connection. Do not invoke `deploy_site.py`, change website pages/assets/current/Caddy/Nginx/SSH or bypass HTTP guard.

## Shared interfaces

`GameLogModels.cs` defines serializable fields. `GameLogAnalyzer.Analyze(IEnumerable<GameLogInput>, CancellationToken, Action<int,string>)` returns `GameLogReport`; `AnalyzeText(string,string)` is the small-buffer convenience entry. `GameLogInput.Open` supplies a fresh read-only `TextReader`. Inputs carry `Id`, `Name`, `Kind`, `SessionKey`, `Length`, `ModifiedUtc`, `Encoding`, `Truncated`, `Warning`; no raw filesystem path is returned.

Report fields: `checkedUtc,origin,summary,state,stage,reportText,incomplete,fileCount,errorCount,warningCount,bytesRead,issues,attempts,sources,warnings`. Issue fields: `id,type,severity,impact,title,meaning,advice,phase,attemptId,count,mods,frames,evidence`. Frames: `mod,file,function,line`; evidence: `source,line,text`; attempts: `id,sessionKey,state,stage,summary,firstLine,lastLine`; source: `id,name,kind,encoding,status,modifiedUtc,bytesRead,truncated`. State strings: `entered-with-issues`, `entered`, `failed`, `disconnected`, `unknown`.

Native UI actions `logAnalysisStatus`, `logAnalysisLocal`, `logAnalysisImport`, `logAnalysisFolder`, `logAnalysisCancel`, `logAnalysisExport` use empty payloads. Status fields: `jobId,state,message,progress,report`, with job state `idle/running/completed/cancelled/failed`. Import/folder/export are handled by native dialogs; local/status/cancel use controller methods. UI polls status while running and escapes every log-derived string.

## Task 1: Generic parser and explanations

Files: create `Orange3DTool/GameLogModels.cs`, `GameLogAnalyzer.cs`, `GameLogAnalyzerTests.cs`.

- [ ] Add failing behavioral tests, including arbitrary fictional MOD names and both `Lua((MOD:...)).func(file.lua:line)` / old `function: ... -- file: ... line #...` syntax.
- [ ] Red test: `AnalyzeText("...MOD:Unseen Workshop Name...attempted index: health of non-table: null...IngameState.update...", "imported")` must identify the mod, explain the null field in Chinese, preserve evidence, and mark runtime error rather than join failure.
- [ ] Parse whole exception blocks with limits; group by type/message/frames, count occurrences once, bound evidence/output, keep unknown ERROR observations and Chinese fallback meaning.
- [ ] Track attempts per SessionKey and strong state evidence; retry success supersedes earlier failure, while a later world-load failure supersedes earlier gameplay. Transport accepted/loading took/mod timeout are not world-ready proof. Detect explicit JVM/resource/dependency/checksum/workshop errors using generic syntax.
- [ ] Green test suite, sanitizer/privacy/long-line limits, cancellation, no hidden mod allowlist. Record scoped review.

## Task 2: Native collection/import and controller integration

Files: create `GameLogAnalysisService.cs`, `GameLogAnalysisController.cs`, `GameLogAnalysisHost.cs`, `GameLogAnalysisServiceTests.cs`; modify minimal hooks in `ModernController.cs`, `ModernHost.cs`, `ModernHostPolicy.cs`, production source allowlist.

- [ ] Red tests create owned logs and ZIP, invoke actual service, verify report, duplicate avoidance, rejected traversal/reparse/unsupported/oversized/encrypted data and byte-exact preservation.
- [ ] Read latest local console/DebugLog/connections and recent session pair using bounded allowlisted discovery. Imported file/folder/ZIP supports PZ logs in full and fragments, strict UTF-8 then GB18030, proper source/session provenance and explicit gaps.
- [ ] Implement background status and cancellation with immutable snapshots, no game guard/mutation lane, bounded reads, disposal cancellation; payload tests reject `path`, `text`, upload and arbitrary options.
- [ ] Hook native dialogs on UI thread and registration; UI export writes only through native SaveFileDialog to user-selected destination. Keep old diagnostics action compatible.
- [ ] Run service/controller and old diagnostics regressions; record scoped review.

## Task 3: Independent analysis interface

Files: `Orange3DTool/Web/app.js`, `index.html`, `app.css` (or current stylesheet), new `log-analysis-ui-tests.cjs`; route preview allowlist hook coordinated by root.

- [ ] Write failing browser tests for independent sidebar entry, local/import/folder/cancel actions, busy/progress/error states, escaped hostile log strings, Chinese meaning + evidence, attempt/source disclosure and copy/export.
- [ ] Create the `diagnostics` route with a clear intake section, outcome summary, grouped issue cards and expandable raw evidence/source/attempt detail. Keep empty/unknown outcomes honest. Link old diagnostic cards to route.
- [ ] Use existing light/dark design tokens and responsive widths; preserve navigation, drafts and unrelated pages. Preview data is clearly synthetic and never replaces native production analysis.
- [ ] Run browser tests and inspect actual dark/light/minimum-size screenshots, fix overflow and affordance issues.

## Task 4: Startup problem area

Files: `GameStartupModels.cs`, `GameStartupMonitor.cs`, `GameStartupWindow.cs`, corresponding startup tests.

- [ ] Red tests feed arbitrary-MOD Lua errors, unknown ERROR, fatal resource failure and normal warnings; ensure no false success/failure or launch interruption.
- [ ] Reuse `GameLogAnalyzer.AnalyzeText` on a bounded raw rolling buffer to populate `GameStartupSnapshot.Issues`; sanitize output, preserve before terminal state and bound CPU work.
- [ ] Bottom-right issue panel displays Chinese title/meaning and expandable/clickable raw evidence within available area. Preserve top progress/memory, central animation, bottom-left logs, reduced motion, no game process ownership or focus stealing.
- [ ] Run native layout/monitor/presenter regressions and screenshots for 1280x720, 1920x1080, light/dark and scaled fonts. Record scoped review.

## Task 5: Real corpus, release and verification

Files: new `GameLogCorpusTests.cs`, `tests/GameLogAnalysis.test.ps1`, sanitized fixtures, `docs/quality/2026-10-25-game-log-analysis-verification.md`, `docs/releases/1.2.17-release-notes.md`, `VERSION`, `CHANGELOG.md`; release-only publication tool if no existing safe entry exists.

- [ ] Replay the authorized 612 local texts and 93 unique downloaded PZ inputs locally with timing and boundedness evidence; no corpus content in source. Compare manually reviewed cases from research, renamed-mod/unknown-mod variants and negative/retry/truncated inputs; do not claim universal accuracy.
- [ ] Execute native parser/service/host/old diagnostics/startup and browser tests. Build signed actual release and exercise its real analyzer through reflection/controlled native integration on real logs, not just fixtures.
- [ ] Review whole feature, fix defects, verify no special-case mod names and no website diff owned by this task. Capture final production UI and archive/RSA signatures.
- [ ] Build frozen 1.2.17 installer/portable/full package. Before publish inspect remote latest/tag and hash website/assets/config/current snapshot; publish only immutable ten artifacts, signed current feed metadata and approved update notes via pinned SSH.
- [ ] Download public manifest/signature/package and installer/ZIP, compare hashes, prove older production client discovers/downloads Ready in isolated locator, verify website/config/current unchanged and SSH read-only check succeeds. Then GitHub upload identical frozen files and verify digests.
- [ ] Commit only task-owned files, tag source, save evidence and report verified public release/download status and remaining limits.
