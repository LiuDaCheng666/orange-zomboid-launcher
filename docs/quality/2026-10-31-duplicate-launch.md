# 1.2.46 重复启动修复验收记录

## 原因与修复范围

Windows .NET 1026 证据指向 `NativeApplyGuard.InstanceLock..ctor → Program.Main`：已有启动器 UI 持有同安装互斥量时，第二次普通启动等待 10 秒后抛出 `IOException`。该调用位于 `ModernBootstrap.Run` 的异常处理之外，因而退出为未处理异常。恢复窗口时曾短暂捕获暗色画面，重新绑定并读取后已正常显示，且实际点击打开帮助页成功；这不足以判定渲染缺陷。本修复不改渲染或游戏启动逻辑。

仅修改 `NativeApplyGuard.cs`、`Program.cs`，新增 `WindowActivation.cs`，在 Velopack production sources 中显式登记；另新增两个独立回归测试文件。普通启动先零等待尝试取得原安装租约，失败时只查找同一规范化安装路径 `current/橙子启动器.exe`、确切 PID、可见顶层 `OZL` 窗口，并在发出窗口请求前重新核对 PID 与映像路径。仅最小化窗口请求恢复，正常/最大化布局保持原样。

没有可激活 UI 时仍调用原 `Instance(root)`，保留更新交接的 10 秒有界等待；期间释放则正常启动。最终未取得租约时显示明确提示并正常返回 0，不再抛出此未处理互斥异常。更新 worker 继续使用原 `Instance(root)`，锁名、规范路径与别名归一化不变；没有取消互斥、增加全局锁、结束其他进程或修改游戏。

独立审查发现初版丢弃原生 BOOL 返回值。补红绿后，`restore=false` 不请求前台且返回失败，`foreground=false` 同样返回失败；保留有界等待和最终提示，不宣称已切换成功。`ShowWindowAsync` 非零只表示请求已开始，`SetForegroundWindow` 的结果受 Windows 前台规则限制。依据：[ShowWindowAsync](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindowasync)、[SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)。

## 已执行的红绿与相关回归

证据根目录：`E:\Projects\OrangeLauncher\artifacts\duplicate-launch-20261031`。

| 验证 | 实际范围与结果 |
| --- | --- |
| 原因基线与首轮红 | 两个独占进程确认原 `Instance` 约 10 秒拒绝；新增非阻塞 API 不存在时测试按预期失败，随后绿色。 |
| 启动决策红绿 | 缺少重复 UI / 更新交接决策时失败；现有 UI 分支快返、不启动第二 UI、不提示错误，旧进程存活。无 UI 的 worker 在约 250ms 后释放时正常继续；持续占用则约 10 秒后提示并返回 0。 |
| BOOL 拒绝红绿 | `os-refusal-red`：旧源把 restore=false 当成功，断言失败；`os-refusal-green`：最终源 61 条断言全过，包含 restore=false、foreground=false、正常/最大化不恢复、PID/路径/标题/隐藏窗口排除、持锁生命周期、异常释放、遗弃锁恢复。61 是断言数，不是 61 个场景。 |
| 真实隐藏窗口 | 自有子进程创建但不显示 Form；生产 EnumWindows/映像库存取得确切 PID、句柄、路径、OZL 标题和隐藏状态；生产激活函数忽略它，没有调用用户桌面的恢复或前台动作；通过自有 stop 信号正常退出。 |
| 最终原有 Velopack 套件 | `velopack-final/native.log`：23 项原生测试及 6 个真实子进程集成通过。包含 8.3/扩展路径别名下原 10 秒互斥、断开 ARMED 不能应用、真实父进程退出后的 package/helper lease、签名版本检查点并发及进程范围。未改原测试逻辑，只将输出环境定向到独占 TEMP 后清理。 |
| 编译与差异 | Velopack/非 Velopack 条件分支均编译通过，最终候选由根任务构建；本任务的精确文件 `git diff --check` 通过。编译仅有原有 4 个 CS0649 警告。 |

永久回归命令：`tests/DuplicateLaunch.test.ps1 -EvidenceDirectory <独立证据目录>`。根任务已将生产修复纳入 `9ea1d67` 候选构建，本报告不修改版本、发行包或部署。

## 隔离桌面的真实 OS 验证及边界

所有测试 desktop 均使用唯一 `ozl-dup-*` 名称，通过 CreateProcess 的 desktop 参数关联；没有调用 SwitchDesktop。输入 desktop 始终为 `Default`。窗口只在未切换的专用 desktop 内显示，不触碰用户现有窗口、游戏或存档。

1. `desktop-probe/desktop-result.json`：自生成测试公钥/清单的 fixture 编译了实际生产 `Program.Main(empty args)`，通过真实 bootstrap、VelopackRuntime、实例锁与 Win32 窗口调用。原自有最小化窗口 `IsIconic=true → false`，持有进程存活。激活没有获成功结果，进入原有有界等待和真实提示框；自有 watchdog 关闭该不可见 desktop 的提示后，入口返回 0。它证明 fixture 中的生产 Main 路径和实际恢复，不能代替正式安装入口或真实输入桌面的前台许可验收。
2. `desktop-probe/final-pe-desktop-result.json`：从原始最终候选 `dist/1.2.46-duplicate-launch-20261031-9ea1d67/app/橙子启动器.exe` 用 Assembly.LoadFrom 加载，反射执行该 PE 的生产二参数 `Run(root, neverStart)`。实际 gate、窗口请求和 MessageBox 均来自未改写的最终 PE；neverStart 调用 0 次、退出 0、原窗口实际恢复、持有进程存活、输入 desktop 未变。该记录的 scope 明确是 Run/gate/Win32/notice，**不是完整 Program.Main 或安装启动**。最终 PE SHA256：`3F3CCB02015A6E7CCC9F6CAC448B094AD73B711F02898F9B4BCFE5515B4FAB8C`。
3. 曾尝试将自有 actor 的映射 EXE 改名后恢复完整候选副本，以便直接运行候选 normal entry。Windows 的映像身份随改名发生变化，已不满足 exact current 路径前提；探针在启动候选进程前停止并清理。该结果是 harness 限制，不是产品缺陷，没有放宽产品路径规则。后续按根任务要求停止该路径，没有尝试正式首 UI 或用户配置。

两类成功记录均观察到真实恢复后的 `IsIconic=false`；没有把异步投递成功等同于恢复完成。非输入 desktop 的激活未成功，实际走了友好提示并退出的分支；这不证明用户输入桌面允许切到前台，也不承诺 Windows 会对所有前台请求放行。实际交互桌面前台切换、完整最终包首次 UI/安装启动及游戏均未由本任务验收。

## 独立审查与清理

backup_copy_audit 独立复核了最终 BOOL 红绿、PID/路径限制、仅最小化恢复及原 10 秒交接保留，无未解决的可证 must-fix。其复核的 `WindowActivation.cs` SHA256 为 `1198EF4181579CFC87E4334C164A292CFEDD671E1467179D8745E1B929A0FDBB`。

每轮使用唯一且带 owner 标记的 TEMP 根，删除前验证绝对路径位于该根。所有自有 holder 经 stop 信号正常关闭，desktop/进程句柄在 finally 释放；旧套件仅操作其明确归属的隔离子进程。测试 EXE/DLL、fixture RSA 密钥、Web/安装副本与 TEMP 数据已删除。最终检查 `ozl-duplicate-*` TEMP 为 0，证据目录不含 EXE/DLL/ZIP/私钥。保留小日志、JSON、探针源码及报告，正式候选与原发行包保持不变。没有结束用户进程、操作用户桌面、改写用户资料或游戏文件。