# OZL / Orange zombiod launcher

**1.2.13 已正式发布为 GitHub 最新稳定版：整理界面文案，删除重复免责声明，现有功能不变。** [正式发布记录](docs/releases/1.2.13-github-release.md)包含源码、安装版、免安装版及正式 1.2.12 程序内更新验证；[1.2.12](docs/releases/1.2.12-github-release.md)及更早记录作为历史保留。

面向 Project Zomboid Build 42 的非官方免费辅助启动器。核心用途是管理 Viewpoint 与中文适配；其他扩展由玩家独立选择。

## 官网与源码

官网已部署为 [IP 验收站](http://101.201.143.163/)，提供产品介绍、使用说明、安装版／免安装版下载、更新日志和备案信息。正式域名为 `orangezl.com`，待解析和 HTTPS 配置完成后启用。旧版程序内更新当前仍走 GitHub。

[项目源码](https://github.com/LiuDaCheng666/orange-zomboid-launcher) · [工作目录说明](docs/workspace.md) · [官网部署与下载镜像](deploy/README.md) · [v1.2.13 安全复核记录](docs/security/2026-10-23-defender-review.md)

本仓库只公开启动器自己的代码与测试，不包含游戏文件、ZombieBuddy/Viewpoint 本体、第三方光影包或作者模组源码。第三方内容及原作者封面从其发布渠道按需获取，版权和分发条件归原权利人。包含私人预设资源的本机测试整合包不能当作公开发行包。

## 技术结构

- Windows x64、C#/.NET Framework、WinForms + 本地 WebView2 页面。
- `Orange3DTool/` 为启动器源码；`docs/` 为设计、实施与验证记录。
- `Build-CoreRelease.ps1` 为正式核心包入口，`Build-Tests.ps1` 为测试入口；缺失依赖从 Microsoft NuGet/作者 GitHub 下载并校验固定版本。需要 Windows x64、.NET Framework 4.8、JDK17+；运行界面需要 WebView2 Runtime。
- 不请求 Steam 密码；使用玩家已登录的 Steam 客户端。Java 文件授权逐哈希确认，不自动信任作者。
- 指定存档/服务器直达功能为实验：不绕过游戏授权、存档转换、服务器规则或角色验证。

## 下载和在线更新

1.2.13 已正式发布为 [GitHub 最新版](https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/tag/v1.2.13)：[安装版](https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v1.2.13/orange-launcher-setup-v1.2.13.exe) 或 [完整免安装 ZIP](https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v1.2.13/orange-launcher-v1.2.13-win-x64.zip)。首次安装选择新的独立目录，默认创建可取消的当前用户桌面快捷方式；ZIP 完整解压后运行“橙子启动器.exe”，不要只复制 EXE。界面和新快捷方式名为 OZL，旧内部文件名保留以兼容更新。已有正式可信核心用户可保留原安装，通过程序内更新升级，无需先卸载组件。

已安装可信核心版本的玩家，可点顶部“启动器更新”→检查→下载。运行1.2.3后，当前下载校验完成会弹出一次确认，同意后自动更新并重新打开启动器；选择稍后则保留“重启并更新”，不会反复弹窗。游戏运行本身不再一律拦截核心更新，但未保存编辑、文件修改、配置事务或待恢复会话仍须先处理。更新只下载变化的自有文件并校验签名及哈希；重启后完成界面健康检查，失败保留恢复资料并回退。更新启动器不等于更新或启用第三方模组，不要求退出 Steam，也不要求输入 Steam 密码。网络必须能访问 GitHub 及其官方资源下载域名。

1.2.1 遇 GitHub API 403／429 时使用同仓库官方最新下载地址回退，信任校验不变。旧版本尚无这段回退代码时需等待限流恢复或手动安装完整新版。内置组件的版本、更新说明和物件贴图问题边界见[2026-10-15 组件核查](docs/releases/2026-10-15-component-compatibility-audit.md)；不把“文件已下载”当成“游戏内效果正常”。

没有内置更新信任的早期单EXE，或缺少官方回退且受API限流影响的历史测试构建，可能需要手动安装一次完整新版。**1.2.1／1.2.2首次升级仍受旧版代码限制，可能需要退出游戏，再手动点“重启并更新”。**历史1.2.8发布验证：原始正式1.2.7通过默认渠道查到1.2.8，实际下载并校验5,393,323字节后达到Ready；新1.2.8返回Latest。均使用隔离目录，未改写原18个文件，不冒充玩家已完成重启。早期版本升级验证作为各版本历史记录保留。 1.2.9发布时，原始正式1.2.8通过默认渠道查到1.2.9并下载至Ready。本次原始正式1.2.9通过默认渠道查到1.2.11，下载并验证5,498,333字节后达到Ready；新1.2.11返回Latest。测试未应用更新或替换真实安装。

## 开发状态

1.2.13 已发布。原始正式 1.2.12 已通过默认更新渠道查到 1.2.13，并实际下载、校验 5,389,662 字节至 Ready；新 1.2.13 返回 Latest。安装器由冻结源码与同一签名核心包在 Windows CI 构建并完成安装／卸载自检，安装版、免安装版及签名更新附件同时交付。

1.2.12 已发布，[正式发布记录](docs/releases/1.2.12-github-release.md)记录冻结源码、交付哈希及原始1.2.11默认在线更新至Ready的验证。联机保留已启用的 TrueWeathers 视觉初始化，Buddy 的运行时排序和保存选项同步关闭，避免重载覆盖；普通本地模组仍不带入服务器。不新增 Java 桥，不改服务器清单或校验。基础3D、汉化、常用工具入口和直达请求修复继续保留，无需将组件全部卸载重装。后台已实际核对本机服务器顺序、Lua校验及地图资源加载，但未进行角色操作和全部视觉效果测试。

1.2.8 检查通过就直接启动，普通提醒不再二次确认；通过记录默认折叠在封面下方，阻断才展开。确认删除本地存档时不再检查进程或直达会话，仍仅回收准确目标并返回实际文件错误，不自动允许Java、不停止进程、不修改远程服务器。

1.2.7 的配置与组件操作不以游戏／服务器／Java运行状态为前提，也不因直达启动会话而禁止编辑。保存失败就地报错并保留草稿；具体依赖与顺序错误可见，仅顺序问题可直接“自动排序并继续启动”。会话清理保留期间编辑，共享写入锁避免相互覆盖，不将临时桥接存入复用配置。仍沿用1.2.6的精确客户端／服务端识别，防止重复启动；删除存档、清理缓存和整个工坊重建是独立的破坏性操作，不属于配置编辑。

1.2.4 使用 OZL / Orange zombiod launcher 名称，[发布说明](docs/releases/1.2.4-release-notes.md)记录紧凑存档／服务器布局、单次恢复反馈、回收站删除和封面轮换；程序、窗口、安装器和新快捷方式沿用同一[原创几何橙子图标](docs/assets/icon-123/README.md)。首页为顶部导航、左侧启动区与右侧大幅封面，右下角显示只读实际状态。原封面保持第一张，三张既有场景每8秒轮换，可暂停固定；候选及来源说明见[封面候选](docs/assets/cover-candidates/README.md)。

此前1.2.1（2026-10-15）修复配置选择失败后成功重新读取仍残留阻断的问题（N1），并新增原生安装向导。默认优先安装包所在非系统固定盘，否则按可用空间选择；没有时明确使用用户应用目录下 Programs/OrangeLauncher。可直接编辑完整目录或选择父目录创建独立子目录，打开向导不创建目录，点安装才后台部署。进度来自真实解压字节/核验文件数，事务部署使用不定进度；全部核验成功后才提供显式“打开启动器”。[1.2.1 本机验证记录](docs/releases/1.2.1-final-verification.md)保留初次本地候选的准确边界；公开发布使用独立构建目录，不覆盖历史产物。

首页选择完整游玩配置与当前实际设置分开；变化先预览、明确应用，再按普通／存档／服务器目标重新体检。便携 JSON 由原生导出和校验，导入仅新建；有界日志诊断保留脱敏原文证据和未核实时间，不自动修复或上传。六个一级分类保留全部原有路由，模组多选只编辑草稿；组件 ID 的批量资格由原生分类，不按作者名字或来源猜测。全局内存／光影／Viewpoint 设置不表示每个配置拥有隔离游戏或工坊版本。

已实现：核心 Viewpoint/中文与可选扩展、富模组目录、命名配置、独立存档清单和依赖排序预览、原版存档/角色截图、原版服务器收藏只读导入、公共大厅、Steam 搜索/订阅列表与确认下载、中文参数、可追踪任务/脱敏日志、独立游玩计时及休息提醒、两套主题及无注册表写入的安装卸载。原版收藏正在写入时拒绝读取/导入。Java 更新需逐文件重新授权；所有“已配置”与“本次已加载”严格区分。

真实只读验证成功获取 Steam 搜索、完整订阅列表和服务器大厅；未在测试中订阅/下载条目，也未执行真实进服/读档。直达存档/服务器和扩展的纯客户端联机效果仍需游戏内验证，不把进程启动冒充进服成功。

## 构建与测试

```powershell
./Build-Tests.ps1
./artifacts/regression.exe --self-test
./Build-Tests.ps1 -Entry PZ3D.SteamLauncherServicesTests -Output ./artifacts/steam-tests.exe
./artifacts/steam-tests.exe
./Build-Tests.ps1 -Entry PZ3D.InstallerExperienceTests -Output ./artifacts/installer-tests.exe
./artifacts/installer-tests.exe
npm install
npx playwright install chromium
npm test
./Build-CoreRelease.ps1 -VersionOnly
./Test-CorePackageBuild.ps1
# 使用明确的仓库外私钥路径和新的输出目录打包：
./Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.2.8-local-build
```

正式核心包严格包含允许清单中的18个文件，不打包第三方光影 ZIP 或作者封面，也不自动选择光影。图标和首页背景是本工具自有资源；组件只使用原作者封面/本地 poster，没有自行生成替代图。`Build-Setup.ps1` 无参数以及 `Build-Modern.ps1` 无参数保留旧单 EXE 测试/截图构建；私人资源开关仅适用于这些旧本机测试，不可上传公开 Release。

正式输出包含 `core/`、完整 ZIP、安装 EXE、签名更新清单及内容寻址资源。1.2.6公开发布采用全新 `dist/1.2.6-github-release`；正式1.2.5／1.2.4／1.2.3／1.2.2／1.2.1、本地候选及更早历史产物保持原样。[1.2.0历史验证与已知限制](docs/releases/1.2.0-final-verification.md)只记录当时结果，不证明当前源码成品。线上更新读取本仓库最新稳定 Release 的签名清单与内容寻址资源；没有可用渠道或网络失败会明确报错，不伪装最新版。版本元数据变化可能需要替换完整小 EXE，不提供二进制差分。构建、密钥与验证记录见 [核心发布指南](docs/core-release.md) 和 [发布验证记录](docs/core-release-verification.md)。SDK 与编译用 ZombieBuddy 不入 Git；自己的 Java 桥接 JAR 由 `BridgeSource` 编译，不包含作者框架类。

交互方法仅概念参考 [PCL](https://github.com/Meloong-Git/PCL)（[原项目许可](https://github.com/Meloong-Git/PCL/blob/main/LICENCE)），没有复制代码、图标或分发素材。

## 许可范围

未经单独说明，本仓库暂保留作者权利；公开可见不自动授予第三方资源的再分发许可。后续开源许可只适用于本工具原创代码，不覆盖第三方模组、依赖、光影包或图片。
