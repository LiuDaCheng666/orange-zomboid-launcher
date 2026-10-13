# 1.1.0 本地签名发布验证

状态：最终集成修复及本地验证完成，等待一次限定范围复审。未上传正式渠道、未推送或合并 main；不声明真实游戏兼容性。

最终目录：`E:/Projects/OrangeLauncher/dist/1.1.0-final-verified/`。原有 `dist/1.1.0-round2-verified/` 和更早产物全部保留。上一轮独立full-healthy共享冲突失败仍保留于 `artifacts/root-final-r2-lifecycle.log`、`artifacts/TestFixtures/native-update-533bf880/full-healthy/native-fixture-error.txt`；本轮已修复并复测。

| 产物 | 字节 | SHA-256 |
|---|---:|---|
| orange-launcher-setup-v1.1.0.exe | 5853184 | 7ABD16BA96BD3C84B7CBBF1A5F72D699B4217F66E874C83E94887381D154EFB1 |
| orange-launcher-v1.1.0-win-x64.zip | 4699035 | 2D4CA58C5FCA8C89F1ED7EE77532F659648CEEB594D38B7AD8FCF115826F81D4 |
| update-manifest.json | 7404 | 7B9EA7695D391C8CF9AF91D4148C000A74464A43393C2737E0D4F78EAF8F872E |
| update-manifest.sig | 384 | AE17F1260FC1C5333E59E4DF0F81094928F3D1D7331DC7F7D5AA22FF9C6F131F |

构建命令：

```powershell
./Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.1.0-final-verified
```

使用现有受保护私钥，未覆盖、打印、提交。签名是确切清单字节的RSA SHA-256发布签名，不是EXE Authenticode签名。三个EXE的FileVersion=1.1.0.0，ProductVersion、嵌入AppVersion、磁盘VERSION、清单及实际ZIP首启记录均为1.1.0。没有第三方光影包、原作者模组或封面。

## 验证证据

- 单独原生baseline exit0、437项PASS，日志 `artifacts/final-native-baseline.log`；随后顺序运行八套浏览器测试，full21/21、compact19/19、ui/profile/java-cover/extensions/inherited-layout/core-update全部exit0，日志 `artifacts/final-*-tests.log`。没有与原生基线同时运行浏览器。最终核心UI还用RED/GREEN修正PendingHealth历史不能显示“健康验证已完成”。
- 最终专用套件：协议79、事务149、包17、服务41、助手66、新增回归267项PASS，日志 `artifacts/final-*-verified.log`。新增覆盖真实文件句柄的确定性读/发布共享冲突、并发完整快照及authenticated GetTaskInfo、持续锁定有限失败、重启成功/回退/中断原因、健康状态主动通知、篡改助手/签名拒绝，以及普通恢复入口拒绝存活或未知候选。
- `Test-CoreNativeUpdate.ps1` 五条真实原生/WebView流程全部通过，最终根 `artifacts/TestFixtures/native-update-a45ac625/`，日志 `artifacts/final-lifecycle-verified.log`：sparse健康/失败，full健康/失败/中断后普通启动。健康后再普通启动，实际页面仍显示成功及历史；失败重启页面显示回退原因及历史。最终PendingHealth历史提示的小范围文字修正另经核心浏览器测试验证。
- full中断：独立夹具驱动器在安装记录提交后退出77，再无更新参数启动已安装B。经过认证的旧版副本助手等待该进程正常退出后恢复A并启动一次；实际运行AppVersion、磁盘VERSION、三个EXE字节/版本属性、安装记录一致为1.1.0。此前RED明确失败于“Actual running native AppVersion mismatch”。夹具主入口调用与生产ModernBootstrap相同的恢复交接函数，使用真实ModernForm和助手，仅注入合成controller/transport。
- sparse夹具B清单/记录1.1.1，未变化的VERSION/EXE仍1.1.0，只证明仅app.js下载及生命周期，不是可发布版本。full夹具真正重编译B的三个EXE为1.1.1，下载三个EXE、VERSION、app.js共5文件；成功运行1.1.1，失败/中断恢复完整1.1.0。没有二进制差分。
- `Test-CorePackageBuild.ps1` exit0，日志 `artifacts/final-package-build.log`：真实签名pack/repack、密钥保护/错误密钥拒绝、实际安装器提取与自有卸载、直接ZIP首启、持锁既有安装初始化及延后卸载。
- `Test-CoreLegacyStartup.ps1` exit0，日志 `artifacts/final-legacy-verified.log`：实际默认Build-Modern单EXE原生UI/服务启动，版本属性匹配当前VERSION。截图已查看，显示1.1.0和缺少更新信任的诚实不可用状态；历史未版本化二进制未被修改或追称1.0.0。
- 当前正式包通过 `CoreReleaseTool validate`，清单恰好18个允许文件；正式installer `--verify-setup` exit0。最终ZIP解压于 `artifacts/TestFixtures/signed-final-verified-ui/`；实际正式EXE两主题执行 `--modern-preview <image> --live-read --page help --theme dark|light --size 820x600` 均exit0。随后逐项核对全部18文件长度/哈希、安装记录和三个EXE版本属性。
- 已实际查看 `artifacts/core-update-shots/final-verified-native-dark-820.png`、`final-verified-native-light-820.png`、`artifacts/legacy-start-f2021bf0339344f39ca1a7638697de85.png`。小窗口沿用既有导航，版本与检查按钮可见，更多内容可滚动，无横向溢出。正式截图使用隔离PreviewData，处于尚未检查；没有联网下载。
- 本轮未重新请求官方网络渠道。上一轮最后无凭据latest请求为403限流；没有本轮线上渠道成功证据。正式发布是独立外部动作。

