# 1.2.36 备用 BAT 阻断修复验收

## 原因与范围

`Engine.GuardCore` 在普通组件、内存等操作前要求备用 BAT 解析成功；`BuddyTakeover.Scan` 遇到复杂 BAT 则拒绝整个扫描，造成安装失败后扫描仍无法处理的闭环。普通生产启动实际由 `LaunchProfiles.BuildStartInfo` 调用 `ProjectZomboid64.exe`，快照调用所选游戏自带 Java，不执行这两个备用 BAT。

普通操作不再检查无关 BAT。显式扫描仍清理能够确定的脚本参数；无法读取、解码或改写的备用脚本保留原文件，继续处理有效的 JSON/Steam 目标。保留文件及原因在扫描和完成结果中均可展开查看。实际 JSON/Steam 解析失败仍报告对应问题。

本次没有添加独立 BAT 启动模式；用户提出的备用启动入口仍是后续设计项，不能把本次解除错误拦截称为已实现新的 BAT 兜底。

## 实际执行

- 修改前新增回归分别复现原错误：普通 Guard 抛出“备用启动 BAT 参数不明确”，扫描不提供有效接管计划。
- `UnusedBatWorkflowTests` 使用 TestMode=false、隔离中文目录和真实文件操作。覆盖条件块、跨行与变量（UTF-16）、管道、ANSI 中文注释、超过读取上限、独占文件锁六种情况。分别执行关闭再启用 Buddy、扫描→确认接管→再次扫描，核对真实配置、组件文件、普通模组保留、原 BAT 精确字节以及没有待恢复记录。
- `BuddyTakeoverTests` 相关完整测试通过，包含真实隔离子进程退出观测与本进程注册表视图隔离；未操作用户 Steam。
- `BuddyLaunchArgumentsTests` 相关完整测试通过。保留复杂脚本拒绝自动改写的解析器断言，将普通 EXE 操作的断言改为不受备用脚本影响且保留原字节。
- `buddy-takeover-ui-tests.cjs` 12/12 页面操作组通过，覆盖明暗主题、扫描、确认、取消、接管完成及继续组件安装。此套原生结果为测试数据。
- `tests/UnusedBatNativeUi.cjs` 实际点击生产 app.js：扫描→确认接管→启用 Buddy；三个动作通过生产 ModernHostPolicy/ModernController（TestMode=false）执行真实隔离文件操作。核对清除旧 Java 代理、写入 Buddy 文件和 `-agentlib:zbNative`、保留 OtherMod、BAT 哈希不变、没有待恢复记录。

## 证据与边界

证据位于 `artifacts/unused-bat-regression/`：guard.log、scan.log、takeover.log、arguments.log、native-ui-result.json、native-scan.png、native-takeover-complete.png、native-component-enabled.png、明暗主题扫描与完成截图。已实际查看相关截图。

原生 UI 测试的游戏与组件内容、进程清单、初始状态是隔离测试数据；未启动实际游戏、未下载工坊文件、未停止或重启用户 Steam、未修改用户游戏/存档/前台。生产游戏启动路径未修改。本次没有声称玩家机器上的实际脚本已被远程验证。

快照关闭后偶发按钮失效由用户明确暂缓；其他启动器的加载界面对比暂缓，不能计入本次修复。
