# 网页更新记录发布

软件发版需要同时保留网页上的版本历史。此步骤在自有服务器和 GitHub 均正式发布后执行，使用已经发布的版本号、仓库 `CHANGELOG.md` 和 GitHub 正式发行时间（转换为北京时间）。未发布版本不会加入网页。

```powershell
python deploy/publish_changelog.py --version 1.2.31 --expected-current /var/www/ozl/sites/20261024T195630Z-d3f3c044 --evidence artifacts/changelog-sync/receipt.json
```

实际操作时替换版本和证据路径；先用 `python deploy/check_ssh.py` 核对当前物理站点，不猜测或切换 `current`。连接继承工程规定的固定指纹和 Ed25519 密钥。

工具重新读取线上 `changes.html`，只补齐新增卡片并将上一版改为历史版本。它保留线上导航、SEO、页面结构、样式引用、页脚及已有历史正文，不从本地网站目录覆盖页面。旧页面备份保存在回执的 `backup` 路径；候选、备份、回滚文件均位于 `/var/www/ozl/test-runs/`。

发布在服务器上持有专用锁，核对当前站点、发行版本和待替换内容的哈希后原子替换；随后验证公网 `/changes` 的字节及其他网站和配置文件未变化。失败时仅回滚仍属于本次发布的内容；检测到他人的新编辑就保留新编辑并报错。专用锁协调本工具的发布者，不锁住其他管理员的编辑工具。

确认回执 `status` 为 `published` 或 `unchanged`，打开 `https://orangezl.com/changes` 核对最新版本、日期和历史记录，抽查桌面与窄屏布局。本地 `website/changes.html` 同样只更新版本内容；未来独立的网站部署也不能用旧更新记录还原线上历史。

若检测到并行编辑、版本变化或网页结构变化，重新读取并核对后再执行，禁止跳过校验、整站上传或恢复旧版页面。
