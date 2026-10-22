# OZL 官网与下载镜像

当前 IP 验收站：[http://101.201.143.163/](http://101.201.143.163/)。正式域名为 `orangezl.com`；DNS 与 HTTPS 尚未启用，不将 HTTP 作为程序自动更新可信传输。

## 已部署结构

```text
/var/www/ozl/
  current -> sites/<UTC部署编号>
  sites/<UTC部署编号>/          仅公开网站文件与最新原始签名清单
  releases/v1.2.13/             22 个原始正式发布附件，不覆盖
  config/<UTC部署编号>/         Caddy 配置输入，不在网站根目录
  config-backups/               旧配置与回退资料，不公开
  test-runs/                    独立 Linux 临时测试入口，不公开
```

服务 `caddy` 使用独立的 `caddy` 账户；配置 `/etc/caddy/Caddyfile` 与 `/etc/caddy/ozl-static.caddy`。Caddy 2.11.7 从 Caddy 文档所列官方 COPR 的 RHEL8 仓库安装，RPM 签名检查开启；没有关闭 TLS/GPG 校验。日志 `/var/log/caddy/ozl-access.log`，滚动保留 5 份、7 天，系统服务诊断在 journal。

没有修改宝塔、云防火墙、SSH 服务或云主机规格；没有开放 8888、购买服务或重启服务器。

## 新版本镜像

先下载准确 tag 的正式附件及该 Release 的公开 API 元数据。不能用测试包替换正式文件，也不能重新生成历史清单的签名。

```powershell
gh release download v1.2.13 --repo LiuDaCheng666/orange-zomboid-launcher --dir artifacts/website-release-source/v1.2.13 --skip-existing
python deploy/prepare_release.py --release-metadata artifacts/publication-1.2.13/github-published.json --source artifacts/website-release-source/v1.2.13 --output artifacts/website-mirror/v1.2.13
```

API 元数据需保留 `tag_name`、`draft`、`prerelease`、`published_at` 与带 SHA-256 digest 的全部 `assets`。工具先核对所有公开文件，再使用仓库公钥验证清单 RSA/SHA-256 签名，核对 18 个自有路径及每个资源版本、长度与哈希。成功后才生成新输出目录和展示元数据；输出已存在则拒绝覆盖。

初次安装需要 `--bootstrap`，会安装签名仓库中的 Caddy；之后不带此参数。Paramiko 必须在本地 Python 环境可导入。凭据只从仓库外交接文档读取，绝不能把密码填在命令参数或记录在 Git。

```powershell
python deploy/deploy_site.py --site website --mirror artifacts/website-mirror/v1.2.13 --release-metadata artifacts/publication-1.2.13/github-published.json --handover <仓库外交接文档的绝对路径>
```

SSH 在发送凭据前固定校验用户确认的 ed25519 指纹 `SHA256:3Kj/0G9bg0xadE8/EjVY49yZEfuSF5iq3qDubXCgYIg`。指纹变更必须从可信控制台核对，不能自动接受。部署再次生成独立已验证目录，只上传该目录的允许文件；原输入中的额外文件不上传。不可变同名文件若已存在，要求远程哈希一致，不覆盖。

启用前以 `caddy` 账户验证配置，避免 root 校验时生成不可写的日志。切换 `current` 后执行服务与 HTTP 检查；校验、reload 或健康检查失败时，恢复旧配置和上一份 `current`。历史版本目录保留，允许手动回退；不要递归删除网站根目录。

## 域名和程序内更新

1. 将域名 `@` 与 `www` 的 A 记录都设为 `101.201.143.163`。当前公共解析尚未查到这两条地址记录。
2. 从独立公共 DNS 确认解析后，备份当前配置，使用随部署保存的 `Caddyfile.domain`，以服务账户验证，再 reload。Caddy 申请有效 HTTPS 证书；不跳过证书检查。
3. 从外网检查 HTTPS、`www` 跳转、两个下载、最新签名清单与所有资源哈希。
4. 在新启动器版本中接入本站优先、GitHub 备用；保持原签名、哈希、版本与自有路径校验。HTTPS 未验证前不发布切换默认源的程序。

已经安装的 1.2.13 仍读取 GitHub。官网上线和提供镜像，不等于旧版自动更新已切换。此次不修改历史 Release 或已签名清单，不用无签名 `version.json` 替代更新协议。

## 发布与安全记录

源码：[GitHub](https://github.com/LiuDaCheng666/orange-zomboid-launcher)。本次官网没有更改启动器版本，下载保持正式 v1.2.13。

公开安装包于 2026-10-23 完成当前 Defender 扫描，退出码 0、未发现威胁；微软复核提交 `1636b467-f12f-4f5d-881e-067321fee1fb` 已受理，状态为分析中。微软页面云端与客户端当前检测均显示未发现恶意软件，最终判定仍等待完成。精确样本、病毒库与源代码/CI 关联见[安全记录](../docs/security/2026-10-23-defender-review.md)。不把当前检测称为人工认证或对后续版本的永久许可。
