# 按公开 NIKKE 攻击链补充浏览器约束

本文件记录发布前实现与诊断验证；之后完成的最终候选操作验收及 1.2.52 正式发布见 [发行记录](../releases/2026-11-05-1.2.52-security.md)。

2026-11-05，实际工作树 `C:/Users/MC233/.codex/worktrees/velopack-updates/OrangeLauncher`，HEAD `44d1b0978c102e2d30e20b11b0f89d43939918bb`。用户要求继续处理 DLSS 之外的同类风险。本次没有安装、发布、操作真实游戏、修改 Windows 防护或读取用户凭据。

## 阅读依据

已实际读取公开的攻击路径、技术分析、IOC README/CSV 及另一份事件分析，包括恶意 DLL/LuaJIT bundle 文件信息、哈希、COM 注册项、落地路径及网络指标。没有下载或运行恶意二进制、访问 C2，未自行证明报告每项归因。

- https://github.com/WormW/nikke_malware_analysis/blob/main/docs/attack-path.md
- https://github.com/WormW/nikke_malware_analysis/blob/main/docs/technical-analysis.md
- https://raw.githubusercontent.com/WormW/nikke_malware_analysis/refs/heads/main/iocs/iocs.csv
- https://github.com/MotherSphere/NIKKE-Launcher-Check/blob/main/docs/incident.md
- https://gist.github.com/MotherSphere/d6c049a09ba8ac3085c51ee9df24a4e5

报告的关键链条为官方 CDN 脚本污染后调用原生下载接口，下载伪装内容并执行；后续载荷具备窃取凭据、远控和部分主机持久化能力。仅拉黑现有文件名或哈希不能约束未来载荷。

## 现有防护与本次改动

| 对应路径 | 处理与边界 |
|---|---|
| CDN 脚本变成原生操作入口 | 原有本地 UI、CSP、消息源/动作/参数白名单、bootstrap 与更新 RSA 签名继续保留；本次收紧实际资源服务链，未增加通用下载/执行接口。没有证明原版存在可利用的远程 XSS。 |
| 同目录增加脚本或伪装 `.log`/DLL | 撤掉 WebView 虚拟目录映射，精确限制本地路径、资源类型、GET 方法。只提供 index.html、app.js、app.css、home.png、orange-app.ico；本机库及其他目录文件不对网页提供。明确 MIME、nosniff、no-store。 |
| 校验后磁盘文件被替换 | BrowserFiles 从内置身份选出的签名清单验证文件，读入字节后再次核对长度/哈希；成功后冻结在内存，后续请求使用独立只读流。文件或签名失败直接停止，不降级到不验签文件。 |
| 内联脚本、隐藏框架、worker、表单外发 | CSP 显式禁止脚本属性、worker、frame/child、form-action；原有 connect-src none 保留。原生侧同时拒绝 frame 导航和浏览器下载。 |
| 浏览器凭据存储与自动填充 | 显式关闭 WebView 密码自动保存、通用自动填充和 JS 默认对话框。现有 HTML dialog 保留；此设置不是 Cookie 清理或整机凭据隔离。 |
| DLSS 第三方原生载荷 | 前一项改动已删除下载与启用链，仅保留经确认恢复旧安装。详见同日 retire-experimental-dlss 记录。 |
| 授权 Java/JAR/其他原生代码 | 复核原有逐文件哈希授权和自有组件认证；本次不更改授权策略。授权不是恶意代码判定，也不是 OS/Java 沙箱。 |

## 实测发现与修正

首次加入纯策略函数后，策略测试通过，但真实 WebView 注入额外本地 extra.js 仍执行，原生拒绝计数为 0。原因是 `SetVirtualHostNameToFolderMapping` 的请求不触发该 WebResourceRequested 事件。微软文档明确说明这一点：

https://github.com/MicrosoftDocs/edge-developer/blob/main/microsoft-edge/webview2/how-to/webresourcerequested.md

因此删除整目录映射，由真正经过过滤器的 handler 返回已验证文件；相同 native 用例复测为 `extra denied=1`、`badLocal=false`。修复依据是实际拒绝回执与未执行状态，不是仅有策略函数通过。

