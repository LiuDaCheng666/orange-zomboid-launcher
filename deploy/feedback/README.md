# 私有反馈收集器

独立服务，仅监听 `127.0.0.1:18082`。唯一公开路由是 `POST https://orangezl.com/api/feedback`；无公开查看、列举或管理接口。实际安装须经本次审查后执行，准备文件和测试不会自动发布。

## 数据契约

请求为 UTF-8 JSON，最多 262144 字节。只允许以下完整字段：

```json
{"schema":1,"requestId":"0123456789abcdef0123456789abcdef","createdUtc":"2026-10-26T08:00:00.1234567Z","appVersion":"1.2.3","description":"用户明确提交的问题描述","categories":[],"files":[],"system":null}
```

- `requestId`：32 位小写十六进制；`createdUtc`：有效 UTC 日期，`YYYY-MM-DDTHH:mm:ss[.1至7位小数]Z`。
- `description`：非空且非纯空白，最多 2000 字符；`appVersion`：非空、无控制字符，最多 32 字符。
- `categories`：去重的 `gameLog`、`launcherLog`、`system` 子集，可以为空。服务端用类别与内容的一致性限定已选信息，不接受额外 `consent` 或任意字段。
- `gameLog` 对应且仅对应 `files` 中一份 `{"name":"console.txt","text":"..."}`；`launcherLog` 对应一份 `launcher.log`。每份最多 65536 字符，最多两份，只接受固定文件名及文本，不接受路径、附件、二进制或解包参数。
- 未选 `system` 时必须为 `null`；选中时必须包含全部且仅包含 `os,architecture,cpu,logicalProcessors,totalMemoryGiB,gpu,vramGiB,driver`。所有值可为 `null`；`os/cpu/gpu` 为最多 256 字符文本，`architecture` 最多 32，`driver` 最多 128。`logicalProcessors` 为 1–1024 整数；内存和显存为 0–65536 的有限数值，布尔值不作为数字。
- 首次成功返回 201，完全相同的请求字节重试返回 200，均为 `{"id":"...","receivedUtc":"...Z","status":"received"}`。同 ID 不同字节返回 409。因此客户端重试必须复用原 ID 和原序列化请求字节。
- 数据无效 400、过大 413、内容类型或编码不支持 415、限额 429、存储上限 507、存储损坏/不可用 503。响应 `Cache-Control: no-store`；限额/繁忙包含 `Retry-After`。

客户端在用户查看预览并明确提交后才调用此路由。服务器不读取客户端路径，不发起游戏或 Steam 操作。

## 私有存储与限额

`/var/lib/ozl-feedback` 及 `reports` 为 0700，服务账户 `ozl-feedback` 所有。报告、SQLite 索引及 HMAC 密钥为 0600。报告名固定为 `<requestId>.json`，包含用户提交的原 JSON；索引只存回执、时间、大小、摘要与 IP 的 HMAC 标签。密钥仅存本机，不上传到工程，不保存或输出连接 IP 明文。

每 IP 滚动一小时最多接受 6 份；全站每 UTC 日最多 1000 份；最多 5000 个回执。相同请求重试不再次计算服务端配额。保存 30 天，在启动、提交、管理读取和每小时清理过期报告/索引；进程停止时不会主动清理，启动即补清理。客户端时间不参与限额或保留期计算。

总存储硬上限为 256 MiB；为最多各 8 MiB 的 SQLite 数据库/回滚日志及密钥、目录项等预留 17 MiB，报告共享余下约 239 MiB，按实际分配块及 4 KiB 向上取整中的较大者计费，避免大量小文件绕过空间上限。已存在但未提交的中断文件在启动时清理；回执存在而内容丢失/被改写时拒绝读取或去重，不覆盖证据。最多四个请求线程，连接读写有 10 秒超时。

反馈专用 Nginx 精确路由：请求体 256k、每 IP `2r/m burst=3`、每 IP 两个并发、请求缓冲开启、上游读取超时 20 秒，覆盖 `X-OZL-Client-IP` 为已由 Caddy/防护层校验的 `$remote_addr`。该路由关闭 Nginx 访问/错误日志；收集器不记录请求 IP 或内容。原 Caddy 与其他已有日志策略保持现状。

## 部署与回滚

