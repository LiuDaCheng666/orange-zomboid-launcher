# 推荐服务器管理（本地预览）

独立的 Python 3 标准库服务。默认仅本地预览；自有服务器安装方式见下文。与反馈服务分开，不修改既有网页或 Caddy/Nginx 配置。

在工程根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File deploy/recommendations/start-local.ps1
```

管理台：`http://127.0.0.1:18083/admin`。公开推荐：`http://127.0.0.1:18083/api/recommended-servers`。服务仅监听回环地址。

首次启动自动生成随机管理员密码，终端**只显示私有凭据文件路径**。使用本机编辑器打开 `%LOCALAPPDATA%\OrangeLauncher\recommendations-local\initial-credentials.json` 查看账号密码，不要复制进工程、截图、日志或发布包。`auth.json` 仅保存加盐 PBKDF2-SHA256 哈希；初始凭据文件用于本机首次登录，可在记入密码管理器后删除。`-SetupOnly` 只初始化并返回凭据文件路径。重复执行不重置密码或数据。

管理台支持添加/编辑、上下移动排序、草稿、发布、撤下和删除。未发布记录不出现在公开接口。每次写入携带列表版本，过期编辑返回 409；点“刷新列表”获取最新记录后再编辑。名称和介绍按普通文字显示。

私有目录保存 SQLite 数据、认证哈希及初始凭据；公开资源只来自 `static/` 固定白名单。不要把私有目录放入 `static/`。SQLite 写事务检查 revision；建议只启动一个实例。会话最多 8 小时、HttpOnly/SameSite=Strict，写请求校验 CSRF 和来源。登录每来源 60 秒最多 6 次，限流表和会话表有上限，请求体最多 64 KiB，列表最多 1 MiB / 200 个服务器。

本地 HTTP 会话不设置 Secure。下面的生产方案使用加密 SSH 通道承载本地 HTTP 管理会话，不对外代理管理接口，因此不传 `--public-origin`。保留 HttpOnly、SameSite=Strict、CSRF 和固定 Host/Origin 校验。登录限流依据直接连接 IP，不信任转发头。

客户端普通编译固定使用 `https://orangezl.com/api/recommended-servers`。仅 `OZL_LOCAL_PREVIEW` 编译固定使用上述回环接口，不提供普通发行版运行时 URL 覆盖。

测试（临时目录与回环，不访问真实推荐源或游戏）：

```powershell
python -m unittest tests.test_recommendations -v
python -m unittest tests.test_recommendations_export tests.test_recommendations_operations tests.test_recommendations_update -v
node deploy/recommendations/ui-tests.cjs
```

浏览器测试通过现有 Playwright runtime 启动无窗口 Edge，使用临时私有目录、随机回环端口，并在结束时清理；截图位于 `artifacts/recommendations-browser/`。原生推荐读取和实时人数查询由 `Orange3DTool/RecommendedServersTests.cs` 覆盖，查询仅使用本机临时 UDP 响应器。实时查询使用已有 A2S 解析器，4 个并发、2.5 秒超时、10 秒缓存，不读取或保存玩家账号密码。

## 自有服务器生产安装：私有管理台 + 静态公开列表

这是独立的功能后端安装。`remote.py` 只使用 `deploy/ssh_connection.connect()`，固定配置、Ed25519 密钥和主机指纹核验全部沿用工程规则；不搜索其他密钥、不回退密码。`audit` / `check` 将源码放在远端 Python 内存中编译和导入，不上传文件、不启动服务、不生成凭据。

安装只新增下列路径：

| 路径 | 权限与用途 |
| --- | --- |
| `/opt/ozl-recommendations` | root 所有；目录 0755、程序/静态资源 0644、安装记录 0600 |
| `/var/lib/ozl-recommendations` | 专用服务账号所有，目录 0700；数据库、认证和初始凭据通过 0077 umask 保持私有 |
| `/var/lib/ozl-recommendations-public/feed.json` | 专用公开目录 0755，唯一公开 JSON 0644；仅已发布字段 |
| `/etc/systemd/system/ozl-recommendations.service` | root 所有 0644；兼容 systemd 219 |
| 当前实际站点的 `api/recommended-servers` | 新符号链接，指向上述 `feed.json`；`api` 不存在时新建 0755 |

