# 旧存档快照来源与实际操作验收

日期：2026-10-27。用户明确允许在作者改标识或旧文件无法取得时，逐项选择新版或跳过。原存档不因快照创建被改写；默认位置、真实进度、实际创建和从副本进入世界均属于本次范围。

## 真实来源与缺失模组

源游戏 E:/steam/steamapps/common/ProjectZomboid 为 42.21；源用户 C:/Users/MC233/Zomboid。将 Apocalypse/2026-08-02_18-36-09 全部 582 文件（16,489,753 字节）复制到有独立所有权标记的临时用户目录。未操作前台或停止其他游戏/服务。

- AutoCook 的 B41 根 mod.info 与 B42 版本目录不再被误判为同版本多来源；dustinguished_cleaning_wipes 的 common-only B42 布局已纳入枚举。
- GardeniaTranslate_CN、WL_WeaponSlings、B42ModCNTranslate、GRTranslationCN42 已通过真实 Steam 下载并重新读取准确 mod.info。通用解析按完整 Mod ID 搜索作者声明；没有为这八项写死映射。只有真实来源记录才能对多个精确候选消歧。
- 当前空投工坊只提供 AirdropMod，旧 AirdropMod.20 不再提供。Ladders42131 属于不适用当前游戏的旧布局，同作者保留的 LaddersOld/42.20 为 Ladders4220；另一个 Ladders42204 标注用于新存档，不自动替换旧存档。
- 本轮实际明确选择 AirdropMod.20→AirdropMod、Ladders42131→Ladders4220。选择进入快照记录与 Java 隔离清单，原 mod.info 与创建前的原 mods.txt 不改。Preview 最终为 141 个选中模组、141 个实际来源。

## 保存位置、进度和体积

默认位置在安装目录同级，例如 D:/OrangeLauncher快照，避开更新器/卸载器的安装目录范围。用户可更改并记住位置；不再每次强制弹文件夹选择。创建与位置更改不被无关的来源启动恢复记录阻挡。

原扫描把游戏目录内整套 steamapps/workshop 缓存算入本体，该树实测 12,098,490,326 字节。本次捕获、复制和来源比较一致排除缓存；实际启用模组仍从自己的来源独立复制，包含来源恰好位于嵌套缓存的情况。

真实快照已创建并登记：142,370 个最终文件、164,104 条含目录清单、16,374,284,638 字节，schema2 清单为 46,869,037 字节。没有用 Steam 硬链接代替副本，也没有宣称实现多个快照共享版本池。首次快照仍需要游戏本体和选中模组的实际空间。

核对显示已读取文件、容量、阶段和用时；复制/摘要验证使用实际总量，未知总量持续活动显示而不伪造百分比。容量计算、清单写入、失败/取消清理分别有可见阶段，不残留旧 100%。用户失败后保留已选新版/跳过与位置，能调整后重试。

## 真实发现并修复的阻塞

- Windows LongPathsEnabled=0 时，正式 manifest/开关不能独立解决目录长度限制；已在快照 I/O 边界使用 Unicode 扩展路径。实际长子文件复制、摘要、发布、读取与自有失败清理通过。不是任意超过 MAX_PATH 的工作目录都可启动，边界见长路径记录。
- 163,688 条真实元数据使旧 schema1 超过 64 MiB，发生在复制完成后；schema2 不重复保存模组来源数组，保留来源时间戳差异，兼容 schema1，并在复制前按元数据做容量预检。真实新清单已完成保存并重读。
- 原生 EXE 误用 PATH 中另一套 Java DLL，修复为仅游戏子进程前置自身 JRE；source/非快照独立版本保持原生启动。
- 原生 EXE 将 JSON 中中文 VM 路径按 GBK 误读。最终冻结快照改从自己的 Java Unicode 入口启动，沿用经过验证的 JSON 参数、实际 Windows 分支、模组代理和 cachedir。未放宽隔离或重写快照内容。

