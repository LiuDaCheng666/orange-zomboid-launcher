# 1.1.0 本地签名发布验证

状态：本地构建与测试完成；未发布线上渠道，未推送或合并 main。此记录描述本地已验证成品，不声明游戏内兼容性。

最终输出目录：`E:/Projects/OrangeLauncher/dist/1.1.0-round2-verified/`。早先的 `artifacts/core-signed-verification-1.1.0/`、`dist/1.1.0/`、`dist/1.1.0-verified/`、`dist/1.1.0-round1-verified/` 保留为开发/首轮验证证据；不要将其与包含两轮审查修复的最终目录混用。

| 产物 | 字节 | SHA-256 |
|---|---:|---|
| orange-launcher-setup-v1.1.0.exe | 5821440 | EF9679C0573F7C76FA208A0D7D3E7E4FEC5730FC99297C284E0AC34485146597 |
| orange-launcher-v1.1.0-win-x64.zip | 4681913 | 304A4ED1320C43833E358E9C7DBB68F8E91E386997AEA981FCD6433F64A3D125 |
| update-manifest.json | 6880 | B1AE1A7545443D2F5ED671BEBB3C26AB543F8493BF4E84248AC7C0A06F9C6EB9 |
| update-manifest.sig | 384 | 7FA078E8603A58E87766EF2288BA54216178E230B482BC50055CACB5646F8B6D |

构建命令：

```powershell
./Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.1.0-round2-verified
```

使用现有受保护私钥，没有覆盖、打印、提交或向第三方发送私钥。签名对象是清单的原始字节；不声称 EXE 有 Windows Authenticode 签名。三个 EXE 的 FileVersion 为 1.1.0.0，ProductVersion/嵌入 AppVersion 为 1.1.0；磁盘 VERSION、签名清单及实际 ZIP 首启安装记录也为 1.1.0。清单恰好18个允许文件，逐文件完整哈希见同目录 update-manifest.json。

验证结果：

- 正式安装 EXE `--verify-setup` exit 0：实际空目录提取、全量签名文件验证、版本记录与自有卸载保留未知文件。
- 最终 ZIP 解压到 `artifacts/TestFixtures/signed-round2-ui/` 后，实际正式 EXE `--modern-preview <截图> --live-read --page help --theme dark|light --size 820x600` 均 exit 0；截图使用隔离 PreviewData，未访问真实游戏会话。原生面板显示1.1.0；检查是手动的，此截图状态为尚未检查。
- `Test-CoreNativeUpdate.ps1` 最终隔离根 `artifacts/TestFixtures/native-update-ad9d5076/`：sparse/full 各自健康成功、故意坏页面45秒超时回滚、旧页面重新启动一次，四条路径的自有卸载全部通过。所有游戏/用户/data为合成目录，日志 `artifacts/task4-round2-lifecycle.log`。
- 稀疏 fixture A 清单/记录/VERSION/EXE均1.1.0；fixture B 清单/记录1.1.1，但没有改变的 VERSION/EXE仍1.1.0。它只证明稀疏文件下载及重启协议，不可当作发布成品。真正更改 VERSION 的新版本通常需要完整替换小 EXE，不是二进制差分。
- full fixture A所有版本为1.1.0；B所有版本为1.1.1，三个 EXE 真正重新编译，下载三个完整EXE、VERSION及app.js共5个变更文件。成功实际运行 AppVersion=1.1.1；失败恢复三个EXE原始字节及版本1.1.0，运行中AppVersion=1.1.0。未将fixture上传渠道。
- 原生 baseline 顺序复跑exit0；协议79、事务149、包17、服务41、助手63断言通过。助手覆盖真实候选存活时拒绝Recover、真正中断持锁助手后仍阻止回滚、候选退出后安全恢复、未知启动意图持续阻塞，以及已知创建失败/身份落盘前退出的正确回滚。所有现有浏览器套件与新增核心更新套件通过：full21/21、compact19/19及 ui/profile/java-cover/extensions/inherited-layout。并行UI验证期间 baseline 曾触发既有物理内存保护；未改测试/代码，UI结束后原二进制重跑通过，失败日志仍保留。
- 额外捕获并修复.NET Framework反射缓存改变JSON字段顺序的问题：跨进程预热字段后派生记录比较曾失败。现在对派生targetRecord严格解析全部字段、类型、数组及授权值，再做语义比较；未知字段/数字字符串等仍拒绝。签名原始字节和baseline/已安装记录的字节级冲突检查不变。
- 对真实官方无凭据 latest API 的最后一次检查收到403 rate-limit。此前任务记录的 Release 列表为空；这次无法重新确认网络状态。产品显示读取失败或404时的不可用，绝不把失败当作“已是最新”。没有发布用于绕过验证的fixture渠道。

截图（已实际查看）：

- `artifacts/core-update-shots/round2-native-dark-820.png`
- `artifacts/core-update-shots/round2-native-light-820.png`
- `artifacts/core-update-shots/native-signed-live-dark.png`、`native-signed-live-light.png`（首包1000×720详情）
- `artifacts/core-update-shots/dark-820.png`、`light-820.png`（浏览器合成Ready/草稿/占用场景）

820×600高DPI沿用既有三列导航；更新操作靠近版本行，检查按钮完整可见。更多说明/发布说明可在内容区滚动阅读，无横向溢出。

运行限制：需系统已有 WebView2，不安装运行库、不提权、不写注册表。运行时任务、校验缓存、备份与日志保留，可能逐渐占用磁盘；失败安装/卸载后重装请选择新的空目录。传统.NET长路径仍可能失败。只终止助手自己启动且身份匹配的失败候选；不能释放候选时保留 Conflicted 状态，不在活跃候选下回滚。游戏、Steam订阅、真实存档和Java授权未在测试中更改。

助手若在创建候选与身份落盘之间中断，持久化启动意图保持未知，恢复持续阻塞而不猜测或杀进程；需保留诊断并人工核查。候选身份已记录时，后续启动会在候选退出后允许安全恢复。

但助手仍持有确定结果时会持久化 Absent：没有尝试创建、固定无 shell/重定向/凭据的启动调用直接返回 Win32 创建错误，或自身返回的原进程句柄已确认退出。随后恢复旧版本并正常重启；其他异常/未返回句柄的未知结果不被当作不存在。Recover 不终止进程，也不依据查询失败生成 Absent。
