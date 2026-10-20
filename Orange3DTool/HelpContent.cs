namespace PZ3D {
 static class HelpContent {
  public static readonly string[] Titles={"01 · 第一次使用","02 · 额外 3D 物件包","03 · 选择和导入光影","04 · 下载与更新","05 · DLSS 5 实验功能","06 · 关闭与人工恢复","07 · 常见问题","08 · 声明与版权","09 · 首次 Java 模组授权","10 · 中文设置与同步"};
  public static readonly string[] Bodies={
   "第一次使用\r\n\r\n1. 安装版按向导完成安装；免安装版解压到固定文件夹后运行 OZL。系统需要 64 位 Windows 和 .NET Framework 4.8。\r\n2. 登录拥有《僵尸毁灭工程》的 Steam。启动器自动查找游戏；也可选择包含 ProjectZomboid64.exe 和 ProjectZomboid64.json 的游戏目录。\r\n3. 在主界面勾选需要的组件后点击“安装”。\r\n4. 启动游戏，允许需要使用的 Java 模组文件并重启，再按 O 开关 Viewpoint 视角。\r\n5. 通过 Steam 启动游戏，在游戏菜单选择单人存档或多人服务器。",
   "额外 3D 物件包\r\n\r\n为 Viewpoint 提供家具、家电、栅栏和街景等物件的立体模型。它是可选组件，基础视角可独立使用。\r\n\r\n勾选额外物件包并点击“安装”，自动下载所需依赖。只勾选物件包后点击“卸载”可关闭此项，保留基础视角和下载文件。\r\n\r\n工坊 ID：3810302175；模组 ID：PZVoxelStudioViewpoint；原模型作者 sour_kisel。",
   "选择和导入光影\r\n\r\n核心安装保留当前光影选择。打开“光影列表”，选择已安装光影或导入 ZIP；下次启动应用。\r\n\r\n光影目录：Windows 用户目录 / Zomboid / viewpoint-shaderpacks。导入 ZIP 的根目录需要包含 shaders/。游戏内也可按 Delete 打开 Viewpoint 设置切换。\r\n\r\n出现黑屏或画面异常时，选择 Default、Normal 等内置效果后重新启动。",
   "下载与更新\r\n\r\n在“更新与下载”点击“检查并下载所需更新”。已下载且版本相同的文件会复用。下载任务显示字节进度、用时及结果。\r\n\r\n启动器本体：下载新版安装包或免安装包，退出后更新程序文件。个人存档与启动器数据保留。\r\n\r\nSteam 下载失败时，检查登录、游戏所有权、网络和磁盘空间，查看任务记录中的错误后重试。",
   "DLSS 实验功能\r\n\r\n第三方 OpenGL 注入组合，面向 RTX 50 系列，通过 ReShade、运动估计与神经渲染组件工作。\r\n\r\n1. 在画面设置中启用 DLSS 实验组件，完成下载后重启游戏。\r\n2. 按 Home 打开 ReShade，启用 Lumenite_Kernel 和 DLSS5_Feed，顺序为前者在前；DLSS5_MV_PROVIDER 设为 3。\r\n3. 在 Add-ons / DLSS 5 Neural Rendering 面板开启神经渲染，并配置场景深度及运动向量。\r\n4. Waiting、计数为 0 或 feature 18 create failed 表示需要检查插件设置与日志。\r\n\r\n出现黑屏、崩溃或拖影时，关闭此实验功能并重启游戏。",
   "关闭与恢复组件\r\n\r\n在主界面勾选需要撤销的组件，点击“卸载”。重新勾选并点击“安装”可重新安装。\r\n\r\n光影 ZIP、下载文件和个人存档保留。文件操作及备份可在安装记录、维护与诊断中查看。数据目录：%LOCALAPPDATA% / PZ3DManager。\r\n\r\n人工恢复时，按对应备份目录中的说明恢复指定文件；恢复整份备份会覆盖之后的设置。",
   "常见问题\r\n\r\n下载失败：检查 Steam 登录、网络、工坊权限和磁盘空间，查看任务日志后重试。\r\n\r\n视角开关：默认 O 开关第一人称，Shift+O 切换第一／第三人称，Delete 打开 Viewpoint 设置。自定义键位在画面设置中查看。\r\n\r\n运行时修改：可以保存配置、切换组件和光影，通常下次启动应用。文件访问失败时，关闭占用该文件的程序后重试。\r\n\r\n旧 Buddy 启动参数冲突：在“维护与诊断”处理旧启动参数，再继续安装组件。\r\n\r\n画面卡顿：按场景降低模型密度、阴影和体积光，或选择较轻的光影包。",
   LegalContent.Statement+"\r\n\r\n本工具不是游戏、Viewpoint、ZombieBuddy 或 NVIDIA 官方产品。工具的界面与管理逻辑不改变任何上游许可证，也不授予公开再分发权。\r\n\r\n游戏、Viewpoint、ZombieBuddy、额外模型、各光影、ReShade、Feeder、LumeniteFX、RenoDX 与 NVIDIA DLL 的版权分别归原作者和权利人。详细原始许可可在“声明与版权”页面打开，文件已随单 EXE 内置并可离线查阅。\r\n\r\nViewpoint / Buddy / 额外物件模组本体不内置，按需从原工坊下载。实验 DLL 不内置，用户主动启用时才下载。正式核心包不包含第三方光影；个人导入资源的使用和分发应遵守作者许可。\r\n\r\n帮助只内置在软件中，无需联网、无需旁边的说明文档；可用“复制本章”保存需要的文字。"
   ,"Java 模组授权\r\n\r\nJava 模组能够执行代码并访问本机文件。在游戏授权窗口选择你信任的文件并允许。\r\n\r\n模组更新后，变化的文件需要重新授权。\r\n\r\n游戏中的授权窗口可选择 Allow（允许），勾选 Save decisions to disk（保存决定），再点 OK。按文件授权无需勾选 Trust author（信任作者）。\r\n\r\n完成后重启游戏，按 O 开启 Viewpoint 视角。"
   ,"中文设置与同步\r\n\r\n画面设置按分类展示 Viewpoint 参数，包含中文名称、用途、当前值和原始键名。修改后点击保存，下次启动应用。\r\n\r\n游戏内保存设置后，启动器自动重新读取。若有未保存修改，会保留草稿并提示文件变化；重新读取后即可继续编辑。\r\n\r\n光影包的高级参数和画质预设可在游戏内按 Delete 调整。尚未生成配置时，先启动游戏并打开 Viewpoint 设置。\r\n\r\n文件备份位于启动器数据目录，可在维护与诊断中打开。"
  };
 }
}
