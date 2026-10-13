# 1.1.0 本地签名发布验证

状态：本地构建与测试完成；未发布线上渠道，未推送或合并 main。此记录描述本地已验证成品，不声明游戏内兼容性。

最终输出目录：`E:/Projects/OrangeLauncher/dist/1.1.0-verified/`。早先的 `artifacts/core-signed-verification-1.1.0/` 和 `dist/1.1.0/` 保留为开发中间验证证据；不要将其与最终目录混用。

| 产物 | 字节 | SHA-256 |
|---|---:|---|
| orange-launcher-setup-v1.1.0.exe | 5797888 | DB64C012D7A73E01A468450ED48F82BC8FEFC8CCAA9F66C897946AD30491DE72 |
| orange-launcher-v1.1.0-win-x64.zip | 4671484 | 641D1783CC3019695FCA3CAA200744BF4069D6A2466324B316A590FD01BB5456 |
| update-manifest.json | 6880 | 5F6FAA8CCDAC7DF6D8EE93DF22EF1511AA9F31AFF5FF21490A835E99E1EF6275 |
| update-manifest.sig | 384 | AAEDD7B0BCAC251188B33B3FBE56DA4CD8B1091703F092B96A6D8729A541489E |

构建命令：

```powershell
./Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.1.0-verified
```

使用现有受保护私钥，没有覆盖、打印、提交或向第三方发送私钥。签名对象是清单的原始字节；不声称 EXE 有 Windows Authenticode 签名。三个 EXE 的 FileVersion 为 1.1.0.0，ProductVersion/嵌入 AppVersion 为 1.1.0；磁盘 VERSION、签名清单及实际 ZIP 首启安装记录也为 1.1.0。清单恰好18个允许文件，逐文件完整哈希见同目录 update-manifest.json。

验证结果：

- 正式安装 EXE `--verify-setup` exit 0：实际空目录提取、全量签名文件验证、版本记录与自有卸载保留未知文件。
- 最终 ZIP 解压到 `artifacts/TestFixtures/signed-final-ui/` 后，实际正式 EXE `--modern-preview <截图> --live-read --page help --theme dark|light --size 820x600` 均 exit 0；截图使用隔离 PreviewData，未访问真实游戏会话。原生面板显示1.1.0；检查是手动的，此截图状态为尚未检查。
- `Test-CoreNativeUpdate.ps1` 最终隔离根 `artifacts/TestFixtures/native-update-37da1e2a/`：真实 WebView2 的健康成功、故意坏页面45秒超时回滚、旧页面重新启动一次、两条路径的自有卸载全部通过。只下载 app.js；所有游戏/用户/data为合成目录。
- 稀疏 fixture A 清单/记录/VERSION/EXE均1.1.0；fixture B 清单/记录1.1.1，但没有改变的 VERSION/EXE仍1.1.0。它只证明稀疏文件下载及重启协议，不可当作发布成品。真正更改 VERSION 的新版本通常需要完整替换小 EXE，不是二进制差分。
- 原生 baseline exit0；协议79、事务149、包17、服务41、助手28断言通过。所有现有浏览器套件与新增核心更新套件通过：full21/21、compact19/19及 ui/profile/java-cover/extensions/inherited-layout。
- 对真实官方无凭据 latest API 的最后一次检查收到403 rate-limit。此前任务记录的 Release 列表为空；这次无法重新确认网络状态。产品显示读取失败或404时的不可用，绝不把失败当作“已是最新”。没有发布用于绕过验证的fixture渠道。

截图（已实际查看）：

- `artifacts/core-update-shots/final-native-dark-820.png`
- `artifacts/core-update-shots/final-native-light-820.png`
- `artifacts/core-update-shots/native-signed-live-dark.png`、`native-signed-live-light.png`（首包1000×720详情）
- `artifacts/core-update-shots/dark-820.png`、`light-820.png`（浏览器合成Ready/草稿/占用场景）

820×600高DPI沿用既有三列导航；更新操作靠近版本行，检查按钮完整可见。更多说明/发布说明可在内容区滚动阅读，无横向溢出。

运行限制：需系统已有 WebView2，不安装运行库、不提权、不写注册表。运行时任务、校验缓存、备份与日志保留，可能逐渐占用磁盘；失败安装/卸载后重装请选择新的空目录。传统.NET长路径仍可能失败。只终止助手自己启动且身份匹配的失败候选；不能释放候选时保留 Conflicted 状态，不在活跃候选下回滚。游戏、Steam订阅、真实存档和Java授权未在测试中更改。
