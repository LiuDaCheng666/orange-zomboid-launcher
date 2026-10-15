# 已批准客户端／服务端识别方案的 Xlog 兼容补正

本补正继续执行已批准的客户端／服务端分开识别及 GitHub 发布方案，不改变安全模型、UI 或安装路径。

只读证据确认当前安装确为正式1.2.5，EXE哈希与正式包相同。实际选中游戏 JRE 的进程入口是 `zombie.network.GameServer`，无原生客户端；原始命令解析为 Unknown。对仅在内存中的同一命令进行参数重编码仍为 Unknown，只删除其中两个 `-Xlog:` 参数则变为 Server，明确缺失选项兼容是当前误报根因。没有输出或保存原始命令及凭据。

按 [Oracle Java17 命令文档](https://docs.oracle.com/en/java/javase/17/docs/specs/man/java.html#enable-logging-with-the-jvm-unified-logging-framework)，支持无额外操作数的 `-Xlog` 及 `-Xlog:` 单参数形式；日志内容或路径中的主类名称不能充当实际主类。其他未知选项、参数文件、Jar入口等仍保持 Unknown，既有 PID／时间／镜像及宽范围写文件保护不放宽。

在固定字面量和真实隔离 WMI 辅助进程中加入日志参数，先红后绿。还要以新版候选对当前服务端做只读检查，实际确认 Server 和 clientRunning=false，不只依赖模拟命令。若当前环境变化为存在真实客户端，应准确区分，不结束任何进程。

发1.2.6，不覆盖1.2.5，保留原品牌、18路径、公钥、封面、旧内部文件名。按既定草稿资源校验→正式latest→公开下载→旧1.2.5实际默认更新服务下载到Ready流程同步 GitHub；不直接更新用户正在运行的安装。
