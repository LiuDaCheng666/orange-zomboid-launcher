# 独立版本弹窗进度与取消验收

日期：2026-10-29。范围仅为保存独立版本弹窗、VersionLibrary 文件复制阶段与已有原生任务进度接线。发布包及 Windows 系统目录窗口由主任务另行验收，本记录不代替该步骤。

## 复现与原因

- 修复前执行 `node tests/version-preserve-dialog-ui-tests.cjs`，在真实生产 HTML/CSS/JS 中点击保存，保留原生请求未回复。测试按预期失败：`pending preserve shows its state inside the owning modal`。日志：`artifacts/version-preserve-20261029/red-ui.log`。
- 修复前执行 `./tests/VersionPreserveProgress.test.ps1`，实际复制隔离游戏文件。测试按预期失败：`native scan emits progress before transfer begins`。日志：同目录 `red-native.log`。
- 路径：原生 `LibraryFolderSelection` 创建目录选择请求 → `FolderBrowserDialog.ShowDialog(this)` → 原生修改队列 → `VersionLibrary.PreserveChecked`。原实现先遍历并 SHA-256 读取全部来源文件，才首次报告复制进度；逐文件校验副本与最终再次哈希来源也没有进度。浏览器在等待整个请求时禁用两个按钮，任务状态只显示在弹窗背后的页面。
- 用户截图能确认弹窗没有状态且两个按钮不可用，不能单独证明截图拍摄时正在系统选目录、排队、扫描还是复制。上述可重复测试及源码调用链证明这些等待路径缺少弹窗反馈。

## 修复行为

- 简短说明复制当前游戏文件、来源版本，以及存档和模组快照所在入口。只允许现有系统选择器提供目录，不加入新默认路径策略。
- 打开目录窗口后立即说明下一步。按钮随阶段显示“正在选择位置…”、“正在保存…”、“正在取消…”。
- 原生报告核对原游戏、复制、校验副本、复核原游戏、登记和清理阶段。显示实际完成文件数及字节；只在已知复制总量时给进度条赋值，其他阶段保持活动指示。
- 复制和副本校验按两个连续阶段处理，保留完整副本 SHA-256 与复制前后来源清单/哈希比对、独立注册及原有回滚语义。原生对高频字节事件节流，阶段切换立即显示。
- 取消及 Escape 保持弹窗和清理状态，等待原生结束后恢复按钮；原生失败、目录选择取消、成功均释放占用。初始返回、取消后返回及重新打开均不留隐藏弹窗。重复提交被本弹窗阻止。
- 弹窗只接收现有“正在保存版本”任务文本，页面刷新返回的空闲状态不会清空其进度。没有修改配置归属、快照归属、资源锁和原生目录选择策略。

## 已执行结果

| 命令 | 实际范围与结果 |
| --- | --- |
| `node tests/version-preserve-dialog-ui-tests.cjs` | PASS。真实生产 DOM 的初始返回、目录选择等待、阶段/字节反馈、重复提交、无关空闲事件、Escape 取消、程序关闭后重新显示取消/清理状态、原生回复前保留状态、选择器取消、失败/重试/成功、取消后返回和重开。此用例的原生 I/O 是合成回复。 |
| `./tests/VersionPreserveProgress.test.ps1` | PASS，新增进度测试 19 项断言及既有 SteamVersionWorkflow 18 项断言。实际隔离文件复制，3 MiB 中文文件字节一致；修改来源后副本不变；扫描、复制、校验副本、最终来源复核各自处理真实字节后取消，目标/partial 清理且不注册；来源变化失败回滚。Steam 测试使用隔离合成 manifest/JAR，不连接 Steam、不启动游戏。 |
| `./tests/VersionPreserveNativeUi.test.ps1` | PASS。生产 DOM 实际点击 → 生产主机参数校验和 `LibraryFolderSelection` 路径生成 → `ModernController` → 原生 `VersionLibrary` 实际文件操作。四阶段取消、失败/清理、中文路径成功及注册、复制后修改来源副本独立、原用户模组清单原字节保持。只用隔离合成游戏文件，系统目录窗口以选择隔离父目录替代。 |
| `./tests/VersionListConcurrency.test.ps1 -EvidenceDirectory artifacts/version-preserve-20261029/version-list` | PASS，版本读并发 266 项断言及既有 VersionLibrary 30 项断言。原生真实复制期间读取不抢占任务/取消所有者，元数据并发发布及已有目录拒绝/清理语义保持。 |
| `node tests/library-ui-tests.cjs` | PASS，版本切换、推荐页、备份操作相关既有 DOM 回归。 |
| `node tests/library-lifecycle-ui-tests.cjs` | PASS，中断恢复、修改任务生命周期、目录选择取消及上下文切换相关既有 DOM 回归。 |

编译输出保留四个既有 CS0649 警告（GameLogInput.Warning/Truncated、GameStartupPresenter.PrelaunchClosed、Program.PreviewMode）；没有编译错误。未运行整个工程测试集。

另外执行了旧 `node tests/java-workflow-ui-tests.cjs`，该用例在进入 `components` 后寻找 `[data-component-discover]` 超时。以 `ce12cde` 的原始 app.js 与当前 app.js 分别渲染生产页面复现，两个版本都返回 0 个该按钮、相同新版配置组件页面。这是既有入口已过期的测试边界；本次未删除、放宽或修改该测试，也不把它报告为通过。比较证据：`java-baseline-comparison.log`。

## 文件及界面证据

证据目录：`artifacts/version-preserve-20261029/`（生成证据按项目规则不提交）。

- `native-ui-result.json` 保存每次实际请求/回复、进度事件、成功文件 SHA-256、目标和注册结果、隔离根目录。失败/取消均为零 partial、无目标、无注册；成功为一个独立注册。
- `native-results.log`、`native-ui-compile.log` 与 `version-list/results.log` 保存原生执行结果。
- `dark-1100.png`、`dark-390.png`、`light-1100.png`、`light-390.png`：已实际查看暗/亮主题及窄窗；标题、用途、来源、文件/容量、取消按钮清晰，无横向溢出。
- `native-scan.png`、`native-copy.png`、`native-verifyCopy.png`、`native-verifySource.png`、`native-failure.png`、`native-success.png`：原生串联的四阶段、错误和成功页面。已查看最终来源复核画面，未知总量使用活动条。

未覆盖：Windows 原生目录窗口的桌面点击、完整生产发布包、自有服务器/GitHub、真实游戏进入/存盘/再启动、22 GB 真实游戏完整复制。本记录没有把合成游戏文件或 DOM/原生接口测试描述成实际进入游戏验收。
