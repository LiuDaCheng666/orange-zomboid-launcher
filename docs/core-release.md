# 核心发布与签名

正式版本来自根目录 VERSION（当前 1.1.0）。旧单 EXE 为未正式编号构建，不追称 1.0.0。已经发布的版本不可重用；构建脚本拒绝覆盖已有输出目录。1.1.0 已完成本地正式签名包、安装器和 ZIP 启动验证；实际 WebView2 更新与回滚使用单独的隔离 fixture 入口，绝不作为正式成品或线上发布。

```powershell
./Build-CoreRelease.ps1 -VersionOnly
./Build-Tests.ps1 -Entry PZ3D.CorePackageTests -Output ./artifacts/core-package-tests.exe
./artifacts/core-package-tests.exe
./Test-CorePackageBuild.ps1
# 使用已有保护私钥：
./Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml
```

私钥位于仓库外，由当前 Windows 用户独占 ACL 保护。不可打印、上传、嵌入二进制或提交私钥。正式公钥为仓库根目录 release-public-key.xml，嵌入所有正式可执行文件的资源名为 `Launcher.UpdatePublicKey.xml`。磁盘中的公钥只供审计，绝不作为运行时信任根。签名私钥与该公钥不匹配时构建失败。

已有密钥绝不覆盖。首次生成命令（本仓库正式密钥已经创建，勿重复运行）：

```powershell
./Build-Tests.ps1 -Entry PZ3D.CoreReleaseTool -Output ./artifacts/core-release-tool.exe
./artifacts/core-release-tool.exe keygen E:/Projects/OrangeLauncher-Signing/release-private.xml ./release-public-key.xml
```

正式公钥规范 XML 的 SHA-256：`943D7FC795E52E9C43AD1587CEF15457C1B8153130496C47BED4EB58044C355C`。RSA 3072 位，SHA-256 签名。测试脚本生成独立 fixture 密钥，只在 artifacts/TestFixtures 内使用，不进入正式 feed。

## 文件与工具接口

`CoreReleaseTool pack <core目录> <新输出目录> <私钥XML> <公钥XML>` 对完整18文件允许清单进行检查，校验三个 EXE 的嵌入版本、公钥及无嵌入 ZIP/SDK 资源，签名未经重编码的清单原始字节。`validate <core目录> <清单目录> <公钥XML>` 重验签名、完整集合、长度及哈希。源码/成品中的意外文件、预设目录与作者封面不能被夹带进包。

`Build-CoreRelease.ps1` 默认编译 `PZ3D.Program`、`PZ3D.CoreUpdaterProgram`、`PZ3D.UninstallerProgram`。缺少更新助手源码立即停止，不生成占位助手。`-CoreDirectory <目录>` 可对预先构建的完整核心文件集进行同样的正式校验；输出仍要求明确签名私钥。`-OutputDirectory` 仅用于选择新的输出目录。`Orange3DTool/Build-Modern.ps1 -Core -SigningKey <路径>` 转交同一入口。

`dist/1.1.0/` 包含：

- `core/`：18个签名核心文件及 runtime 下两份签名引导元数据。
- `orange-launcher-v1.1.0-win-x64.zip`：与 core 相同的可直接解压运行布局。
- `orange-launcher-setup-v1.1.0.exe`：嵌入相同完整包、签名清单；运行前重新认证，安装必须是新的空目录。
- `update-manifest.json`、`update-manifest.sig`：签名的原始 UTF-8 JSON 与二进制分离签名。
- `<完整小写SHA256>.bin`：清单 URL 对应资源；相同字节共享资产名。

独立 JS/CSS/图片与 DLL 按原字节复制；ZIP 项时间戳固定，重复打包相同输入得到相同清单、签名及 ZIP。再次编译 EXE 可能改变编译器时间戳，所以不声称跨编译 EXE 位级复现。依赖许可随 Web/WebView2-LICENSE.txt 提供，第三方说明追加到使用说明.txt。

`Build-Setup.ps1 -CoreReleaseDirectory <发布目录>` 构建正式安装器；其 `-PublicKey` 仅用于明确选择信任公钥（测试用自己的 fixture 公钥）。无参数仍保留旧单 EXE 安装器验证兼容。安装器 `--verify-setup` 在系统临时目录创建隔离安装，核对 v2 版本/全部哈希并验证卸载保留未知个人文件。

## 首次启动及更新集成

ZIP 的未知解压路径不能预写安装记录。包中 `OrangeLauncherRuntime/CoreBootstrap/manifest.json` 和 `manifest.sig` 经嵌入公钥验证后，`CoreDeployment.AdoptPortable(root)` 校验所有现有核心文件，原位创建 installationId、owner marker 与 schema 2 安装记录。失败不会把修改的文件认领为自有文件；安装器原有“空目录”要求不受影响。

