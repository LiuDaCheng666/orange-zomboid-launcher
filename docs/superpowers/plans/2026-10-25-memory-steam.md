# 内存与 Steam 兼容修复 Implementation Plan

> **For agentic workers:** 按用户已批准范围在当前目录修复分支内顺序执行；不重复确认，不开前台窗口。

**Goal:** 修复真实 Steam VDF 中的文字制表符误判，解除内存读写与 Steam 的耦合并发布 1.2.15。

**Architecture:** `SteamLaunchOptions.Vdf.Quoted` 接受三种原始空白字符；`Engine.GuardHeap` 与控制器内存文件快照不读取 Steam。UI 和更新协议保持不变。

**Tech Stack:** C#/.NET Framework、既有原生诊断、PowerShell、签名更新清单、GitHub 和 Caddy 镜像部署。

## Global Constraints

- 后台运行，不写入或上传玩家 Steam VDF；真实配置验证结束恢复原值。
- 只改解析兼容与内存读写，不改游戏、服务器或模组顺序。

### Task 1: 先复现再修复

**Files:** `SteamLaunchOptionsTests.cs`、`ModernControllerTests.cs`、`SteamLaunchOptions.cs`、`Program.cs`、`ModernController.cs`、`LauncherMaintenance.cs`。

- [x] 添加 quoted TAB/CR/LF 原字节保留测试和 `memoryGet/memorySave` 不访问 Steam 的真实控制器测试。
- [x] 使用 `Build-Tests.ps1 -Entry PZ3D.SteamLaunchOptionsTests` 的 `--literal-whitespace` 与 `PZ3D.ModernControllerTests --memory-independence` 观察旧代码失败。
- [x] 允许 `c == '\t' || c == '\r' || c == '\n'`；新增 `GuardHeap()`，内存调用它而非统一 Steam Guard；内存 stamp 删除 Steam 字段。
- [x] 重跑专项、完整原生、内存 UI 和完整 UI；独立只读代码审查。

### Task 2: 真实配置与交付验证

- [x] 本机 VDF/启动 JSON 私有副本保存与重读；核对 VDF 不变。
- [ ] 正常构建一个 1.2.15 候选并扫描；使用候选生产控制器后台验证正式 JSON 12→8→12，核对其他参数和恢复原字节。
- [ ] 安装器验证、1.2.14→1.2.15 签名事务与回退验证，记录准确字节。

实际进度：候选构建和扫描已执行；未检出启动器的生产控制器对正式 JSON 验证成功并恢复。更新助手／卸载程序被检测隔离，整包安装升级未验收，因此以上交付项保留未完成，不发布候选。

### Task 3: 发布与线上验证

- [ ] 推送源码，发布 GitHub 稳定版 22 附件；签名同批字节镜像至官网。
- [ ] 用旧版实际更新服务检查并下载至 Ready，核对官网版本、全部附件、签名与界面测试；保留历史版本。
