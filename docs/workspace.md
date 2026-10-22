# OZL 项目工作目录

维护入口：`E:/Projects/OrangeLauncher`。将这个文件夹单独添加为本地项目即可，同时维护启动器与官网；无需复制现有 Git 仓库。

| 目录 | 用途 |
| --- | --- |
| `Orange3DTool/` | 启动器、原生安装器、Web 界面与现有功能测试 |
| `website/` | 官网静态源码、原创图标、沿用的启动器封面和展示版本元数据 |
| `deploy/` | 签名发布镜像核验、自有服务器部署、Caddy 配置与操作说明 |
| `tests/` | 官网、镜像及部署的回归测试 |
| `docs/` | 设计、发布、验证、安全复核及维护记录 |
| `artifacts/` | 忽略的临时测试结果、截图、公开产物副本和核验资料 |
| `dist/` | 忽略的历史及候选构建，保留发布证据，不作为源码 |
| `Dependencies/` | 忽略的本机构建依赖 |

2026-10-23 整理：将根目录下的 `profile-ui-shots`、`ui-browser-shots`（共 98 张生成的 PNG）移入 `artifacts/workspace-archive/2026-10-23/`，未永久删除。恢复时可移回同名根目录。其他项目、历史发布产物、Git 历史、游戏文件和用户配置未删除。

私钥继续保留在仓库外，不上传服务器；服务器密码只从原交接文档按需读取，不复制到本项目。网站部署不包含任何用户日志、服务器角色资料、登录凭据或测试目录。

## 常用验证

```powershell
node --test tests/website.test.cjs
python -m unittest discover -s tests -p test_prepare_release.py -v
python -m unittest discover -s tests -p test_deploy_site.py -v
```

网站测试使用已有 Playwright 依赖并保持无头运行。镜像测试需要 `cryptography`（现有开发环境已提供）；发布准备工具只使用 Python 标准库。部署器在本地使用 Paramiko，激活回滚测试在 Linux 的临时目录中运行，服务命令均为替身，不修改真实服务。

官网与下载镜像：[部署说明](../deploy/README.md)。程序内更新的旧版默认源仍是 GitHub；新默认源需要域名和 HTTPS 验证后单独发布，不能仅靠改网页影响已经安装的旧程序。