服务使用 Python 3.6 标准库，仅监听 `127.0.0.1:18083`。不增加公网 HTTP 端口，不改 Caddy/Nginx 配置，不 reload Web 服务，不切换 `current`，不改既有网页、发行文件或反馈服务。systemd 使用 `MemoryLimit` / `ReadWriteDirectories`，不依赖 `StateDirectory`。`install` 只用于首次安装；已有安装使用下方独立的精确代码更新流程。

先运行只读审计，保存返回的 `current` 和 `protectedSha256`。审计覆盖现有网站、发行文件、根文件、Caddy/Nginx 配置和有效 include，检查路由未被截取、符号链接未禁用。`check` 还在服务器实际 Python 中验证导入兼容性。示例中的 SHA256 必须替换为**当次审计结果**；不要从旧日志复制。

```powershell
python deploy/recommendations/remote.py audit
python deploy/recommendations/remote.py check --expected-current /var/www/ozl/sites/20261024T195630Z-d3f3c044 --expected-protected-sha256 <当次审计的64位SHA256>
python deploy/recommendations/remote.py install --expected-current /var/www/ozl/sites/20261024T195630Z-d3f3c044 --expected-protected-sha256 <同一次审计的64位SHA256>
```

安装前再次核对精确站点、物理身份和保护文件哈希；只向新的 `/root/ozl-recommendations-<随机标识>` 上传固定代码/静态文件及独立更新器。创建专用无登录账号后安装 systemd 服务，验证管理员服务只在回环监听、Nginx worker 能读取公开文件，再创建唯一新链接，检查现有 18080/18081 静态入口并再次比较保护文件。完成后删除本次固定上传文件及空目录。不要在此过程并行发布网站或发行文件，否则保护哈希检查会拒绝继续。

安装后的公开地址保持为 `https://orangezl.com/api/recommended-servers`，发行版客户端无需更改。公开响应沿用现有静态 MIME/缓存及 Web 防护规则；原生客户端按 UTF-8 JSON 内容解析，不依赖扩展名或 MIME。初次列表为空，不会代替管理员发布任何真实游戏服务器。

### 管理员连接

先关闭占用本机 18083 的本地预览服务。仅在本人需要管理时执行：

```powershell
python deploy/recommendations/admin_remote.py --credentials --open
```

也可在资源管理器双击 `deploy/recommendations/Open-Admin.cmd`。已有本地初始凭据时直接打开管理通道；没有时先安全取回凭据。只有本人执行该文件才启动通道和浏览器。

工具使用同一固定指纹 SSH 连接，将**本机 `127.0.0.1:18083`** 转发至服务器 **`127.0.0.1:18083`**；仅显式 `--open` 才打开浏览器。访问 `http://127.0.0.1:18083/admin`。关闭工具即关闭通道，后端及公开推荐继续工作。不提供任意远端地址或端口参数。

`--credentials` 只将固定远端初始凭据取回到 `%LOCALAPPDATA%\OrangeLauncher\recommendations-production\initial-credentials.json`，在 Windows 使用当前账号的专有 ACL；终端只显示路径，不显示密码。已存在且内容不同的本地凭据不会被覆盖。用本机编辑器查看并登录，保存到密码管理器后可自行删除初始文件。平时省略 `--credentials` 即可。不要把凭据内容、私钥或管理会话带入聊天、截图、日志、仓库或发行包。

### 发布的一致性和恢复

每次编辑都先在同一 SQLite `BEGIN IMMEDIATE` 事务保存文档、新 revision 和公开快照 outbox，再提交。单实例进程锁和发布锁防止旧版本覆盖新版本。公开文件使用同目录独占临时文件、文件 `fsync`、原子替换及目录 `fsync`；最后确认导出 revision。

若数据库已保存但导出失败，HTTP 返回 503 和 `saved=true`，管理台采用已提交 revision，明确显示“公开列表仍为上次同步内容”，提供“重试公开同步”。重复同步不新增 revision；不要重复添加同一服务器。服务重启先恢复最新 outbox 再接受请求，替换完成但确认尚未保存时也会安全重放。公开内容不会出现半个 JSON。数据库、导出目录身份或已有导出文件若被外部替换，拒绝覆盖并保留现场。

### 精确回滚

```powershell
python deploy/recommendations/remote.py rollback
```

