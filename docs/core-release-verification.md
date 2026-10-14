# 当前 1.1.1 本机交付验收 2026-10-14

当前交付目录为 `E:/Projects/OrangeLauncher/dist/1.1.1-bat-context-verified/`。多行 BAT 输出上下文及 `<&`／`>&` 句柄合并漏检已修复，限定复审无阻断。协调者独立原生 1279／核心 621／十套浏览器、签名包／实际安装卸载、五条真实更新回退与中断恢复、正式包认证及最终 EXE 十项内存内探针均 exit0；六张正式 EXE 两主题启动截图已查看。

三份 EXE assembly/file1.1.1.0、product／嵌入版本1.1.1；官方公钥保持943D7FC795E52E9C43AD1587CEF15457C1B8153130496C47BED4EB58044C355C，18个工具管理文件／更新资产及20条ZIP正确。正式安装包 SHA256 `367562D3C8D9B5C465E4DF792D57FDB49E10C58066FF1B19D5239B434650C478`，ZIP SHA256 `D8527C489DB9A19EE8FEAC7AB0B07C005E5B6AC025A159FE8C5AE01F913D2040`。完整哈希、命令证据、历史失败及验证边界见 [1.1.1-verification.md](releases/1.1.1-verification.md)。

该结果是本机隔离验收，不代表受影响玩家的真实 Steam 在线下载已经复测。没有真实 Steam／游戏／注册表／分页修改，未查看私钥内容、合并main、推送或线上发布；第三方光影／原作者模组／用户数据未打入包。此前候选和全部1.1.0记录如下保留，不作为当前交付。

# 早先 1.1.1 候选包验证（2026-10-14）

状态：限定复审未通过，剩余 1 项 Important 阻断，不能作为完成版交付。多行 BAT 整块末尾的重定向可将 Buddy 启动命令写入另一个脚本后调用，分类器漏判；当前目录／哈希仅保留候选包机械验证证据，不表示最终安全验收。协调者独立重跑原生 910／核心 621 条 PASS，仍未覆盖该缺失边界。复审证据、裁定及后续状态见 [1.1.1-verification.md](releases/1.1.1-verification.md)。未上传发布。

当前候选资产为 `E:/Projects/OrangeLauncher/dist/1.1.1-final-verified/`；完整18文件哈希和证据见 [1.1.1-verification.md](releases/1.1.1-verification.md)。BAT无害Echo与无加载器链误拒已修复，已覆盖的真实／含糊加载器链、包装与单行重定向／管道仍拒绝，整块重定向漏判尚未修复；锁定源码聚焦210、全量原生910、核心621条PASS，十套浏览器全部exit0。

| 当前资产 | 字节 | SHA256 |
| --- | ---: | --- |
| orange-launcher-setup-v1.1.1.exe | 6152704 | 038DF4A53C3B13EBBC52255991BC71E2EA0DA3E266389D13155D723BE2FF2D37 |
| orange-launcher-v1.1.1-win-x64.zip | 4855146 | FAA4F98182CBA810274F1A8E1EAF37FB262A12357974EB1ECAADD7043EEBE05C |
| update-manifest.json | 8720 | 7CF3F43B3E01CF5ED0EB87992F5CA8F8BCCFFC44AE435F299BB53BC983BECF2C |
| update-manifest.sig | 384 | 4892C17F5C2F6FCEC099C3C474E24F232DD18B49203FD62DA6B5A1DD4A64FF47 |

正式公钥SHA256仍 `943D7FC795E52E9C43AD1587CEF15457C1B8153130496C47BED4EB58044C355C`。三EXE assembly/file1.1.1.0、product／嵌入资源1.1.1，实际默认UI、认证记录和清单一致；18自有文件、20条ZIP及18个更新资产通过签名／长度／哈希，无第三方光影／模组／用户数据。外部私钥仅传路径至现有签名工具。

新的包回归、5/5真实原生1.1.1→1.1.2升级／回退／中断恢复（`artifacts/TestFixtures/1.1.1-final-native-update-locked`）、实际默认／旧界面截图全部通过。正式PS5完整认证脚本exit0，实际ZIP安装18项哈希、installer精确提取及限定卸载保留个人文件／诊断均通过；原进程出生身份、健康授权、单次旧版重启及文件所有权校验未放宽。日志前缀 `artifacts/1.1.1-final-fix-locked-`、`artifacts/1.1.1-final-formal-`；完整报告 `.superpowers/sdd/2026-10-14-steam-takeover-memory/final-fix-report.md`。

`dist/1.1.1-verified-2` 是已被替代的历史验收，最初PS7宿主失败与本波CS0016宿主占用尝试均保留，全部1.1.0证据如下保留。未在线玩家复测、未修改真实Steam／游戏／授权／注册表，未推送、合并main或上传；本波限定复审发现阻断，按修复／复审上限暂停，等待用户方向。任何后续代码／包内输入修改须重新构建唯一目录。

