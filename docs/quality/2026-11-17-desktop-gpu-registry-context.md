# 桌面显卡设置反证与更正

> 后续用户明确要求发布，1.2.55 已于 2026-11-17 北京时间 08:53 在官网和 GitHub 正式发布并同步网页更新记录；下文“未发布”描述的是更早的本地修复阶段。发布结果见 `2026-11-17-1.2.55-publication.md`。

## 最终处理结果

2026-11-17 用户继续明确要求自行修复后，已通过真实 Windows 设置应用完成两项设置：

- 游戏 `jre64/bin/java.exe`（Zulu Platform x64 Architecture）：高性能 NVIDIA GeForce RTX 5090 Laptop GPU。
- `ProjectZomboid64.exe`：高性能 NVIDIA GeForce RTX 5090 Laptop GPU。

使用 `computer-use` 的 `sky` 接口操作系统图形页；没有用后台注册表写入结果替代桌面配置。两项完成后的设置界面已在本次会话的工具截图中实际查看；系统设置窗口已关闭。没有修改全局显卡、驱动、MUX 或其他程序的偏好。

真实桌面上下文回读：`opengl/existing-launcher-parent-gpu-after-ui.json` 两项均存在，均为 `GpuPreference=2;`。对照备份 `existing-launcher-parent-gpu-before.json` 两项均不存在，前后记录分开保留。

同一原启动器进程 80928 的上下文中，修改前隐藏 OpenGL 为 Intel，修改后 `parent-after-ui-comparison.json` 为 `NVIDIA GeForce RTX 5090 Laptop GPU/PCIe/SSE2`。该对照不是只在后台隔离环境执行。

另在同一真实桌面上下文执行生产启动构造代码，使用独立用户目录和基础 Buddy、实时隔离、加载页组件，以诊断代理保持窗口不可见并在渲染器创建后立即退出。`opengl/desktop-game-prefix-after-ui.json` 和 `E:/Projects/OrangeLauncher/artifacts/gpu17d/buddy/stdout.log` 记录：

```text
PROBE_GAME_WINDOW_SHOW_SUPPRESSED
PROBE_GAME_VENDOR=NVIDIA Corporation
PROBE_GAME_RENDERER=NVIDIA GeForce RTX 5090 Laptop GPU/PCIe/SSE2
PROBE_STOPPED_AT_RENDERER_BEFORE_WORLD
```

此项验证了真实桌面环境下游戏显示初始化已分配到独显，但使用了隐藏/提前退出诊断代理、独立用户目录和较小堆，并非未经修改的完整游戏启动、所有玩家模组或世界内验收。没有进入世界、连接服务器或修改玩家存档，相关诊断 Java 进程均已退出。

启动器自动设置逻辑已进入 1.2.55 完整包并修复安装到本机 `D:/OrangeLauncher`，实际打开主页正常；未远程发布。后续正式发布仍需按工程发布流程处理。

## 用户实际反馈

用户重新启动游戏后截图仍为 Intel。实查实际 Java PID 40048，启动时间为 2026-11-16 23:47:54，父进程为桌面启动器 80928（22:34:11 启动）。实际 `console.txt:90` 为 `GraphicsCard: Intel Intel(R) Graphics`，91 行为 Intel OpenGL；模块为 Intel `igxelpgicd64.dll`。首次设置时间 23:36，故不能用“用户没有重开游戏”解释。

## 真正差异

证据目录 `E:/Projects/OrangeLauncher/artifacts/game-gpu-preference-20261116/opengl`：

- `registry-context-comparison.json`：只读小程序在后台执行上下文下读 HKCU 与 HKEY_USERS/用户 SID，均为 `GpuPreference=2;`；在真实桌面启动器上下文下，两处均是 `missing value`。
- 两个上下文的用户 SID、会话 ID（1）、认证 LUID、提升状态均一致（另行原生 token 采样见本次会话工具输出；对照 JSON 本身仅保存 SID）。已证实运行环境的注册表视图不同，具体隔离实现没有证明，不能通过用户名相同假定系统设置已写入。
- `parent-comparison.json`：同 Java、同隐藏上下文检测、同参数，在后台环境为 NVIDIA，在实际桌面启动器上下文为 Intel。
- `relay-comparison.json`：桌面 Explorer 上下文和由原启动器上下文派生的中转程序也为 Intel；不能将其解释为启动器自身的一项新产品 bug。

创建上下文比较进程只作诊断；没有写入真实桌面偏好或更改用户进程。没有使用进程继承方式绕过注册表隔离实施系统修改。

