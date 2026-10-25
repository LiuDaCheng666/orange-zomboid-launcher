# 1.2.17 日志分析与启动问题展示验证

日期：2026-10-25（Asia/Shanghai）。发布结果在本文末尾单独记录；本地测试与候选构建不等同于已经上线。

## 已实现范围

- 左侧独立“日志分析”，支持本机最近日志、玩家 TXT / LOG / ZIP 及日志目录；后台执行、进度、取消、复制与原生导出。
- 通过通用 Lua / Java 异常语法及 B41 / B42 堆栈提取模组、脚本、函数、行号；未知错误仍保留并关联前后帧，生产解析器没有样本模组适配名单。
- 会话及每次连接尝试分开；明确区分进入世界、重试、世界加载阻断、运行期异常、断连及证据不足。传输连接被接受、加载耗时与模组自己的 timeout 均不能独立证明进服成败。
- 每项问题包含中文含义、可执行检查建议、脱敏原文与来源文件映射；完整报告附来源表和尝试记录。多来源不可比较时不制造统一“最后一次”。
- 启动监控复用解析器；在滚动缓冲驱逐前收集问题，保留单批日志突发中的早期错误。观察窗口不控制游戏进程，严格就绪 nonce 和授权让出前台规则保留。

## 自动验证与实际输入

| 检查 | 结果与证据 |
| --- | --- |
| 通用分析器 / 采集服务 / 旧诊断 | 最终冻结版 110 + 38 + 145 = 293 项通过；`artifacts/log-analysis/corpus-final/corpus-final-fixtures.log` |
| 真实案例、改名、完整覆盖、取消 | 44 条断言通过；世界字典阻断、必需动画资源退出、运行期 nil / 空值错误、首次连接失败后重试进入、加载后连接丢失均有人工核对依据 |
| 全量语料回放 | 本机 612 份、下载精确去重后 93 份，共 705 个输入；15 个 ZIP 诊断成员按 SHA-256 对应已分析文本，未解压执行。1,207,422,612 字节、12,125,994 行，214.8 秒。`corpus-summary.json`、`corpus-files.jsonl` 与独立 `corpus-coverage-audit.json` |
| 读取约束 | 流式测试同时最多 4 个输入，单文件 64 MiB、批次 256 MiB；观察峰值约 95.6 MiB。该数值是测试器运行时观察，不是所有原生批量导入的内存保证 |
| 新日志界面 | 12/12 浏览器组通过，包括未知模组、转义攻击文本、真实阶段枚举汉化、证据文件名、原生选择取消保留报告、目录变更忽略旧回复、超时与小窗口结果优先 |
| 原有主要 UI | 57/57 full UI 组通过，11 个 Fluent 回归组通过；保留内存保存失败恢复、目录/修复入口、完整多人清除确认等原有交互 |
| 生产源边界 | 12/12 通过；103 个明确生产源保留原 92 个并加入 11 个已批准启动/日志源；诊断测试与语料不进入生产编译输入 |
| 软件专用发布器 | 15 个模拟测试与 9 个既有发行准备测试通过；验证签名后才连接，拒绝额外/不一致交付物、不可变碰撞、网站/current 漂移和激活中途并发编辑；仅回滚自己的元数据替换，保留网站自定义字段 |
| SSH 配置 | 7 个连接策略测试通过；实际只读 SSH / SFTP 检查成功，固定主机指纹先验证，再使用项目配置引用的 Ed25519 私钥 |

上述语料回放中 **307 个报告标记为不完整**，原因包括问题分组、异常/事件或展示预算。标记及提示原样保留，关键末尾阻断证据按优先级展示。文件数量、错误出现次数和状态分布不能当作独立玩家数、根因数量或诊断准确率。

测试程序集 SHA-256：`76bf4017b110a873aea2e0f09f54eef311aeb30d1a755a7165285c86785a7d44`。

冻结分析器 SHA-256：`999825bdc5b871b45fa048518b3867f86563bbe5e3b61e05450103204bad2ddc`；采集服务 SHA-256：`04feee84fb565a2a28b0eb05a245d9ae08182af399378ee57871d62cb107da36`。最终启动视觉调整未改变这两个实现。

## 正式程序与人工核查

`tests/GameLogProductionProof.ps1` 加载实际正式构建的程序集，通过原生采集服务分析四份原始玩家文件，并验证一个从未见过的虚构模组及脚本行号；不使用 WebView 合成响应。每个最终构建单独记录 EXE 哈希及结果，记录在 `artifacts/log-analysis/production-*/production-analysis-proof.json`。完整 705 输入回放由诊断测试程序集完成，不冒充正式 EXE 的全量播放测试。

独立审查发现并修复了：未知错误丢失堆栈、批量启动日志遗漏早期错误、阶段未汉化、证据来源无法映射、普通说明文本误报致命失败，以及过滤器错误屏蔽包含 example/sample 的真实异常。发布器另修复激活中途覆盖并发元数据修改的问题。相关用例均先复现失败，再验证修复；复审无剩余 P1/P2。

启动外观按用户反馈改为同一画布：两侧底部紧凑日志、左侧左对齐、右侧右对齐，相关模组单列优先显示；没有右侧卡片底板或边框。长名称通过提示和详情完整查看。窗口、布局与顶部进度条的最后一次检查记录随最终发布证据保留。

原有体验界面的四项组件封面/提示测试在基线 `59d74f0` 已失败，已隔离复跑确认同断言失败，未将它们算作本次通过或日志功能回归。证据：`artifacts/log-analysis/baseline-ui/results.log` 与 `baseline-metadata.json`。旧内嵌诊断 UI 测试按独立入口迁移，旧诊断原生行为继续由 145 项回归覆盖。

## 边界

原始日志仅本地读取，不上传或提交；没有通过测试修改真实游戏、存档、订阅或在线服务器。分析能指出观察到的错误与涉及的调用链，复杂模组逻辑及服务端根因仍可能需要同一时段服务端日志和复现步骤。没有声称所有模组都可以自动修复。

软件发布仅允许发行文件、三个 Velopack 最新元数据文件和合并后的 `release.json`。网页、图片、样式、脚本、网站 current、Web 防护和 SSH 配置均由发布前后保护快照核对。


## Final startup presentation verification

The final player-requested presentation uses 12px corner text, four left-aligned startup lines and a right-aligned analysis area with an explicit related-MOD label and script position. The loading rail is a 10px rounded gradient track with indeterminate motion or actual measured counts; the separate 6px memory rail shows the actual working-set/system-memory ratio. Reduced motion, authorization wait, failure and ready states are covered. Final window verification: 197 passing assertions plus the complete startup/presenter/launch and Lua suites. Evidence: `artifacts/log-analysis/startup-memory-rail-*`.

## 1.2.17 发布记录

已发布至 orangezl.com，随后于 2026-10-25 15:52:06（Asia/Shanghai）公开 GitHub v1.2.17。GitHub 十个附件在草稿阶段全部下载核对哈希。正式 1.2.16 客户端使用生产网络传输，在隔离定位器中实际检查并下载 1.2.17，达到 Ready；未对用户安装执行更新。主服务器保护快照发布前后完全一致，网站 current、网页、Web 防护及 SSH 配置未更改。证据：`artifacts/publication-1.2.17/primary-receipt.json`、`production-client-proof.json`。后续 1.2.18 另修复任务栏关联，记录见同日任务栏验证文档。
