# 1.2.40 本地候选验收

本轮修复 Buddy 安装长路径失败、联机临时组件选择与新 Java 文件授权，并按后续要求整理服务器列表。使用独立测试资料，不操作用户前台、真实存档或 Steam 配置。此记录不代表已经发布。

## Buddy 安装反馈与根因

从私有反馈服务读取两份 2026-10-29 的 1.2.39 报告。两次均扫描到 202 个文件、14,466,055 字节，复制到第 48 个文件后失败；结合截图中超过 260 字符的 `ConsoleModApprovalFrontend.java` 目标，复现了相同的 `DirectoryNotFoundException`。当时文件和目录实际存在，失败发生在传统 .NET 路径接口，并非该次工坊订阅或下载失败。报告中的较早游戏连接失败记录不作为这次安装问题的根因。原始玩家报告只保存于 ignored 私有 artifacts，不提交到文档或发行包。

`OwnedSupportInstallations` 使用项目已有的 Win32 长路径适配，覆盖枚举、读写、元数据、完整复制校验、收据、更新、取消回滚及卸载。不缩减原文件、不改变系统设置，不要求玩家手动搬文件。已有句柄删除、来源保留和同配置占用合同保留。

| 验证层次 | 已执行结果 | 证据 |
| --- | --- | --- |
| 失败复现 | LongPathsEnabled=0，截图同构目标在普通 FileStream 创建时报相同路径不存在；深层来源枚举也单独复现 | artifacts/support-long-paths-1240/red-reported-destination-results.log、red-before-native-io-results.log |
| 原生文件流程 | 277 字符及更深路径；完整复制、收据、停用、复用、更新、真实卸载、取消后重试，10 项通过 | tests/SupportLongPaths.test.ps1；同目录 green-native-io-results.log |
| 独立真实来源复验 | 实际 Buddy 工坊源 202 个文件、14,466,055 字节，目标逐文件哈希一致，进度到 202/202，真实卸载后原来源全部哈希不变 | independent-actual-202-receipt.json、actual-202-source-inputs.json |
| 独立失败回滚 | 发布副本后配置选择失败，旧副本和收据原字节恢复，无临时目录残留；首次安装失败清理与立即重试，9 项通过 | independent-rollback-receipt.json |
| 生产 DOM → 原生文件 | 后台真实点击启用、停用、重新启用、确认卸载与取消；实际目标 270/405 字符，取消前深文件已存在且内容正确，取消后已清理；其他配置、来源、原 JSON 和存档清单保留 | artifacts/support-long-paths-ui-1240/result.json、long-path-final-inputs.json、trace.json 与截图 |
| 相关既有回归 | support-files 5 项、support-installation 10 项、support-local-source 2 项通过；旧 fixture 入口补与生产相同的 LauncherRuntime.Initialize | artifacts/ordinary-java-membership-1239/long-path-* |

## 联机会话与授权

所有服务器连接入口共用“跟随服务器／强制加载本地组件”选择。选择只写入当次临时会话，保留原配置、Steam JSON 与快照执行合同。强制模式包含已启用组件和递归依赖，同 ID 服务端来源优先；在实际服务器清单到达前不先加载本地同包 Java 类。

Buddy 使用真实 `policy=prompt`；服务器会话移除可能覆盖它的旧 `-Dzb.*` 与 `-Dzombie_buddy.*` 属性。新服务器 JAR 可到达原生授权，拒绝后不得执行。已有单人／快照严格规则单独保留。排序循环仅提示，B41 原有 Steam 连接路径保留，原版或未启用 Buddy 的配置明确说明无法加载服务器 Java。

- 原生隔离准备、真实副本、生产 BuildStartInfo 与恢复等 105 项通过，输入和 EXE 哈希绑定：artifacts/server-session-native/final-owned-isolation-bound-source.json。
- 实际 Buddy 2.3.4 授权调用链 11 项通过：真实 Console 同意后执行 Main，拒绝／EOF 不加入类路径，变化哈希重新询问，同 ID 只执行服务器来源，自定义本地 Java 可保留。artifacts/buddy-server-approval-dcae7152/results.json。
- 生产 DOM 组件选择 13 项与受影响启动 9 项通过；已独立查看桌面深色、窄屏浅色、无 Buddy 配置三图。取消、关闭、Escape、切页、A→B→A 与迟到上游回复不会启动旧目标。artifacts/server-component-choice-1239/frontend-receipt.json。

## 边界

