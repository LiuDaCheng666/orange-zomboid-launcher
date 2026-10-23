# OZL 界面、动效与功能复验

日期：2026-10-23。工作目录：`E:/Projects/OrangeLauncher`。基线：`21c70ae`；启动器界面：`d7ca0c4`；官网：`f7ab8d5`、`cc1a1d1`；诊断测试契约：`156427d`。本轮只调整源码和本地官网，不构建安装包、不部署线上网站、不修改公开更新清单。源码版本仍为 1.2.14，公开稳定版仍为 1.2.13。

## 界面优化

- 启动器统一标题、导航、卡片、边框和状态标签的层级；保留原封面、三项核心组件、附加组件、模组列表＋详情及两套主题。
- 切页使用有限时长的透明度／位移动画。快速切页取消旧动画；同页刷新不重播；减少动态效果或页面进入后台时停止新增装饰动画。
- 保留输入焦点、未保存草稿、列表操作与原生接口，不新增游戏／服务器运行时的配置锁。
- 官网首页、帮助、更新记录统一暖白／橙色排版。五类功能摘要全部保留，帮助改为编号卡片，移动端导航和页脚更紧凑。十张展示图全部换成本轮新版启动器截图，原作者封面保留。
- 官网图片预览和功能切换动画分别为 200／240ms，单次区域入场为 360ms；同目标只有一个受管理动画。关闭、快速重开、切换标签、进入后台或减少动态效果均有取消／清理测试。

## 已完成的原生测试

原生审查仅调用现有测试方法，使用合成游戏目录、配置、Steam 回执和下载传输。没有连接真实服务器、订阅／退订工坊、删除玩家存档、修改服务器配置或启动真实游戏。

五项独立更新测试入口通过，断言合计 **475**：协议 79、部署 13、事务 149、重启策略 38、更新服务 196。另有 **69** 项功能套件／方法调用；初轮 68 项通过、1 项诊断旧文案断言失败，同步测试契约后该项重新通过。连同上述五项入口，最终为 **74/74 调用通过**；诊断复验为 **145 断言、0 失败组**。这不是 74 个产品功能或断言总数，不能与其他套件内部断言相加。

| 功能范围 | 实际测试证据 |
| --- | --- |
| 首页配置、启动目标、启动参数、状态识别 | ConfigurationRuntime／Lifecycle、LaunchTransport、LaunchProfiles、ServerLaunchPolicy、BuddyLaunchArguments、SteamLaunchOptions、GameProcessGuard、ModernHost／Controller |
| 核心／附加组件、依赖、授权、组件恢复 | CoreComponents、ComponentEvidence／Batch、JavaApprovals、LocalAddon、ChinesePatch、ShootClient、ReticleSettings、Addon／Resolver |
| 模组配置、清单与依赖顺序 | ModConfigurations（309 断言）、PlayProfiles（759 断言）、AimBridgeRetirement、BuddyTakeover、ConcurrentSnapshot |
| 工坊、缓存、重下与失败回执 | CatalogCache、LauncherMaintenance、LauncherWorkshopRebuild、WorkshopSubscription、SteamSubscriptionWait／Unavailable、SteamLauncherServices、UpdateTests |
| 存档、删除、继续游戏 | SaveProfiles（74 断言）、SaveDeletion、QuietLaunch128、Lua 存档目标测试；回收操作使用注入的测试回收目录 |
| 服务器、收藏、大厅、服务器工坊清单 | ServerProfiles（60 项）、ServerWorkshop（81 项）、ServerRules、SteamLauncherNativeHall、Lua 工坊观察与连接事件 |
| 画面、光影、内存、硬件信息 | Settings／SettingsChinese、Shader、MemoryPolicy、HardwareProbe、UiUpdate、Portable |
| 日志、任务进度、计时 | JournalPrivacy、PhaseProgress、PlaytimeDay、Status、ModernHost 取消任务、FullLauncherIntegration、GameDiagnostics |
| 安装路径、服务、快捷方式 | InstallerExperience.Paths／Service、InstallerShortcut.Service；只在合成安装／桌面目录操作，不启动安装器窗口、不修改真实桌面 |
| 程序内更新、部署工具、卸载事务 | 五项更新入口 475 断言、Python 签名／目录树／主机指纹测试、合成安装事务与重启策略 |