路径与元数据失败的旧记录保留，不能只引用最后成功结果隐藏问题。详细原因、RED/GREEN 和边界分别见同日 snapshot-long-path-io、snapshot-manifest-capacity、native-jre-dependency-search 记录。

补充实际发现：原样保存 134 项清单曾被自动合并成 141 项组件列表，导致后续启动失败。r4 改为先校验 stamp/输入后，无改动直接返回，不写文件或自动添组件；真实 source-controller 与 ForSave 两条路径的字节、mtime 和后续 Validate 均通过。27 项新回归和原 84 项测试通过，详见同日 save-unchanged-noop 记录。

## 证据与测试边界

- artifacts/save-progress-native 与 save-progress-native-ui：真实原生只读 Preview 进度、取消/重试与页面截图；不代替实际创建。
- artifacts/save-environment-ui：22 组 UI 操作与 5 组进度操作，以及最终 manifest-save/cleanup 定点回归；深浅主题、小窗口、选新版/跳过、位置保存与失败恢复已查看。该组模拟原生响应，另有真实后端文件验证。
- artifacts/snapshot-manifest-save-final：123 项快照文件、来源、取消与清单读取测试；snapshot-manifest-runtime-final：321 项运行隔离与复制测试。
- artifacts/snapshot-controller-final158：158 项 Controller 行为，包括旧恢复记录下独立创建、默认位置与递归下载。
- artifacts/snapshot-launch-unicode-final：29 项启动、会话与恢复 fixture；launch-jvm-unicode-frozen：34 项参数、JRE、Windows 分支与实际客户端角色探针。
- artifacts/frozen-environment-438bd529e59343abbf9f8beceb8a9a55：真实 42.21 ActiveModsFile/getRequire/setRequire API，替换/跳过持久化、原文件不改；API 探针不冒充进入世界。
- artifacts/SaveSnapshotReal-isolation-e710975b01d54d6ab940c4d3073521f1：生产 Controller、实际游戏 JAR/JRE 的独立副本和测试模组。主动改变/移除这些自有来源后，冻结文件摘要保持不变、CheckUpdates 正确报告、冻结 JVM 读取通过；没有改真实 Steam 安装，也不把此用例说成完整图形世界进入。
- artifacts/SaveSnapshotReal-20261027-main-a3：真实存档克隆、21 项 RSA 验证、真实模组来源、完整 16.4 GB 创建和运行证据。r1/r2 失败、直接 Java 诊断和最终正式入口分目录记录。直接诊断只用于定位原因。r3 两次生产入口进入真实 IngameState、原生存盘、正常退出后，r4 最终包继续以生产入口进入世界并正常存盘退出（exit 0、无强杀、恢复 pending=false），退出后 Validate 与实际 JRE 验证通过。角色坐标继承上一轮进度；原生 Core.TakeFullScreenshot 截图已实际查看。后台测试仅在游戏完整加载、明确等待点击时设置正常点击标志，未跳过加载或强制切状态。
- artifacts/publication-1.2.35/original-save-all-files-proof.json：全部 582 个原存档文件与来源入口摘要保持不变。

## 发布状态

1.2.35 最终候选为 final-r4，生产启动、实际世界、存盘/退出/再次读取和原样保存回归已完成。152 项生产输入及最终资源冻结、21 项 RSA 认证、10 项发行文件校验通过；实际 final-r4 按需 Defender 扫描无检测、35 文件字节无变化，没有改 Defender 设置。Windows 发布者证书仍未配置，按用户明确免费且先发布的要求显式允许未签名发行；本次扫描不能代表未来永久不会误报。已发布到自有服务器和 GitHub，网页更新记录已按线上原文件增量同步。20 个匿名发行下载哈希、三项最新更新清单和 release.json 均匹配；正式 1.2.35 客户端为 Latest，1.2.34 实际下载验证为 Ready。保护区 865 项与其余网页配置保持不变，已公开 1.2.34 附件保持不可变。完整发布回执见同日 release-1.2.35 记录。