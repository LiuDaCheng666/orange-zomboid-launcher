# 1.2.33 存档环境快照、模组排序与引导验证

## 实现边界

- 复制完整游戏和启用/启动所需模组根，保留版本目录、common及Java库；原世界不复制、不恢复、不改写。绑定原单人存档，启动时保持原cachedir。
- 必需Java8环境代理独立于可选加载页，只允许登记模组根和本次启动桥。默认清单重定向至快照runtime；Buddy preload固定重映射、相同JAR SHA的授权保留，其他来源不回退工坊。
- 创建前/复制后使用隔离的API字节码探针；复制校验及原来源复核通过后原子登记并切换，取消/失败保留旧绑定。不可用的API明确拒绝创建。
- 正常轮询只读轻量索引和一次命名进程枚举，历史manifest不进入进程轮询热路径。预检、启动、观察、计时、退出恢复、显式修复和强制关闭跟随实际快照目录，主版本选择不变。
- 继续沿用21个bootstrap认证路径，必需环境JAR置于现有OrangeStartup.jar认证容器内；不增加旧客户端不认识的发行路径。

## 定向验证

- Storage79、Runtime224、Controller68、真实原生控制器启动接线29、JavaComponents142、Guide55项断言通过。原生接线使用既有Startup容器提取代理，删除原游戏/Workshop后可预检和Stage，原world/default/loaded字节保持；跨控制器残留会话显式修复、轻量索引、单次PID枚举有回归。
- 必需Java代理的清单/负例/隔离发现/桥接/默认及loaded读写、实际安装B42 ZFS变换、Java8类文件与包边界测试通过。实际B41运行未验证；以API探针结果决定是否可创建。
- 模组排序62新断言，加原ModConfigurations312和SaveProfiles84通过。检查incompatible/conflicts、稳定排序、真实SCC环成员与受阻下游，错误保持原清单；工坊链接不扩大下载白名单。
- Snapshot UI11/11、拖动排序12/12、引导6/6、既有library14/14及相关成员/启动按钮测试通过。实际指针/触控、邻行WAAPI动画、松手一次修改、Esc/过滤/路由/异步变化取消、隐藏固定ID保持、重复来源不可拖动、减少动态效果都有检查。
- 修复独立审查复现的两个快照界面时序问题：晚取消后的实际成功绑定需刷新，旧列表回复不得覆盖新绑定。修复多人只读缓存启动按钮被全局disable覆盖。
- 深浅色桌面和窄屏截图目视检查：artifacts/save-environment-ui、artifacts/mod-ordering-ui、artifacts/guide/screenshots。
- 受影响的原启动组件所有权、启动演示生命周期、LaunchProfiles/Transport、预检证据和重复启动/强制停止恢复检查通过。较长的原测试目录会触发旧fixture的Windows路径长度限制，改在E:\Projects\OrangeLauncher\artifacts\aff-51333e1e运行通过，不改产品来迁就测试。

所有检查均后台执行，没有打开真实前台游戏或Steam，没有把合成Stage/字节码测试声称为真实游戏进档验证。

## 发布校验

源提交7a8314f1f581e09ac92d9429e7d396a8cbca0702，GitHub公开tag v1.2.33，自有服务器同步。正式目录E:\Projects\OrangeLauncher\dist\1.2.33-snapshots-ordering-20261027-final。10项发行文件、安装/便携归档和图标验证通过；Defender扫描未发现威胁。

**后续运行事件补充：**上述结果只代表发布前那一次扫描。2026-10-27 04:19 用户更新至本版后，Defender 实时保护以 `Trojan:Win32/Prowloc.A!cl` 隔离实际主程序和桌面快捷方式，启动入口随后无法找到主程序。本版目前不能据此前扫描称为可正常运行；准确事件、样本身份及微软复核进展见[运行时检测记录](../security/2026-10-27-v133-runtime-detection.md)。

实际生产认证容器提取必需代理通过（4666292字节），保持21项bootstrap。旧1.2.32实际编译客户端在GitHub仍为草稿时从自有服务器下载并校验至Ready1.2.33；新1.2.33客户端为Latest。两来源20项匿名下载长度/SHA及当前feed/display元数据通过。

网页仅同步changes.html，当前1.2.33，19项历史记录；1440/390px实际渲染无溢出。未切换网站current、未覆盖其它页面/样式/脚本/图片，未修改防护或SSH配置；软件发布843项、更新记录发布857项受保护资源核对保持。网页备份/var/www/ozl/test-runs/changelog-193d92501d6749808ce06809390463f5/before.html。发布后固定指纹的密钥SSH/SFTP只读检查仍通过。

Main EXE SHA256：6CACB700BCCB2AD89DF49F2C357702F03C3CE6EA55E07977F9A94BCD4287FE6A。
Startup JAR SHA256：3AC14738AFF2A156A35FCB54C1F7CB0707EB4A4270F2FFF1702C364F3415732B。

回执与公开校验：artifacts/publication-1.2.33，包括primary-receipt.json、production-agent.log、defender.log、old-client-ready.json、new-client-latest.json、public-download-proof.json、changelog-receipt.json及live-proof.json。
