# 1.2.39 本地候选：模组管理与操作状态验收

日期：2026-10-29。没有发布、部署网站或操作用户的 D 盘正式安装。此次按用户要求测试启动器部分，没有启动游戏，也没有用模拟成功代替原生文件结果。

## 交付范围

仅保留模组区三个入口：3D 快捷启用、模组配置、Steam 工坊。依赖 Buddy 的普通 Java 模组在普通清单启用；仅 Buddy 属单独安装的框架，不承诺自动适配任意第三方手工安装方式。五项快捷和普通清单共用实际选择。用户取消的名称翻译未集成，未执行其 ZIP 中的 EXE/脚本。

## 本次确实执行的验证

| 层次 | 操作与结果 | 证据 |
|---|---|---|
| 隔离原生控制器 | readonly 并发、状态读取、设置收据、取消、失败 finally；38 项通过 | artifacts/native-read-concurrency-1239/final-handoff-validated-* |
| 真实文件与生产 JVM | 普通 Java 选择/授权、同哈希来源隔离、真实副本、旧快照、限定 ID、稳定依赖排序；90 项通过 | artifacts/ordinary-java-membership-1239/final-handoff-validated-* |
| 既有原生排序功能 | SaveProfiles/ModConfigurations/排序图；62+312+84 项通过。旧的隐藏 Viewpoint 自动保留断言按新的普通模组合同改为准确断言，未删除测试 | artifacts/ordinary-java-membership-1239/existing-sort-exact-native/ |
| 真实生产 DOM → 原生控制器 | 在新隔离目录中创建配置；先勾选普通 Java/调顺序，再快捷启用模型；实际准备 Buddy/Viewpoint，保存后普通 IDs 同步，再用普通和快捷两条路径停用；读取真实文件并验证原游戏 JSON/BAT、默认清单、存档和来源 JAR 原字节保留 | tests/ComponentConfigurationNative.cjs；artifacts/component-configuration-20261029/native-approved-flow-result.json |
| 独立 UI 状态审查 | 配置 A→B→A、选择失败、提交成功后摘要读取失败、迟到目录/命名配置失败、双栏滚动、过滤拖放、草稿合并、存档授权隔离、重读取消、保存失败重试；11/11 | Web/mod-manager-state-review-tests.cjs；artifacts/mod-manager-state-review-1239/results.json |
| 双栏与依赖 | 1000 条目录、40 条启用，左右搜索、添加移除、实际按钮排序与保存；红黄绿即时变化且不锁保存；桌面每栏至少8完整行；5/5 | artifacts/mod-dual-pane-1239/ |
| 拖放与无障碍 | 真实鼠标/触摸事件、平滑邻行动画、Escape/取消、边缘滚动、隐藏项位置、手动多来源顺序、工坊原页、无效排序不覆盖草稿、暗/亮/窄屏和减少动态效果；12/12 | Web/mod-ordering-ui-tests.cjs |
| 弹窗生命周期 | 实际 HTMLDialogElement.close、迟到旧 close、未完成读取取消、窗口移除、showModal 失败、放弃确认、Escape、重开；7/7 | artifacts/configuration-editor-lifecycle-20261029/ui-results.json |
| 其他相关 UI | 后台读取/轮询5项；Buddy标签3项；同字节不同来源和存档目标授权；五项快捷10项；Steam组合筛选/分页重试/清除 | 对应 Web/*-ui-tests.cjs 与 artifacts 子目录 |
| Steam 查询构造 | 精确 AddRequiredTag、排序枚举0/19/1、宿主校验及公开 preview_url解析；22项。公开元数据实际读取成功 | tests/SteamSearchFilters.test.ps1；artifacts/steam-filters-20261029/ |

UI 测试使用 Headless Edge 加载实际生产 HTML/JS，实际点击控件、指针和原生 dialog close；除“DOM → 原生控制器”行外，WebView I/O 是合成回复，不是 Windows 文件操作验收。

## 独立视觉审查与修正

子 Agent 明确驳回了首轮小列表：1366 宽只看到2~6行，733 宽曾出现依赖名称逐字竖排。改为全高主工作区，去掉大标题和冗余名称行，前置状态复用低优先级 ID 行空间；窄窗移动操作换行、依赖省略并保留完整 title。

最终实际查看 dark/light 的1366×768、1769×1170、733×768六张截图：含常见依赖时左右分别9/8行、18/17行；窄窗不横溢、不逐字竖排；保存按钮常驻。快捷页压缩重复配置栏，把无有效值的版本状态移入详情，模型标题使用不含过期数量的名称。Steam 空查询结果用“工坊模组”，重置按钮为“清除筛选”。

截图：artifacts/mod-manager-state-review-1239/{dark,light}-{1366,1769,733}.png。五项快捷真实封面包含货架、天气、汉化、树木的官方预览；隔离模型使用96px生成图验证本地封面优先级，不能当成其真实作者图片验收。

## 未覆盖与已知边界

- 未执行真实 Steam SDK 的账号查询、真实下载和实际游戏进入；原生查询构造、控件和官方公开图片元数据分别验证。
- 未执行此次实际游戏入口、真实存档写入、用户 Steam 参数接管；不能由 JVM 探针推断这些已验收。
- 一次早期真实副本捕获出现 stale 后未能直接证明当次原因；已修复可确定复现的字典枚举换序误判，随后100次捕获及额外5份隔离副本通过，记录不混称直接根因已证实。
- 原始未包装读取超时可结束前端等待，但其原生生成的 readId 不暴露给 JS，不能逐条取消；这些读取使用独立上下文，不占修改 busy。
- Windows Authenticode 状态仍为 NotSigned；RSA bootstrap 签名不是 Windows 发布者签名，不声称杀毒问题已根治。
- 名称翻译取消后有4张 ignored 测试截图暂留，自动审批以 blocked by policy 拒绝其递归清理；不在产品包中。

## 早期中间产物校验与交付错误

早期校验在 artifacts/mod-manager-state-review-1239/candidate-verification.json：版本、主 EXE SHA-256、21个 bootstrap 文件哈希、RSA 签名、Web源码和包内字节相等。这些检查没有覆盖完整 Velopack 根入口，不能证明该手工 ZIP 可以解压运行。

当时误用的构建中间目录：`E:/Projects/OrangeLauncher/dist/1.2.39-mod-manager-20261029-final/app`，不是完整便携分发目录。

- 已实际指向最终包 `app/Web` 重跑：生产 DOM→真实原生控制器文件流程通过、快捷10/10、独立状态与视觉审查11/11、Steam组合筛选通过。
- 独立复看了压缩后的快捷页：首卡 y397→321，启用按钮 y699→591；空查询组合筛选使用“工坊模组”和“清除筛选”。
- 主程序 SHA-256：`29D52B391C66077A8A27F6F935FAFDF0CD0E96CEC6E903D3FB484EBC210973DF`。
- app.js SHA-256：`128695642156E9C99764502030F2A6565406B7E90148C7C0FB58638DD3D6BEDC`，源文件和最终包一致。
- ZIP 26,124,001 字节、23个文件；逐文件读取与 app 原字节一致，CRC通过，SHA-256：`39888647FCD8372F21363F2810D6EB916AC545823AFDEBFE8B3E900663ABCD57`。
- Windows 发布者签名 `NotSigned`，RSA bootstrap有效；未签Windows证书、不构成正式已签名发行。

### 用户双击无反应的实际原因

Windows Application 事件记录显示该手工 ZIP 的 EXE 在 `PZ3D.VelopackRuntime.Run → DistributionPaths.RunFramework → Program.Main` 抛出“此版本需要完整 Velopack 安装或便携分发包”，尚未创建主窗口。缺失的是 Velopack `.portable`、根执行 stub、`Update.exe` 与 `current/sq.version` 等分发结构。不是通过此次证据确认的 Defender 隔离。

已改用 `Build-VelopackRelease.ps1` 的正式 Portable ZIP，构建器实际执行归档内容、RSA bootstrap、容器元数据及图标检查。后续不再手工把 `Build-VelopackLauncher.ps1` 的 app 中间目录当作便携包。

`1.2.39-mod-manager-20261029-reviewed-r2` 的完整 ZIP 已实际解压，零参数运行根 stub 后真实生产 `current/橙子启动器.exe` 显示 OZL 主窗口；最小化时 WebView 初始化等待显示，恢复后只读 UI Automation 观察到 `https://orange.local/index.html?page=home&theme=#saves`、已找到游戏、本机服务已连接，以及用户正在操作的真实存档页面。没有注入测试代理或启动游戏。用户随后指出的“已启用又要求安装”和存档编辑旧布局仍需修正，因此 r2 不能作为最新完成结果交付。

### 本地详情与独立审查

详情改为原位通栏封面、完整介绍及工坊入口；原生按 token 懒读当前来源，目录仍只留预览。`ModDetailNative` 36项、并发读取38项通过。独立审查实测找出前端65,536字再截断、重读目录后旧全文缓存、重复ID来源展开串联三项，补红绿用例后全部复验转绿；详情DOM5项与相关页面状态11项通过。

最终完整包内 Web 已与真实原生控制器连接测试：实际打开隔离 mod.info，读出多段全文与本地图片，点开/收起，再执行普通选择与快捷状态联动。不是用合成回复证明文件读写。证据 `artifacts/component-configuration-20261029/real-local-mod-detail.png`；图片为标明的96px测试图，仅用于验证本地文件读取和布局。暗/亮/窄窗及长介绍截图在 `artifacts/mod-details-1239/`。这些检查未替代实际游戏进入。

### 用户复看 r2 后仍发现的问题

- 基础卡把“有本配置安装副本”误当作额外用户步骤。旧配置虽然已启用，页面仍给出“安装到此配置”，卸载则不可点。先前测试只证明内部字段一致，没有证明这套操作合理，用户复看否决。修正合同为：一次启用完成所需工作；已启用只显示停用与卸载。旧引用不要求二次安装，卸载解除该配置记录并只清理确实属于此配置的受管内容，不删外部来源。
- 存档“查看模组”仍调用旧长列表，只改了普通配置入口，没有覆盖同类用途。改为存档专用双栏编辑视图，保留原列表的删除、备份、快照入口，保存限定所选存档的 mods.txt。后续验收须包含长列表与真实文件保存，不能用普通配置的通过结果代替存档编辑。

以上两项是实际漏项，r2 的通过记录不能当作它们已验收；最终修正包与新结果在本节之后补记。

## r4 最终本地修正与包内复验

最终包：`E:/Projects/OrangeLauncher/dist/1.2.39-mod-manager-20261029-reviewed-r4/release/OrangeZomboidLauncher-win-Portable.zip`，28,635,970 字节。SHA-256：`41CB9A351D55F132DF6CEAAC78D539B31C5CA49849BD798257EC30767910ABDD`。没有上传服务器、GitHub 或网站。

- Buddy/Viewpoint 一次启用即完成必要复制和选择；已启用只显示停用、卸载。实际验证旧引用直接解除、取消释放句柄、外部来源和其他配置原字节保留，不再要求先二次安装。相关原生回归152项；最终包内页面另与新编译控制器实际操作通过。
- 普通存档编辑统一为全高双栏：左右检索、添加移除、拖动、依赖颜色、自动排序、展开大封面和完整介绍。未绑定存档仅保存其清单。绑定存档打开对应快照修订，新增项目仍复制到快照；原存档文件不随修订改写。
- 独立归属审查确实发现并修复：离开存档页后旧读取弹快照窗口；保存成功后的重读失败被成功文字覆盖；同一缓存存档经 A→B→A 后旧排序清掉新操作状态。另验证关闭快照来源读取、取消另一个存档读取、迟到保存与排序等7组路径。
- 额外真实文件异常测试发现并修正：外部损坏使存档从列表暂时排除后，恢复文件并重读成功仍缺少列表项。现在只回填已成功读取的目标存档，清除其对应旧提示，不重扫其他存档。

最终源码固定后重新编译，152个编译输入哈希无变化。实际执行 `OZL_TEST_WEB_ROOT=<r4>/portable-verified/current/Web` 的三个原生页面流程均退出0：`SaveModEditorNativeUi.cjs` 18项、`SupportConfigurationNative.cjs`、`ComponentConfigurationNative.cjs`。前者包括400个实际模组目录、123个初始已选项、真实保存与文件字节、取消、失败重试、快照修订和当前来源更新后副本介绍独立。测试编译入口为隔离控制器桥接，不是启动实际游戏。详见 `artifacts/save-mod-editor-1239/acceptance-summary.md`。

同一最终页面的相关 DOM 回归：基础卡23/23、存档双栏12/12、独立归属审查7/7、详情5/5、编辑窗口生命周期7/7、普通页面状态11/11、排序12/12。DOM-only 测试的原生传输为合成回复，与前述真实文件操作分开记录。主 Agent 复看最终包产生的存档双栏、基础卡启用后、写入后重读失败截图；测试资料不是玩家资料，缺图占位不冒充真实作者封面。

完整 Velopack 构建退出0，归档/RSA bootstrap/元数据检查通过；解压含 `.portable`、根启动 stub、`Update.exe`、`current`。包内 app.js/app.css 与最终源文件相同；index.html 仅包含构建器预期生成的首页图片数据注入，归一化后完全相同。主 EXE SHA-256：`1435503AAE1DD1C7D8BDE021C4F66CF6DD68934358FB02487EBBF25440AEFE77`，Windows 签名仍 `NotSigned`，未声称已签名或杀毒问题根治。验证摘要：`artifacts/portable-entrypoint-1239/reviewed-r4-verification.json`。

未操作用户正在使用的 r2 窗口，未对正式存档做测试，未启动实际游戏。r3 的旧 `--modern-preview` 合成示例出现配置 fixture 不适配，`--live-read` 截图等待真实状态超时；没有把它们的截图或进程启动当作功能通过。r2 的正常零参数窗口已实际显示；本次 r4 的功能证据是完整包内页面到真实隔离控制器的点击与文件结果，不将它混称为 r4 正常 WinForms 全流程或实际游戏验收。
