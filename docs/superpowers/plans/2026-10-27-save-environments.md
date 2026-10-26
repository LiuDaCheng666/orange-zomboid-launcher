# 存档运行环境快照与模组操作计划

> **For agentic workers:** Use focused independent tasks with shared API contracts; run only affected checks and review each boundary before integration.

**Goal:** 为单人存档固定游戏版本和启用模组原文件，继续使用原存档；随后改进模组排序、拖动和首次使用引导。

**Architecture:** `SaveEnvironments` 保存复制、校验、历史和绑定；独立必需的 Java 环境代理限制模组来源与默认清单位置；原存档路径保持不变。版本管理集中展示快照，原存档页提供入口。可选加载画面和必需环境隔离相互独立。

**Tech Stack:** .NET Framework 4.8、原有 C# 控制器/文件事务、Java 8 兼容的 Byte Buddy 代理、现有 WebView HTML/CSS/JS、PowerShell/Python 测试。

## 约束与用户要求

- 游戏版本副本优先，复制完整启用模组原文件、顺序和实际 Java 预加载来源；存档备份是独立操作，创建环境快照不复制世界数据。
- 日后 Steam 游戏或工坊更新不能替换绑定快照的来源；检查和接受更新由玩家选择，新快照成功后才切换，旧快照保留。
- “版本管理 / 存档快照”显示存档、游戏版本、模组数量、占用、时间、使用状态；按钮“创建快照”，不用含糊的“保护存档”。
- 不操作用户前台或启动真实游戏/Steam做测试。使用合成文件、隔离类加载和隐藏浏览器/GL验证；不得冒充真实联机测试。
- 不覆盖网站；发布后单独同步更新记录；保留其他任务未提交改动。

## 已完成前置：加载页 1.2.32

- [x] 90条原创梗文字，每轮随机且不重复；连接按钮移除“实验”。
- [x] 根据实际游戏500ms菜单等待和渲染提交时序衔接启动与联机画面。
- [x] Lua重载保持同一操作的动画时间及布局，旧渲染票据仍失效。
- [x] 相关隔离生命周期/实际GL/文案节奏检查；候选包构建与Defender检查。
- [x] 自有服务器和GitHub发布验证、网页说明同步。

## Task 1：快照复制、登记和历史

Files: `SaveEnvironments.cs`、`SaveEnvironmentsTests.cs`、`tests/SaveEnvironments.test.ps1`；`SaveProfiles.SnapshotEntries`仅增加创建时完整来源解析，普通启动仍只检查启用项。

Interfaces: `SaveEnvironments(Engine)`；`Preview(saveId)`返回版本/大小/数量/stamp；`Create(saveId,destination,name,stamp,token,progress,prepareBeforeFinalize)`；`List/Bound/Get/Bind/CheckUpdates`；`FindOwned(data,game,user)`不构造Engine。

- [x] 先运行失败用例：创建后修改和删除原游戏/模组，快照原字节仍存在；原世界文件没有变动或新增备份。
- [x] 完整复制游戏、各唯一模组完整根目录；保留common/版本目录/Java相对路径；拒绝缺失和多来源ID，不静默创建不完整快照。
- [x] 同卷临时目录、分段复制/哈希/取消、来源复核；新目录成功登记后切换绑定，失败保留旧绑定与旧副本。
- [x] 元数据读取不重新哈希全部游戏文件；完整校验用于创建和主动检查。

## Task 2：必需模组隔离运行时

Files: `EnvironmentSource/ozl/environment/*.java`、`Build-EnvironmentComponent.ps1`、新的隔离Java测试；与可选StartupSource分离。

Manifest `environment.properties`: `version=OZLENV1,id,game,user,home,roots,bridge,mods`，路径Base64；JVM参数指定manifest绝对路径及SHA256。`mods`是snapshot/runtime/mods，`home`是snapshot/runtime/home，原cachedir不变。

- [x] 失败用例覆盖manifest变化/缺失、根目录消失、非冻结类来源、native字节码签名变化；必需隔离失败不能回落到工坊。
- [x] `getAllModFolders(List)`仅返回冻结根和本次受管桥接；仅在ZFS默认/loaded清单读写方法替换Core目录调用，保留原存档路径及原解析器。
- [x] 不依赖加载页开关/会话有效期；新代理自有类以Java8字节码构建。
- [x] 对安装的实际B42类验证契约；B41仅明确已知契约，无法证明的版本拒绝创建而非假称兼容。

## Task 3：原存档启动和C#来源隔离

Files: `SaveEnvironmentRuntime.cs`、`SaveEnvironmentController.cs`、`Program.cs`、`SaveProfiles.cs`、`GameDiscovery.cs`、`LaunchProfiles.cs`、`GameStartupPresentation.cs`及显式构建输入。

- [x] 冻结Engine自动关联登记记录，`Mods`指向runtime/mods/default.txt；C#目录/组件/授权只解析冻结根，不读实时Workshop缓存。
- [x] 快照创建回调重写副本启动JSON、固定代理字节、设置snapshot的Java用户目录；映射Buddy预加载与审批到相同哈希的副本。
- [x] 启动准备绑定源Engine、目标存档与快照版本；实际预检和进程启动使用快照Engine，观察器/计时/退出恢复/强制关闭也跟随实际游戏路径。
- [x] 不改全局版本选择，不改世界数据；所选存档清单被玩家主动改动时提供更新快照或使用当前环境的明确选择。
- [x] 运行时自身清单和session可变，游戏/模组载荷固定；未来启动器升级不替换快照固定的代理/组件。

## Task 4：快照页面

Files: `LibraryController/LibraryHost/ModernHostPolicy`、`Web/app.js/app.css`及专属浏览器用例。

- [x] 版本管理增加“游戏版本 / 存档快照”；从单人存档页一键定位，创建前展示实际版本、模组数量、空间与目录选择。
- [x] 创建显示真实阶段/字节进度并可取消；禁用重复点击，成功直接展示当前绑定。
- [x] “检查更新”只比较；“创建更新快照”另建目录，成功后自动绑定；可选历史快照或恢复使用当前环境。
- [x] 实际DOM点击核对创建、取消、失败、更新、切换及窄屏截图，原存档备份入口保持独立。

## Task 5：模组排序、链接和引导（快照完成后）

- [x] 排序保留稳定拓扑策略，补游戏实际`incompatible`元数据；区分缺依赖、同ID多个来源、确切环及受阻下游，失败不改原清单。
- [x] 展示有依据的冲突/排序建议，不凭模组名字推断通用运行时冲突；来源歧义在勾选前说明。
- [x] 仅对真实工坊ID生成固定Steam原页面URL，独立于组件下载白名单。
- [x] 手柄拖动、插入提示、其他行平滑让位、松手一次提交草稿、边缘滚动；过滤、取消、并发变化和减少动态效果都有测试，保留键盘上下移。
- [x] 首次使用引导覆盖各页面，重点说明快照固定游戏和模组版本、原存档继续保存、备份另行操作；帮助中可重新打开。

## 验收与交付

- [x] 分别记录相关C#/Java/浏览器检查和截图，修复观察到的问题后再打包；不每次重跑全套。
- [x] 复核冻结包内真实文件和生产入口；自有服务器优先、GitHub备用，软件发布与网页历史同步分别留下回执。
- [x] 用户前台的真实游戏结果未经验证时如实说明，不用合成测试冒充实际启动成功。
