# 2026-10-27 安装向导依赖边界与真实操作验收

本次仅收敛中文安装 wrapper 的编译输入，保留原安装 UI、非系统盘推荐、路径与重解析检查、21 项 RSA bootstrap 和 Shell Link 读取实现。主程序源文件未改。正常安装仍委托嵌入的官方 Velopack 1.2.161 Setup；wrapper 不再编入 Steam、游戏、WebView2、旧安装/卸载及旧更新事务实现。

## 文件范围

- `build/Build-VelopackSetup.ps1`：显式九个源文件的 allowlist；仅引用 BCL；调用参数化 `Build-Version.ps1 -VersionFile/-OutputPath`；独立临时 identity/版本源码，结束时清理。
- `Orange3DTool/InstallerOnly/`：四份安装专用源码，同命名空间编译。从原文件机械提取 policy/models、路径/嵌入公钥/有界读取、RSA bootstrap、只读快捷方式解析；不将这些副本编入主程序。
- `tests/VelopackSetupBoundary.test.ps1`：编译真实 wrapper，检查程序集依赖与入口类型、版本资源及嵌入 payload 原始字节。
- `tests/New-VelopackSetupFixture.ps1`、`tests/VelopackSetupIsolation.test.ps1`、`tests/VelopackSetupValidation.test.ps1`：独立安装 fixture、真实文件结果及原验证约束。
- `tests/VelopackInstallerFailure.test.ps1`：失败 fixture 改用唯一 appID/title，并输出证据目录。

## RED → GREEN 与针对性检查

回归测试先在旧 wrapper 上实际失败，发现 Program、Engine、SteamWorkshop、WorkshopWorker、旧 CoreUpdater/Installer/Uninstaller 等类型，Velopack/WebView2/System.Management 引用和 PowerShell 自删除方法；传入 VERSION=7.8.9 也没有进入程序集/文件/产品版本。

首次 RED 记录：`artifacts/setup-boundary-c057d226c0214ea8b4549dac3cd0f5fb/failures.txt`。

修改后新编译的回归测试通过，版本为程序集/文件 `7.8.9.0`、产品 `7.8.9`，官方 payload 原始字节不变。最后一次 GREEN 位于 `artifacts/setup-boundary-abd2b8e877cc4d23b04ca5a307f21b6b/`。

原验证约束实际通过：空目录保留准确路径；非空目录、盘根、游戏目录标记、真实 junction 均按原原因拒绝且保留已有文件；有界读取拒绝过长文件；嵌入公钥匹配；完整 21 项 RSA bootstrap 通过；篡改 Web/app.js 或 RSA 签名分别按对应原因拒绝。

最后一次约束检查：`artifacts/setup-isolation-221efe93d3c241e987bf104d841a5ab7/validation-results.log`。这些检查使用 .NET Framework PowerShell 执行 wrapper 的原验证方法，不作为 UI 验收替代品。

## 真实点击、原生安装与文件结果

使用 Computer Use 操作真正的 WinForms 控件和原生目录选择器，实际查看并保存页面截图。唯一 appID 为 `OzlSetupOnly221efe93d3c2`，中文 title 为 `橙子安装验收-221efe93d3c2`；版本 `7.8.9`。所有目标仅在 `E:\AppCaches\Temp\ozl-setup-221efe93d3c241e987bf104d841a5ab7` 下。未操作 `D:\OrangeLauncher`、游戏、Steam、网站或现有应用注册项/快捷方式。

正常安装 payload 由固定官方 vpk 生成，应用为仅处理 Velopack 生命周期并写一个显式启动 marker 的惰性测试程序。21 个应用槽位均进入真实 RSA 清单；fixture RSA 仅内存生成、未保存私钥，不使用正式发布密钥。

| 实际操作 | 原生/文件结果 | 页面结果 |
| --- | --- | --- |
| 默认页、打开原生目录选择器并取消 | 推荐 E 盘，取消不创建目录 | 路径与安装按钮保持可用 |
| 输入准确独立路径，保留默认桌面勾选，点击安装 | 官方 Setup 完成；21 项文件逐项长度/哈希相同；唯一桌面/开始菜单快捷方式和注册项指向 checked 目录；未自动启动应用 | 立即显示活动进度，最后显示安装完成、桌面已创建、打开/完成按钮 |
| 点击“打开启动器” | 真实启动 checked/current 内的惰性 fixture，写入准确 marker | 保持完成页，无打开失败状态 |
| 点击完成，调用唯一目录官方 Update.exe 卸载 | 唯一注册项、桌面/开始菜单快捷方式及主程序实际删除 | wrapper 正常关闭 |
| 重新打开，输入 unchecked 路径，实际取消桌面勾选后安装 | 官方安装成功，21 项匹配，开始菜单存在而桌面快捷方式不存在；随后官方卸载清理通过 | 页面明确显示已跳过桌面快捷方式，完成按钮可关闭 |
| 独立原生失败进程实际返回退出码 7 | 保留包含 fixture-native-error 的 setup.log，临时释放的 Setup.exe 清理；没有目标安装或注册项 | 显示退出码/保留日志，零进度、可关闭；未显示安装成功 |
| 重新打开，输入 cancelled 路径，打开并取消原生 picker，最后关闭安装器 | cancelled 目录、唯一注册项和快捷方式均不存在 | picker 取消后返回准备状态，按钮可用，关闭正常 |

错误路径的退出码 7 payload 是明确的测试进程，用于验证 native 失败传播；不把它描述为官方 Setup 本身发生故障。没有在安装进行中提供或测试中止能力，保持现有不可取消部署行为。

## 截图、结果及边界

证据根目录：`artifacts/setup-isolation-221efe93d3c241e987bf104d841a5ab7/`。已经实际查看以下截图，中文标题、完整状态、按钮和进度均可辨认；长目标路径会换行，路径框可横向编辑。未更改现有布局/文案。

- `checked-ready.jpg`、`checked-installing.jpg`、`checked-complete.jpg`、`checked-opened.jpg`
- `unchecked-ready.jpg`、`unchecked-installing.jpg`、`unchecked-complete.jpg`
- `native-failure.jpg`、`cancel-folder-picker.jpg`、`cancel-returned-ready.jpg`

原生结果见 `results.log`，失败原始日志副本为 `native-failure.log`；失败编译/后端检查另见 `artifacts/installer-failure-ae5797f299c4451abe52b8fb02054a38/`。完成后检查无本次 wrapper/fixture 进程存活，正常安装的唯一注册项/快捷方式已由官方卸载清理；保留测试文件和截图供审计。

本轮验收 wrapper SHA-256：`CC9DCB2369DC889D21BF52D4E46AC34BBF49AC1385D44C366C41637F8C75032C`；嵌入官方 Setup SHA-256：`A8D983C535C156E0E72FF1D7727E527F346E2A6909AD59A2E34107AFF4F6DEA7`。

本轮没有运行 Defender 扫描、没有测试正式主程序/游戏功能、没有正式证书签名，也没有更新/发布/上传。以上结果证明安装 wrapper 的依赖收敛及对应隔离安装流程；不能据此宣布 Prowloc 检测已根治、正式 Authenticode 已完成或正式产品全流程已验收。
