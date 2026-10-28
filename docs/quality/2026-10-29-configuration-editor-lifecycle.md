# 配置编辑窗口关闭后的状态清理

## 现象与证据

用户确认在打开、关闭编辑窗口后，组件页面编辑按钮点击无反应，外层导航仍可使用。本机 `D:/OrangeLauncher/current/橙子启动器.exe` 为 1.2.37，安装中的 app.js 与修复前源码哈希相同。最近本机 `diagnostics/launcher.log` 没有点击对应异常；前端点击没有完整日志，因此不能以此断言没有问题。

后台实际 Chromium DOM 复现：调用原生 `HTMLDialogElement.close()` 后，窗口不再显示，但 `model.editor` 仍保留已关闭窗口引用。再次点击两个组件编辑入口都会被 `if (model.editor) return` 忽略。常规自定义 Close、Escape、放弃草稿和读取中 Close 原本均能通过；用户当时究竟通过哪个浏览器事件进入原生关闭路径尚未直接观测，不把这次复现描述成已捕获现场事件。

## 修复

统一 `releaseEditor(editor)` 清理该窗口的引用、节点和未完成只读请求；监听原生 close，清理幂等。下一次进入时释放已关闭或移除的旧引用，显示窗口失败也可重试。旧窗口延迟事件和旧草稿确认不能释放新窗口。已提交写入与作业继续执行，保留原有草稿一次确认。生产 app.js 与可编辑模块保持一致。

## 实际执行

- 新增真实 DOM 回归修复前 1/6 通过，五项失败覆盖残留引用、立即重新打开、未完成读取、已移除节点和显示失败。证据 `artifacts/configuration-editor-lifecycle-20261029/ui-results-before-fix.json`。
- 修复后新增延迟草稿确认用例，源码和实际候选包 Web 资源均 **7/7** 通过。桥接回复使用 experience fixture，未将其描述成原生文件操作。
- `UnifiedConfigurationNative.test.ps1 -FinalCase EditorLifecycle` 实际编译生产控制器并串联 DOM 点击：8 次读取、关闭、导航和再次打开；实际取消匹配的元数据读取；另一命名配置添加模组并持久保存；迟到读取没有覆盖新编辑器。86 次控制器调用、32 个事件，6 个原始文件字节保持。隔离临时目录，未触碰玩家游戏、存档或配置。
- `-FinalCase OrdinaryCancel` 相关回归通过：111 次实际控制器调用、68 个事件。读取/保存取消、草稿重试、写入回执、关闭后写入继续、另一配置的草稿与焦点，以及最终读取和文件字节结果。6 个原始文件保持。
- 额外执行既有“closing a submitted ordinary save releases the view while B remains editable”实际 DOM 组，1/1 通过。
- 已查看关闭后组件页面、放弃草稿后重新打开和原生保存完成截图。第一轮重新打开截图处于入场动画，按独立审查建议等待有限动画完成后重新捕获并查看清晰截图；未以模糊中间帧作为最终验收。
- 四个改动 JavaScript 文件 `node --check`、内联模块一致性和限定改动 `git diff --check` 通过。独立只读审查未发现重要问题；建议的延迟确认回归和截图重拍均已完成。

## 可运行候选与边界

候选 `E:/Projects/OrangeLauncher/dist/1.2.38-editor-lifecycle-20261029-local/app/橙子启动器.exe`，同级 `orange-launcher-1.2.38-local.zip`。使用现有 Build-VelopackLauncher 与外部 RSA 签名器构建；Windows 代码签名仍 **NotSigned**，没有宣称杀毒问题已根治。

- 候选自身生产 `DistributionBootstrap.Read` 验证 RSA、版本和21个冻结文件通过；app.js 与测试源码完全一致，SHA-256 `86F1810D72507E2EA0805ECC08542077D9003EA7A0A5D12B2158ADAD05023C03`。
- 主程序 SHA-256 `B69472478C4F8945CA18E1DFDBEFB57F94E013ACFFCE48F34264BC54FBEBBA33`，FileVersion 1.2.38.0。ZIP 的23个文件逐字节对比构建目录通过。
- 曾误用 artifacts 中嵌入1.2.16的历史 bootstrap 测试程序，因固定版本不匹配失败，未将其报告为通过；随后改为加载本次候选程序集执行其生产验证器，结果通过。辅助 PowerShell 验证脚本最初的混合路径/编码错误已修正并重新运行。
- 本轮没有物理点击用户桌面窗口，也没有启动游戏。用户实际窗口当前最小化，未激活、替换或终止。真实桌面现场关闭路径仍未直接捕获；后台 DOM、原生控制器和文件结果的边界如上。
- 本轮只生成本地候选，未发布服务器/GitHub，未修改线上网页。已发布1.2.37容器未覆盖；其他任务的网站与部署未提交改动保留。

证据另见 `artifacts/configuration-editor-lifecycle-20261029/` 的 `ui-results.json`、`native-results.json`、`candidate-verification.json` 和截图，原生详细跟踪在 `artifacts/owned-launch/task6/final-fixes-EditorLifecycle*.json`、`final-fixes-OrdinaryCancel*.json`。
