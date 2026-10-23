# SignPath 免费签名申请记录

日期：2026-10-23。用户选择免费优先，申请主体为个人。初查基于源码提交 `61fc352`，GPL 许可已由 `515b664` 合并 main 并推送。免费签名申请已提交成功，页面显示“Form submitted / Thank you, we'll be in touch soon.”；尚无批准、账户启用或证书发放结果，未购买或公开签名产物。

## 当前状态

- 现有仓库：https://github.com/LiuDaCheng666/orange-zomboid-launcher 。官网：https://orangezl.com/ 。正式发布仍为 1.2.13，1.2.14 候选保持原状态。
- 用户已确认采用 GPL-3.0。本次新增根项目完整 `LICENSE` 和 `docs/LICENSING.md`，明确 `GPL-3.0-only`；README 与发行说明同步。第三方保留原许可，不另外添加禁止商用限制。
- 正常源码构建通过 `Build-Guide.ps1` 将完整 GPL、原创许可声明及第三方说明并入既有 `使用说明.txt`，核心更新允许清单保持 18 个文件。历史 Release 与候选二进制不变。
- Windows 代码签名证书未配置。原有 RSA 更新清单签名不是 Authenticode，继续保留原密钥及信任关系。

## 许可决定已确认

用户在了解 MIT/GPL 区别后选择 GPL。GPL 无需向 FSF 或其他机构申请；在有权授权的内容中放置许可文本和适用声明即可，不能把这一过程称为“申请已获批”。

- 本次采用 `GPL-3.0-only`，没有额外授予“或任意后续版本”。
- 分发基于本工具的修改版时，需要按其条款提供对应源码及同等自由；允许商业使用和收费，不能称作“禁止商用”。未分发的私人修改不要求公开。

第三方内容继续适用各自许可，不进行一揽子重新授权。采用 GPL 不代表所有依赖、隐私、可追溯构建及基金会资格检查已经通过。

## 已核对的依赖和构建

- `git ls-files` 没有跟踪游戏 DLL、第三方模组 JAR 或 ZIP。作者封面和第三方模组不作为本工具自有文件签名。
- `Prepare-Dependencies.ps1` 固定 WebView2 SDK 1.0.4258.31，核对 Core.dll 哈希；本机下载包的 `LICENSE.txt` 与仓库保留许可为相同 BSD 三条款式文本。具体 DLL 的分发许可仍以官方包及原始通知为准。
- ZombieBuddy 2.3.4 仅作本地 Java 编译依赖，下载哈希固定；`BridgeSource` 自有源码在仓库中。申请时需说明运行时游戏/Steam/Buddy 依赖，不宣称它们由 OZL 授权或签名。
- 构建会复用已存在的自有 JAR。正式可追溯 CI 应从已记录源码重建该 JAR，而非直接接受本地缓存。
- `Build-Version.ps1` 已给正式 EXE 写入 OZL 标题、完整产品名和统一版本。SignPath 侧还需配置并强制产品名与版本的文件元数据限制。
- 当前 GitHub 工作流只从现有草稿下载核心包再编译安装器，不是完整的源码构建/签名流水线。服务认可的构建来源链路需单独接入和验证。
- 正确顺序是源码构建核心 EXE → Authenticode 签名核心 EXE → 生成原有 RSA 更新清单与 ZIP → 构建安装器 → Authenticode 签名安装器 → 扫描、验证和发布。不得在生成更新哈希后直接修改 EXE。
- 原有 RSA 发布私钥继续留在仓库外。本次没有授权将其上传到 GitHub Secrets 或任何签名服务；接入 CI 前须明确密钥保管方案。

## 免费服务的实际条件

