# 启动配置统一界面：Task 6 验证记录

日期：2026-10-28。独立源码审查及定点修复复核已通过至 f4834e93，本地候选已构建并校验。本文保留各阶段记录；实际进入游戏的验收仍待执行，不授权发布。

## 行为

首页保留封面和启动目标布局，使用一个「启动配置」选择器。启动器、Steam、原版、普通命名配置和已登记快照从同一入口选择、编辑。普通配置引用当前文件；快照添加时实际复制当前文件。普通模组清单显式保存，组件与 Java 授权立即单独保存。组件页不显示普通清单的保存/排序按钮。

编辑器固定标题、关闭和普通清单操作区，内容与来源列表分别限制滚动范围。组件发现按需读取；长扫描可取消。复制显示实际阶段、文件数和容量，未知总量不生成百分比。A 的读取/复制结束不覆盖 B 的输入、焦点或草稿；关闭已提交的写操作可在首页查看或取消。

## 实际执行的隔离原生验收

入口：`tests/UnifiedConfigurationNative.test.ps1`，真实 Edge 无头 DOM 点击 → 生产 HostPolicy/CaptureRequest/ModernController → 独立临时文件。界面使用实际发布的 app.js，未使用合成成功应答替代配置操作。只使用合成游戏/模组目录和固定 Buddy JAR，游戏代码不执行；Steam 退出/重开进程边界由隔离实现代替，VDF 文件修改是真实原生代码。

最后证据目录：`artifacts/owned-launch/task6/`。最后 fixture：`E:\AppCaches\Temp\o6-mvrxvy9k`。

- 三内置配置、命名配置创建/改名/版本修改/删除、缺失版本修复、普通顺序保存和无改动保存（字节和 mtime 不变）。普通配置内没有复制 EXE/JAR 负载。
- 同名 Java 组件两个原生来源（工坊999111/999112）：可辨认来源，操作使用每行原生 token，只启用所选来源；授权按钮只属于实际启用的一行。普通清单不会因组件操作改变。
- 快照重排、真实复制/移除，原来源更新后副本字节不变；实际索引锁定失败保留草稿，解锁重试成功；复制阶段取消，再次复制成功。禁用保留 Java 副本，卸载删除最后未引用副本。
- A 复制时关闭编辑器并打开 B，A 成功后 B 草稿/焦点和 B 文件保持；关闭 A 的只读扫描取消指定 readId，B 继续读取。根 Dispose 取消尚在运行的快照来源读，更新器 drain 能看到未结束的读取。
- 立即选择 B 后编辑绑定 B；延迟旧选择写回不能覆盖更新的原生选择。相同配置的只读查询不被长写任务占用拒绝。
- 800 个真实 mod.info 目录的长列表，桌面/窄屏标题、关闭和保存可见，没有横向页面溢出。普通页不会预先扫描整个 Java 组件库。
- Steam 强制接管一次确认后真实清除隔离 VDF 的108600启动参数，保留其他游戏字段。生产 launch 入口执行准备并由 TestMode 明确拒绝实际启动，结束后选择器可用。
- 原 JSON/BAT、默认模组和原存档在启动器/快照操作后逐字节不变；B 普通文件未被 A 操作写入。六个外来部署/网站文件 SHA-256 与既有基线一致。

## 回归与范围

| 检查 | 结果与边界 |
|---|---|
| UnifiedConfigurationNative | 原生点击/真实文件 PASS，360 个实际请求；不是游戏验收 |
| FrozenConfigurationEditor | 75 断言 PASS；含崩溃重放、外部更改保留、冷 B 查询与 A 发布/版本互斥并行、逐行 optimistic inventory |
| LaunchConfigurationProfiles | 72 断言 PASS；不因全局快照/版本写占用阻断正常列表/选择 |
| OwnedReviewRegression | 38 断言 PASS；原生参数/JVM 探针，不是进入游戏 |
| SaveEnvironmentRuntime | 完整重跑377断言 PASS；此前一次 preview-stale 拒绝尚未归因，见下文 |
| OwnedAffectedRegression | Host/LaunchProfiles/Mods/Transport/ConfigurationRuntime/StartupControls 通过；恢复原六参数 Engine 重载后独立 GameProcessGuard 通过 |
| OwnedStartupSession | 4 个 JVM receipt 断言 PASS |
| 旧 PlayProfiles / 强制接管 | 759 / 95 断言 PASS；旧事务显式绑定 Steam，专用接管 runner 支持子进程崩溃入口 |
| Runtime Refusals 定点入口 | 22 断言 PASS；Create 拒绝时保留输入哈希/时间戳及 fixture |
| experience UI | 34 个组 PASS；含排序错误保留草稿的独立 RED/GREEN |
| mod ordering / launch pending / guide UI | 分别12 / 6 / 6组 PASS；I/O 合成夹具，DOM/指针/键盘是真实浏览器事件 |

