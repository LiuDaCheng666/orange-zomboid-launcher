# OZL 发行源码与依赖边界整改

用户确认反复问题是 Defender 报毒／隔离。只读审计确认 1.2.13 安装包、1.2.14 安装包及核心 EXE 有历史检测记录；现有记录未证明具体触发代码。此次整改解决已核实的工程缺陷，不把编译或测试通过称作杀毒复核通过。

## 本轮实施

1. 正式发布不再把整个 C# 目录及 84 个测试文件一起编译。使用明确的 92 项生产清单，将 Program 中测试／开发入口和 Test 类搬入独立诊断编译输入；正常工作进程、UI、游戏行为不变。所有正式构建入口共用清单，诊断构建保留原测试集合和顺序。正式入口明确拒绝诊断参数，不静默打开 UI。
2. 校验实际交付的三个 WebView2 文件，而非仅 Core.dll；缓存存在也必须校验。NuGet 包先按官方包摘要认证再解压，CI 使用同一资料和验证函数。
3. 只进行源码、合成字节测试和不执行输出的 C# library 语义编译。保留原隔离状态，不恢复／运行被拦截文件，不重打安装包，不改 Defender，不执行 CI 或发布更新。

## 已核验的官方依赖

Microsoft.Web.WebView2 1.0.4258.31，官方 catalog:
https://api.nuget.org/v3/catalog0/data/2026.09.28.08.32.55/microsoft.web.webview2.1.0.4258.31.json

官方 nupkg SHA512 Base64 与现有原包逐字节摘要一致：
`HlGVwvyP/IWiXAU8K57Vmg59khY3DWMOxMnH4AHusFRy0/vDAjKItXUIVNXiXXaq4CLfSZyNUic1wOIlnPLsPQ==`

该包内与当前本地三份文件分别匹配的 SHA256：

- lib/net462/Microsoft.Web.WebView2.Core.dll: D60E6E94245078CB56E44EBD5F360DC04BF18E460136BE93BFC4CFCA43B98505
- lib/net462/Microsoft.Web.WebView2.WinForms.dll: 6A9E6856F23C463783C3AFB0CA5D78F56324C86657162CCB16D5227B3233B32A
- runtimes/win-x64/native/WebView2Loader.dll: 3426DCC55FDFB8B5E7AC623CF33B4CCF283FBF68A0C5A65BDDFDB4D749A8ABEE

## 不混入本轮

不改 18 文件更新协议或历史 JAR 路径认证，不重建已退役桥接 JAR，不拆分整个 Engine/MainForm，不改变游戏配置／组件／DLSS 功能或安装自检入口，不修改 VERSION、更新源和官网。旧第三方下载与 legacy 实现继续单独记为后续审查项，不声称本轮已全部清除。

最终验收分别写清：源码边界、依赖认证、非执行编译结果；安装运行、最终二进制恶意软件判定尚未由这些测试证明。不能承诺任何版本永久不再被 Defender 检测。
