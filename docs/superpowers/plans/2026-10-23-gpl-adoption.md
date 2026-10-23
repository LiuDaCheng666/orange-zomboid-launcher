# GPL 许可实施计划

设计：`docs/superpowers/specs/2026-10-23-gpl-adoption-design.md`。工作目录：`E:/Projects/OrangeLauncher`，现有 `chore/signpath-readiness` 分支；不新建工作树。

1. 加入官方完整 LICENSE 与项目适用声明，更新 README 和使用说明。
2. 先编写并运行说明文件生成测试，确认缺少生成脚本时失败；实现独立 `Build-Guide.ps1`，接入正常核心构建和打包测试，不新增核心文件。
3. 运行生成测试、既有 Web 回归与差异检查；核对许可与上游原文一致。审查变更，不构建候选安装器。
4. 更新 SignPath 申请前记录，分别列明许可已采用、签名仍待申请；提交并同步 GitHub。申请表的姓名、邮箱及条款必须有实际授权，不能代填猜测信息或宣称审批成功。