保留旧 PlayProfiles 存储/导出后端。旧首页捕获、预览/应用、分享、目标绑定与旧组件中心封面假设已由新所有者选择/草稿/启动用例取代；详细映射在 Task6 报告。未增加测试专用旧选择器。

## 事务耗时

13,255,039字节 Buddy 事务曾在提交后重新解析完整 byte[] JSON 恢复日志，提交后等待26.838秒；文件复制本身约0.07秒。成功写入者现在只在磁盘日志字节与刚写入字节完全相同时复用已解析日志，其身份/哈希/输出校验保留；重启/不确定恢复仍从磁盘解析。

最后原生总耗时 2766ms，各阶段首次进度时间（相对开始，ms）：{"sources": 74, "scan": 448, "copy": 703, "publish": 2213, "committed": 2339}。组件及封面读取在原生响应后才开始，没有把后续全目录查询误称为文件复制耗时。

## 截图

| 状态 | 文件（证据目录内） |
|---|---|
| 一个首页选择器、版本/内存/组件摘要 | home-normal-selected.png |
| 普通命名配置编辑 | normal-editor.png、normal-editor-second.png |
| 同名来源明确选择、授权后 | normal-components-enabled.png、normal-components-approved.png |
| 真实复制与取消/失败草稿 | snapshot-copy-progress.png、snapshot-cancel-draft.png、snapshot-failure-draft.png、snapshot-component-failure.png |
| A 完成后 B 草稿焦点 | owner-B-draft-after-A-copy.png |
| 深/浅、1366/420宽 | editor-dark-1366.png、editor-dark-420.png、editor-light-1366.png、editor-light-420.png |
| 800来源滚动到底 | long-catalog-light-1366.png、long-catalog-light-420.png |

实现代理已实际查看相关截图。标题/关闭/保存保持可见；浅色截图通过原生主题偏好及实际CSS类检查。原生 fixture 的 status 已读取到已知版本 42.21.0；此前首页「未知」由 UI 误用库存哨兵值遮盖，已在 R3 修复并核对封面、摘要与所选项一致。该版本证据来自隔离 fixture，不代表实际进入游戏验收。

## 尚未关闭的审查项与实际游戏边界

一次完整 SaveEnvironmentRuntime 并发回归在 Refusals → Fixture.Create → SaveEnvironments.Create 报「快照预览已变化，请重新读取后创建」。没有保存前后 stamp 输入，无法确认是否存在共享输入变化；后续完整377 PASS不能说明已定位或修复。这项交独立审查继续核查，不能隐藏或据此宣布可发布。精确命令、并发组合和栈在 Task6 报告。

Task7 仍须使用待发布包生产入口进入实际游戏，覆盖中文快照路径、正常保存退出/再启动、进程识别/恢复，以及来源更新后副本独立可用。没有触碰实际存档/游戏目录或用户前台，没有发行、VERSION、服务器或网站操作。

最终视觉补充：编辑器进度轨道明确为 8px，轨道/填充沿主题颜色，未知总量继续使用活动条和真实计数。受影响路径单独通过生产 native fixture 点击复制→取消→文件清单不含 Big；截图 snapshot-copy-progress.png 已重新生成并实际查看。聚焦运行证据 artifacts/owned-launch/task6/progress-visual-report.json，隔离根 E:\AppCaches\Temp\o6-mvry8tts；完整套件记录仍对应前述最终功能运行。

