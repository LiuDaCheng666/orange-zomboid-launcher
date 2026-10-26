# Windows 签名接线与发布前验证

日期：2026-10-27。**签名流水线已接入，正式可信证书尚未取得，未构建或发布新的正式二进制。** 已修复的存档工作流保存在提交 `f73f710`；安装向导的真实操作结果另见同日 `2026-10-27-setup-wrapper-dependency-boundary.md`。

## 实际改动

- `Build-Version.ps1` 接受独立 VERSION 和输出路径，保留默认接口；中文安装器获得准确文件/程序集/产品版本。
- 主程序签名发生在 RSA bootstrap 文件摘要之前。vpk 使用单个正则排除六个已冻结的应用 PE，仅签生成 updater/stub/Setup。最终选择器在嵌入官方安装器后签名。
- `build/Authenticode.ps1` 精确选择证书指纹/Subject/Windows My 库，不导出密钥；SHA-256 与 RFC3161 时间戳；WinTrust 与 SignTool `/pa /all /v /tw` 双重核对预期发布者和时间戳。
- 最终验证按容器位置检查 nupkg 中主程序/Squirrel/执行 stub、portable 中主程序/Update/根目录 stub、外层安装器。构建时额外验证嵌入的官方 Setup 输入。第三方保留原字节与固定来源摘要。
- 验签期间只读共享句柄阻止并发修改/删除。上传前把实际认证并载入内存的十项文件与该次验签摘要绑定，变化时在 SSH 连接前退出。
- 正式构建与发布命令要求真实可信签名配置。开发编译、测试 fixture 不被描述为正式签名发行。发布与签名规则已同时写入当前工作树和主工程 AGENTS.md。

## 已执行验证

| 范围 | 证据/结果 |
| --- | --- |
| 版本输入和独立生成路径 | `tests/BuildVersion.test.ps1` 实际 RED → GREEN；7.8.9 元数据及四种非法版本不覆盖旧输出；pwsh、Windows PowerShell 5.1 通过 |
| 签名配置与验证器 | `tests/Authenticode.test.ps1` 实际初始失败后通过；真实未签名 Velopack DLL、错误发布者拒绝；原始 Microsoft 签名 SignTool 的可信签名/时间戳正例；六个 frozen PE 正则及生成 stub 不豁免 |
| 容器角色 | 同一未改写 Microsoft 工具构成明确的测试归档，逐一验证八个位置；删除 portable 根 stub 后拒绝。证明验证器行为，**不是给 OZL 签名或真实产品安装验收** |
| 正式构建无身份 | 实际调用缺失签名配置的正式构建，在读取不存在的 RSA 私钥和创建输出目录之前按准确原因退出 |
| 发布字节绑定 | 两项回归实际 RED → GREEN；Setup 摘要变化在 SSH 前失败，匹配摘要与内存 remote 的上传字节完全一致 |
| 发行操作保护 | `python -m unittest discover -s tests -p test_publish_software.py`，17 项通过；原有四项元数据/十项发行/网页保护范围保持 |
| 真实发布 CLI 失败预检 | `test_publisher_signing.py`，1 项通过；实际 Windows PowerShell 子进程报缺失身份，没有发布或签名成功回执 |
| 安装及历史 bootstrap 接线 | 新编译 wrapper 边界、7 项历史 bootstrap case、中文 chooser release contract 通过；独立 review 未发现 P1/P2 |
| SSH | `python deploy/check_ssh.py`：固定指纹、密钥认证、只读命令与 SFTP 通过，当前网站路径保持 |
| 他人网站/部署文件 | 六项 SHA-256 与既有 `artifacts/publication-1.2.28/foreign-before.json` 相同 |

根签名测试日志：`artifacts/authenticode-tests.log`、`artifacts/authenticode-winps51.log`、`artifacts/publisher-signing-red.log`、`artifacts/publisher-freeze-red.log`。原始 Microsoft 工具来自已经固定全部文件摘要的 Velopack 1.2.161 包，签名有效且有时间戳；没有伪造或导入受信任自签根。

独立 review 另核对 InstallerOnly 的 14 个关键实现与原代码文本相同，并重跑摘要绑定与 Windows PowerShell 编译边界。没有重跑无关游戏功能或整个项目。

## 仍待完成

本机没有正式签名证书，SignPath 旧申请未获批准。用户的付费证书/已有证书选项正在确认；身份核验及付款未代办。没有证据证明 Prowloc 的具体触发原因，微软当前样本复核仍为 In progress。

本机读取到 Defender 防病毒启用、实时保护关闭，定义版本 1.461.215.0；本轮未修改该设置，也未运行新的 Defender 扫描。须在取得可信身份后对**同一最终签名候选**完成实际安装、更新、启动及开启防护情况下的验收，再同步自有服务器/GitHub/网页更新记录。以上源码、验证器与惰性安装测试不能替代该步骤。
