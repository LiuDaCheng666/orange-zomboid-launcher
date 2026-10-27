# 原生游戏启动的 JRE 依赖搜索修复

日期：2026-10-27。仅生产变更 `LaunchProfiles.BuildStartInfo`。

## 真实故障与定点证据

实际 16.4 GB 快照已完成创建、逐文件校验和运行配置验证，但 `ProjectZomboid64.exe` 在 JVM 初始化时退出 1：`Could not find agent library instrument`，本机中文错误对应 Win32 127（找不到指定的程序）。失败原日志已由 QA 保存在 `artifacts/SaveSnapshotReal-20261027-main-a3/launch-1-native-vm-instrument-error`。

源与冻结副本的 `ProjectZomboid64.exe`、`jre64/bin/java.exe`、`java.dll`、`instrument.dll`、`server/jvm.dll` 和 JRE release 文件 SHA-256 一致。真实 JRE 25 有 java.instrument 模块，问题不是漏拷模块。

原生 EXE 内嵌 `jre64/bin/server/jvm.dll`，随后 instrument.dll 需要同套 java.dll 和 jli.dll。本机继承 PATH 含 Microsoft JDK 21 的 bin；JDK 21 java.dll 引用了 JRE 25 jvm.dll 不存在的 15 个 JVM 导出，同套 JRE 25 java.dll 没有缺失引用。

独立后台进程只调用 Windows LoadLibrary，未进入游戏或 Java main：继承原 PATH 时先成功加载游戏 jvm.dll，再加载 instrument.dll 失败 127；仅在该子进程 PATH 前置游戏 jre64/bin 后成功，四个已加载 DLL 的实际路径均属于同套游戏 JRE。诊断代码、导入/导出比较与结果在 `artifacts/snapshot-instrument-loader`。

## 最小修复与回归

BuildStartInfo 仅在返回的游戏子进程环境中，前置 `Path.GetFullPath(e.Game)/jre64/bin`，原 PATH 按字节保留在后面。来源游戏与非快照独立版本仍调用原生 `ProjectZomboid64.exe`，保留原工作目录、cachedir 参数和独立版本 Steam identity。没有修改机器/用户/父进程环境、游戏 JSON、JRE 或原生 EXE。

`tests/LaunchJvmEnvironment.test.ps1` 先 RED：未修改生产时，source 启动 PATH 缺少游戏 JRE 前缀；再 GREEN 22 assertions，涵盖 source、已登记独立版本、冻结版本的启动参数与环境，以及父/用户/机器 PATH 不变。测试以生产 BuildStartInfo 的环境启动无 UI loader，真实 source 与现有冻结副本四个 DLL 均从各自 JRE 加载，前后六项 EXE/JSON/DLL 哈希一致。证据在 `artifacts/launch-jvm-environment-red` 和 `artifacts/launch-jvm-environment-green`。

限制：PATH 使用分号分隔。本次未对包含分号的游戏根目录增加拦截或路径改写，也未验证其 Windows DLL 搜索行为。DLL loader 不替代真实游戏运行验收。

## 原生 JSON 的中文参数错误与最终入口

r2 解决 DLL 后，真实快照原生启动退出 78。独立只读 premain 探针记录：系统工作目录 `user.dir` 和 `java.home` 保持中文“快照”，JSON vmArgs 传入的 `user.home` 和 `ozl.environment` 却变成“蹇収”，`native.encoding=GBK`；读取实际属性文件得到 NoSuchFileException。当前 JSON 其余 vmArgs、classpath 和 mainClass 均为 ASCII，问题可准确定位到原生 JSON→JNI 的 UTF-8/GBK 参数边界。证据 `artifacts/SaveSnapshotReal-20261027-main-a3/native-argument-properties.json`；probe 额外反射发生在 mandatory agent 加载之前，其 ClassNotFoundException 不是生产失败原因。

最终仅冻结快照改用自己 `jre64/bin/java.exe` 的 Windows Unicode 命令行，`CreateNoWindow=true`。读取已经验证的 JSON，依次保留 root vmArgs、最高适用 Windows 分支 vmArgs、root classpath/mainClass，最后附加原 cachedir。代理顺序、内存、工作目录、Steam identity 不变，不移除隔离，也不改快照文件。该入口同时避免未来自定义非 ASCII VM 参数的同类乱码。

Windows 分支采用真实当前版本；[微软说明未声明兼容版本的应用可能只得到 6.2](https://learn.microsoft.com/en-us/windows/win32/sysinfo/operating-system-version)，本机 Framework 正是 6.2.9200，RtlGetVersion 为 10.0.26300。确定性测试覆盖 6.1、10.0.17134、10.0.26300 的最高适用分支与参数覆盖顺序。

`artifacts/launch-jvm-unicode-red` 先复现旧 frozen native 入口不满足契约；`artifacts/launch-jvm-unicode-frozen` 最终 34 assertions 通过。CommandLineToArgvW 与真实 JRE 无界面探针核实中文、空格、双引号、尾反斜杠、cachedir 和属性文件读取，实际 source/frozen JSON 均被现有 GameProcessGuard 识别为 Client。`artifacts/snapshot-launch-unicode-final` 29 项启动/会话 fixture 通过。真实世界进入、存盘与再次读取另见快照验收记录，不能以这些探针代替。
