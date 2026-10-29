# 加载画面开销优化：2026-10-30 本地验证

## 改动与结论

本次降低了启动器自带加载画面的文字测量和日志文件读取开销，保留已有橙子图标、动作、配色、布局、60 FPS 节奏和原生下载/授权/取消行为。没有调整服务器模组清单、排序、游戏加载算法、Windows 输入或添加全局任务锁。

- `LoadingScene.fit` 与地址换行使用按 Unicode 码点边界的二分前缀查找，替代逐字删除并重复测量。`LoadingModel.safe` 同时避免截断代理对。
- `IssueSampler` 使用单个按需守护线程读取原有有界错误信息文件；绘制线程只取缓存。读取发生阻塞时不持有取缓存的锁。新会话/连接代次丢弃旧结果；关闭不 join 或 interrupt 文件读取，后台空闲时等待，不持续轮询。
- 实际操作另外发现：首次启动停在原生 `TermsOfServiceState`，但 BOOT 加载层没有退出，导致条款被遮挡。通过只读 JVM 字段确认原因后，增加 `OnGameStateEnter` 的限定交接，让游戏自己的条款页显示、接收输入。没有自动接受条款或跳过其原生处理。

这些结果不能证明用户此前服务器模组加载时 Windows+D 也无反应的短暂卡顿已经修好。此前日志与系统事件没有足够证据确定该次原因，本次也没有复现该现象。

## 同场景测量

基线资产来自正式 1.2.43：`E:/Projects/OrangeLauncher/dist/1.2.43-session-recovery-20261030-5470aba/app/Assets/OrangeStartup.jar`。同一安装游戏 Java 25、同一隐藏 OpenGL 场景，预热 24 帧后测 90 帧，固定动画时刻、内存值和较长活动/错误文字。候选使用最终代码生成的资产。

| 场景 | 基线每帧 wall p50/p95 (ms) | 最终 p50/p95 (ms) |
|---|---:|---:|
| 初始加载 | 2.1645 / 2.5407 | 0.4192 / 0.8951 |
| 服务器加载 | 2.0184 / 2.3066 | 0.4012 / 0.8162 |
| 存档加载 | 1.9403 / 2.1567 | 0.2757 / 0.6633 |
| 工坊下载 | 0.6564 / 0.8242 | 0.2337 / 0.6464 |

512 字符测试中测量调用数从 502 降为 11。四类场景的固定帧 PNG 与基线逐像素相同，结果在 `artifacts/loading-overhead-20261030/visual-comparison-final.json`。这些是加载画面自身绘制耗时，不是服务器整体进入时间。Windows ThreadMXBean CPU 时间量化约 15.625 ms，逐帧 CPU 百分位无解释价值，因此不据此宣称精确 CPU 使用率改善。

## 回归执行和发现

- `tests/LoadingOverhead.test.ps1 -Label final-with-terms`：文字边界与成本 335 项、后台读取生命周期 546 项、真实绘制入口/文件读取隔离 11 项通过。包括慢读时继续绘制、真实错误文件刷新、无效文件清空、旧连接迟到结果、关闭和异常恢复。
- 初始成本测试在正式旧资产上因 502 次测量失败；新读取测试在旧资产因缺少采样器失败。Unicode 边界新案例先失败再修复。旧代次结果测试经独立审查发现原断言窗口不足，改成阻塞新读的双阶段检查；去掉 epoch 校验的临时变异实现现会失败。
- `BootInteractionTest`：旧实现会被原生条款遮挡断言拒绝，最终 11 项通过，已接入 `IngameStartup.test.ps1` 的原生探针列表。事件 fixture 不是完整引擎验收，真实操作见下节。
- 聚焦 `Run-NativeCompositionFixture.ps1`：LoadingFrameRateTest 8、NativeCompositionTest 23、NativeStatusTest 45、WorkshopDownloadTest 110、NativeLuaGlProbe 32 项通过。WorkshopDownloadGlProbe 的 720/1080 实际 Lua 行、滚动与按钮图通过；RendererTextProbe 的隐藏 GL、字形缓存复用、红绿着色和无 GL 错误通过。
- `StartupTransition.test.ps1`：27 个收据/过渡与 11 个真实 GL 连续性检查通过。
- `LuaReloadStartup.test.ps1`：20 个重载生命周期/实际动画检查及安装游戏 Core.ResetLua API 核对通过。
- 原有 LuaReloadTest 要求橙子增长超过 3 倍，与已批准的固定大小旋转设计矛盾，正式旧资产同样失败。改为确定时钟与实际橙色区域的角度差异检查，保留原有交接/异常/旧 owner/无关重载断言。StartupTransition runner 原先漏编译 WorkshopDownloadTest，已仅补该依赖。
- 新采样器 method reference 曾让故障注入用 `begin(null)` 提前抛错；保留该空会话路径，使绘制异常仍在原位置被捕获，并验证同帧 Abort 与下帧原生页恢复。没有删故障测试。
- 没有重跑无关项目全套、旧的全局 growth 套件、快照、删除、网站部署或安装更新链。

