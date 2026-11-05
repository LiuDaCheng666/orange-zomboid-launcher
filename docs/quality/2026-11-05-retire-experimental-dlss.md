# 删除实验 DLSS 下载与启用：实现和验证记录

本文件记录删除阶段的验证；后续最终候选原生页面操作与 1.2.52 正式发布见 [发行记录](../releases/2026-11-05-1.2.52-security.md)。

日期：2026-11-05。基线：`44d1b0978c102e2d30e20b11b0f89d43939918bb`，工作目录 `C:/Users/MC233/.codex/worktrees/velopack-updates/OrangeLauncher`。

用户在供应链风险排查后明确要求“实验DLSS下载可以关闭了，这个删除把”。本次采用删除，不继续维护第三方下载认证；不发布、不替换现装 1.2.51，不操作用户实际游戏文件。

## 实现

- 删除 `ComponentCache.cs`、生产构建清单条目和历史实验模板，移除 HTTP 下载、ReShade 提取、ZIP 导入与 DLSS 安装方法。旧缓存不能重新安装。
- 删除现代页面和旧 WinForms 的下载、启用、本地组件选择按钮，删除诊断准备/缓存安装入口。生产仍拒绝这些旧 CLI 标志。
- 原有 `dlss` 原生动作只接受 `operation=disable, confirmed=true` 两个参数，WebView 与直接控制器调用均验证；禁止页面传路径。
- 原样保留旧部署记录路径与逐文件恢复逻辑。设置页只在当前所选游戏存在旧记录时显示“移除旧版 DLSS”，一次确认后恢复原文件；没有旧安装时不显示。
- 完成后使用后端返回状态清除旧按钮，保留未保存 Viewpoint 编辑；取消和失败后可继续操作。帮助与版权说明清除安装教程。
- 保留共享 Hash/SafePath 工具，未改动其他组件启停、存档、网页发布或系统设置。

## 已执行验证

1. 测试先于实现：原代码运行新增策略用例，实测失败 `FAIL retired DLSS operation rejected: enable`，随后实施删除。
2. `Build-Tests.ps1 -Entry PZ3D.DlssRetirementTests` 及原生测试 EXE 返回 0。覆盖直接控制器拒绝退休动作、旧版记录读取、中文隔离目录实际文件恢复、原备份字节还原、用户改动保存、无关文件/内存配置保留、文件占用时无部分卸载、释放后重试、重复恢复、状态更新、无下载缓存创建；关联 ModernHost 策略/队列及 WinForms 离线帮助构造验证通过。
3. 真实 Edge DOM `dlss-retirement-ui-tests.cjs`：6/6 组通过。覆盖无旧安装、一次确认与取消、成功状态和切页、失败重试、可见处理中状态、未保存设置保留、深浅主题与 1100/500 宽度。WebView 传输为合成数据，不能代替原生可见窗口全流程。
4. 原有 `experience-ui-tests.cjs` 的 help 组 1/1 通过；曾运行 settings 名称筛选得到 0/0，未计入验证。
5. 按真实 `OZL_PRODUCTION,OZL_VELOPACK` 源清单编译成功。Windows .NET Framework 只读元数据确认产物无 `PZ3D.ComponentCache`，无 `DlssInstaller.Enable`。产物 SHA256：`3A6F6E2E5524CAE5910BF72F70DA905F88730723405B139D044E6E15F410D594`。这只是编译检查，不是可发行的已签名安装包。
6. 新旧 CLI 边界及普通 UI 冻结的本次受影响用例通过；源码/JavaScript 语法与 diff whitespace 检查通过（沿用原 app.js 的 CRLF，检查时使用 `cr-at-eol`）。
7. 独立子 Agent 实际审查代码及截图，复核正常、确认、取消、处理中、完成、失败及宽窄/深浅主题。修正了旧帮助遗漏、恢复后按钮残留与可能清除草稿的问题，重拍了动画未落稳及失败按钮截断的截图；最终范围内无阻断问题。

## 发现且未扩大处理的旧测试失配

完整 `tests/production-boundary.test.cjs` 并非全绿。它仍按较早发布的固定源码成员与 117/239 等计数断言。以下 4 项在修改前 HEAD 源副本与当前源码均失败；对照结果没有新增失败名称，未删除或跳过这些断言：

- `actual production input excludes tests and is the pinned approved membership plus explicit library files`
- `resolver excludes unknown source and fails closed for missing listed source`
- `diagnostic membership adds pinned approved and library tests, fixture paths and diagnostic files exactly once`
- `existing CoreReleaseTool selector adds exactly one source only for that entry`

本次只更新两项受删除影响的契约：11 个旧诊断标志仍禁止用于生产，已删除的两个 DLSS 工具也不得留在诊断；普通界面冻结仅豁免明确删除的旧 DLSS 页定义行，其他内容仍冻结。本次没有把旧套件描述为通过。

## 调试过程中纠正的验证环境问题

- 老 `full-ui-tests` transport 没有当前启动配置协议，页面初始化超时；改用项目现行 `experience-ui-tests` helper。未修改生产代码规避初始化。
- 深工作树目录触发 .NET Framework 测试夹具路径过长，改在 `E:/Projects/OrangeLauncher/artifacts/security/2026-11-05-nikke` 下运行测试 EXE，所有数据仍为隔离夹具。
- 构造 WinForms 控件后在无消息循环的控制台等待异步队列，会捕获 UI synchronization context 导致测试超时；将队列用例放在构造控件前，组合重跑通过，未修改生产队列。
- PowerShell 7 不支持 ReflectionOnlyLoadFrom；元数据检查改用本机 Windows PowerShell/.NET Framework，未运行被检查的生产 EXE。

## 证据与边界

证据目录：`artifacts/dlss-retirement/`，包括 `native-tests.log`、`ui-tests.log`、`existing-help-ui.log`、`production-compilation.json`、`affected-production-boundary.log`、`boundary-baseline-comparison.json` 和两份完整旧边界日志。

截图：`screenshots/normal-settings.png`、`recovery-confirmation.png`、`recovery-cancelled.png`、`recovery-in-progress.png`、`recovery-completed.png`、`recovery-failed-retry.png`，以及 `dark/light-1100/500-recovery.png`。

未覆盖：真实可见启动器窗口与 WebView 原生传输串联点击、真实游戏渲染/进服、签名发行容器验收。未将 DOM 合成传输、隐藏 WinForms 控件构造、隔离文件操作或编译成功当成这些实机验收。用户当前安装和已上线更新均未替换；全部其他供应链风险也不因此归零。
