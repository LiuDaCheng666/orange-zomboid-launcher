# 核心发布与签名

**1.2.3 已正式发布为 GitHub latest。** 源码标签、公开下载哈希、全部门禁与1.2.2在线升级边界见[正式发布记录](releases/1.2.3-github-release.md)。[1.2.2记录](releases/1.2.2-github-release.md)、[1.2.1记录](releases/1.2.1-github-release.md)及以下历史验收说明保留。

正式版本来自根目录 VERSION（当前源码 1.2.3）。1.2.3 公开发布工作采用全新 `dist/1.2.3-github-release/`，正式1.2.2／1.2.1、本地 UI 候选均保持不变；不得用历史包证明新源码已交付。原生安装向导和配置重新读取的历史本机验收见[1.2.1 验证边界](releases/1.2.1-installer-recovery.md)，其冻结目录 `dist/1.2.1-installer-verified/` 保持不变。已验收的1.2.0位于 `dist/1.2.0-experience-verified-final/`，准确门禁和哈希见[历史最终验证记录](releases/1.2.0-final-verification.md)；1.1.1、r4及其本机未发布结论同样作为历史保留。旧单 EXE 为未正式编号构建，不追称1.0.0。构建脚本拒绝覆盖输出目录，fixture不能作为正式发布产物。

