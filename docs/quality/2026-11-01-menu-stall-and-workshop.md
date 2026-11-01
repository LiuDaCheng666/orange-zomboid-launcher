# 主菜单阻塞与工坊下载接管：验证记录（进行中）

## 真实事件基线

用户运行发行版 1.2.50，游戏 Build 42.21.0，2026-11-01 22:22:09.489 至 22:24:17.017 menu-ready 共127.528秒。loadMods 69.951秒，目录筛选10次累计4.937秒。Buddy checking mods ban status→作者列表超时25.103秒，ContainerStates.initialize前后15.215秒。后一项读取本地2352个OBJ及其MTL，不是网络请求；仍需冷读/CPU采样，不能把整个间隔直接归为某一次文件读取。

强制中文汉化loading标记22:23:29.451到load-summary22:23:29.515为64ms，仅是标记边界，不能代表全部汉化加载成本。菜单就绪至22:31开始联网之间430秒无日志，不视为卡住证据。当前客户端22:47:04.821正常退出；本轮未操作游戏窗口、未热attach、未结束游戏。

## 已完成：目录筛选

SafePaths.canonical原实现对每一级祖先执行exists、readAttributes及toRealPath，JFR native sample主要为FindFirstFile0和GetFileAttributesEx0。现在每一级仍以NOFOLLOW读取属性并拒绝链接/other，仅最深现存路径解析真实路径一次（同时覆盖祖先）；复用本次叶属性，不保存跨调用信任缓存。

- Performance RED：同一旧版JAR作baseline/candidate，470个自有真实目录筛选中位数比1.024，未达到25%改善而按预期失败。
- GREEN：同一自有目录基线中位数5218ms→候选1099ms（约79%下降），发现目录及顺序保持一致。不是实际游戏耗时。
- 用户现有470个工坊目录只读独立进程：旧版3600/3582/3538ms，候选1233/1061/1036ms；使用隔离会话manifest，无游戏初始化。
- SafePathsTest：13项真实文件/目录/缺失/越界/容量/junction根和祖先检查通过；Windows符号链接创建权限不足而跳过，对应覆盖边界明确保留。
- DiscoveryScope：28项作用域与4项Java执行范围检查通过。
- 强制汉化runtime与服务器10项通过，实际游戏类签名检查为只读字节码验证。
- FrozenEnvironment：32项manifest、3组13项发现/默认路径、4项active依赖、故障拒绝和安装游戏API探针通过；无游戏/UI/Steam主入口，真实游戏JAR哈希不变。
- 只读审查未发现Important问题；已将性能探针表述改为“发现目录及顺序”，不冒充直接核对所有解析ID。

## 工坊与网络

独立任务进行中。工坊临时布局拒绝永久使download.valid=false已在隔离fixture RED→GREEN；用户当前首次拒绝的具体条件尚未证实，必须由候选诊断收敛。Buddy只前移同一次原始网络正文请求，原生签名、缓存回退、解析及授权流程保持，不修改第三方JAR；仍需整合复核。

## 当前边界

本轮未发布、未覆盖安装、未进行真实游戏入口/工坊下载画面验收。任何隔离测试、微基准或JVM探针都不能替代这些验收。发布前须重新记录最终组件绑定与实际结果。

## 整合候选与用户延期

2026-11-01 用户选择“暂时不继续实机验证”。按此停止本机实际启动/连接验证，不启动或结束游戏、不替换现有安装、不发布更新。源码版本1.2.51为本地候选，线上仍是1.2.50。

候选完整产物：E:/Projects/OrangeLauncher/artifacts/diagnostics/2026-11-01-menu-stall/candidate-1.2.51；免安装入口 portable/橙子启动器.exe。原始10项发行文件位于release，RSA/清单/哈希通过prepare_release核验，Windows签名NotSigned。portable/current与app的OrangeStartup.jar均逐字节等于最终测试资产，SHA256为841ca5023575816e06d2d3c1d56aaf6dd0870ab5273a22ed78f60aec9242ca6e；component-binding.json记录realGameVerified=false。

### 工坊候选

临时CPU页面尚未准备好或额外可见root使resolve失败时，旧代码永久download.valid=false。现在只让出当帧，后续原生update与全部控件重新验证后才能恢复；持续不匹配仍保留原生界面。真正Error/取消/命令异常/渲染失败继续终止；补充已yield时真实命令异常不能复活、旧owner及generation不能误伤当前操作。每操作一次的拒绝日志包含stage、Java类、LuaType及可见性，不含账号/地址。用户截图当前实际第一次拒绝条件尚不确定，因此不能称已根治。

最终整合Startup资产直接运行WorkshopTransientTakeoverTest、WorkshopTakeoverDiagnosticsTest、WorkshopCommandGenerationTest、WorkshopGeometryRecoveryTest均通过。最终WorkshopDownloadGlProbe在隐藏GL环境通过，已查看1280x720图；测试包含故意触发的renderer failure/native fallback，不能把故意故障输出当作真实游戏崩溃或当作已实机验收。此前原下载110项、所有权24项由独立任务验证。

### Buddy网络候选

固定2.3.4 Loader.initConfig退出后前移同一次原fetchRemoteBody请求，首次原生需求处采用该请求；loadAuthors网络正文验签、缓存写入/回退验签、解析及授权原实现保留，第三方JAR不改。仅匹配已固定哈希JAR的CodeSource、定义classloader和helper可见性。优化不可用时回到原生查询。存在preloadMods先消费预取的配置时，后续查询仍走原生，不永久缓存，不保证所有阶段都消除25秒。

最终嵌入Live Agent的BuddyAuthorsPrefetch测试通过：早开始/一次transport、成功、IO异常、HTTP500、坏网络签名回退、网络与缓存坏签名、unchecked异常、等待中中断、预取完成后caller预置中断、配置/类定义边界、foreign CodeSource拒绝和production premain接线。网络测试只替换HttpClient.send，原Buddy验签/缓存/解析实际执行；没有真实联网或真实25秒超时/菜单总耗时证明。

独立审查发现的预置中断采用成功body、yield后terminal失效遗漏两项均已RED→GREEN修复并复核，无已确认残余重要问题。新单元/fixture不代表当前截图真实触发条件已核实。待用户继续后：先核对候选真实生产入口、相同配置到菜单耗时、[OZL-WORKSHOP]实际拒绝原因和画面，再决定进一步修复；不得仅凭本地微基准公开发布或宣称根治。