回滚在内存执行本地已审阅的安装器，读取 root 所有的固定安装记录，先核对代码/单元哈希、物理身份和链接归属。只停止/禁用本服务、移除仍属于本次安装的符号链接、单元和代码文件；只移除本次创建且仍为空的 `api` 目录。保留私有数据库、凭据、公开快照、专用账号和安装记录供恢复，保留任何额外或改变的文件。不会递归删除目录，不改 Web 配置或既有页面。中断安装同样保留记录，检查后使用该明确回滚命令；不要直接重复安装或清空目录。

Windows 测试覆盖临时文件审计、保护哈希/占用/路由拒绝、精确回滚及假 SSH 的本机 socket 转发；实际 Linux 账号/systemd/站点符号链接安装须由执行者在 `check` 和审阅后验证，本机测试不会执行生产安装。


### 已安装后端的精确代码更新

管理员可设置 `extendedDescription`（最多 8000 字普通文字）、`descriptionHtml`（最多 32768 字富文本源码）和 `screenshots`（最多 8 个图片地址）。截图只允许本站及上述两个 Steam 图片域名的 HTTPS URL，暂不提供文件上传接口。公开 `schema` 仍为 1；这些可选字段不会使旧客户端拒绝推荐列表。旧数据库记录缺省为空，新程序启动不会替换、重新命名或重新发布现有服务器。

1.2.40 的详细介绍提供可视编辑、HTML 源码和即时预览：标题、字号、粗斜体、下划线、文字／背景颜色、边框提示框、对齐、列表、网页链接及表格。默认文字继承启动器主题；管理员显式设置的颜色、背景与边框保留，可用明暗预览检查。源码模式保存原串，展示端只重建支持的排版标签和样式，不执行脚本、事件、框架或定位遮盖内容。链接通过系统浏览器打开，不替换启动器页面。

旧记录只读、切换预览或修改其他字段不会重写原有纯文。真正修改富文本时生成旧版纯文回退，完整格式内容由 `descriptionHtml` 保留。接口请求体与公开文档均沿用 1 MiB 预算，避免合法中文富文本超过旧 64 KiB 请求体而无法保存。客户端详情上方为单张图片展示窗，左右箭头切换；下方展示完整富文本，无有效富文本时显示纯文。

更新器只替换四个已归属本功能的文件：`server.py`、`static/admin.html`、`static/admin.css`、`static/admin.js`。原安装器、systemd 单元、公开链接、数据库、认证、初始凭据、现有公开快照均不由更新器写入。服务正常启动及管理员后续保存仍沿用原有 outbox 发布机制。

先取得本次**已安装代码**和现有网站/配置的精确审计值：

```powershell
python deploy/recommendations/remote.py update-audit
python deploy/recommendations/remote.py check-update --expected-current <本次current> --expected-protected-sha256 <本次protectedSha256> --expected-installed-sha256 <本次installedSha256>
python deploy/recommendations/remote.py update --expected-current <同次current> --expected-protected-sha256 <同次protectedSha256> --expected-installed-sha256 <同次installedSha256>
```

只读审计和检查在远端匿名内存执行本地已审阅的程序，使用 `python3 -B`，不导入或执行远端未知的安装器。更新再次核对 root 安装记录、所有六个原安装文件的哈希、站点/代码物理身份和唯一链接，任一外部改动即拒绝覆盖。不要在更新过程中并行发布其他网站或发行文件。

更新前将四个原文件及原安装记录保存到 `/opt/ozl-recommendations/update-backups/<32位更新ID>`（root 专有目录 0700、备份/记录 0600），持久化恢复记录后才停止本服务。每个文件原子替换，更新安装记录，再启动本服务并验证就绪及受保护文件哈希。服务停止后再次核对代码，避免覆盖停止过程中出现的外部改动。Web 服务不 reload。

新服务就绪失败时只恢复哈希仍属于该操作的原文件和原安装记录，保留数据库/凭据/公开列表。进程中断产生的 `prepared` / `applying` 记录会阻止新的更新，须先检查并明确恢复：

```powershell
python deploy/recommendations/remote.py rollback-update --update-id <本次返回或保留目录中的32位更新ID>
```

恢复可以处理四个文件仅替换一部分的中断；后来安装状态或代码已变化时拒绝回滚，保留现场，不覆盖其他人的修改。恢复前和停止本服务后都再次核对文件。备份、记录和上传失败目录保留供检查；不递归删除、不回滚服务器推荐内容，不触碰 Web 配置、current 或已有页面。`rollback` 是原有整项后端卸载入口，正常代码退回应使用 `rollback-update`。