```powershell
./Build-CoreRelease.ps1 -VersionOnly
./Build-Tests.ps1 -Entry PZ3D.CorePackageTests -Output ./artifacts/core-package-tests.exe
./artifacts/core-package-tests.exe
./Test-CorePackageBuild.ps1
# 使用已有保护私钥：
./Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.2.3-local-build
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

已验收的 `dist/1.2.0-experience-verified-final/` 包含以下产物：

- `core/`：18个签名核心文件及 runtime 下两份签名引导元数据。
- `orange-launcher-v1.2.0-win-x64.zip`：与 core 相同的可直接解压运行布局。
- `orange-launcher-setup-v1.2.0.exe`：嵌入相同完整包、签名清单；运行前重新认证，安装必须是新的空目录。
- `update-manifest.json`、`update-manifest.sig`：签名的原始 UTF-8 JSON 与二进制分离签名。
- `<完整小写SHA256>.bin`：清单 URL 对应资源；相同字节共享资产名。

独立 JS/CSS/图片与 DLL 按原字节复制；ZIP 项时间戳固定，重复打包相同输入得到相同清单、签名及 ZIP。再次编译 EXE 可能改变编译器时间戳，所以不声称跨编译 EXE 位级复现。依赖许可随 Web/WebView2-LICENSE.txt 提供，第三方说明追加到使用说明.txt。

`Build-Setup.ps1 -CoreReleaseDirectory <发布目录>` 构建正式安装器；其 `-PublicKey` 仅用于明确选择信任公钥（测试用自己的 fixture 公钥）。无参数仍保留旧单 EXE 安装器验证兼容。安装器 `--verify-setup` 在系统临时目录创建隔离安装，核对 v2 版本/全部哈希并验证卸载保留未知个人文件。

1.2.1正常启动安装器使用原生WinForms，不在安装前初始化WebView。默认优先安装包所在的非系统就绪固定盘，否则按空闲空间及稳定盘符次序推荐；没有时明确使用用户应用目录下Programs/OrangeLauncher。推荐与输入只读，既有路径采用有界未占用后缀；完整自定义路径仍经既有根目录/重解析/非空目录检查。服务直接调用原CoreDeployment事务，释放准备按实际解压字节、落盘核验按真实文件数报告，事务本身保持不定进度。安装记录认证和全部自有文件验证成功后才完成，打开按钮再次核对准确的已安装EXE。测试/截图从专用测试入口使用fixture后端；生产入口仅保留原`--verify-setup`，自动验证时由调用者把子进程TEMP/TMP限制在fixture内，不能创建真实推荐目录。

1.2.3交互安装默认勾选当前用户桌面快捷方式，可取消。只在安装认证与全部文件核验后调用，使用系统已知桌面，不写注册表、不覆盖同名文件；创建失败独立告警，不改变安装成功结果。runtime记录installationId、准确路径、link哈希及目标字段；卸载先通过核心预检，之后只删除仍匹配的自有link。相同installationId的正常签名升级不会失去清理资格；玩家修改或外部替换时保留。ZIP、普通在线更新及`--verify-setup`不静默创建真实桌面入口。

## 首次启动及更新集成

ZIP 的未知解压路径不能预写安装记录。包中 `OrangeLauncherRuntime/CoreBootstrap/manifest.json` 和 `manifest.sig` 经嵌入公钥验证后，`CoreDeployment.AdoptPortable(root)` 校验所有现有核心文件，原位创建 installationId、owner marker 与 schema 2 安装记录。失败不会把修改的文件认领为自有文件；安装器原有“空目录”要求不受影响。

正常启动顺序是 `AdoptPortable(root)` → `CoreUpdateTransaction.Recover(root)` → `ModernAssets.Prepare()` → 初始化界面。已有记录时 AdoptPortable 验证当前记录匹配有效签名清单并幂等返回，不预检磁盘 sidecar 字节，所以 Recover 可以先恢复中断更新造成的损坏。该认证从 bootstrap 或现存更新任务目录查找匹配的签名清单，绝不信任磁盘公钥或仅凭未签名记录。完全丢失/破坏签名资料则失败关闭。

1.2.3将两个既有受管监控角色放到已认证EXE哈希标识的`OrangeLauncherRuntime/BackgroundWorkers/<sha256>/`副本运行，保留原计时／会话算法。核心重启仅取消“游戏运行本身”这一项拒绝；未保存编辑、写队列、配置事务和恢复会话仍阻挡，普通游戏文件操作保护不放宽。当前主动下载完整校验后最多弹一次确认，明确同意后原生重检并自动更新／重开；历史Ready或取消不自动重试。旧1.2.1／1.2.2首次迁移仍可能要求退出游戏并手动确认，发布新资源不能改变已运行旧代码。

健康检查启动分支必须在普通恢复分支前处理，不能调用 Recover；由原生宿主和助手校验任务、候选进程 PID/启动时间/路径与 nonce，真实加载 UI/bridge 后确认健康。已有记录的 AdoptPortable 是无写入、无互斥锁的认证读取，因此候选进程可在更新助手持有事务锁期间初始化。`ModernAssets.Prepare()` 内部也执行幂等 AdoptPortable，再通过 `PrepareOwned` 验证全部 Web sidecar。工具桥接读取 `BundledAssets.ReadOwnAsset`；其正式分支读取已认证安装记录并核对 Assets 字节，旧测试/截图构建无嵌入公钥时才回退内嵌资源。

助手在创建候选进程前持久化启动意图，随后持久化 PID/启动时间/路径；候选在身份记录完成前不能初始化健康 UI。所有回滚路径（包括下一次正常启动的 Recover）都先确认已记录候选不存在；仍存活、无法读取身份、启动意图未完成时保留 Conflicted 和现有文件，不结束任意进程。若助手恰在启动与身份落盘之间中断，启动结果未知，自动恢复持续阻塞，需保留诊断并人工核查，不能仅删除 journal 绕过。

启动结果明确时不把它误当作未知：助手自身尚未调用创建、当前固定无 shell/重定向/凭据的创建调用直接返回 Win32 创建错误，或助手持有的原进程句柄已确认退出（包括身份落盘前退出），会持久化 Absent 后允许回滚与旧版本重启。其它异常、未返回句柄且结果不明、查询失败都不能据此宣称不存在；Recover 不推断或写入这份助手确认。

卸载后仅剩运行中的卸载器时，复制已核对哈希的自身至已绑定 installationId 的 runtime 子路径。worker 校验自身路径/哈希、父 PID/启动时间/路径及根目录 ownership，等待父退出，再核对只剩自有卸载器并删除其与记录。绝不执行任意参数指定程序或递归删除根目录。worker 和任务备份仍作为诊断保留。

初次安装中断可能留下 runtime 资料；选择新空目录重试，不自动清空旧目录。Windows 传统 .NET 长路径限制仍适用，请选择较短的独立目录。真实 WebView2 健康确认、失败页面超时回滚、旧页面重启一次及自有卸载已通过隔离测试。测试使用合成游戏/用户/数据根目录，不连接 Steam 或更改实际授权；不能证明游戏内效果。

## 发布前

发布前再次核对 CHANGELOG、版本、完整文件哈希和公钥身份，由维护者明确决定发布。本次用户已授权推送和发布 1.2.3，沿用先上传完整草稿、核对全部资源，再切为正式 latest 的流程；本次22项资源、匿名下载和原始1.2.2默认查询／真实staging均已验证，边界见对应发布记录。未完成线上验证前不得宣称已发布或可用。官方 latest 渠道没有发布时显示不可用，绝不冒充最新版。

更新查询首选 GitHub latest API。仅其 HTTP 403／429 时读取同仓库 `releases/latest/download/update-manifest.json` 与 `.sig`；原有签名、路径、下载域名、取消、超时和响应大小限制保持。旧客户端没有新回退代码时仍可能受匿名额度限制，需等待或手动安装完整新版；不能通过新服务器资源让旧代码凭空获得回退逻辑。

## 1.1.0 历史验证边界（保留）

`Test-CoreNativeUpdate.ps1` 构建临时签名 fixture，运行真实 ModernForm/WebView2、独立原生更新通道及正式助手入口；验证仅 app.js 下载、健康成功、故意坏页面回滚、旧界面重启一次和自有卸载。其 A 的 VERSION、EXE AppVersion、清单和记录均为 1.1.0；B 仅用于文件稀疏性与协议测试，清单/记录为 1.1.1，字节未变的 VERSION/EXE 仍为 1.1.0。此 fixture 不可发布，也不证明真实版本升级仅下载 JS。正式新版本必须同步 VERSION 与所有 EXE 元数据，通常需要下载完整小 EXE；本实现没有二进制差分。

同一脚本还独立构建 full fixture：A 所有版本为1.1.0，B 清单、VERSION、三个 EXE 元数据/嵌入版本和安装记录全部为1.1.1。成功路径验证真实运行 AppVersion=1.1.1，失败路径验证三个 EXE 的原1.1.0完整字节恢复、旧界面只启动一次；两条路径均验证自有卸载保留无关文件。fixture 密钥隔离，未上传任何发布渠道。

当时正式包保持18文件允许清单，三个 EXE/嵌入 VERSION/磁盘 VERSION/发布清单/安装记录均为1.1.0。正式密钥从既有仓库外受保护路径显式传入，未改写或生成新正式密钥。当前和历史实测产物、SHA-256、截图与准确命令见 `docs/core-release-verification.md`。