## 完整签名自有文件

| 文件 | 字节 | SHA-256 |
|---|---:|---|
| 橙子启动器.exe | 1146368 | 441912709BB7EEF0D45B0F46F099E4D69F8179B623AB688A8944971E1CA1FAC2 |
| 橙子更新助手.exe | 1146368 | 722995D4A4B189EDC25AD782BA0F5746B78BC596CEDBD380F5E913EFDC8DEC5D |
| 卸载橙子启动器.exe | 1146368 | B2A31455882E880696478DC850072DD37A82F2AEEDFDBA11AA2121B0FC7957CD |
| VERSION | 6 | 1575E1AF4A95F12F70B4EE6A6ADCE8160953D93EA17DC2611B90883CCC3AD3B8 |
| CHANGELOG.md | 2193 | C250F12D0597CB39529071B1DBDA092A216CECDEB20C0D448C9BB6C760FAB5CC |
| 使用说明.txt | 14829 | F9999CF1F209DC0FD9EED458E12DAEF423EBCF892FCD3EC7865FC04CCF661951 |
| release-public-key.xml | 583 | 943D7FC795E52E9C43AD1587CEF15457C1B8153130496C47BED4EB58044C355C |
| Web/index.html | 3317 | 4BC83E794F1BEE715057CC7B761FF165833D7EFBBD63E48616D3CBE263497AD4 |
| Web/app.js | 111396 | 2FE9325F1062883343F8F47BF91E896CAF1DB1B45497AE6C40DC3900BBC7839C |
| Web/app.css | 26683 | D8FDD936D4AC903743BA18F85FE98723A8124A3B38467335B2BA99673AAA2572 |
| Web/home.png | 2694305 | 82FCDE2FDAF188629D9ED9903CEEB21FD9C1C99EA16CCFFB123E9A51FA529D01 |
| Web/orange-app.ico | 130158 | 0795B120DC9BAF1FDAB165FCB6E7E85C532E3D08C11303E3159BD50FD6C12080 |
| Web/WebView2-LICENSE.txt | 1487 | 0AF8F1B807512AAE39C2AC1AA4D0CAE65CABECB6FD554B8439A5162A0D6ECA55 |
| Web/Microsoft.Web.WebView2.Core.dll | 698208 | D60E6E94245078CB56E44EBD5F360DC04BF18E460136BE93BFC4CFCA43B98505 |
| Web/Microsoft.Web.WebView2.WinForms.dll | 38752 | 6A9E6856F23C463783C3AFB0CA5D78F56324C86657162CCB16D5227B3233B32A |
| Web/WebView2Loader.dll | 194912 | 3426DCC55FDFB8B5E7AC623CF33B4CCF283FBF68A0C5A65BDDFDB4D749A8ABEE |
| Assets/OrangeShootPresentation.jar | 17278 | 1EBF1DFC7B867F2B1F200522C4B5569D71A72C5ED54D22652536F8638B9B133F |
| Assets/OrangeLaunchBridge.lua | 2694 | 612CB735695D6918FA0108F219F8BA3B6F036B4A23A44B1839EDECFA6C7F068A |

## 信任与运行限制

普通恢复只执行签名基线允许、完整哈希匹配的任务备份助手副本。原进程PID/启动时间/路径和助手自身路径/哈希匹配后才交接；原进程正常退出后恢复，写入前再次检查候选确实已不在。未知创建结果或存活候选继续阻止恢复，不猜测或终止任意进程。首次未发布的1.1.0包含该入口；不支持入口的历史助手失败关闭。

持久历史来自严格解析、安装/任务归属核验以及基线/目标发布签名匹配的事务记录，不使用自由格式诊断日志作成功凭据。本机结果、原因和时间不是发布者签名的声明。独立更新服务在健康提交后主动通知页面，不需要控制器任务队列空闲；启动不自动查询渠道或下载。

需系统已有WebView2，不安装运行库、不提权、不写注册表。任务、缓存、备份和日志保留，可能持续占用磁盘；同目录重装仍要求新的空目录。传统.NET长路径限制仍在。所有生命周期夹具使用合成游戏/用户目录；游戏、Steam订阅、真实存档、Java授权未被更改或作实际兼容性测试。其他非阻断P3测试可读性/保留策略后续项未扩入本轮。
