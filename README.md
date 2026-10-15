# 橙子启动器 / Orange Zomboid Launcher

面向 Project Zomboid Build 42 的非官方免费辅助启动器。核心用途是管理 Viewpoint 与中文适配；其他扩展由玩家独立选择。

本仓库只公开启动器自己的代码与测试，不包含游戏文件、ZombieBuddy/Viewpoint 本体、第三方光影包或作者模组源码。第三方内容及原作者封面从其发布渠道按需获取，版权和分发条件归原权利人。包含私人预设资源的本机测试整合包不能当作公开发行包。

## 技术结构

- Windows x64、C#/.NET Framework、WinForms + 本地 WebView2 页面。
- `Orange3DTool/` 为启动器源码；`docs/` 为设计、实施与验证记录。
- `Build-CoreRelease.ps1` 为正式核心包入口，`Build-Tests.ps1` 为测试入口；缺失依赖从 Microsoft NuGet/作者 GitHub 下载并校验固定版本。需要 Windows x64、.NET Framework 4.8、JDK17+；运行界面需要 WebView2 Runtime。
- 不请求 Steam 密码；使用玩家已登录的 Steam 客户端。Java 文件授权逐哈希确认，不自动信任作者。
- 指定存档/服务器直达功能为实验：不绕过游戏授权、存档转换、服务器规则或角色验证。

## 下载和在线更新

1.2.1 发布文件位于 [GitHub Releases](https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/tag/v1.2.1)：[安装版](https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v1.2.1/orange-launcher-setup-v1.2.1.exe) 或 [完整免安装 ZIP](https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v1.2.1/orange-launcher-v1.2.1-win-x64.zip)。安装版选择新的独立目录；ZIP 完整解压后运行“橙子启动器.exe”，不要只复制 EXE。

已安装可信核心版本的玩家，可点顶部“启动器更新”→检查→下载，保存未完成编辑、退出游戏后确认重启。更新只下载变化的自有文件并校验签名及哈希；重启后完成界面健康检查，失败保留恢复资料并回退。旧单 EXE 若提示“没有内置更新信任”，需先手动安装一次完整签名版。更新启动器不等于更新或启用第三方模组，不要求退出 Steam，也不要求输入 Steam 密码。网络必须能访问 GitHub 及其官方资源下载域名。

1.2.1 遇 GitHub API 403／429 时使用同仓库官方最新下载地址回退，信任校验不变。旧版本尚无这段回退代码时需等待限流恢复或手动安装完整新版。内置组件的版本、更新说明和物件贴图问题边界见[2026-10-15 组件核查](docs/releases/2026-10-15-component-compatibility-audit.md)；不把“文件已下载”当成“游戏内效果正常”。

此前已经显示 1.2.1 的本地测试版属于同号旧构建，不会被当作更高版本推送；先手动安装本次正式完整包到新的独立目录，之后再使用在线更新。

## 开发状态

当前源码版本为1.2.1（2026-10-15），修复配置选择失败后成功重新读取仍残留阻断的问题（N1），并新增原生安装向导。默认优先安装包所在非系统固定盘，否则按可用空间选择；没有时明确使用用户应用目录下 Programs/OrangeLauncher。可直接编辑完整目录或选择父目录创建独立子目录，打开向导不创建目录，点安装才后台部署。进度来自真实解压字节/核验文件数，事务部署使用不定进度；全部核验成功后才提供显式“打开启动器”。[1.2.1 本机验证记录](docs/releases/1.2.1-final-verification.md)保留初次本地候选的准确边界；公开发布使用独立构建目录，不覆盖历史产物。

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
./Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.2.1-installer-verified
```

正式核心包严格包含允许清单中的18个文件，不打包第三方光影 ZIP 或作者封面，也不自动选择光影。图标和首页背景是本工具自有资源；组件只使用原作者封面/本地 poster，没有自行生成替代图。`Build-Setup.ps1` 无参数以及 `Build-Modern.ps1` 无参数保留旧单 EXE 测试/截图构建；私人资源开关仅适用于这些旧本机测试，不可上传公开 Release。

正式输出包含 `core/`、完整 ZIP、安装 EXE、签名更新清单及内容寻址资源。1.2.1公开发布采用全新 `dist/1.2.1-github-release`；已验收1.2.1本地候选、1.2.0及1.1.1保持原样。[1.2.0历史验证与已知限制](docs/releases/1.2.0-final-verification.md)只记录当时结果，不证明当前源码成品。线上更新读取本仓库最新稳定 Release 的签名清单与内容寻址资源；没有可用渠道或网络失败会明确报错，不伪装最新版。版本元数据变化可能需要替换完整小 EXE，不提供二进制差分。构建、密钥与验证记录见 [核心发布指南](docs/core-release.md) 和 [发布验证记录](docs/core-release-verification.md)。SDK 与编译用 ZombieBuddy 不入 Git；自己的 Java 桥接 JAR 由 `BridgeSource` 编译，不包含作者框架类。

交互方法仅概念参考 [PCL](https://github.com/Meloong-Git/PCL)（[原项目许可](https://github.com/Meloong-Git/PCL/blob/main/LICENCE)），没有复制代码、图标或分发素材。

## 许可范围

未经单独说明，本仓库暂保留作者权利；公开可见不自动授予第三方资源的再分发许可。后续开源许可只适用于本工具原创代码，不覆盖第三方模组、依赖、光影包或图片。
