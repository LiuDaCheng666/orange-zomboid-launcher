# 快照保留原清单修复验收（2026-10-27）

本次修复只涉及快照捕获、快照启动检查和相关界面。未发版、上传或修改网站；保留已有六个未提交的网站/部署文件及其他未跟踪资料。新增编辑快照清单功能另行处理。

## 行为

- 捕获阶段不排序、不因作者声明的顺序、循环或不兼容拒绝复制。明确的存档、默认及预加载清单保留原顺序；补充依赖文件可被复制，但不写入默认启用清单。
- 真正缺失的来源/依赖仍进入原有自动查找、下载以及用户明确替换/跳过流程。未修改已有选择协议。
- 完整盘点后才标记 inventoryAvailable；提前失败显示“未统计”和明确重试文案，不把未完成统计当成零。
- 快照默认清单 ID 未变化时直接保留原字节；明确替换/跳过导致清单变化时才生成新清单。
- 仅冻结快照启动检查改用来源/文件依赖检查，不以作者顺序、冲突或循环为启动门槛。普通来源启动检查保持原逻辑。

## RED / GREEN

1. SaveEnvironmentsTests 新增 reverse/conflict/cycle/implicit 四类隔离来源。最初 RED：反向顺序预览 ready=false，报告“First → Second（前者必须先加载）”。移除捕获排序和默认依赖展开后 GREEN：147 assertions。
2. SaveEnvironmentRuntimeTests 新增四类相同案例。第一轮 RED：原默认清单字节被规范化。保留未变化 ID 的原字节后，第二轮 RED：冻结启动检查仍因逆序失败。冻结检查改为实际来源检查后 GREEN：349 assertions，含原有明确替换/跳过、取消、来源隔离和运行配置检查。
3. Native DOM 回归新增明确失败文案断言，RED 为“核对完成，请先处理下方提示”；修复后为“未能完成核对，请检查下方文件提示后重试”，GREEN。

## 实际执行

- `tests/SaveEnvironments.test.ps1`：147 assertions；最终记录 artifacts/snapshot-original-order/final-storage。
- `tests/SaveEnvironmentRuntime.test.ps1`：349 assertions；runtime-green。
- `tests/SaveEnvironmentLaunch.test.ps1`：29 assertions；launch-green。
- `tests/SaveEnvironmentController.test.ps1`：158 assertions；controller-green。
- `node Orange3DTool/Web/save-environment-ui-tests.cjs`：25/25 组。此套为真实 DOM、合成 native 响应，不能单独证明文件复制。
- `tests/SnapshotOriginalOrderNative.test.ps1` 与 `node tests/SnapshotOriginalOrderNative.cjs`：真实无头 Edge DOM 点击 → 生产 ModernController（TestMode=true）→ 隔离磁盘真实复制、运行配置、不可变清单；未注入假复制成功。初始 B,A，B require=A，A incompatible=B。先实际取消预览并再次打开；再将隔离 mods.txt 改为无法解析的内容，确认失败/未统计/创建禁用，恢复原隔离文件后点击重新核对；显示 2 个、游戏 80 B、模组 49 B、合计 129 B，再点击创建。
- 新建中文保存路径下真实文件，断言游戏 payload、两个复制 mod.info 与原件相同；manifest.Record.ModIds 与 DefaultModIds 均为 B,A；SaveModsBytes 与原隔离存档清单字节一致；runtime/mods/default.txt 与原隔离默认清单逐字节一致；运行配置含固定环境代理；原存档/默认清单/模组元数据未变化。report.json、manifest-proof.json 保存在 artifacts/snapshot-original-order/native。
- 已实际查看 `failure-unmeasured.png`、`preview-original-order.png`、`created-original-order.png`：失败时可重试，正常预览无顺序/冲突提示，创建后卡片为当前使用且按钮可用。截图均为隔离测试数据。

## 边界及环境问题

没有启动游戏，没有占用前台或操作 Steam，也没有修改真实游戏、工坊或玩家存档。本次运行配置与 preflight 验证不等同于待发布包真实游戏入口、正常存盘重启、原来源更新后的实际游戏验收；不借用历史实际游戏记录作为本次通过证据。

native harness 的 TestMode 和 500ms 测试调度延迟只用于可重复取消；来源扫描、复制、运行环境准备和 manifest 均为生产实现及真实隔离文件。初始读取版本/页面其余上下文沿用 UI fixture，因此不是完整桌面应用发布包验收。

长 worktree 路径触发已有 Build-EnvironmentComponent.ps1 的 .NET 解压 MAX_PATH；使用临时 R: subst 同一 worktree 后成功构建，未修改构建脚本。测试结束后移除此临时别名。

独立 SnapshotPreviewCancellation.test.ps1 编译成功，但其 Start-MpScan 在测试执行前返回 0x80508023，故该脚本不计通过。未关闭 Defender、添加排除项或绕过其检查。新增 native DOM 取消/再次核对路径已实际通过。编译有四处既有 CS0649 警告，未声称零警告。

最后一次隔离快照目录：`E:\AppCaches\Temp\ozl-original-order-1793102471367\中文快照\OZL-快照-20261027-200114-02fc65`。保留该目录供复核；仅包含本测试新建数据。
