# 橙子启动器 / Orange Zomboid Launcher

面向 Project Zomboid Build 42 的非官方免费辅助启动器。核心用途是管理 Viewpoint 与中文适配；其他扩展由玩家独立选择。

本仓库只公开启动器自己的代码与测试，不包含游戏文件、ZombieBuddy/Viewpoint 本体、第三方光影包或作者模组源码。第三方内容及原作者封面从其发布渠道按需获取，版权和分发条件归原权利人。包含私人预设资源的本机测试整合包不能当作公开发行包。

## 技术结构

- Windows x64、C#/.NET Framework、WinForms + 本地 WebView2 页面。
- `Orange3DTool/` 为启动器源码；`docs/` 为设计、实施与验证记录。
- `Build-Setup.ps1` 与 `Build-Tests.ps1` 为构建入口；缺失依赖从 Microsoft NuGet/作者 GitHub 下载并校验固定版本。需要 Windows x64、.NET Framework 4.8、JDK17+；运行界面需要 WebView2 Runtime。
- 不请求 Steam 密码；使用玩家已登录的 Steam 客户端。Java 文件授权逐哈希确认，不自动信任作者。
- 指定存档/服务器直达功能为实验：不绕过游戏授权、存档转换、服务器规则或角色验证。

## 开发状态

已实现：核心 Viewpoint/中文与可选扩展、富模组目录、命名配置、独立存档清单和依赖排序预览、原版存档/角色截图、原版服务器收藏只读导入、公共大厅、Steam 搜索/订阅列表与确认下载、中文参数、可追踪任务/脱敏日志、独立游玩计时及休息提醒、两套主题及无注册表写入的安装卸载。原版收藏正在写入时拒绝读取/导入。Java 更新需逐文件重新授权；所有“已配置”与“本次已加载”严格区分。

真实只读验证成功获取 Steam 搜索、完整订阅列表和服务器大厅；未在测试中订阅/下载条目，也未执行真实进服/读档。直达存档/服务器和扩展的纯客户端联机效果仍需游戏内验证，不把进程启动冒充进服成功。

## 构建与测试

```powershell
./Build-Tests.ps1
./artifacts/regression.exe --self-test
./Build-Tests.ps1 -Entry PZ3D.SteamLauncherServicesTests -Output ./artifacts/steam-tests.exe
./artifacts/steam-tests.exe
npm install
npx playwright install chromium
npm test
./Build-Setup.ps1
```

默认公开构建不打包第三方光影 ZIP 或作者封面，可自行导入光影。已有合法本地私人测试资源时，`./Build-Setup.ps1 -PrivatePresets` 单独产生私人资源包；不要将它上传到公开 Release。图标和首页背景是本工具自有资源；组件只使用原作者封面/本地 poster，没有自行生成替代图。

输出是可直接运行的便携 EXE，以及带独立卸载程序的安装包。SDK 与编译用 ZombieBuddy 不入 Git；自己的 Java 桥接 JAR 由 `BridgeSource` 编译，不包含作者框架类。自己配置的 Steam 库/游戏目录不会写死在源码中。

## 许可范围

未经单独说明，本仓库暂保留作者权利；公开可见不自动授予第三方资源的再分发许可。后续开源许可只适用于本工具原创代码，不覆盖第三方模组、依赖、光影包或图片。