## 已执行验证

- 测试先行最初失败 `FAIL browser has a resource-role allowlist`；实现后 BrowserContainmentTests、既有 ModernHostTests 策略/队列测试返回 0。覆盖允许的 5 文件角色、额外/伪装/错误角色/编码路径、外部脚本、原生动作拒绝、冻结字节与缺文件停止。
- BrowserContainmentNativeTests：实际 ModernForm + 本机 WebView2，8 项 PASS。运行在测试自有不激活的屏幕外窗口，只捕获该 WebView；未操作或截图用户桌面/游戏。使用真实 app.js、CSS 和生产 Build-CoverGallery 生成的约 11.3 MB HTML，实际解码 3 张内嵌场景 PNG 及 home.png；实际原生消息桥完成只读状态请求往返。HTML dialog 开关通过；额外脚本 HTTP 403 且未执行；CSP 拒绝内联脚本、worker、fetch、iframe 和 form。
- BrowserContainmentSignedTests：使用本轮独立临时测试密钥、嵌入的测试公钥和发行 marker，实际执行 ModernAssets.BrowserFiles 的 Velopack 认证分支，5 项 PASS。认证的 UI 字节冻结；更改 app.js 或 bootstrap 签名都拒绝，未退回不验签读取。临时测试私钥已删除，未读取或使用发行私钥。
- 原有 CoreReleaseProtocolTests `--protocol-only`：73 PASS；JavaApprovalsTests 完整隔离回归返回 0，覆盖哈希变化/陈旧授权/未知代理等；没有把授权称为安全沙箱。
- 相关真实 Edge DOM 回归 dlss-retirement-ui-tests：6/6 组通过；该组 WebView 传输为合成数据，不与上面的真实 native 只读桥混称。
- 按实际 `OZL_PRODUCTION,OZL_VELOPACK` 生产清单编译成功，仍有已有 CS0649 警告；未签名、打包、安装或发布。编译 SHA256 `265EC16084DA4DDB0E267A7C43E41C669AA390CF1D89934EA6D1E6B546E9C5C4`，详见 artifacts/browser-containment/production-compilation.json。
- 独立代码审查核对 SDK 行为、manifest 选择与失败路径、冻结字节、流并发、合法图片/HTML dialog 兼容性。修正过期映射注释后无阻断问题。开发者已实际查看最终 native-browser.png：主页面、场景图、导航与文字渲染正常；隔离 fixture 显示未选择真实游戏、显存不可读，不计为游戏验收。
- diff whitespace 检查通过，保留源文件原有 LF/CRLF；未修改无关网站、部署或系统配置。

证据：工作树 artifacts/browser-containment/ 下的 policy-tests.log、native-webview.log、signed-browser-tests.log、core-protocol-regression.log、java-approval-regression.log、dlss-ui-regression.log、production-compilation.json、compile-verification.ps1。最终原生截图：`E:/Projects/OrangeLauncher/artifacts/security/2026-11-05-nikke/TestFixtures/browser-fc90f2af/native-browser.png`。

## 未覆盖与保留边界

没有运行恶意样本或构造真实远程 XSS；下载事件拒绝和 frame 原生回调已接线，但 native 用例中的 frame/form 首先被 CSP 阻断，不能宣称每个回调均由独立攻击触发过。签名分支是受控测试密钥的字节夹具，真实 WebView 是诊断构建；未完成真实发行容器从签名打包到可见安装窗口的全链验收。没有测真实进服、真实组件下载/导出，也没有声称游戏卡顿已解决。

合法签名但有恶意行为的发行内容、被盗签名身份、已授权的第三方 JAR/原生代码、浏览器/系统自身漏洞及机器已有入侵均不由这些限制完全解决。当前 Windows Authenticode 状态未改变，RSA 更新认证不等于可信 Windows 发布者签名。

完整旧 production-boundary 套件本轮未重跑；前一项 DLSS 记录已记录 4 个在基线和修改后均存在的固定成员计数失配。本次使用实际生产源编译，不把旧套件称为全绿。现有安装和线上版本未替换。