## 撤回和保留

- 撤回“本机已设置好”“重新启动游戏即可使用”的结论。
- 首次隐藏 OpenGL 的 NVIDIA 结果仅代表后台隔离环境；不能代替真实桌面启动结果。后续同一桌面上下文的 NVIDIA 结果另行记录，且仍限定为隐藏显示初始化诊断。
- 保留启动器在自身运行上下文中、实际启动前写入游戏 EXE 图形偏好的实现。44 项逻辑/隔离进程测试和 18 项 JVM 回归及生产编译仍通过，但不是主机实际设置验收。
- 不修改 NVIDIA 全局配置、不增加父进程重启策略、不设置 Explorer 的显卡偏好来掩盖验证环境错误。
- 原 `local-preferences-before.json` 和 `after.json` 都属于隔离环境，不能作为真实桌面用户的回滚备份。
- 已在真实桌面进程上下文重新取得只读备份 `opengl/existing-launcher-parent-gpu-before.json`：Java 和原版 EXE 的值均不存在。此备份与原隔离环境备份分开保存，没有据此修改实际注册表。

## 当时的阻碍（已解除）

用户原先要求不要占用前台，曾请求其选择是否现在允许短暂打开 Windows 图形设置。用户随后明确要求“自己修好”，已继续完成上述真实系统设置与复测。没有发布本次候选。

## 额外诊断边界

使用独立缓存目录、隐藏窗口且在渲染器创建后立即退出的游戏前缀诊断，表明该诊断配置下 Steam 开关和基础 Buddy/隔离/加载页组件未阻止 NVIDIA 显示初始化；不推广到全部模组与后续阶段。这些诊断添加了停止检查代理，不是生产入口最终验收，也没有连接服务器或进入世界。没有终止用户 PID 40048；其后查询该进程已不存在，不推断用户退出的原因。

## 1.2.55 本地安装修复

- 原 1.2.54 于 07:54 被 Defender 检测 `Trojan:Win32/Prowloc.A!cl` 并隔离，涉及原主程序、桌面快捷方式及卸载记录。首次检测进程为 MiSmartShareHost.exe；后续哈希读取也触发旧 WorkerImages 缓存文件检测。没有恢复隔离、关闭防护或增加排除项，也不将检测自动认定为误报。
- 按现有生产构建脚本生成 `E:/Projects/OrangeLauncher/dist/1.2.55-gpu-local-20261117`，显式允许 NotSigned；版本内容为真实 GPU 功能变更。独立复核 app、nupkg、Portable 一致，bootstrap、发行清单与外层清单的 RSA 签名有效。
- 对最终候选目录执行一次 Defender 自定义扫描，日志明示 `found no threats`，退出 0；仅代表当次扫描结果。防护和实时保护保持开启，未反复变更样本试扫。
- `release-1255-regression/results.log` 为 44 项混合回归：进程提交和 GPU 函数来自最终候选程序集，启动参数构造及 Steam 调用链来自同源测试程序集。`release-1255-jvm/results.log` 为 18 项 JVM 参数回归。均未真实连接服务器或进入游戏世界。
- 原安装残余先移动到明确路径 `D:/OrangeLauncher-repair-backup-20261117`，未执行其中旧程序。通过真实桌面安装向导将新包装回 `D:/OrangeLauncher`，桌面快捷方式及官方卸载记录恢复，安装日志确认成功。
- 实际点击“打开启动器”和关闭更新公告，主页正确显示 42.22.2、8 GiB、7 个现有组件。08:10 检查新主程序 SHA-256 为 `A8C608017F303195EA0D1ADC07047256FC4099BF6E99C9CBF52252C4D419F1CB`，与候选一致；快捷方式指向 `D:/OrangeLauncher/current/橙子启动器.exe`。本机主进程为 15356（08:08:44 创建）。
- `local-install-repair-result.json` 记录安装前顶层设置文件的哈希无变化；未写入游戏或玩家存档。首次尝试较广的用户数据摘要因旧缓存被 Defender 拒绝读取而不完整，不能将 `data-before-repair.json` 当成完整数据清单。
- 08:10 检查安装后的新包/新安装路径没有新增检测；这不承诺未来 Defender 结果。新版未上传自有服务器或 GitHub，无网站改动。
- 源码 VERSION 后续仅将换行规范为 LF，包内仍为构建时 CRLF，版本值均为 1.2.55；没有为该无行为变化重新打包或重复试扫。