所有新编译的原生测试入口在执行前对**该测试文件**完成 Defender 按需扫描，扫描和执行的退出码分别记录。测试文件扫描通过不等于产品安装包已获安全审核。本轮未恢复、重打包或执行被隔离的产品文件。

Lua 使用实际生产脚本和替身游戏 API，**7 组通过**。Python 发现 19 项，**11 通过、8 跳过**：7 项要求 Linux 的激活测试，1 项需要 Windows 符号链接权限。未执行上传、SSH 登录、CI 或部署。

## 测试中发现的问题及处理记录

1. 更新测试会创建一个 `--workshop-worker` 子进程，项目 ID 为无效的 `999`。隔离审查入口最初拒绝了这组测试参数，导致输出不是预期 JSON。核对 `WorkshopWorker.Run` 在访问 Steam 前即拒绝该 ID 后，只开放这组精确的合成参数，重新编译并扫描测试入口；`UpdateTests.Run` **9 项通过、退出 0**。其他未知参数仍不允许回退普通界面。
2. `GameDiagnosticsTests.Age` 仍要求报告包含已在 `a3c6b18` 按用户要求删掉的整段推断限制文案。`156427d` 仅修改测试：保留全部年龄／隐私／负例／读取预算测试，改查真实报告的 UTC 检查时间、错误标题、来源、支持行和历史／近期分类是否一致，没有把旧文案加回界面。新入口扫描通过后，145 断言通过、0 失败组；主审再次运行相同隔离入口，结果一致。初次过严要求原始时间前缀的测试尝试也保留日志，最终核对经过已有处理后实际保留的错误证据，不改变生产代码。
3. 较长的测试根目录曾使 ServerWorkshop 的合成写入路径失败。保留失败日志，换用本仓库内的短测试路径并重新扫描入口后，81 项通过。不是据此认定生产目录不存在路径问题。
4. 初期测试包装脚本参数构造有误，可能短暂打开过测试启动器窗口；已立即停止该测试进程，并改成拒绝未知参数的专用入口。没有启动游戏或执行 Steam 操作。不能把整轮测试描述为绝对零窗口激活。

原生测试包含原项目指定的额外合成目录，例如 `E:/AppCaches/OrangeLauncherProfiles/TestFixtures` 和 `artifacts/config-order-127/mod-workspace-fixtures`；并非所有输出都位于本轮新目录中，没有观察到正式玩家配置写入。

## Web 界面与视觉证据

Task 1 实现者全量界面回归 **41/41**，新增动效测试 **6/6**。动效测试直接观察真实 DOM 动画、焦点、输入值和几何尺寸，不使用生产测试接口。独立任务审查的规格与质量均通过。

已生成两主题 × 11 页面 × 3 宽度（1440／1280／768，900 高）的 **66 张**截图和 **6 张**拼图，并补拍 1100×650 的组件及恢复弹窗。自动检查无主容器横向溢出；768×900、1100×650 下遍历全部页面，两主题可操作按钮字体至少 12px、高度至少 30px。另测同页更新保持焦点与草稿、快速导航最多一个入场动画、减少动态效果和隐藏文档取消动画。

主审已查看浅色／深色桌面拼图、深色 1280 与浅色 768 拼图，以及首页、模组、服务器、存档、内存等完整截图。另用现有合成桥接补拍两主题 × 1440／1100 宽的服务器编辑、任务中心、Java 授权共 **12 张**截图；四组几何／状态检查全部通过。1100×650 的服务器编辑、任务和组件恢复弹窗未超出可视窗口；已允许的 Java 项没有重复“允许”按钮。

官网三个页面在 1440／768／390 宽全部截图检查，未见横向溢出和脚本错误。首页 1440 宽时总高 **2191px**（要求不超过 2700）；768／390 宽分别为 2909／2801px。帮助页分别为 1152／1381／1533px，更新记录为 900／977／977px。主审查看了全部九张三页面截图及首页桌面首屏。十张 `website/assets/launcher-*.png` 与 `artifacts/product-refresh-ui/` 中本轮导出的对应原图 SHA-256 **逐一一致**，没有使用旧版截图。