Task6复审修复第1轮：R1来源读取取消独立于写任务，真实点击取消/重读/关闭再打开通过；R2初始扫描中切组件会在完成后加载；R3首页封面、摘要和选择项统一已知版本，内存写入与待读取三组DOM检查通过；R4乐观index/manifest/关键JSON读取允许原子替换，持生产旧句柄时真实发布通过，旧句柄仍读取完整旧代。限定delete sharing于乐观读，默认受锁读/复制散列保持原行为。新增tests/UnifiedConfigurationReviewFixes.cjs聚焦原生四组通过；主页37/37，相关快照后端75通过，原生完整受影响文件/点击套件361回复/828进度通过。截图home-normal-selected.png已更新为已知42.21.0，review-R2-components.png、review-R3-home-known.png均已实际查看。首个Runtime377未分类问题和Task7真实游戏边界保持未关闭；Task6待独立复审。


## 独立复审与父代理复测

Task6 初次审查发现四项实际问题：扫描取消后重读失败、初始扫描中切换组件页未加载、封面版本/内存陈旧、乐观读取句柄妨碍原子替换。修复提交1fa6142，独立复审规格与质量均通过。父代理在该提交执行 `tests/UnifiedConfigurationNative.test.ps1 -ReviewCase all`，四项生产控制器/真实隔离文件定点用例通过，耗时8.7秒；未把这次定点通过说成重跑全套。已查看刷新后的组件页和已知版本首页截图。此前完整原生界面父复测在5a467b8通过360响应/830进度事件，后续修复覆盖范围见实现报告。

尚待最终整批审查：有意切换配置后，旧 `acceptStatus` 可能留下“游戏目录已变化，请重新核对配置”提示；这条跨界面观察已登记，不能由定点通过隐去。此前Runtime377首次未归因的预览失效记录仍保留。Task7真实游戏验收尚未执行，当前没有发布。

## 最终源码复核与本地候选

整批审查发现并修复了同名来源卸载错配、包装读取的 Host/健康确认分类、首次打开普通清单与保存契约问题，以及有意切换配置产生的目录变化提示。后续限定复核又发现并修复 Steam 读取分支、未选择的通用 Java 模组混入普通模组添加，以及新增资料读取无法取消的接线问题。独立复核在 f4834e93 确认这些源码问题已关闭；此前事务格式整理仍为非阻断维护项。

父代理在 f4834e93 重新执行 `tests/UnifiedConfigurationNative.test.ps1 -FinalCase OrdinaryCancel`，退出0。实际发布 DOM 点击→生产控制器→隔离文件覆盖：首读取消/关闭/重读、首次保存取消不初始化文件、已授权配置保存取消保留清单字节及 mtime 和组件状态、重试保存、提交后迟到取消仍显示保存成功、A 后台保存完成时 B 草稿及焦点保留、操作结束后更新器等待解除。只用测试观察器暂停真实资料读取和提交后的应答，没有替换生产成功结果。编译仅有原有四条 CS0649 警告。

已实际查看四张刷新后的截图：`final-OrdinaryCancel-progress.png`、`final-OrdinaryCancel-initial.png`、`final-OrdinaryCancel-save.png`、`final-OrdinaryCancel-background-B.png`。进度显示真正打开的资料文件数/字节及活动条；取消后标题不再停留在读取中，重读/关闭可用；保存取消保留待保存清单；另一配置的搜索焦点不受后台完成影响。

本地候选：`E:\Projects\OrangeLauncher\dist\launch-configurations-local-20261028-f4834e9\app`。使用生产 Build-VelopackLauncher 入口构建，未使用 LocalPreview，VERSION 保持1.2.36。校验通过：受信公钥一致、bootstrap RSA有效、清单21个文件的长度和SHA-256均匹配。bootstrap SHA-256为 `1B80F5C02512B6B2773AA2C37E35F93CB3AC29A31B0D290DA8908F0E2329D2AA`。Windows发布者签名为 **NotSigned**；RSA校验不代表Windows代码签名，也不能宣称杀毒问题根治。证据：`artifacts/owned-launch/task7-candidate-integrity-f4834e9.json`。

六个外来网站/部署跟踪文件SHA-256仍与既有基线一致。未改网站、服务器、线上更新清单或正式版本号。整目录 `git diff --check` 报告的是保留的外来 deploy/README.md CRLF/空白；限定本次源码提交范围检查通过，未为消除此报告改写外来文件。

