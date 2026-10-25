# 固定 SSH 密钥连接

2026-10-25 根据用户提供的“服务器SSH登录方式.txt”切换。服务器仍为 `101.201.143.163:22`，使用 `root` 和 Ed25519 私钥 `C:\Users\MC233\.ssh\ozl_panel`。私钥留在仓库外，工程不保存任何密钥内容或密码。

`ssh-config.json` 是后续操作的连接配置，`ssh_connection.py` 提供统一的 `connect()`。部署器复用同一函数，保持原有 Transport/SFTP 接口；旧 `--handover` 参数可解析，但不会读取旧交接文件、尝试密码登录或更改服务器认证设置。

验证连接：

```powershell
python deploy/check_ssh.py
python -m unittest discover -s tests -p test_ssh_connection.py
```

本机 Python 必须已安装 Paramiko。已实测可用的解释器为 `C:\Users\MC233\AppData\Local\Programs\Python\Python312\python.exe`。检查只执行 `id -un`、读取当前站点链接、查看发行目录是否存在；不上传文件、不发布站点、不修改 SSH/Caddy。

先校验服务器 Ed25519 主机密钥的固定 SHA-256 指纹，再加载明确指定的私钥并执行 publickey 认证。指纹不符、密钥缺失/需要未提供的口令、认证失败时关闭连接并报错；不尝试密码、ssh-agent 或其他密钥。

验证结果：SSH 密钥认证、远程账户核对和 SFTP 读取全部通过，服务器仍指向 `20261024T195630Z-d3f3c044`。原有站点恢复内容保持不变。

连接方式与部署范围是两回事：普通软件发版不应调用整站部署脚本，也不能覆盖网站布局、样式、脚本和图片。工程根目录 `AGENTS.md` 记录了这项用户约束。
