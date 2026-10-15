# Xlog server recognition correction implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct the reproduced standard JVM logging-option omission and publish OZL1.2.6 through the already approved update channel.

**Architecture:** Keep the approved1.2.5 roles, observation boundary, PID/image/time checks and all broad guards. Add only known single-token JVM logging options before actual main recognition. No UI redesign or new exception policy.

**Tech Stack:** Existing C#5/.NET Framework, real WMI process fixtures, trusted signed core-release workflow.

## Global Constraints

- 保留1.2.5的实际主类／PID／启动时间／镜像身份验证、默认系统枚举、未知用途拦截及所有文件占用保护；不使用字符串包含主类名来排除服务端。
- 不停止、重启或修改真实游戏、服务器、Steam、正在运行的安装、真实存档、凭据、桌面、注册表、分页或 Steam 服务。原始命令行不能输出、落盘、上传。
- 版本1.2.6，OZL / Orange zombiod launcher，18条路径、原始封面、正式公钥、旧内部EXE名不变；历史正式资产和标签不得覆盖。
- 复用当前目录分支fix/xlog-server-126；原生编译串行，副作用只在自有隔离TestFixtures。私钥仅向构建工具传已有路径，禁止读取、打印、复制或生成。

## Task 1: Handle standard logging tokens and prepare signed1.2.6

**Files:** `Orange3DTool/GameProcessGuard.cs`, `GameProcessGuardTests.cs`, `VERSION`, `CHANGELOG.md`, `docs/releases/1.2.6-release-notes.md`. Fresh ignored evidence `artifacts/xlog-server-126/`, output `dist/1.2.6-github-release/`.

**Interfaces:** Existing ClassifyJavaCommandLine/ReadJavaRole/IsClientRunning/IsRunning/Engine.GuardLaunch contracts and observation seam unchanged. Coordinator owns real-process read-only probe and publication.

- [ ] **Step1 RED:** Add literal cases to Roles before production: `java.exe -Xlog:gc*=debug -Xlog:safepoint=off -cp pz.jar zombie.network.GameServer` => Server; bare `-Xlog` Server; quoted log filename containing zombie.network.GameServer plus actual MainScreenState => Client; logging plus OtherMain => Unknown; `-Xlogger` and an unrelated unknown option followed by Server => Unknown. Change the owned ProcessEvidence start command to include two-Xlog arguments for all Server/Client/Unknown roles, retaining unconditional normal-preflight/controller positive and all broad/native protections. Run `Build-Tests.ps1 -Entry PZ3D.GameProcessGuardTests -Output artifacts/xlog-server-126/process-red.exe`, then exe; record expected Unknown-instead-of-Server failure (not compile error).
- [ ] **Step2 minimal GREEN:** Extend the existing known-single-token branch only, e.g. `a=="-Xlog"||a.StartsWith("-Xlog:",StringComparison.Ordinal)`. Do not accept arbitrary-X flags or search for class substring. Run process suite and existing LaunchPreflightTests, ModernControllerTests, PlayProfilesTests from a short owned artifacts/<short> output root; run Program --self-test aggregate once. New real fixture path must prove Server allows normal launch despite two logging tokens; Client/Unknown blocked. No ignored/skipped central assertion.
- [ ] **Step3 version/package:** Set1.2.6 and concise notes explaining this omission honestly. Run existing trusted `Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.2.6-github-release`. Fresh output only. Adapt previous reviewed `artifacts/client-server-125/verify-package.ps1` plus framework/native probe into this task evidence; validate RSA, actual old compiled parser, all18 paths/hashes/lengths, original cover/source equality, identical setup ZIP/portable payload, isolated real--verify-setup and fixture native preview. Existing published1.2.5 manifest7150B74DB03BD838EE2B6C3DC52ACC7947E8C74764958CDB7E5E0FA6074E9552 and sig17B4E40E5A905A27B39ABBDC15E9D14002ED630C1D1B3F8F38580FB281830D22 unchanged. No extra UI changes or repeated browser gate necessary for unchanged Web sources.
- [ ] **Step4 report/commit:** Explicit-path commit, no raw logs/key or ignored artifact force staging. Write own workspace task-1-report.md with RED/GREEN commands/outputs/counts, source commit, exact final hashes and limitations. Release compiler ownership. Root will perform actual current-process candidate comparison; do not retrieve raw current commands yourself.

## Coordinator gates and publication

Task scoped then final whole-branch review. Fresh final process/aggregate/package checks on merged source and current real-process sanitized read. Normal main merge/source+annotatedv1.2.6 push,22assets draft validation, formal stablelatest, anonymous four public downloads and actual original1.2.5 default CoreUpdateService check/download-toReady in isolated fixture; new1.2.6 Latest. Document timestamp/hashes and default channel proof, keep all old packages/tags. Existing user approval for ongoing bugfix and GitHub updates applies; no separate live installation apply or process termination.
