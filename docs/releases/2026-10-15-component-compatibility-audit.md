# 2026-10-15 内置组件更新与贴图问题核查

## 结论与范围

核对启动器目录中的 13 个工坊组件。Steam 官方详情接口返回的远端 `hcontent_file` 与本机 `appworkshop_108600.acf` 已安装 manifest 全部一致，未发现“本地清单落后于当前线上清单”。这不是 Steam 全文件校验，也不能证明游戏已重新加载最新文件。

本次只读检查，没有下载、订阅、重新安装、清缓存、修改授权或改动真实游戏配置。启动器 1.2.1 的发布不代表修复了第三方模组的游戏内贴图异常。

## 当前版本和上游变化

更新时间按北京时间（UTC+8）；版本取本机当前工坊包的声明，作者可能没有同步更新所有版本字符串。

| 组件 | 当前声明版本 | 最后工坊更新 | 核对结果／更新要点 |
| --- | --- | --- | --- |
| [ZombieBuddy](https://steamcommunity.com/sharedfiles/filedetails/?id=3619862853) | JAR 2.3.4 | 10-03 | 清单一致；最新变更记录未说明具体内容。已部署的 JAR、DLL 与工坊来源 SHA-256 分别一致。 |
| [Project Viewpoint](https://steamcommunity.com/sharedfiles/filedetails/changelog/3809306528) | 0.1.6 | 10-11 | 清单一致；更新涉及渲染、屋顶、光影和实验性超分／帧生成。不能继续把旧渲染补丁视为天然兼容。 |
| [6244 物件模型](https://steamcommunity.com/sharedfiles/filedetails/changelog/3810302175) | 作者 0.15-alpha；mod.info 1.0 | 10-14 | 清单一致；新增／重做一批社区模型。`common/media` 保存资源，`42.20.4/mod.info` 是版本目录，不表示只支持恰好 42.20.4。 |
| [老余汉化 JAVA 版](https://steamcommunity.com/sharedfiles/filedetails/changelog/3810637496) | 1.16.0 | 10-14 | 清单一致；新增驾驶、True Weathers 等设置汉化，并跟随优化模组更新。 |
| [Shoot to Thrill](https://steamcommunity.com/sharedfiles/filedetails/changelog/3813032735) | mod.info 3.6.49 | 10-14 | 清单一致；优化镜内画面和瞄准性能，修复退镜中心点；完整多人功能要求服务端与客户端版本匹配。 |
| [KK 动态货架](https://steamcommunity.com/sharedfiles/filedetails/changelog/3815931973) | mod.info 0.4.7 | 10-14 | 清单一致；修正杂志架方向及红色边框、简化部分填充物、保留原衣物模型。 |
| [Surface Fix](https://steamcommunity.com/sharedfiles/filedetails/changelog/3814398576) | mod.info 1.0 | 10-14 | 清单一致；修正建筑纹理、重复窗框等；作者把重复网格重建的性能修复标为尚未确认。 |
| [Door Fix](https://steamcommunity.com/sharedfiles/filedetails/changelog/3812151347) | mod.info 1.0 | 10-12 | 清单一致；已声明针对 Viewpoint 0.1.6，修复门帘并兼容 Fitted Sheets。 |
| [Complete Roofs](https://steamcommunity.com/sharedfiles/filedetails/?id=3814178814) | 1.3 | 10-11 | 清单一致；最新变更页没有具体文字说明。仍保留作者关于移除屋顶标记的注意事项。 |
| [Viewpoint Foraging](https://steamcommunity.com/sharedfiles/filedetails/changelog/3814994364) | 0.3.0 | 10-15 | 清单一致；新增 Construction 1P 检测，调整发现物箭头切换；已声明适配 Viewpoint 0.1.6。 |
| [Viewpoint Fishing](https://steamcommunity.com/sharedfiles/filedetails/changelog/3814560827) | 1.0.37 | 10-11 | 清单一致；适配 Viewpoint 0.1.6，抛竿随观察方向；水面与其他光影仍需组合测试。 |
| [True Weathers & Lighting](https://steamcommunity.com/sharedfiles/filedetails/?id=3813908957) | 3.11.1 | 10-14 | 清单一致；最新记录没有详细文字。启动器仍只选择视觉模块，不自动启用 Gameplay。 |
| [KK TREE](https://steamcommunity.com/sharedfiles/filedetails/changelog/3813172420) | 0.3.13 | 10-11 | 清单一致；迁移 Viewpoint 0.1.6 渲染标记，恢复风动、落叶、积雪。 |

## 安装方式核对

当前各组件的 Mod ID、已声明客户端 JAR 相对路径与启动器所识别的结构未发现必须迁移的变化。物件包的资源位于 `common`，不是把它们复制到游戏 `media` 或把 `42.20.4` 改名为 `42.21`。工坊下载完成后，应完全退出并重新启动游戏；JAR 更新可能要求玩家针对新文件重新做授权。

完整射击多人模块不能承诺“服务器什么也不装就拥有全部命中和同步功能”。启动器的本地实验和瞄准回退并不代替作者要求的服务端部署。其它扩展缺少双重预加载声明时，启动器仍不把它们自动列为经过确认的进服保留组件；不能把“启动前本地清单已启用”画成“进服已加载”。

## 水槽、微波炉、书架贴图异常的证据

- 用户报告对象是水槽、微波炉、书架，尚无异常截图或精确 Sprite ID。微波炉、水槽应先沿物件模型包排查；书架还涉及动态货架替换。墙面、门、屋顶 Fix 不是这些对象的首要定位目标。这是排查优先级，不是已确认根因。
- 6244 包共 35,887 个文件，总长 450,786,523 字节。16 个 package 引用 9,233 个唯一 OBJ、8,456 个 MTL 和 8,456 个漫反射贴图；上述引用没有缺失，所查 PNG 文件头有效，同包 Sprite 绑定未发现重复。
- 动态货架包共 4,714 个文件，总长 184,844,234 字节。package 引用 2,352 个唯一 OBJ、2,352 个 MTL，共用 1 张漫反射贴图；上述引用没有缺失，PNG 文件头有效，同包 Sprite 绑定未发现重复。
- 以上不验证全部像素解码、UV 正确性、跨包替换优先级、显存生命周期或着色器效果，也不是把本地字节与 Steam 内容清单逐文件比对。
- 动态货架 mod.info 为 0.4.7，但 Java 常量仍含 0.4.5、Lua 为 0.4.7-preview、README 为 0.4.6。上次常规日志中的 Java／Lua 则分别为 0.4.5／0.4.6。作者包内标记不统一，不能只据这些字符串判定下载或安装失败。
- 可用的常规游戏日志最后写入于 2026-10-14 17:09（北京时间）；它显示模型包注册成功，未见所查 `NoSuchMethod`、`NoSuchField`、`LinkageError` 或 `GL_OUT_OF_MEMORY`，但不是本次异常的新日志。日志和保存配置指向 `trueweathers`，不是早期预设 Eclipse。

## 建议的最小验证，不自动执行

1. 留下同一物件、同一位置的 2D 与 3D 截图，并取得该次正常游戏进程的新日志。
2. 保持 6244 和光影不变，仅停用动态货架，完全退出并重启后对比书架。不要一口气卸载全部扩展。
3. 恢复原状态后，单独关闭 6244 模型包，对比水槽、微波炉是否恢复。恢复说明的是模型渲染路径相关，不自动证明资源损坏。
4. 单独对比 Viewpoint 内置光影与 True Weathers。只有按单变量结果确定范围后才决定重下某个包或修补代码；不先清多人存档缓存。

未完成上述游戏内对比，本次不宣称找到唯一根因或修复成功。原始只读检查结果保存在本机忽略目录 `artifacts/github-release-121/component-audit.json`，不随公开源码上传用户日志。