主审在集成版本重新执行：

| 命令 | 新鲜结果 |
| --- | --- |
| `npm test` | 41／41 界面组通过，退出 0 |
| `node Orange3DTool/Web/motion-quality-ui-tests.cjs` | 6 通过，0 失败，退出 0 |
| `node --test tests/website.test.cjs tests/license-guide.test.cjs` | 21 通过（官网 15＋许可说明 6），0 失败，1 线上缓存检查跳过，退出 0 |
| `node artifacts/quality-ui-overlays.cjs` | 四组主题／尺寸组合通过，12 张补充状态截图，退出 0 |
| `artifacts/q26/NativeAuditEntry.exe --audit-suite GameDiagnosticsTests Main` | 145 断言通过，0 失败组，退出 0；运行前已确认该入口扫描日志 |
| `git diff --check` | 退出 0 |
| `git diff --exit-code 21c70ae -- VERSION website/release.json` | 退出 0，版本及下载元数据未变 |

官网回归包含键盘操作、Esc 关闭与焦点恢复、快速关闭后重开、减少动态效果、后台取消、无 JavaScript、所有预览图按需载入与原尺寸放大、下载失败备用地址、QQ群链接／复制成功及拒绝、恶意元数据处理。未设置 `OZL_SITE_URL`，线上缓存头检查明确跳过；没有访问线上部署来冒充新版验证。

本地证据目录（不提交二进制到源码）：

- `artifacts/quality-20261023-native/`：编译、扫描、原生／Lua／Python 日志，保留失败与纠正后的记录。
- `artifacts/q26/`：短路径原生测试入口与合成测试目录。
- `artifacts/motion-baseline/`、`artifacts/motion-final/`：启动器调整前后截图、拼图与补充状态。
- `artifacts/product-refresh-ui/`、`artifacts/website-shots/`：最终启动器导出与官网三页面／主题预览。
- `artifacts/quality-ui-overlays/`、`artifacts/quality-final-*.log`：补充弹窗／授权截图及主审集成回归输出。

## 本轮没有验证的内容

真实 Steam 下载、实际服务器进入和实机游戏内 3D／汉化效果、正式安装／卸载、真实桌面快捷方式、被隔离安装包、线上升级和 Linux 服务器部署未执行。全量 `--self-test` 中会打开窗口的旧 UI 用例不运行，改用上述逐项无界面方法。不能以替身测试通过代替这些实机结果。

当前记录不宣称固定帧率或所有机器性能提升；已证明的是新增动效有界、可取消、不累积、不阻塞控制及已测尺寸无横向溢出。两个界面任务和诊断测试契约调整的独立规格／质量审查均通过，无遗留建议。

## 最终审查

独立整分支审查范围 `21c70ae..e928118`，结论 **Ready to merge: Yes**。没有 Critical／Important／Minor 问题或延期事项，不需要返修波次。审查核对完整差异、动效与旧渲染／封面生命周期的整合、诊断报告契约和原始测试日志，未重复启动游戏／原生窗口。该审查仅支持本轮已验证的源码合并，不代表产品发行或网站部署。

测试驱动证据：启动器新增测试先复现五处缺失行为，最终 6／6；官网先复现隐藏文档仍有动画，以及快速重开后旧 close 事件取消新状态，修整后相关 DOM 测试通过。没有为获得绿灯改回被用户删除的公共文案。

## 合并与源码同步

`main` 快进合并到实现结点 `4f596be`，合并后再次执行 `npm test`，**41／41、退出 0**。该结点已推送到现有 GitHub 仓库 `LiuDaCheng666/orange-zomboid-launcher`，`git ls-remote` 返回的 `main` SHA 与本地一致。后续收尾提交只更新本计划与复验记录。

检查现有 GitHub 工作流仅由 `workflow_dispatch` 手动触发；本次源码推送没有请求安装器构建或发布。官网文件仅同步到源码仓库，没有上传到服务器。公开版本／更新入口仍不切换。

原始测试日志和图片保留。本轮临时审查目录的清理命令被环境策略拒绝，目录继续保留在忽略列表中，没有换用其他方式删除，也没有清理其他工作目录。
