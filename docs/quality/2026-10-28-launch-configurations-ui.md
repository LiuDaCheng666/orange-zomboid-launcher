# 启动配置统一界面：Task 6 验证记录

日期：2026-10-28。实现提交待根代理独立规格/质量审查。本记录不是实际进入游戏的验收，也不授权发布。

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