所有服务器连接均复用 `deploy/ssh_connection.py` 固定主机指纹和唯一密钥，不搜索凭据、不开密码认证。默认只读审计：

```powershell
python deploy/feedback/remote.py audit
python deploy/feedback/remote.py check --expected-nginx-sha256 <已核对的64位哈希>
```

`check` 在实际 Python 上只编译源代码，在匿名内存文件中将新增 include 合并后运行 Nginx 语法检查；日志与 pid 指向设备，不上传文件、不写线上配置、不监听端口、不重载服务。

只有本次部署审查通过后才执行以下命令；从刚完成的审计复制实际哈希，不能用旧模板覆盖配置：

```powershell
python deploy/feedback/remote.py install --expected-nginx-sha256 <已核对的64位哈希>
```

仅首次安装。安装器先备份当前 Nginx 配置、当前站点/Caddy 内容压缩包及当前站点、全部发行文件、站点根文件、Caddy 文件的 SHA-256 清单，存入 `/var/backups/ozl-feedback/<时间-随机标识>/`。然后创建专用服务和私有存储；程序/include 目录显式为 0755，非敏感源码、单元、include 文件显式为 0644，私有目录/备份仍为 0700/0600，不受进程私有 umask 影响。在实际当前配置中只增加一个 http include 和两个监听器各一个 location include。发布前再次核对原配置哈希；`nginx -t` 后才 reload 防护服务，验证 18081 精确路由，并通过该入口提交一份无效架构的 `{}`，要求返回上游专用的 400 JSON 和 `no-store`，同时核实伪造客户端 IP 已被覆盖；该探测不保存回执、不消耗接收配额。最后比较所有受保护文件及 `current`。

不改 Caddy、网站、发行文件、`current`、现有请求/下载/扫描规则、SSH、防火墙或 DNS，不开放公网新端口。systemd 单元使用兼容 219 的 `MemoryLimit`、`ReadWriteDirectories`，不要求 `StateDirectory`。

安装器输出精确状态文件路径。回滚命令：

```powershell
python deploy/feedback/remote.py rollback --state /var/backups/ozl-feedback/<时间-随机标识>/state.json
```

只有当前 Nginx 哈希仍匹配本次新增配置时，才移除三个自有 include 块并验证、reload；始终只停用 `ozl-feedback`。检测到后来配置修改则停止覆盖并停用反馈服务，等待人工核对，保留其他人的修改。服务和备份文件保留供审查，不删除用户反馈。失败不会恢复整个网站或历史 Caddy 配置。

Nginx 的 systemd reload 只发送 HUP，不代表新 worker 已开始接受新路由。安装器分别用总计最多 10 秒、每次最多 1 秒的 GET 探测等待服务与 18081 路由就绪，并记录实际响应状态；只有返回预期 405 后才发送一次无持久化的 POST 探测，不循环重试 POST。

2026-10-26 首次安装曾在 reload 后的立即 GET 验收失败并自动回滚，保留了四个反馈自有路径。`archive_failed.py` 是只针对 `/var/backups/ozl-feedback/20261026T090323Z-68d89fb9/state.json` 的一次性恢复辅助脚本；须经主任务审查并确认后执行。它要求原 Nginx 哈希、服务停用、旧审查文件哈希、精确目录清单/权限/所有者、无符号链接、零接收记录、空报告目录、受保护文件基线及相同文件系统全部匹配，再将四个精确路径原子重命名到该备份的 `failed-install` 中；不删除内容，不发布或改写 Nginx。四个路径和数据库证据完整保留，随后才可按主任务协调执行新版首次安装。不要手动批量删除目录或将该脚本改作通用迁移器。

## 仅 SSH 管理

```powershell
python deploy/feedback/remote.py list
python deploy/feedback/remote.py read --id <32位回执ID>
python deploy/feedback/remote.py delete --id <32位回执ID>
```

命令使用服务账户读取/删除指定回执，不提供网页管理入口。`read` 会向当前 SSH 操作者显示报告，`delete` 精确删除该回执及内容；不要把报告批量加入工程或公开发布。

隔离验证：`python -m unittest discover -s tests -p test_feedback_server.py`。测试只创建本机临时目录和随机回环端口，不连接线上服务。
