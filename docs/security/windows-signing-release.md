# Windows 签名与正式发布

Windows 发布者签名使用可信 CA 签发的代码签名证书。`release-private.xml` 只签 RSA 更新清单，不能用于 Authenticode。证书私钥留在云签名/硬件提供者内，配置只记录公开的指纹、Subject、证书库位置和时间戳服务；不导出 PFX、不保存密码、不上传 RSA 私钥。

## 当前证书状态（2026-10-27）

本机 CurrentUser/My、LocalMachine/My 尚无可用代码签名证书。10 月 24 日审计已记录 SignPath Foundation 因项目采用信号不足未批准申请；10 月 23 日的“申请已提交”是历史状态。没有获批的签名账号或有效发布者产物。

付费候选为 Certum Open Source Code Signing on SimplySign，官网实时商品页标价 €49、期限一年。它发给个人，发布者包含真实姓名；需要本人身份核验、账单和公开项目关系证明，且该 OSS 产品限制商业分发。当前官方自动核验支持表列出中国护照；这不能替代 Certum 对大陆自然人当前签发资格、中文账单和实际证件的确认。付款及证件提交由用户本人完成；未购买或向厂商发送身份资料。

微软样本复核 `df47e309-95c1-45d4-8cf3-d5859eb45c3b` 仍显示 In progress，部分文件 Not malware/当前无检测不等于整包已有最终结论。签名、SmartScreen 信誉与 Defender 恶意软件判定分别验收。不能承诺签名或一次复核后所有版本永远不再检测。

## 证书获批后配置

1. 按证书厂商官方文档安装并登录密钥提供者，例如 SimplySign Desktop；由用户本人处理登录/手机令牌。证书在 Windows My 库中可见，并具有代码签名用途和私钥提供者。
2. 将 `build/authenticode.example.json` 复制到 Git 忽略的 `private/authenticode.json`，填入该证书的准确 SHA-1 指纹与完整 Subject。默认使用已固定包哈希且具有效 Microsoft 签名的 Velopack 内置 SignTool；不要写私钥或口令。Certum 官方示例 RFC3161 服务为 `http://time.certum.pl`，签名响应本身须通过信任验证。
3. 在 Windows 构建机运行：

```powershell
./Build-VelopackRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -AuthenticodeConfig private/authenticode.json -OutputDirectory <全新候选目录>
```

构建顺序固定为主 EXE 签名和验签 → 21 项 RSA bootstrap → vpk 生成并签名更新程序、执行 stub、官方 Setup → 嵌入官方 Setup 的中文目录选择器 → 签名最终选择器 → 原有 RSA/哈希/图标检查及各最终位置的 Windows 签名检查 → 发布元数据。`--signExclude` 是 vpk 1.2.161 的单个 .NET 正则，只排除已冻结的六个应用 PE；不能拿它跳过 portable 根目录同名执行 stub 的最终验签。

第三方 SDK/库保留原始字节、原发布者签名与固定来源摘要，不使用 OZL 身份重新签第三方。缺少可信身份、错误发布者、无时间戳或签名无效时，正式构建失败；不伪造证书、安装自签根证书或关闭防护。

## 发行验收与上传

- 冻结最终候选，按本次变更验证真实安装、更新、启动及失败/取消页面。记录准确产物 SHA-256、源提交、工具和防护状态；不能把惰性安装 fixture 或源码测试当作正式产品通过。
- 发布工具先用 Windows WinTrust/SignTool 验证最终两个容器中的主程序、更新程序、执行 stub 和最终安装器，再核对已有 RSA bootstrap。验签时持有只读共享句柄；待上传字节必须匹配该次验签的十项摘要，变化时在 SSH 连接之前退出。
- 普通软件发布命令新增必填 `--authenticode-config private/authenticode.json`；其他 source、preserved-legacy、expected-current、evidence 参数沿用既有发布流程。该本地配置不会上传到服务器。
- 只更新十项发行文件、既有版本/签名清单。服务器渠道正常后发布 GitHub 相同字节，随后使用 `deploy/publish_changelog.py` 增补线上更新记录。不得部署整个网页、覆盖他人改动或改 HTTP/SSH 配置。
- 如果同一最终产物仍被检出，保留准确哈希、检测名称和版本，交微软判定；不以改名、重复打包试探、恢复隔离或排除项冒充解决。

## 官方依据

- [Microsoft SmartScreen 信誉规则](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)：固定可信发布者可累积信誉，EV 已不能直接跳过 SmartScreen。
- [Microsoft SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)：SHA-256、RFC3161、签名与时间戳验证。
- [PowerToys 安装器流程](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/core/installer.md)：内嵌组件先签，再构建安装容器；不能只签最外层安装器。
- [Certum 云签商品](https://shop.certum.eu/open-source-code-signing-on-simplysign.html)、[材料与限制](https://support.certum.eu/en/code-signing-required-documents/)、[支持证件](https://support.certum.eu/en/what-documents-are-supported/)、[官方 SignTool 使用手册](https://files.certum.eu/documents/manual_en/CS-Code_Signing_in_the_Cloud_Signtool_jarsigner_signing.pdf)。
- [SignPath GitHub 来源验证要求](https://docs.signpath.io/trusted-build-systems/github)：免费服务不能用任意本地二进制冒充托管来源构建。
