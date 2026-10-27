# 快照长子路径 I/O 验证

日期：2026-10-27。工作树：`velopack-updates/OrangeLauncher`。

## 真实失败与系统状态

- 系统 `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled` 实测为 `0`；本轮没有修改注册表或系统策略。
- 正式入口已有 `LauncherRuntime.Initialize()` 的两项路径开关，正式 `app.manifest` 已声明 `longPathAware`。单独使用这些设置仍不足以解决此机器的目录创建限制。
- `artifacts/SaveSnapshotReal-20261027-main-a3/formal-entry-path-probe.log` 记录了采用正式 manifest、第一行设置相同开关的独立进程：405 字符目录创建失败。两个开关都为 `False`，没有子 AppDomain 干扰。
- 同目录的 `create-failure.json` 记录真实快照 `SaveEnvironments.Copy` 在 `Directory.InternalCreateDirectory` 失败。QA 定点源目录 147 字符，目标目录 254 字符，位于 KATTAJ1 Military Pack 的服装资源子目录。此长度包含暂存目录名，目录限制不应按最终目标文件名长度推断。

## 实现范围

新增 `SnapshotFileSystem.cs`，仅在快照磁盘操作边界转换为 Windows 扩展路径：本机盘符使用 `\\?\`，UNC 使用 `\\?\UNC\`。目录创建、枚举、属性、文件流、时间戳、移动、原子替换和删除采用对应 Windows Unicode API；文件流以 `CreateFileW` 的 `SafeFileHandle` 构造，避免旧 .NET 路径权限检查拒绝扩展前缀。

`SaveEnvironments.cs` 与 `SaveEnvironmentRuntime.cs` 通过局部类型别名接入。目录身份句柄及快照的逐级重解析点检查也使用扩展路径。枚举结果、相对文件清单、索引、路径比较和 Java 参数保持普通规范绝对路径。

没有修改系统设置、真实游戏内容、现有进程或前台。没有缩短用户选择的目录。保留主任务的 `Stopped(true)` 只读源规则和 `GameSourceExcluded` 排除规则。

## 已实际执行

| 范围 | 结果 | 证据目录 |
| --- | --- | --- |
| .NET Framework 独立程序，正式 manifest，第一行初始化，注册表为 0 | 12 条通过；366 字符目录真实创建、流读写、枚举普通路径、元数据、时间戳、移动、原子替换和目录发布 | `artifacts/snapshot-long-path-native-green` |
| 真实快照创建/取消与运行校验 | 321 条通过；包含长子路径、超过 260 字符的索引根、暂存发布、ForSave/Validate、取消后删除自有部分目录 | `artifacts/snapshot-long-path-runtime-final` |
| 原保存与来源测试 | 122 条通过 | `artifacts/snapshot-long-path-save-tests` |
| 原快照启动路径测试 | 29 条通过 | `artifacts/snapshot-long-path-launch-tests` |
| 真实安装 JRE 的独立副本，无界面探针 | 生产 `SaveEnvironmentRuntime.Probe` 成功；复制的实际 JRE 读取 374 字符子路径的精确字节、实际游戏 JAR，并确认 JVM 工作目录等于所选游戏目录 | `artifacts/snapshot-long-java-child-green` |

复制 JRE 的探针只使用明确归属的随机临时目录，结束后清理。安装来源的 `java.exe` 和 `projectzomboid.jar` SHA-256 前后不变。此探针没有运行游戏入口、Steam 或游戏界面，不能代替完整游戏实际启动验收；完整游戏验收由主任务另行记录。

回归先记录失败：`artifacts/snapshot-long-path-red` 为缺少适配器；`artifacts/runtime-long-path-red` 复现原 `Copy` 的 `PathTooLongException`。修复后执行上表中的对应验证。

## 超过 MAX_PATH 的工作目录边界

另外复制实际 JRE 与游戏 JAR 到独立的更长根，使 `java.exe` 路径达到 289 字符。调用生产 Probe 失败于 `Process.StartWithCreateProcess`，错误为“目录名称无效”。给 FileName 和 WorkingDirectory 都添加扩展前缀后仍失败。证据分别在 `artifacts/snapshot-long-java-final` 和 `artifacts/snapshot-long-java-green`。

微软 [SetCurrentDirectory 文档](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setcurrentdirectory) 明确说明，超过 MAX_PATH 的当前目录会使 CreateProcessW 失败。因此，磁盘 I/O 支持更长路径不能推导为游戏进程支持任意长度的工作目录。

本轮已还原无效的进程前缀尝试，没有修改 `LaunchProfiles.BuildStartInfo`，没有新增根长度拦截，也没有静默移动或缩短用户目录。当前实际证明覆盖正常游戏根下的长子路径；不声称已解决任意超过 MAX_PATH 的游戏工作目录启动。

## 只读来源统计

`artifacts/snapshot-source-path-survey.json` 是真实游戏目录的文件系统元数据普查：119,687 个文件、10,725 个目录、22,473,217,558 字节；其中 `steamapps` 子树 12,098,490,326 字节。最长来源路径 258 字符，没有发现重解析点或读取失败。统计未读取全部游戏内容作摘要，也没有自行修改复制排除策略。