Task7实际游戏、正常存盘退出/再次启动、中文路径快照与来源变化后独立使用、多人连接和实际进程恢复仍未验收。本地已准备独立游戏副本与两个普通Lua标记模组，准备副本与编译/JVM/native测试均不计为进入游戏。首次Runtime377预览失效仍未归因；旧重跑通过不构成修复证明，保留输入未变化时若复现必须继续调查。

## 完整便携容器与静默入口复核

上节 `...-f4834e9/app` 是 Build-VelopackLauncher 的打包中间产物，并非可直接运行的完整分发容器。父代理从该目录静默启动时实际失败；Windows事件1026明确为 VelopackRuntime → Program.Main 的未处理 InvalidOperationException。源码对应“需要完整 Velopack 安装或便携分发包”，未进入 ModernForm 或游戏。该失败保存在 `task7-intermediate-startup-failure.json`，没有通过 LocalPreview 或绕过生产入口掩盖；这是验收包准备遗漏，不是已定位的新游戏启动逻辑缺陷。

随后通过原有 Build-VelopackRelease 流程，仅在本地以 AllowUnsignedRelease 生成完整容器，无上传/安装/快捷方式操作，版本仍1.2.36。实际便携候选为 `E:\Projects\OrangeLauncher\dist\launch-configurations-local-20261028-f4834e9-container\portable`，包含 `.portable`、Update.exe、执行stub及current。标准发行归档清单/RSA验证、包容器图标检查均通过；解压后的current再次通过21项长度/SHA-256和bootstrap RSA验证，bootstrap SHA-256为 `CE9BBF3E0D945F08F155630F56FB67A7DFCBED283238B548AEAF7DE7CCB48F71`，Windows仍NotSigned。证据为 `task7-local-container-verification-f4834e9.json` 与 `task7-portable-integrity-f4834e9.json`，都在上述artifacts目录。

用户要求继续后台，不占用前台。父代理用完整容器current中的实际生产EXE、空参数、WindowStyle Hidden静默启动；该进程持续响应，并产生真实msedgewebview2子进程，没有中间目录那项异常。未进行窗口激活、鼠标或键盘输入，也未启动游戏。观察后按PID、可执行文件路径和完整启动时间核对身份，仅结束这个新建的隐藏候选进程，未操作任何用户进程。证据为 `task7-background-portable-start.json` 与 `task7-background-portable-observation.json`。一次PowerShell7日期自动反序列化使最初的字符串身份比较误报，后续改为UTC ticks复核，确认进程身份一致；这不是产品进程识别故障。

这只关闭完整容器的后台启动冒烟项。实际点击与实际游戏验收仍未完成，不发布更新。当前Windows工具的输入会自动激活目标窗口，用户当前后台限制保持；没有把后台启动存活、无头DOM或控制器调用说成完成游戏菜单、存盘和快照重启。

## 2026-10-28 实际点击与生产游戏入口检查

用户本轮明确授权点击验收。使用完整便携容器的生产 EXE，无 LocalPreview、替代启动 main 或手工测试代理；原始 Steam 游戏、默认模组与存档不作破坏性测试。

实际 Windows 操作（f4834e93 完整容器）：原生文件夹选择器导入独立中文路径游戏，创建“OZL 验收 A”命名配置，选择独立游戏版本，搜索并添加 OZLQAFirst / OZLQASecond 两个普通 Lua 测试模组，实际拖动顺序、点击自动排序、保存清单。最终 owned default.txt 顺序为 First → Second，组件状态没有被清单保存改动。物理路径选择器取消 → 返回 → 首页 → 编辑也能继续操作。首次成功导入后首页的一次不可点击/AX 缺失观察仍未归因；重启恢复及后续取消路径通过不能证明这项已修复。

实际点击发现三个问题，均保留原失败证据并作限定修复与复核：

