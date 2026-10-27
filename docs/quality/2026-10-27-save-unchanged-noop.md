# 未编辑存档模组清单时保存不再重写

日期：2026-10-27；1.2.35 最终候选 r4。

## 实际问题

完整快照已从真实存档克隆进入世界两次，游戏原生存盘、正常退出和再次 Validate 均通过。额外实际操作 ForSave Read→完全相同 enabled→Save 时，SaveLocked 却合并当前默认组件，把 134 项变成 141 项，接下来 Validate 提示模组顺序变化，快照无法再次使用。

新增项正是 ZombieBuddy、Viewpoint、PZVoxelStudioViewpoint、ProjectViewpointShelves、ViewpointTrueWeathers、ProjectViewpointNearVegetation、LaoYuTranslationJava；用户本次没有选择这些修改。原始输入、返回和前后文件在 artifacts/SaveSnapshotReal-20261027-main-a3/launch-7-r3-resave-regression。保留坏产物后，仅将独立克隆的 mods.txt 恢复到其第二次游戏真实存盘的精确字节；未改源存档或冻结副本。

## 最小处理

SaveLocked 仍先检查已有 stamp 和请求文本/重复 ID。只有文件实际存在且请求清单与当前 Read/PersistentMods 的顺序完全相同，才直接返回 Read：不写文件、不合并组件、不扫描全部模组。

继承默认列表的首次创建仍需执行原流程；真正编辑仍保留原合并/排序和写入规则。未通过修改 MatchesSaveMods、吞掉异常或把 141 当作新捕获来隐藏问题，也未改变 controller 路由。

## 结果

- artifacts/save-profiles-noop-red 先真实复现 source 无改保存加入 7 个默认组件。
- artifacts/save-profiles-noop-green：27 项新回归通过。覆盖 source、managed、真实 source-controller saveGet/saveSave、真正 ForSave frozen；BOM、CRLF、未知文本块、原字节和 mtime 保持，持仅共享读取句柄时也成功。过期 stamp/无效输入仍拒绝，继承首次保存和真正编辑行为继续有效。
- 原 SaveProfiles 84 项通过；两处 fixture 改为实际编辑，以继续验证合并和未知语法写入拒绝。
- r4 发布包的两条真实 134 项操作均已通过：source ModernController saveGet/saveSave 和 ForSave Read/Save，文件均为 3,876 字节、SHA-256 9CAEF7C48250E7458B17C0157309EAA634F0711E8615050587C6BA06FE32239A、writeTicks 639286723024102450，后续实际 Validate 均通过。证据 r4-noop-before.json、r4-controller-noop-after.json、r4-frozen-noop-after.json。

完整图形游戏与最终发布回执归入同日快照验收记录；本测试不冒充所有页面的完整回归。