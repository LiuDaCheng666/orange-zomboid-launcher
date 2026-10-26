# 存档删除与快照流程修复验收

日期：2026-10-27。本记录为本地修复验收；尚未重新打包或发布安装包。

## 用户要求与实现

- 直接永久删除所选存档；确认一次，取消保持原样。移除回收站 COM 接口，实际调用 `Directory.Delete`，文件占用或只读错误返回给页面，宿主继续运行。
- 删除不再依赖无关的 Buddy、工坊、普通配置或其他存档恢复记录；不再要求先保存即将删除的存档草稿。仅可证明涉及当前存档 `mods.txt` 的未完成写入才报告该具体冲突；精确目录身份和同目标写锁保留。
- 预览使用独立任务与取消标识，文件核对立即显示活动进度、已处理文件/容量及用时，取消不影响其他网络读取。取消先到达也能阻止尚未开始的核对。
- 路径选择不再检查全局 `controller.Busy`；无关网络读取期间可以选择位置，取消后仍可重新选择。真正的文件修改仍进入原生修改队列。
- 缺失模组和依赖显示名称、来源及具体原因。有唯一可靠工坊映射时，在同一次创建流程下载、重新扫描依赖并继续创建。无唯一映射或版本子模组不匹配时说明具体项目，不擅自省略必需内容。
- 独立游戏版本通过登记的实际 Steam 安装下载，保留下载来源映射，仍复制玩家所选的独立游戏版本。保留 `steamapps/common` 目录形状的备份不会被当作实际 Steam 库。
- 复制、校验阶段报告真实文件数和字节数，包含零字节文件；阶段切换及最终完成强制报告，中间按 100ms 节流。只有原子登记、绑定成功才向全局报告完成。
- 核对及复制时隐藏无用的禁用底部按钮，复制时收起已确认的容量摘要；取消/失败后恢复可编辑内容。已查看深浅色及 733×507 窄屏状态。
- 已把“不擅改操作含义、无关任务不阻断、实际点击与文件结果验证、目视验收、只测受影响功能、未通过不发布”写入主项目和当前工作树的 `AGENTS.md`。

## 针对性验证

| 验证 | 结果 | 本地证据 |
| --- | --- | --- |
| 真实文件系统删除、占用/只读异常及同进程后续操作 | 27 项通过 | `artifacts/save-deletion-filesystem/filesystem.log` |
| 删除 API 和公开 Controller，无关恢复记录及当前存档精确冲突 | 73 项通过 | `E:/Projects/OrangeLauncher/artifacts/sdg-20261027/guards.log` |
| 预览提前取消、精确任务隔离、其他网络继续、重试 | 18 项通过 | `artifacts/snapshot-preview-cancellation/cancellation.log` |
| 快照存储、实际计数与取消 | 106 项通过 | `artifacts/snapshot-preparation/green-copy-detail/` |
| Controller 递归补齐、绑定竞态、独立版本及备份形状下载上下文 | 141 项通过 | `artifacts/snapshot-preparation/steam-shaped-backup-green/` |
| 运行环境及启动接线 | 229、29 项通过 | `artifacts/snapshot-preparation/final-copy-runtime/`、`final-copy-launch/` |
| 快照页面 DOM、进度、缺失项、成功/失败/取消、深浅色窄屏 | 14 组通过 | `Orange3DTool/Web/save-environment-ui-tests.cjs`、`artifacts/save-environment-ui/` |
| 存档页面删除确认、取消、错误、草稿与无关恢复状态 | 15 组通过 | `Orange3DTool/Web/library-ui-tests.cjs` |
| 实际页面按钮 → 原生 Controller/队列 → 实际文件结果 | 3/3 通过 | `artifacts/save-workflow-integration/report.json` |

最后三组分别是真实永久删除、网络读取期间取消选择后创建快照、缺模组补齐后同次创建。删除后的目录不存在，其他存档字节不变；快照生成实际副本和清单并绑定，原存档字节不变，宿主均存活且空闲。复制阶段原生事件和实际 DOM 的文件/字节计数有递增。

原生测试 EXE SHA256：`CE6F4DB71AE017A6C703429DA8CF2300C4DE8B270AE29590D6E966CAEA9A5C74`。编译清单 129 份源码在执行前后均无变化；该测试 EXE 的 Defender 按需扫描未检出。摘要：`artifacts/save-workflow-final/evidence-summary.json`。

测试边界：页面点击运行在后台 Edge，调用真实原生 Controller、派发队列、目录选择后处理和文件系统。系统文件夹弹窗的返回值由测试选择唯一自有目录替代；Steam 下载运输受控，`test:true` 禁用真实 Steam/进程发现并跳过实际游戏 JVM 兼容探针。因此这份报告不等于真实 Steam 在线下载、正式安装包或真实游戏进档的验收。未操作用户前台、真实存档、游戏文件或 Steam 配置。

## 失败记录与保护范围

- 提前取消、独立版本下载上下文、保留 Steam 目录形状的备份，以及无关恢复拦截均保留了失败后修复通过的证据。
- 深层测试输出目录曾触发旧 .NET fixture 的 MAX_PATH；在明确归属的较短证据目录重跑通过。并发按需扫描曾被 Defender 告知已有扫描，随后串行扫描成功；没有绕过检测。
- 原始六项网站/部署未提交改动的 SHA256 与开始前一致；本次没有服务器或 GitHub 发布，没有覆盖网页，也没有改 SSH、HTTP 防护或 Defender 设置。
- 1.2.33 已发生的运行时隔离仍见 `docs/security/2026-10-27-v133-runtime-detection.md`，不能把本次测试程序的扫描结果说成正式发行包已解决误报或已有 Windows 代码签名。
