# 仅发布软件

`publish_software.py` 是软件发行专用入口。它不会调用 `deploy_site.py`，不会更换网站 `current`，不会写网页、样式、脚本、图片或服务器配置，也不安装软件或重载服务。SSH 仅使用 `ssh_connection.connect()` 的固定主机指纹和指定 Ed25519 密钥。

在工程根目录执行（将本地候选目录和证据路径替换为实际位置）：

```powershell
python deploy/publish_software.py --source artifacts/velopack/1.2.17 --preserved-legacy artifacts/preserved-legacy --expected-current /var/www/ozl/sites/20261024T195630Z-d3f3c044 --evidence artifacts/publish-1.2.17.json
```

`--source` 必须是原始的十个已签名发行文件，包含 `velopack-artifacts.json/.sig`；不是镜像根目录。`--preserved-legacy` 包含经过验证的旧 `update-manifest.json/.sig`。公钥默认取工程的 `release-public-key.xml`。所有候选的 RSA 签名、包哈希和索引在建立 SSH 连接前通过现有 `prepare_velopack_release()` 重新验证；额外文件、无签名文件、版本/哈希不一致均拒绝。

写入范围仅为：

- `/var/www/ozl/releases/v<版本>/` 下的十个不可变文件。先暂存、远程核对 SHA-256，再将目录原子改名；已存在的目录必须文件集合及哈希全部相同，才能继续。
- 既有物理网站目录中的 `updates/velopack/latest/releases.win.json`、`velopack-manifest.json`、`velopack-manifest.sig` 及 `release.json`。逐文件原子替换，`release.json` 保留未知顶层字段及 `setup`、`portable` 对象的未知字段，覆盖本次发行信息。
- `/var/www/ozl/test-runs/software-v<版本>-<随机值>/` 内的四个旧文件备份、四个新文件暂存以及必要的回滚暂存。备份不自动清除；证据中的 `backups` 将目标路径对应到备份路径。

旧版更新接口完全保留，并要求其两个文件与提供的已认证旧清单逐字节相同。已有发行文件、所有网站文件及相关 Web/SSH 配置只计算哈希；排除本次允许写入的四个文件、新发行目录及测试记录目录后，发布前后保护快照必须一致。激活前再次验证 `current` 和原有四个文件，并在每个文件原子替换前重新核对该目标的旧字节。发现并发更改立即停止，仅回滚本次已替换的目标，保留其他任务刚写入的内容。显示元数据及当前 Velopack 清单任一版本高于候选版本时拒绝降级。

默认从 `https://orangezl.com` 读取四个更新文件校验字节，并用 HEAD 校验安装包和便携包的公开长度；可以用 `--health-origin` 指定已确认的站点源。健康检查或最终保护快照失败时，仅恢复本次替换的四个文件。如果发现这些文件已被其他发布修改，则拒绝覆盖并在证据中记录 `rollback-failed`。成功回执包含保护快照、发行和更新文件 SHA-256；本地 JSON 证据不应放在候选目录中。

本地安全测试（不连接服务器）：

```powershell
python -m unittest discover -s tests -p test_publish_software.py
```
