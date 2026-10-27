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

## 正式发布

2026-10-27 已发布 1.2.36，源码标签指向 d4225c4549112a54c433745c24ed1b2853a1e2af。发行目录为 `E:\Projects\OrangeLauncher\dist\1.2.36-bat-fix-20261027-final-r2`，主程序 SHA-256 为 `8F4727BF97368C1D266100299AA0CF337843A65660D1690BA5CA2424D4A2B049`。

自有服务器和 GitHub 各十个文件经过匿名下载逐一校验；旧版 1.2.35 的生产更新后端检查到 1.2.36 并真实下载至 Ready，新版检查为 Latest，未执行安装或打断用户进程。网页记录仅通过增量发布器追加本版，桌面与窄屏实际查看通过。软件发布与更新记录发布的其他受保护文件快照均相同，本地六个其他任务的修改哈希保持不变。

Windows 发布者签名仍为 NotSigned，按用户明确免费例外发布；RSA 更新签名保持有效。本次最终发行目录 Defender 自定义扫描未检出威胁，三十五个文件哈希不变；没有更改防护设置，不能据此承诺未来版本永久不会检测。

发布与验证回执保存于 `artifacts/publication-1.2.36/`。GitHub 匿名发布信息接口遇到限额后，使用已登录的 gh API 读取真实正式发行元数据，经 `--published-releases` 传给同一增量发布器完成同步。
