# Buddy option unavailable：本地修复验证（未发布）

本页保留开发阶段的验收边界；后续已随1.2.42正式发布，见 [正式发布回执](2026-10-30-1.2.42-publication.md)。

用户实际日志 `Zomboid/Logs/2026-10-30_03-07_DebugLog.txt` 在03:08:02记录 `Buddy option unavailable`，调用点是 `OrangeLaunchBridge.lua:54/serverOrder()`。当时框架API已经暴露，但主菜单只载入连接桥接模组，Buddy的Lua设置尚未注册。代码主动调用 `error()`；外层pcall虽继续执行，游戏仍记录异常。当天03:11:02的连接日志随后记录player-connect。前一日21:20也有相同错误，不能归因为1.2.41绘制修改。

## 最小修复

沿用同一连接入口。可选ModOptions API、Buddy选项组或选项缺席时，不调用 `error()`，不等待注册；已注册则更新原选项。之后仍执行既有的追加式INI覆盖和Buddy运行时接口，使服务器顺序在后续注册/ResetLua后保持。真实读取、写入或接口异常仍保留原处理及诊断，原连接不新增门禁。没有新增后台任务、产品UI或接管策略。

## 执行证据

证据：`artifacts/buddy-option-readiness`。

- 先增加缺API/选项组/选项的集成回归，旧代码明确失败于“未注册可选设置不应抛Lua错误”。实际Kahlua回归同样先失败于 `no-api` 人工error调用。
- `tests/launch-bridge-modoptions-native.py` 使用已安装B42 ModOptions和Buddy原始Lua文件、Lupa Lua5.1真实字符串库，INI/事件/连接边界为内存fixture。修复后8组通过：延迟注册与原Buddy重载、原字节保留、已false不写、单人不变、open/partial/close失败继续连接、首次/重复设置、无末尾换行。
- 新 `BuddyOptionAvailabilityTest` 运行游戏原Kahlua VM及生产排序分支，覆盖API/选项组/选项/setter缺席及正常五种状态，人工error调用为0，运行时false与INI覆盖保留。文件IO和单项INI匹配为封闭fixture；没有加载会初始化引擎ObjectPool的游戏StringLib，真实字符串语义由上述安装Lua集成测试覆盖。
- 原 `Orange3DTool/launch-bridge-lua-tests.py` 七组通过，保留主菜单一次提交、工坊清单观察、原参数、过期请求、失败状态和单人行为。
- `BridgeParentVisibilityTest` 提取并执行原生连接/失败/返回/Abort Lua及生产提交分支，50项通过，原按钮回调、父容器可见性及取消次数保持。
- 原 `BuddyOrderProbe` 对已安装ZombieBuddy.jar实际调用原排序API，在独立artifacts配置目录确认关闭并保存顺序偏好，授权清单哨兵原字节不变。
- 独立审查未发现阻断缺陷，另行重跑安装ModOptions/Buddy Lua八组通过。`git diff --check`通过。

没有操作玩家配置、真实服务器连接、运行完整游戏、替换安装程序或发布新版本。当前线上仍为1.2.41；本记录不能描述为实际游戏中的红字复验或正式发行验收。