- 自动排序夹带全目录封面/作者资料提示。be54a8b 仅去除排序过程的展示资料读取，保留所选模组依赖、循环和冲突信息。父代理在0dad012重新运行 `tests/UnifiedConfigurationNative.test.ps1 -FinalCase SortWarnings` 退出0；普通/快照真实DOM点击与生产控制器、隔离文件结果通过，三张 final-SortWarnings 截图已实际查看。该定点复测不是完整包窗口的再次点击。
- 第一次实际生产 Launch 的 JVM 在 Steam 初始化后以78退出，原因是未启用工坊模组中的合法 namespace ID（如1299328280/ToadTraits）被 live 解析器拒绝。0dad012 修复后，重建实际 runtime agent 与完整便携包，再从首页实际点击 Launch。进程44152实际使用中文 QA 游戏自带 java.exe、16GiB 和生产 live/startup agent；参数保存在 task7-0dad012-game-processes.json。两个普通 Lua 版本A标记均进入真实 console，随后记录 `[OZL-STARTUP] menu-ready`。视觉只检查到首次启动警告页；菜单就绪来自日志，不能声称已点击主菜单或进入世界。用户随后关闭测试游戏。
- 完整0dad012程序重启到持久化命名配置时，误报“游戏目录已变化”。cb91cae 修正首次目录异步加载前错误路由到内置 launcher 的状态请求，拒绝旧owner回复；真实外部路径/配置变化的失效处理保持。父代理新跑 `-FinalCase BootSelection` 退出0，覆盖持久化目录回复的两种时序，已查看两张 final-BootSelection 图片。独立限定复核通过。这是生产控制器与新DOM回归，最新完整 EXE 的重启点击仍待验证。

测试游戏使用新导入版本的独立 user 目录，首次启动为全屏，干扰了用户前台。用户要求关闭后，已停止游戏验收，不再恢复或启动游戏；仅将该 QA user/options.ini 改为 fullScreen=false、borderless=false、1280×720。原用户游戏设置未改。游戏进程已不存在；仅终止精确路径和出生时间再次核对的测试候选进程。通过生产 LaunchConfigurationProfiles.Select 将仍选中的本次 QA 配置恢复为内置 launcher；命名条目完整保留，没有覆盖整个索引。清理步骤是原生API调用，不冒充界面点击验收。

最新只供后续本地验收的候选为 `E:\Projects\OrangeLauncher\dist\launch-configurations-local-20261028-cb91cae-container\portable`，尚未启动。标准完整归档校验及current内RSA、21项长度/SHA-256验证通过，bootstrap SHA-256 `D0472D105D55BF7BF64AD71F7F7D8CAE07DDFFE240EBDFF66A50FED769341CD0`，Windows发布者签名仍为 **NotSigned**。没有安装、VERSION变更、GitHub/服务器上传或网站操作。

对应证据位于 artifacts/owned-launch：task7-real-ui-preparation.json、task7-first-game-failed-console.txt、task7-0dad012-game-processes.json、task7-0dad012-menu-console.txt、task7-0dad012-candidate.json、task7-cb91cae-candidate.json、task7-qa-process-cleanup.json、task7-qa-cleanup-selection.json；相关后台点击截图和响应在 task6/。实际Windows画面通过当轮工具截图直接查看，未将后台DOM截图冒充Windows游戏画面。原始JSON/BAT/default.txt及原loaded.txt不存在状态逐项保持，六个外来网站/部署文件哈希保持。

**仍未通过的最终门槛：** 最新包实际重启、选择Java组件并进入游戏、真实创建中文路径快照、从快照进入世界并保存退出/再次启动、来源变化后快照独立使用、多人连接与实际恢复/强制关闭。首次原生导入/Home观察及旧Runtime377预览失效仍未归因。Task7保持未完成，不能宣布全功能验收或正式发布。
## 2026-10-29 启动器管理侧验收收尾

用户本轮明确把验收限定为启动器的配置、界面与文件操作，并要求解释功能、原理和用法；本轮不再启动游戏，也不把进入世界作为这部分管理功能验收的前置。此前游戏内加载、存盘与再次启动等边界仍保留为未覆盖，不能据本节反向声称已完成游戏内验证。当前仍是本地候选，没有正式发布。

父代理先对当时源码执行九组最终回归：前八组通过，BootSelection 在第二次页面加载超时。原生回执证明两个并发 versionList 查询中有一条被根控制器的全局 busy 拒绝，导致首页目录读取失败。失败 trace、截图和71个隔离文件哈希/长度/mtime留存在 artifacts/owned-launch/launcher-only-boot-failure-20261028；没有用重跑通过覆盖原失败。修复52447263只把明确的版本列表查询转为独立只读入口，返回同一版本目录文档中的selectedId和版本行；读取不替换/取消复制任务的CTS，不进入全局busy。版本元数据打开与原子发布使用同一路径的短命名互斥，整段复制的原有写互斥保持。严格参数、已释放对象、真实冲突写操作和现有元数据边界保持。独立限定复核通过。