# 1.1.0 本地签名发布验证

状态：最终集成修复、限定范围复审及独立验收通过。未上传正式渠道、未推送或合并 main；不声明真实游戏兼容性。

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

## 独立验收与审查结论

源码实现提交为 `992f0339d2fa7e475c2f77ce995ae894b9afb14f`。协调端在此提交重新编译并顺序执行：协议79、事务149、包17、服务41、助手66、新增回归267项，以及全量原生437项；随后八套浏览器测试均 exit0。日志为 `artifacts/root-accepted-*.log`。另独立执行默认旧构建实际UI启动与 `Test-CorePackageBuild.ps1`，均 exit0。

独立重跑的五条真实生命周期全部通过，根目录 `artifacts/TestFixtures/native-update-e6f56e40/`，日志 `artifacts/root-accepted-lifecycle.log`：包含此前失败的full-healthy、健康后普通重启、失败原因/历史显示、完整EXE中断后普通启动恢复与单次重启、自有卸载保留无关文件。独立运行最终正式installer `--verify-setup` 和完整18文件签名验证，均 exit0；四个发布产物SHA-256与上表一致。最终两主题截图也已实际查看。

整分支审查发现的四项Important及过期请求错误P3均在一次集成修复后通过限定复审；报告 `artifacts/core-final-review.md`、`artifacts/core-final-rereview.md`。没有新的Critical/Important阻断项。源码保留在本地功能分支，main仍为 `8277391c032988090c328f0eabb8fbe33cca944f`。

非阻断后续项：健康确认阶段若手动点击检查更新，可能提前结束本次页面的健康结果观察，使历史暂时仍显示PendingHealth；重新读取状态或重启后恢复显示。持久结果、健康授权和安全回退不受影响。该P3已明确记录为后续修复，不以删除诊断或放宽校验规避。原有协议测试可读性、个别负面测试和运行资料保留策略等非阻断后续项同样保留。

## 信任与运行限制

普通恢复只执行签名基线允许、完整哈希匹配的任务备份助手副本。原进程PID/启动时间/路径和助手自身路径/哈希匹配后才交接；原进程正常退出后恢复，写入前再次检查候选确实已不在。未知创建结果或存活候选继续阻止恢复，不猜测或终止任意进程。首次未发布的1.1.0包含该入口；不支持入口的历史助手失败关闭。

持久历史来自严格解析、安装/任务归属核验以及基线/目标发布签名匹配的事务记录，不使用自由格式诊断日志作成功凭据。本机结果、原因和时间不是发布者签名的声明。独立更新服务在健康提交后主动通知页面，不需要控制器任务队列空闲；启动不自动查询渠道或下载。

需系统已有WebView2，不安装运行库、不提权、不写注册表。任务、缓存、备份和日志保留，可能持续占用磁盘；同目录重装仍要求新的空目录。传统.NET长路径限制仍在。所有生命周期夹具使用合成游戏/用户目录；游戏、Steam订阅、真实存档、Java授权未被更改或作实际兼容性测试。其他非阻断P3测试可读性/保留策略后续项未扩入本轮。

## 历史1.1.1验收追加（已被final-verified替代）

历史资产：`E:/Projects/OrangeLauncher/dist/1.1.1-verified-2/`，已由本文顶部 `final-verified` 替代；原证据及18文件哈希仍保留在 [1.1.1-verification.md](releases/1.1.1-verification.md) 历史段。既有1.1.0证据和目录保留；首轮 `dist/1.1.1-verified` 的 PowerShell7诊断宿主类型兼容失败亦保留。改用WindowsPowerShell5运行相同认证检查后，正式EXE实际启动、1.1.1安装记录和限定卸载均通过，未修改生产信任或跳过认证。

VERSION、三EXE的assembly/file/product/嵌入资源、实际UI、安装记录、签名清单均为1.1.1（assembly/file为1.1.1.0）。公钥SHA256仍 `943D7FC795E52E9C43AD1587CEF15457C1B8153130496C47BED4EB58044C355C`。installer、完整ZIP和18个文件级更新资产通过签名、长度、哈希及自有文件allowlist检查，无第三方光影或原作者模组包。

最终全量原生783 PASS，核心协议79／事务149／包17／服务43／助手66／最终回归267；十套浏览器全部exit0。五条真实更新生命周期通过（稀疏与完整健康／失败、完整中断后的普通启动恢复），原进程身份、单次旧版重启、结果历史、EXE字节及限定卸载断言保留。默认兼容单EXE与正式默认现代EXE均实际启动；两主题内存／接管截图已查看。

尚未问题玩家Steam在线复测或线上Release验收；未修改真实Steam／游戏／授权／注册表，未推送、合并main或上传。最终全分支审查如改变代码或包内资料，本包验收即失效，须在另一个唯一目录重新构建并复验。
