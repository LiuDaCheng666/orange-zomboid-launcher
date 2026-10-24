# OZL 官网与下载镜像

## 1.2.16 Velopack：官网主渠道

官网继续使用 [orangezl.com](https://orangezl.com/)。1.2.16已于北京时间2026-10-25先部署至自己的服务器，再正式发布到GitHub备用渠道，10个公开附件均已匿名下载核对哈希。当前站点为`20261024T194730Z-a36191ee`，保留此前网站内容及旧版更新入口。新发行保留安装版、完整免安装ZIP，以及程序内检查、下载和明确确认后的一步更新／重启。Velopack仅作为本地安装与更新框架；不使用Flow或其他云托管，也不要求先上传GitHub才能准备官网发布。

### 从认证的本地交付物准备

```powershell
$releaseRoot = Join-Path $PWD 'dist/1.2.16-velopack-candidate'
./Build-VelopackRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory $releaseRoot
python deploy/prepare_release.py --distribution velopack --source "$releaseRoot/release" --preserved-legacy artifacts/velopack-preserved-legacy-1.2.14 --output artifacts/website-mirror/1.2.16-velopack
```

输出必须全新；现有私钥只按路径传给构建，不打印或复制内容。构建恢复中文目录选择向导，默认优先推荐非系统固定盘，声明.NET Framework 4.8及中文Desktop、StartMenuRoot快捷方式，返回app/release两个子目录。release的10项是安装EXE、portable ZIP、full.nupkg、releases.win.json、assets.win.json、RELEASES，以及velopack-manifest和velopack-artifacts各自的JSON／二进制签名。详细文件名与签名职责见[核心发布指南](../docs/core-release.md)。

`--source`必须是使用既有发布身份签名的完整本地release目录。准备工具使用跟踪的公钥验证 `velopack-artifacts.json/.sig` 和更新清单，检查准确集合、版本、长度与SHA-256；拒绝夹带文件和伪造本地断言。新lane不需要伪造公开GitHub API元数据。`--preserved-legacy`必须提供已认证的旧版原始update-manifest.json／.sig，不能只复制未经验证的额外文件，也不需要下载或重建旧辅助EXE。

### 部署与备用渠道

```powershell
python deploy/deploy_site.py --distribution velopack --site website --mirror artifacts/website-mirror/1.2.16-velopack --version 1.2.16 --handover <仓库外交接文档的绝对路径>
```

这是显式发布命令，准备本地候选不会执行它。部署从镜像的不可变release交付物及保留的旧latest重新生成认证输入；凭据仍通过仓库外交接文件读取，SSH固定指纹、TLS验证和失败回退保持。不会把密码填入命令或Git。

待切换站点必须同时包含旧 `/updates/latest/` 和新 `/updates/velopack/latest/`；不可变下载置于 `/releases/v1.2.16/`。旧latest和历史v1.2.14附件保持原字节，不能因切换current丢失。新可变feed使用no-store，版本目录保留不可变缓存策略。发布前确认版本/tag未被其他发布占用，不覆盖历史同名文件。

客户端首先获取本站 `updates/velopack/latest/velopack-manifest.json`，通过不可变版本路径获取签名与完整包；仅传输失败时使用 `LiuDaCheng666/orange-zomboid-launcher` 的canonical GitHub release路径。签名、长度、哈希、格式或版本错误直接失败，不以换源绕过。GitHub备用渠道随后上传本次同一份原始交付字节；不能重新构建另一份包冒充相同版本。安装EXE／ZIP的文件名为 `OrangeZomboidLauncher-win-Setup.exe` 和 `OrangeZomboidLauncher-win-Portable.zip`，由版本目录保证不可变性。

1.2.14及更早客户端第一次迁移仍需手动安装或完整解压新版到新的独立目录，保留原 `%LOCALAPPDATA%/PZ3DManager` 设置；新feed本身不能给旧客户端增加Velopack协议。完成迁移后，安装版和portable都使用新版程序内更新。实际安装／更新／重启与部署结果见[1.2.16验证记录](../docs/releases/1.2.16-validation.md)，未执行的验收不宣称通过。

## 1.2.14及更早的部署历史

**以下余文保留旧站点状态、公开GitHub镜像入口和22附件协议，仅适用于历史发布；新lane使用上面的显式velopack参数和签名本地交付物流程。**

官网：[https://orangezl.com/](https://orangezl.com/)。`www` 与旧 HTTP IP 地址跳转官网。2026-10-23 已配置公网 DNS 与有效 HTTPS；没有跳过证书校验。

当前站点`20261024T121954Z-04feeb54`：C版玻璃导航与轻量动画、新版启动器截图、功能介绍、帮助、文字更新记录、QQ群和源码入口。2026-10-24正式同步1.2.14安装版、免安装版及签名更新，GitHub同版发布；官网22附件和两个最新签名源已核对。实际安装与升级测试见[正式发布结果](../docs/releases/1.2.14-github-release.md)。

## 历史：已部署结构

```text
/var/www/ozl/
  current -> sites/<UTC部署编号>
  sites/<UTC部署编号>/          仅公开网站文件与最新原始签名清单
  releases/v1.2.13/             22 个原始正式发布附件，不覆盖
  releases/v1.2.14/             22 个新版正式发布附件，不覆盖
  config/<UTC部署编号>/         Caddy 配置输入，不在网站根目录
  config-backups/               旧配置与回退资料，不公开
  test-runs/                    独立 Linux 临时测试入口，不公开
```

服务 `caddy` 使用独立的 `caddy` 账户；配置 `/etc/caddy/Caddyfile` 与 `/etc/caddy/ozl-static.caddy`。Caddy 2.11.7 从 Caddy 文档所列官方 COPR 的 RHEL8 仓库安装，RPM 签名检查开启；没有关闭 TLS/GPG 校验。日志 `/var/log/caddy/ozl-access.log`，滚动保留 5 份、7 天，系统服务诊断在 journal。

没有修改宝塔、云防火墙、SSH 服务或云主机规格；没有开放 8888、购买服务或重启服务器。

## 历史：新版本镜像

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

启用前以 `caddy` 账户验证配置，避免 root 校验时生成不可写的日志。正常部署默认选择 `Caddyfile.domain`，并通过 `curl --resolve orangezl.com:443:127.0.0.1 https://orangezl.com/release.json` 检查真实 TLS。仅显式 `--ip-acceptance` 用于域名上线前的 HTTP 验收，不应对正式站使用。校验、reload 或健康检查失败时，恢复旧配置和上一份 `current`。历史版本目录保留，允许手动回退；不要递归删除网站根目录。

## 历史：域名和程序内更新

1. 域名 `@` 与 `www` 的 A 记录均为 `101.201.143.163`，TTL 600 秒，公共 DNS 已确认；原 `pz` 记录未改。
2. 从独立公共 DNS 确认解析后，备份当前配置，使用随部署保存的 `Caddyfile.domain`，以服务账户验证，再 reload。Caddy 申请有效 HTTPS 证书；不跳过证书检查。
3. 从外网检查 HTTPS、`www` 跳转、两个下载、最新签名清单与所有资源哈希。
4. 在新启动器版本中接入本站优先、GitHub 备用；保持原签名、哈希、版本与自有路径校验。HTTPS 未验证前不发布切换默认源的程序。

已经安装的 1.2.13 仍读取 GitHub，本次已实际检查并完整下载1.2.14至Ready。升级至1.2.14后使用官网优先、GitHub备用；新版生产服务已实际从官网检查到Latest。保持原更新签名身份，不修改历史Release或已签名清单，不用无签名`version.json`替代更新协议。

## 历史：发布与安全记录

源码：[GitHub](https://github.com/LiuDaCheng666/orange-zomboid-launcher)。当前正式版为v1.2.14，保留v1.2.13不可变下载以供旧版本依赖和回退。

本次新UI安装包、原始编译目录和全部交付文件实际扫描未发现威胁，防病毒及实时保护保持开启。微软两份历史样本的受理／检测范围与本次不同，不能当作本版人工认证；精确哈希和范围见[正式发布记录](../docs/releases/1.2.14-github-release.md)及[历史安全记录](../docs/security/2026-10-24-actual-candidate-detection.md)。
