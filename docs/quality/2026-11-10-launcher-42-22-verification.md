# 42.22 启动器模式观察兼容修复

日期：2026-11-10。源码基线 `b20efacd47ed44ac1ae2a4fa516ae124892a81c8`，工作树为 `C:\Users\MC233\.codex\worktrees\velopack-updates\OrangeLauncher`。用户本轮要求负责启动器本身，第三方 3D 新版适配交由模组作者。

## 原因与实际修改

42.21 的 `Core.gameMode` 是静态 String，42.22 改为实例 `zombie.core.GameMode`。旧加载组件按静态 String 读取时得到空文本，加载页缺失模式信息；没有证据表明该显示信息缺失本身阻止游戏启动。

唯一生产代码修改是 `StartupRuntime.string` 的模式读取分支：字段精确匹配实例 GameMode 时，从当前 `Core.getInstance()` 读取并使用模式 ID；旧静态 String 保留原读取路径。不缓存先前模式，空值或观察异常仍只省略文字。新版实际 `GameMode.toString()` 返回其 ID，既有显示层可直接使用。

没有修改页面布局、动画、单人/联机生命周期、Java 授权、任务占用、Steam 参数、游戏文件或玩家存档。联机分支仍不读取本地模式/存档名称。没有改动任何第三方模组，也没有增加版本拦截或新弹窗。

## 本轮执行的验证

证据根目录：`E:\Projects\OrangeLauncher\artifacts\launcher-4222-fix-20261110`。

| 检查 | 实际结果与范围 | 证据 |
| --- | --- | --- |
| 先验证旧组件失败 | 已安装 1.2.52 原始 JAR 在新版实例模式断言正确失败，旧模式断言先通过 | `mode-red.log` |
| 新旧模式生产 helper | 11 项通过；覆盖旧 API、新 API、切换模式、不缓存、空值/缺字段/实例异常/文本异常及未知 Object | `green/mode-green.log` |
| 新版真实游戏类 | 18 个实际 42.22 类及加载钩子、队列、生命周期、事件接口通过；不初始化游戏类，不调用游戏入口 | `green/actual-current.log` |
| 旧版真实游戏类 | 同一 18 类契约在保留的 42.21 游戏副本通过 | `green/actual-legacy.log` |
| 加载模型和生命周期 | 模型 28、细化模型 23、真实代理转换 51、进入/退出/排队/重复加载 32 项通过。后两组使用生产代理和游戏形状夹具，不是实际入服 | `lifecycle/*Test.log` |
| 版本元数据 | 5 项通过；游戏 JAR 优先于旧 version.txt，读取不伪造或写入元数据 | `version-green/metadata.log` |
| 类文件版本读取 | 17 项通过，包含旧版补丁号、不同整数编码、无效输入和精确原字节保留 | `version-green/classfile-direct.log` |
| 启动配置 | 72 项通过；真实隔离文件验证配置、组件/普通模组状态、顺序、版本选择与快照索引，未调用游戏入口 | `profiles-green.log` |
| 存档发现与列表 | 35 项、0 失败；隔离存档/绑定/异常清单文件，未点击正式用户页面 | `save-read/results.log`、`save-read/run.log` |
| 既有加载页画面 | 后台绘制并实际查看 1280×720 与 640×480 的新旧 API 四张图；相同输入的新旧图摘要一致，模式恢复到原元信息行 | `green/scenes/*.png` |
| 独立审查 | SubAgent 逐张查看四图并独立重跑 11 项及新旧 18 类验证，代码/画面通过；另复查三个 C# 测试引导和夹具维护通过 | `independent-review.md`、`reviewer-verification/*.log` |

候选加载组件 SHA-256：`4DEEBD948422A510C6C720C41B026C1E93777111C3082E9A13A4F44BC7C85AF1`。实际新版游戏 JAR：`3C4A7C5F149D784758C6FB5F8FA85F3A21D572E38BC081400E89035F8120D287`；旧版 JAR：`E1A69EB743EDE60B213A0FE7F8B83D4FCAB773036D256CC4543A336F3B058A33`。探针运行前后真实游戏 JAR 摘要相同；测试 GameMode 类不在真实游戏探针 classpath，也没有进入候选组件。

本地实际游戏是 Steam public 42.22.2 构建，但其 Core 兼容版本为 42.22，根目录 version.txt 仍旧。启动器现有版本读取优先 Core，未将发布页补丁号臆造进兼容版本，也没有为显示版本重写文件。

同日先前后台检查已覆盖实际新版 JRE/参数构造、普通配置清单与快照目录隔离，见 `E:\Projects\OrangeLauncher\artifacts\game-update-review-20261110\TEST-REPORT.md`。这些是本次最小 Java 观察修复之前的结果，不能称为本轮重新执行或完整游戏验收。

## 测试承载问题及修正

1. 两个 C# 版本测试入口未调用生产入口已有的 `LauncherRuntime.Initialize()`，深层证据目录触发长路径异常。测试 Main 补齐相同的进程内初始化后通过；没有修改产品路径校验、注册表或测试断言。原失败保留于 `version/metadata.log`。
2. 配置测试仍将泛称 Component 视为需安装的框架。现有产品已将普通模组统一管理，该夹具不会生成组件 selection.json。夹具改用现有明确安装组件 ZombieBuddy，原隔离、no-op 字节和顺序断言完整保留；原失败保留于 `profiles.log`。
3. 首次通过 PowerShell 反射调用版本测试 Run，错误地使用 PowerShell 的应用基目录；改为直接运行该测试 Main 后 17 项通过。此反射承载错误不是玩家故障，未修改系统目录。
4. 新加的证据脚本最初在代理 JVM 的 classpath 重复放入原始位置 JAR，导致会话位置不匹配；移除重复 JAR 后代理成功安装。其后因遗漏实际游戏 JAR 的 Kahlua 运行时类型导致细化测试失败，补齐既有测试所用真实游戏 JAR 后通过。保留 `lifecycle/*classpath-red.log`。编译期 Kahlua 声明没有加入运行 classpath，未改产品或放宽断言。

编译存在既有 CS0649 字段未赋值警告；Java25 的 Byte Buddy Unsafe 弃用警告同样保留在原输出中，未将警告过滤成成功。

## 验收与交付边界

全程后台运行，没有显示/控制用户前台窗口，没有调用游戏主入口或结束未知/其他任务的 Java 进程。截图为生产 LoadingScene 的 AWT 后台绘制，不能代替原生游戏渲染和动画实机验收。本轮没有修改交互控件，不声称完成玩家页面逐项点击、实际进入存档/服务器、整套快照复制、存盘退出后再次启动或第三方 3D 兼容验收。

本轮形成已构建和验证的本地源码修复；没有提升 VERSION、制作正式发行包、上传更新或替换 `D:\OrangeLauncher\current`。正式安装仍为原 1.2.52，不能说玩家已经收到此次修复。

测试副本按精确目录、构建摘要及测试回执核对后清理，共删除 9,829 个自有测试文件，释放 167,260,990 字节（约 159.5 MiB）；保留日志、失败记录、截图、脚本和摘要。逐文件清单与保护文件前后摘要见证据根目录 `cleanup.json`。工作树的新版加载组件保留供后续构建；其他任务的未提交网站/部署改动保持原状。