父代理在最终生产源码独立执行 `tests/VersionListConcurrency.test.ps1 -EvidenceDirectory artifacts/owned-launch/version-list-concurrency/root-final-20261029`，退出0：266项定点检查和原有VersionLibrary30项断言通过。实际复制暂停期间八个查询可用，原写任务占用及CTS保持；跨进程40次真实原子发布与200次读取并行、旧文件句柄保留原字节通过。这个结果限于按VersionLibrary协议操作的进程，不承诺任意第三方直接改文件都无I/O失败。

第一次完整管理界面复测另在旧807行计数断言处停止，保留330回执/779事件的失败现场到 launcher-only-catalog-assertion-20261029。实际目录808行中有804个普通模组、3个Java来源、1个托管Buddy；原807假设把三个Java来源算进普通“添加模组”，与已审查的分离行为不符。8b446f2仅修改测试：核对独立夹具的精确804个普通ID集合，确认Java未出现在普通列表，并实际切到组件页核对三个来源、两个工坊ID、原生token、未启用/未授权状态及浏览未产生写请求。未修改生产实现，限定审查通过；原失败仍保留。

最终父代理在8b446f2执行完整 `UnifiedConfigurationNative.test.ps1` 后，用同次新编译NativeUi执行BootSelection，均退出0。结果为 **361条真实原生回执、861条进度事件、0条页面异常**；144个生产源码文件在本次执行前后SHA-256相同。隔离根 E:\AppCaches\Temp\o6-mvsbdpo9，原生程序 E:\AppCaches\Temp\o6-bin-226f6076，汇总证据 launcher-only-final-verification.json 与 task6/report.json/trace.json。覆盖三内置选择、命名配置创建/改名/版本修改/删除、普通清单及无改动保存、顺序、组件来源与单独启用/禁用/授权状态、快照真实复制/失败/取消/重试/增删及文件独立性、A/B后台完成归属、长目录、Steam单次强制确认后隔离VDF实际清空、结束后的按钮状态与控制器释放。启动按钮只提交到测试模式明确拒绝实际进程的边界；没有执行游戏，也没有把占位游戏/JAR文件称为真实游戏。

已实际查看本次刷新后的 home-normal-selected.png、long-catalog-component-sources.png、snapshot-copy-progress.png、editor-dark-420.png：所选版本/内存/组件一致，三个Java来源在组件页，进度有实际文件与容量，窄窗口保留关闭和保存。这些截图是实际发布前端经后台浏览器调用生产控制器及隔离文件的结果，不冒充完整程序的Windows操作。此前完整cb91cae EXE本轮正常打开并读取到首页，因前台遮挡没有完成选择器点击；随后仅结束该自有候选进程，未改变真实选择。

最新本地完整包 `E:\Projects\OrangeLauncher\dist\launch-configurations-local-20261029-5244726-container\portable` 由已审查的524生产源码构建；8b446f2仅改测试，生产字节来源相同。标准归档校验、current内bootstrap RSA及21项长度/SHA-256通过；bootstrap SHA-256为 AC2CACED20E5E2606EC3D5362EACA2858CFF1B3C90EB5DB0B9795E7F6BD9673E，Windows发布者签名仍NotSigned。完整包实际生产EXE、空参数、Hidden启动，保持响应并产生WebView2子进程；按精确路径/出生时间确认后结束自有PID65384。该检查只证明正常入口可运行，没有伪称点过完整程序各按钮。证据 launcher-only-5244726-candidate.json、launcher-only-hidden-entry-identity.json/result.json。

本轮启动器管理侧范围已通过上述验收。首次物理导入/Home观察和旧Runtime377未归因记录继续保留，本轮未把它们描述为已修复；完整游戏内快照/存盘等验证不在用户这次限定范围。未改正式版本号、真实Steam参数、原游戏/模组清单或真实存档；未上传、安装、修改网站或覆盖外来改动。