正常启动顺序是 `AdoptPortable(root)` → `CoreUpdateTransaction.Recover(root)` → `ModernAssets.Prepare()` → 初始化界面。已有记录时 AdoptPortable 验证当前记录匹配有效签名清单并幂等返回，不预检磁盘 sidecar 字节，所以 Recover 可以先恢复中断更新造成的损坏。该认证从 bootstrap 或现存更新任务目录查找匹配的签名清单，绝不信任磁盘公钥或仅凭未签名记录。完全丢失/破坏签名资料则失败关闭。

健康检查启动分支必须在普通恢复分支前处理，不能调用 Recover；由原生宿主和助手校验任务、候选进程 PID/启动时间/路径与 nonce，真实加载 UI/bridge 后确认健康。已有记录的 AdoptPortable 是无写入、无互斥锁的认证读取，因此候选进程可在更新助手持有事务锁期间初始化。`ModernAssets.Prepare()` 内部也执行幂等 AdoptPortable，再通过 `PrepareOwned` 验证全部 Web sidecar。工具桥接读取 `BundledAssets.ReadOwnAsset`；其正式分支读取已认证安装记录并核对 Assets 字节，旧测试/截图构建无嵌入公钥时才回退内嵌资源。

助手在创建候选进程前持久化启动意图，随后持久化 PID/启动时间/路径；候选在身份记录完成前不能初始化健康 UI。所有回滚路径（包括下一次正常启动的 Recover）都先确认已记录候选不存在；仍存活、无法读取身份、启动意图未完成时保留 Conflicted 和现有文件，不结束任意进程。若助手恰在启动与身份落盘之间中断，启动结果未知，自动恢复持续阻塞，需保留诊断并人工核查，不能仅删除 journal 绕过。

启动结果明确时不把它误当作未知：助手自身尚未调用创建、当前固定无 shell/重定向/凭据的创建调用直接返回 Win32 创建错误，或助手持有的原进程句柄已确认退出（包括身份落盘前退出），会持久化 Absent 后允许回滚与旧版本重启。其它异常、未返回句柄且结果不明、查询失败都不能据此宣称不存在；Recover 不推断或写入这份助手确认。

卸载后仅剩运行中的卸载器时，复制已核对哈希的自身至已绑定 installationId 的 runtime 子路径。worker 校验自身路径/哈希、父 PID/启动时间/路径及根目录 ownership，等待父退出，再核对只剩自有卸载器并删除其与记录。绝不执行任意参数指定程序或递归删除根目录。worker 和任务备份仍作为诊断保留。

初次安装中断可能留下 runtime 资料；选择新空目录重试，不自动清空旧目录。Windows 传统 .NET 长路径限制仍适用，请选择较短的独立目录。真实 WebView2 健康确认、失败页面超时回滚、旧页面重启一次及自有卸载已通过隔离测试。测试使用合成游戏/用户/数据根目录，不连接 Steam 或更改实际授权；不能证明游戏内效果。

## 发布前

发布前再次核对 CHANGELOG、版本、完整文件哈希和公钥身份，由维护者明确决定发布。本次实现没有创建草稿、上传 Release、推送分支或合并 main。官方 latest 渠道没有发布时，显示不可用，绝不冒充最新版。

## 本地验证边界

`Test-CoreNativeUpdate.ps1` 构建临时签名 fixture，运行真实 ModernForm/WebView2、独立原生更新通道及正式助手入口；验证仅 app.js 下载、健康成功、故意坏页面回滚、旧界面重启一次和自有卸载。其 A 的 VERSION、EXE AppVersion、清单和记录均为 1.1.0；B 仅用于文件稀疏性与协议测试，清单/记录为 1.1.1，字节未变的 VERSION/EXE 仍为 1.1.0。此 fixture 不可发布，也不证明真实版本升级仅下载 JS。正式新版本必须同步 VERSION 与所有 EXE 元数据，通常需要下载完整小 EXE；本实现没有二进制差分。

同一脚本还独立构建 full fixture：A 所有版本为1.1.0，B 清单、VERSION、三个 EXE 元数据/嵌入版本和安装记录全部为1.1.1。成功路径验证真实运行 AppVersion=1.1.1，失败路径验证三个 EXE 的原1.1.0完整字节恢复、旧界面只启动一次；两条路径均验证自有卸载保留无关文件。fixture 密钥隔离，未上传任何发布渠道。

正式包保持18文件允许清单，三个 EXE/嵌入 VERSION/磁盘 VERSION/发布清单/安装记录均为1.1.0。正式密钥从既有仓库外受保护路径显式传入，未改写或生成新正式密钥。本地实测产物、SHA-256、截图与准确命令见 `docs/core-release-verification.md`。
