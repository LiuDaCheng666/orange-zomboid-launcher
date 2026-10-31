# 工坊页面恢复与强制中文汉化：本地验证记录

## 最终行为

- 工坊列表的原生坐标 setter 使用 float；Lua resize 会回写 float 值。专属页之前用 double 精确比较判定坐标所有权，既可能提前交还原生页，又可能拒绝恢复已下移的列表。现在位置比较容差为 0.01，保持与现有 setter 比较一致。
- Error 仍遵循既有逻辑，完整恢复原生错误页。没有加入新错误页、重试机制，或绕过工坊版本检查。
- 启动按钮旁箭头提供“强制加载统一中文汉化”，默认关闭，单独记忆。身份固定为 B42Trans_CN / 工坊 3556544454；不是 Java 汉化插件。
- 支持 B42 启动器及纯净配置的菜单、读档和联机，以及服务器会话入口。服务器已有相同 ID 时保留服务器顺序及来源，不重复追加；仅在菜单预载后切入服务器时刷新这一个 ID 的来源缓存。设置不重写原默认模组清单。
- Steam 与冻结快照的主菜单、单人路径未接入；界面已明示，不悄悄切换配置、不修改冻结合同。未覆盖 B41。
- 选项读写独立于游戏路径和无关后台任务；保存属于写操作。启动只等待正在进行的设置保存，保存失败则保留可重试状态。

## 已执行

最终 OrangeStartup.jar SHA256：F38F4B95B4212225F1128BFC5804CE9EBA58433709A5380416A61A774880AA4F。

- Build-StartupComponent.ps1 -GameDirectory E:/Steam/steamapps/common/ProjectZomboid：通过。
- tests/Run-NativeCompositionFixture.ps1 -Probe WorkshopGeometryRecoveryTest -Output artifacts/workshop-final-recovery：通过，包含原生 float 回写、继续展示、真正 Error 后恢复位置。隔离 Lua/引擎 fixture。
- tests/ForcedChineseNative.test.ps1：通过；编译生产 C# 源码，实际创建隔离设置文件及菜单/读档/联机会话，恢复后核对原清单字节；包括不可用游戏路径、busy、非法设置载荷及已含汉化的清单。
- 从上述最终启动 JAR 提取内嵌 LiveEnvironmentAgent.jar，再运行 ForcedChineseRuntime.test.ps1：通过，含强制模式、来源/顺序、真实安装游戏字节码签名和既有 10 项服务器检查。
- tests/forced-chinese-agent-probe.py artifacts/forced-chinese-final-live.jar：通过 normal/follow-server/force-local；真实 Java Agent 转换合成引擎类，核对来源缓存、服务器顺序及原文件不变。
- Web/forced-chinese-ui-tests.cjs：通过；无头 Edge 实际 DOM 点击、失败恢复、保存期间点击启动、桌面/窄窗口/520px 布局。此测试的 Native 传输为 mock；真实文件写入由上一项 C# fixture 验证。
- Web/quiet-launch-ui-tests.cjs：3/3 通过。
- Orange3DTool/Build-Modern.ps1：通过，生成本地开发程序，未安装。只有既有 CS0649 字段未赋值警告。
- git diff --check -- Orange3DTool build/ProductionSources.ps1：通过。
- 人工查看 artifacts/forced-chinese-ui/desktop.png、narrow.png、mobile-save.png；阅读/保存/回滚与布局由上述 UI 测试覆盖。

## 用户日志与边界

2026-10-31_21-07_DebugLog.txt：21:08:26.254 工坊 3789836701 下载完成，21:08:26.257 CheckItemState -> Fail；21:09:07.749 再次 Fail。截图在第一次 Fail 之后，回原生页由 Error 路径主动触发，布局未恢复则由可复现的 float 比较问题造成。

安装游戏字节码显示，Installed 且无需更新时仍会检查安装时间戳是否为零及是否等于服务器时间戳；现有日志没有两端时间戳或具体 error 字段，不能据此唯一归因为 VersionMismatch。未修改 Steam 下载、服务器文件或绕过校验，不宣称已解决工坊下载校验失败。

旧 tests/live-agent-probe.py 的“菜单 JVM 转联机时追加 Viewpoint”断言失败。使用 HEAD 原始 Agent 重建对照后在同一断言失败，属于既存预期不一致；没有删除/改写该断言来制造通过。

未启动真实游戏、未进行原生窗口点击/截图、未测试生产包实际进服/存盘/重入。以上均不能代替真实操作验收，依据工程规则，本次未宣布实机验收完成、未覆盖本机安装、未发布。已有线上 1.2.49 不包含本次改动。
