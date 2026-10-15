# 1.2.1 配置恢复与原生安装向导

用户已确认：修复选择失败后的重新读取恢复；默认优先非系统盘，自动创建独立工具目录，保留自定义位置；原生黑金圆角安装向导、真实阶段与进度、完成后的显式打开按钮。以当前feature/core-self-update继续，本机交付1.2.1，不另建工作树或修改main。

## 边界

- Real Steam/game/save/pagefile/registry data remain unchanged during development and tests.
- Keep the existing feature/core-self-update checkout; no new worktree, main merge, push or publication.
- Version 1.2.1; preserve accepted 1.2.0 and 1.1.1 artifacts and trust key bytes.
- No registry writes, new updater authority, third-party payloads, automatic game launch or Java authorization.
- Existing signed allowlist, independent-root/reparse/nonempty-directory checks, transactional deployment and scoped uninstall remain authoritative.
- Installer preview and tests use isolated fixtures; no real default destination is created or installed during verification.

## 配置重新读取

已复现：playProfileSelect失败后，playSelectionError残留；playProfiles成功返回selectedId为空、selectedProfile为null及新stamp，旧错误仍阻止启动。修复只在当前游戏目录/请求代际有效、返回结构完整且选择/目标已核对后清除此过期错误，并替换旧反馈。不能在点按钮时直接清除。

失败、畸形、陈旧、目录已变或与进行中的选择相冲突的回复不能解除保护；存档/服务器目标不一致不能应用或启动。保留选择token/epoch、预览凭据、待恢复事务、草稿和每次真正启动前的原生体检。不自动应用或启动。

## 默认目录

候选顺序：安装包所在的就绪非系统固定盘；否则可用空间最多的就绪非系统固定盘（并列按盘符稳定排序）；确实没有时使用Windows用户应用目录下Programs/OrangeLauncher，清楚显示系统盘提示。盘符/用户目录来自本机环境，不硬编码E盘。自动推荐采用独立OrangeLauncher子目录，已有非空目录使用有界的未占用后缀，绝不覆盖既有文件。盘不可用、路径无权限/重解析/不安全则展示中文原因，让玩家更改，不静默改写到另一盘。

打开安装器、推荐/编辑目录只读，不创建默认目录或写注册表。玩家明确点安装后才经现有校验创建目录。文本框显示最终路径；“更改位置”选择存放父目录，明确会创建OrangeLauncher子目录，另允许直接编辑完整自定义路径。

## 安装向导

采用原生WinForms，不新增安装前WebView依赖。现有橙子图标、石墨灰/橙金、圆角主次按钮、清楚的版本/路径/状态、可折叠详情；DPI/小屏允许重排或滚动，文字和按钮不互相覆盖。只安装工具，不偷偷安装游戏/模组/光影。

后台执行文件操作，UI保持响应。阶段依次为校验包/读取资源、释放准备/事务部署、落盘核验、完成。可观测的解压和核验按实际字节或文件数显示；现有事务部署若没有细粒度回调，显示不定进度和真实阶段，不编造总百分比/倒计时。仅当签名、部署记录和最终自有文件验证全部成功后显示完成/100%。失败保留现有事务证据，展示阶段、原因和可复制详情；不自动清空、覆盖或重装原目录。安装中关闭受保护，不能从UI强杀部署。

完成展示安装位置、卸载器位置、“打开启动器”和“完成”；只有玩家点击打开才启动已安装的自有EXE，绝不调用Steam或游戏。明确现有安装不直接覆盖，可用启动器更新或选择新目录。

## 验证与交付

先加入并观察失败测试，再实现。配置用真实DOM/事件，原生I/O仅在fixture边界替代；覆盖成功恢复、空列表、失败/畸形/陈旧读取、正在选择和目标不匹配。路径推荐用合成驱动与存在状态验证，不枚举真实用户资料。安装服务用签名fixture和真实部署/验证，UI用fixture后端验证状态、线程响应、错误、显式打开和关闭保护；截图实际原生控件在默认/安装中/失败/完成及小屏/高DPI下的布局。

独立复查后全原生/浏览器回归，签名构建到全新dist/1.2.1-installer-verified，核对版本、18文件/20 ZIP条目、自有资源/指南字节、安装器验证、隔离更新回退和卸载。原1.2.0哈希不变。只提供本地安装包/ZIP，源码与证据保留，不发布更新渠道。