- 实际游戏窗口、ImGui 授权点击、真实服务器连接和 3D 渲染未执行；Console 授权与 JVM 探针不能替代这些验收。真正需要 JVM PREMAIN 的新模组也不能承诺在已启动进程内即时完成早期加载。
- 本次反馈的下载来源已经完整存在；未用本人的 Steam 账户测试首次订阅。现有首次下载入口会调用订阅确认和下载，不能把代码检查称为已实际订阅验收。
- Windows 发布者签名仍是 NotSigned；后续完整本地候选保留 RSA 包清单与哈希验证，不宣称 Defender 永不检测。
- 新服务器卡片、查询轮询与完整候选包的最终收据待本轮操作验收完成后补记。

## 顶部内存条实际场景验证

用户指出原内存条过细、文字太小。我先做成独立带框面板；此前“字号大、无重叠”的通过记录没有证明设计符合整页视觉，用户明确否决，该方案已标为 superseded，不能作为最终设计依据。

最终按用户明确方向改为横贯加载页面顶部的内存条，不再引入卡片、边框容器或窗口。条下同一背景中左侧为加载标题，右侧为 Java 内存及大号已用／上限数字；阶段标签恢复全宽位于下方。条的真实填充比例仍来自 heap used/max，不作为加载百分比。

`tests/MemoryPanelLayout.test.ps1 -RunTag top-strip-first` 已执行：257 项共享生产 Canvas 几何与真实离屏栅格、9 项原加载信息布局、63 项 640 至 4K 的原有场景／动画帧边界通过。1280 宽的条为1280×18像素，1920宽约1920×22像素；标签至少14像素，数字至少20像素。根任务与独立子 Agent 实际逐看启动、存档、普通服务器与原生控件共存四张整页图，检查是否融入背景和三种配色、是否突兀，随后将新图发给用户。22份源码和最终 OrangeStartup.jar 输入哈希无变化，记录在 artifacts/memory-panel-1240/top-strip-first/source.json 与 independent-ui-review.json。没有启动真实游戏或 GL 窗口。

## 服务器卡片、轮播与后台富文本

- 推荐列表使用小卡片和短介绍，不再显示大面积编号封面。1366 宽显示六张完整卡片；有图显示小缩略图，无图不预留大块空位。独立详情使用单图片窗口、左右箭头和计数，下方为完整介绍；单图不留多余箭头，无图不留空窗口。
- 进入列表即查，停留时每5秒更新数据。推荐、个人、未导入收藏和大厅复用只读端点查询，不先导入／修改本地资料。后台同时最多4个端点请求，共享2.5秒 INFO+RULES预算、4秒缓存。取消交接的最后读者竞态已精确同步复现并修复，独立回环 UDP 重复10轮通过。
- 大厅按当前可见卡片查询，滚动后优先新可见项，不为300个端点排整页长队。轮询不重建卡片、不重置轮播图或详情阅读位置，不占全局修改任务。
- 未查询／查询中为黄灯，实际响应为绿灯，信息不完整为黄灯，本轮无响应为红灯并明确原因。失败时保留已读介绍并标注上次读取，人数等本轮未知值不伪造为在线数据。
- 本机和部署服务器分别只读查询用户提供的四个端点，当时均未收到公开 UDP 回复；在线清单与本地个人查询端口均核对为0。仅说明该次查询没有响应，不把它解释为游戏必定离线，也不因查询失败禁止连接。证据 artifacts/server-live-query-1240/。
- 富文本采用可选 `descriptionHtml`，纯文回退字段保留。真实 HTTP 覆盖中文超过64KiB的保存、重读与静态导出；新旧缓存、旧数据库和旧客户端字段合同分别验证。渲染按同一格式函数重建标签／展示样式，后台源码原串不被清洗覆盖。未执行的 HTML 不能借预览或链接替换启动器页面。
- 后台实际登录、格式工具、源码／可视切换、明暗预览、保存与发布到独立导出文件、重新编辑、超长失败后保留草稿并重试均通过。最终公开 JSON 已加载到生产启动器 DOM，真实检查字号、字色、背景、边框和表格样式。后台与启动器格式函数逐字符镜像，4份共享格式 fixture 通过。
- 视觉独立审查实际发现并修复暗色引用块强制背景导致深色文字难读；另修复空白／无可显示 HTML 挡住纯文、不选中文字插入链接失败、Java 提示跨页及配置 ABA 残留。
- 最终 DOM 定向回归：新详情14组、连接选择13组、受影响启动9组通过；相邻服务器刷新、列表稳定和 Java 授权目标回归通过。纯 DOM 的回复与图库图片是明确合成 fixture；真实 UDP／原生文件流程单独记录，不互相冒充。

证据：artifacts/server-compact-detail-1240/final-web-summary.json、artifacts/server-public-query-native/、artifacts/recommendations-browser/final-source.json。后台仅使用本地回环的独立数据和临时账号，未部署线上管理器或改变现有四服信息。
