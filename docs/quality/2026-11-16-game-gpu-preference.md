# 游戏显卡选择修复：2026-11-16

> **2026-11-17 反证：本机桌面设置验收未通过。** 下列首次回读及隐藏 OpenGL 结果来自后台执行环境的隔离注册表视图，不能证明真实桌面启动器已设置成功。实际桌面上下文读到两项偏好仍未设置，23:47 的真实游戏仍用 Intel。此前“本机已设置好”及用隐藏上下文证明实际游戏会使用独显的结论撤回。详见 `2026-11-17-desktop-gpu-registry-context.md`。源码回归测试及编译结果仍成立；没有发布。

> **后续真实桌面修复已完成：** 已通过 Windows 图形设置将两个 EXE 设为 RTX 5090，并从原启动器上下文回读为 2、复测 OpenGL 为 NVIDIA。上述反证及首次错误记录仍保留，不覆盖旧证据；实际处理结果、游戏前缀诊断和完整验收边界均见 2026-11-17 记录。

## 目的和结果

用户本机 23:19 启动日志明确为 Intel OpenGL，独显为 RTX 5090 Laptop。启动器实际直接运行游戏 `jre64/bin/java.exe`，当前用户没有给该程序配置高性能图形偏好。

首次操作曾在后台执行环境备份并写入游戏 Java 和 `ProjectZomboid64.exe` 的图形偏好为 `GpuPreference=2;`。此环境现已确认使用隔离注册表，不能当作真实桌面用户的备份或设置。该隔离环境使用同一个游戏 Java 和游戏 JAR 内的 LWJGL，创建不可见 OpenGL 上下文，得到：

```text
VISIBLE=0
VENDOR=NVIDIA Corporation
RENDERER=NVIDIA GeForce RTX 5090 Laptop GPU/PCIe/SSE2
OPENGL=4.6.0 NVIDIA 621.14
```

没有启动游戏世界、Steam、显示新窗口或终止用户进程。

## 产品改动

- `GameGraphicsPreference` 只设置实际游戏程序的 HKCU `Software/Microsoft/DirectX/UserGpuPreferences` 值；保留其他图形字段，删除重复 GPU 字段，已为高性能时保持原值。
- `LaunchProfiles.StartGameProcess` 在真实进程提交前设置实际 EXE，覆盖独立 Java、原生游戏、托管版本、快照及服务器直连。
- 两个 Steam 启动入口在提交 Steam 请求前，设置所选游戏目录的原生 EXE 与 Java EXE。
- 写入失败仅诊断日志，忽略返回的失败并继续正常启动。没有新增页面、确认、任务或拦截。
- 不改全局 GPU/MUX、驱动配置、Steam 参数、游戏文件或模组；实现请求高性能偏好，最终使用设备由 Windows/驱动选择，不能保证任意运行环境都选到独显。

## 验证

证据目录：`E:/Projects/OrangeLauncher/artifacts/game-gpu-preference-20261116`。

1. 红阶段 `red-process/results.log`：原实现启动的隔离子进程在入口读到省电偏好并退出 2，测试实际失败。
2. 绿阶段 `green-final/results.log`：44 断言通过，包括 5 种真实隔离进程传输、中文路径、注册表保留、重复调用、只读注册表失败，以及生产 `LaunchNormal` 在 Steam 回调前写入完成与失败后会话恢复。
3. `jvm-regression/results.log`：原有 JVM 参数/环境回归 18 断言通过。
4. `candidate`：按真实 `OZL_PRODUCTION,OZL_VELOPACK` 源码清单编译生产 EXE，包含新辅助类；SHA-256 `1D1DA2C57E3A0D6FCB7F9BB57E97946C612F19F3F1CF957FAAFF398061624F36`。
5. `production-assembly/results.log`：直接加载上述生产 EXE 的提交函数及图形偏好实现，44 断言再次通过。
6. `opengl/stdout.log`：真实游戏 JRE 与 LWJGL 的不可见 OpenGL 上下文确认 RTX 5090；上下文正常释放，进程正常退出。
7. 首次独立审查 Agent `gpu_preference_review` 回读实现及隔离测试记录，未发现代码待修问题。该次审查不支持主机实际设置生效结论；后续反证审查明确整体未验收。审查发现测试同步读取流使超时失效，已改为双流异步读取，超时只终止该测试拥有的隔离子进程并等待退出。
8. 本次三处跟踪文件的 `git diff --check` 通过。预先存在的网站、部署改动不属于本次，未修整或覆盖。

## 明确边界

- 隐藏 OpenGL 检测证明本机该 Java 程序已分配到 NVIDIA，不是进入实际游戏世界或渲染全部第三方模组的验收。
- 注册表拒写用例验证辅助方法返回失败而不抛出；没有更改用户生产注册表 ACL 来执行完整拒写启动。
- 旧 WinForms Steam 入口仅源码复核；未实际点击 Steam 启动，没有真实 Steam/服务器连接验收。
- 首次记录阶段没有产品 UI 改动，也未替换安装版。后续已通过真实 Windows 图形设置完成并回读桌面偏好，显示初始化确认 RTX 5090；2026-11-17 又完成 1.2.55 的 D 盘本地安装修复，详见次日记录。
- 初始候选为仅编译的 1.2.54.0。后续已构建完整 1.2.55 分发包，并从最终程序集执行 44 项混合回归及 18 项 JVM 回归。Windows 签名仍为 NotSigned，RSA 归档签名有效；本次只安装本机，未上传或远程发布。
- 临时测试程序、class 和 native 提取缓存暂未清理：一次已限定根路径的清理和一次逐项列出具体文件的非递归删除，均被自动审批拒绝，只返回 `blocked by policy`。没有通过其他 API 绕过，保留了这些产物；源文件、日志、设置备份和候选也均保留。

## 后续面向玩家的更新说明

启动游戏时自动优先使用独立显卡，减少误用核显的情况，覆盖普通启动、服务器直连和快照启动。
