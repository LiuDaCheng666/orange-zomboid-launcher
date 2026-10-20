# 联机光影保留与 Buddy 排序同步

用户已在对话中批准两项修复，并要求后台验证后正式发布 GitHub 更新。复用当前 checkout 的修复分支，不新增工作树、不操作前台，不再重复询问已确认的方案。

## 已核实原因

1. `LaunchProfiles.Retained` 只允许带双重 preload 声明的可选扩展。已启用的 TrueWeathers 视觉模块没有这些声明，因此准备联机默认清单时被排除，原作者的 Java 初始化和光影安装没有执行。
2. 上次隔离原生测试只复制光影选择、没有复制用户的 `viewpoint-shaderpacks`。真实用户已有该目录，故不能把隔离环境缺包完全归因于正式程序；这次补齐目录再验证。
3. Buddy 的 `loadMods` 在 `auto_fix_mod_order` 开启时将 Buddy/zdk/ZModUnbork 前移。原有连接桥只调用一次 `setAutoFixModOrder(false)`；Buddy 的 `ZombieBuddy_Options.lua` 在 `OnMainMenuEnter` 又用 ModOptions 的默认/保存值回写。必须同步该选项，避免重载后重新开启。

## 方案

- 原有清单白名单中明确保留已启用、能解析到本地客户端 JAR 的 `ViewpointTrueWeathers`，让原作者初始化执行。这里只修视觉模块，不加载 `ViewpointTrueWeathersGameplay`，不把其他普通本地模组加入服务器清单。没有启用或基础 3D 未就绪时不自动启用。
- 不伪造 preload 标记，不修改第三方 mod.info/JAR，不强制服务器安装 Java，不加入新的 Java 桥。进服重载仍由原版游戏的服务器清单决定；不强插可选扩展 Lua 到该清单。
- 在服务器连接请求提交前，使用原版 `PZAPI.ModOptions` 找到 Buddy 的 `autoFixModOrder`，将其设为 false；使用原版文件 API 的 append 模式在文件末尾追加关闭行并读回有效值。原版按最后一条合法行决定选项，因此已有有效 false 时不追加。随后调用 Buddy 原生 API 关闭当前运行时排序。绝不截断/重写原文件；原有选项字节保持前缀不变，部分写入/flush 失败仍保留它们。全局 `ModOptions:save()` 会重写所有选项且可能拼接未加载模组的未知行，因此不调用。该普通偏好会持久化；单人启动不主动修改它。玩家自行重新开启后，下次 OZL 发起的联机再次同步关闭。
- 模组选项 API 不可用或保存失败时记录不含凭据的诊断，仍尝试运行时 API，不新增阻止进服的门槛。不得宣称这种降级情况下已保证全部后续重载顺序。
- 不接触 Java 授权、可信作者、订阅、Lua 校验或服务器配置。继续使用既有临时清单恢复机制。

## 验证与发布标准

- 测试先复现 TrueWeathers 被剔除、Buddy 重载回写两项故障，再做最小修复。
- 实际执行 Lua 桥，并加载原版 ModOptions 与 Buddy 选项脚本，模拟全新 Lua 环境从保存文件重载，证明 false 持续有效、其他选项保留，单人和无 Buddy 连接不受阻。
- 原生 Buddy JAR 反序服务器样例（NeatUI 在 Buddy 前）验证自动排序关闭后的原顺序，不修改正式服务器以制造样例。
- 独立缓存/授权副本及非可见桌面启动实际游戏验证光影识别、3D/汉化初始化、服务器模组重载顺序和 Lua 校验。测试不发送前台输入，不以停在 GameLoadingState 冒充角色可操作。
- 正式用户文件哈希保持不变，停止的只可为本次拥有的测试进程。发布 1.2.12 安装版、免安装版、签名更新清单和自有增量文件；验证官方匿名下载及旧版默认更新渠道。