隐藏 GL 与 fixture 主要证据目录位于当前工作树 `artifacts/loading-overhead-20261030/` 的 `baseline`、`final-with-terms`、`final-*`，以及 `artifacts/startup-transition-acd5262f956d4273b2c405dd35d12139` 与 `artifacts/lua-reload-fix-20261026`。首次失败目录保留。`native-lua-light-1920.png` 截到既有透明入场首帧，不作为成熟状态截图；stale ticket 黑底图属于失效渲染断言，不作为正常完成界面。

## 实际点击与测试资料

测试资料根目录：`E:/Projects/OrangeLauncher/artifacts/loading-overhead-runtime-20261030`，有本任务 owner 标识。复制独立 42.21 游戏目录和新用户目录；显式窗口模式，无用户真实存档操作。没有改变 D 盘已安装程序或原 Steam 游戏目录。

最终候选：`E:/Projects/OrangeLauncher/dist/loading-overhead-local-20261030/final-app`。源码中的 C# 启动器未改；`LoadingAcceptanceHost.exe` 是未纳入发行清单的独立测试辅助宿主，载入该候选真实 ModernForm/ModernController（TestMode=false）和生产游戏进程启动实现，仅提供隔离的 game/user/data/distribution 路径及离线 updater。**这不是未改 Program.Main 或完整安装/更新链验收**。启动参数由生产控制器构造，没有手工改变游戏命令，没有向最终游戏进程注入诊断代理。

实际步骤：

1. 打开候选生产 UI，点击引导的“进入启动器”，点击“启动到主菜单”。
2. 第一次尝试因上一辅助宿主遗留的请求失败。该请求及状态文件已仅从有 owner 的测试目录移到 `first-attempt-request-evidence` 留证，未改任何真实资料，也没有为其扩大产品策略。
3. 再次点击后，日志记录生产 process-submit 和 completed。最终实际游戏 PID 11640，使用独立副本的 Java；窗口约 643×458，原生条款页面正常出现。
4. 实际点击“接受”，日志出现 `STATE: exit zombie.gameStates.TermsOfServiceState`、`[OZL-STARTUP-JAVA] game-menu` 和桥接 menu-ready。原生菜单完整可见。
5. 点击“退出游戏”→原生确认“是”，日志 `GameThread exited.`，进程退出；随后关闭测试启动器。未留下本次测试的游戏/辅助宿主进程。

实际截图：

- `E:/Projects/OrangeLauncher/artifacts/loading-overhead-runtime-20261030/final-native-terms.png`
- `E:/Projects/OrangeLauncher/artifacts/loading-overhead-runtime-20261030/final-main-menu.png`

首次停留的旧进程 PID 42540 曾使用标准 jcmd 和本任务只读 attach 探针确认 states.current、60 FPS 与 BOOT FULL；这些仅为诊断，不能替代最终操作验收。最终 PID 11640 未附加该探针。

辅助宿主的 session-watch 未正确建立，UI 提示退出监控未启动。这是隔离宿主与正式进程入口不同导致的测试限制，本次未验证完整安装的退出恢复，不能把该 warning 隐藏或当作已通过。完整 Program.Main、安装更新、真实服务器模组加载与原始整机短暂卡顿仍是正式发布前后续验证范围。本次没有发布。

## 独立审查、包完整性

按工程要求由独立子 Agent 严格只读复审实现、测试和实际截图。Unicode、旧代次测试窗口、测试接线遗漏已修正。复审查看 23 张最终 GL 图和 2 张真实窗口图，未发现新增遮挡或布局问题。两张真实截图只证明条款/菜单在隔离宿主调用生产控制器后可见，不能扩大成完整安装验收。

最终包 RSA bootstrap 验证通过，21 个发行清单文件哈希与长度一致。OrangeStartup.jar SHA-256：`89B91DD3B500E9747816938230B574DB1ACB1B2C3732348B014E52BD540D94A1`，验证收据 `E:/Projects/OrangeLauncher/artifacts/loading-overhead-runtime-20261030/final-candidate-verified.json`。Windows Authenticode 状态 NotSigned，本地包仍标 1.2.43，仅用于候选验证；不得以同版本推送，也未宣称杀毒问题解决。

本次不修改 VERSION、CHANGELOG、服务器、GitHub、自有网站、推荐服元数据或 D 盘安装。其余网站/部署任务的未提交改动保留。
