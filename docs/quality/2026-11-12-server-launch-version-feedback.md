# 服务器连接无反馈及完整游戏版本读取修复

日期：2026-11-12。用户本机安装为1.2.53；22:45、22:52的连接在创建游戏进程前报“当前版本不在作者声明的兼容范围内”。

## 更正后的根因

本机实际安装的是最新稳定版42.22.2。先前只查看Core静态初始化里的gameVersion=42.22，就判断用户未更新，是错误结论。本机Core.getGameAndBuildVersion使用自己的StringConcatFactory bootstrap recipe `\u0001.2` 生成完整42.22.2；旧GameVersionProbe漏读这一步。即使本机version.txt仍残留42.21.0，也不能覆盖实际JAR的版本。

模型扩展PZVoxelStudioViewpoint要求versionMin=42.22.2，本机实际满足。旧读取器把游戏当成42.22，进而误拦强制本地组件。另一个独立问题是Web run对带preflight的异常只更新首页状态，服务器页没有明显错误消息。

## 修改

- GameVersionProbe解析明确的getGameAndBuildVersion方法，绑定该方法引用的BootstrapMethods和StringConcatFactory recipe，补齐纯数字构建后缀。不会扫描任意版本字符串，不根据Steam Build ID硬编码版本，不依赖可能过期的version.txt；原构造器读取和旧版兼容保留。
- 服务器页沿用既有通知展示失败并滚动到可见区域；保留owner/token过滤和任务释放。
- 组件游戏/Buddy版本错误包含模组名称、ID、实际版本和作者要求。未删除版本边界、修改模组声明或新增拦截。

## 验证及边界

- 实际本机JAR：旧读取器42.22，新读取器42.22.2。真实本地七个已选组件逐项检查通过；follow-server和force-local均生成成功计划，Buddy保留。未调用游戏启动入口，不能据此声称实际3D画面/进服效果已经验收。
- 新全版本回归先红后绿，15项断言通过；既有GameVersionProbe17项和LauncherGameVersion5项通过。覆盖不同数字后缀、原构造器补丁号、无关方法/字符串、旧版及过期version.txt。
- 失败通知此前先红后绿；原生版本错误诊断9项，以及个人/推荐入口、深浅主题共4条失败/释放/重试的真实DOM点击路径通过，原生WebView回复为合成。旧版错误截图保留为误判历史，不当作当前不兼容结论。
- 旧experience-ui-tests server过滤为3/5：late server query的0!=2和home saved target snapshots旧data-mod-add定位器超时，在未改基线独立重跑同样失败。未删除或修改旧测试，不宣称整套通过。
- 新补丁号解析另行独立审查；证据在E:/Projects/OrangeLauncher/artifacts/game-build-version-20261112，上一轮误判更正见CORRECTION.md。

不占用前台，不启动或退出游戏，不修改玩家配置/游戏/工坊/服务器。修复未发布到1.2.53安装包。