- 许可证需为 OSI 认可，项目及依赖要满足服务条件，必须有可验证的源码构建来源、维护者/审核者/签名批准者及多因素认证。
- SignPath Foundation 是证书主体；不能把预计签名发布者写成已核验的个人姓名或 OZL 自有证书。
- 每个正式签名请求需要维护者批准，不是每版送微软复核，也不是用户安装时批准。申请通过前不承诺完全无人值守签名。
- 需要公开 Code signing policy 以及真实隐私说明。1.2.14 启动静默检查会主动联网，不能使用“所有网络请求都由玩家逐次手动发起”的模板；是否需要增加关闭选项应按服务条件核对。
- 现有候选的 Defender 检测仍未定性，材料应准确说明记录，不声称已确认误报，也不把被隔离样本重建/签名当作绕过检测的方法。

## 申请内容草稿

项目：OZL / Orange zombiod launcher。个人维护的 Windows x64《僵尸毁灭工程》辅助启动器，支持组件、模组配置、存档、服务器连接、内存及工坊维护。源码与构建脚本公开，无游戏或作者模组再分发。

申请目标：在符合基金会条件并获批准后，为自有启动器、更新助手、卸载器和安装器接入来自对应源码构建的 Authenticode 签名。第三方组件不使用本项目签名重新签署；保留现有 RSA 在线更新完整性验证。

项目许可证已由所有者确认。联系人姓名与申请邮箱已由所有者专门提供，仅用于申请页，不存入公开仓库。GitHub 登录授权和多因素认证尚未核对。表单提交成功不表示项目已获批准或签名服务已可用，后续应查看申请邮箱收到的实际回复。

## 申请页面核对

已在后台打开官方 `https://signpath.org/apply.html` 并读取当前表单。必填项包括项目名、仓库、主页、简介、用途、项目信誉、构建系统、联系人的 First Name / Last Name / Email 和发现渠道。页面另要求同意 Code of Conduct 及个人数据存储处理；营销邮件同意为可选项，不默认勾选。

已按用户授权在后台申请页填写联系人及公开项目资料，营销邮件不勾选。用户查看提交前页面后，明确同意 Code of Conduct、个人资料存储处理及提交免费申请。两个必选同意项已勾选、营销项关闭；点击 Submit 后出现 reCAPTCHA 图片挑战，用户再次明确允许完成本次验证码。完成图片验证后，页面显示“Form submitted / Thank you, we'll be in touch soon.”，确认已提交成功，没有重复提交。

提交成功截图保存在被 Git 忽略的 `artifacts/signpath-submitted.png`，不包含姓名邮箱。没有上传私钥、支付费用、配置签名账户或使用证书。下载页的 Code signing policy 和完整来源构建仍需实际接入，申请描述已说明当前状态及未定性的 Defender 检测，不能提前宣称已签名或承诺申请一定通过。

## 本次许可变更验证

- `LICENSE` 与从 SPDX 官方 `license-list-data/main/text/GPL-3.0-only.txt` 读取的 34,509 字符正文完全一致（包括结尾换行），没有修改许可条款。
- `node --test tests/license-guide.test.cjs`：先在生成脚本缺失时得到 6 项失败，再实现后 6 项通过；包含实际仓库说明与完整许可逐字合并、中文编码以及缺失输入不覆盖旧说明。
- `npm test`：41/41 Web UI 测试组通过。三个涉及的 PowerShell 脚本语法解析通过，`git diff --check` 通过。
- `CoreReleaseProtocol.cs`、`VERSION`、公钥及 CI 工作流未改。没有运行会编译并执行安装器的完整 `Test-CorePackageBuild.ps1`，不以本次文本生成测试冒充安装／签名验证。
- 独立只读审查未发现需要修复的关键、重要或轻微问题，同意按源码许可及签名准备范围合并。
- 合并 main 后重跑说明生成 6/6 和 Web UI 41/41 均通过。远程 main 与本地 `515b664` 一致，GitHub license API 已识别 GPL-3.0；项目适用声明进一步明确为 GPL-3.0-only。

## 官方依据

- [SignPath 申请入口](https://signpath.org/apply.html)
- [SignPath Foundation 条件](https://signpath.org/terms.html)
- [MIT 许可证](https://opensource.org/license/mit)
- [GPL-3.0 许可证](https://opensource.org/license/gpl-3-0)
