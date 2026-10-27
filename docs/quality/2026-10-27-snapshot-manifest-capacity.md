# 快照文件清单容量与保存阶段验证

日期：2026-10-27。只涉及 `SaveEnvironments.cs` 的快照清单编码、读取、复制前容量预检及保存/清理阶段事件。

## 真实失败依据

正式 a5 副本复制与双向 SHA 校验完成后，旧 schema1 清单超出 64 MiB `MaxJsonLength`。QA 的只读真实来源元数据探针为 163,688 项（含目录），原编码 69,756,870 字符、69,758,882 UTF-8 字节。`verify-copy` 完成至异常的约 50 秒还包含 finally 删除未完成的 16 GB 副本，不能当作序列化单独耗时。

## 最小变更

- 新写入外层 schema2；每个模组冻结文件仅在 `Files` 保存一次。wire record 的 `ModRoots.SourceFiles` 为 null，读取时按验证过的 `CopiedRoot` 前缀重建。原 schema1 保留原始来源数组，继续可读。
- `GameSourceFiles` 独立保留，因为运行环境准备会修改游戏配置。来源与副本时间戳不同时，用稀疏 `SourceWriteTicks` 保留来源时间；不要求目标文件系统支持完全相同的时间精度。
- `EncodeManifest(record, files)` 新建 wire record/mapping，不改变传入的运行时 `SourceFiles`、文件对象或时间戳。`DecodeManifest(bytes)` 返回已重建来源数组、已设置 `Record.Files` 的 manifest。
- UTF-8 字节和 JSON 字符统一限制 128 MiB，冻结清单仍最多 250,000 项。复制前用元数据长度的保守估算、生成项预留量预检，不重复完整序列化或读取文件内容。
- 容量预检、清单构造/写入各自发 `manifest-capacity`、`manifest-save` 阶段，仅显示元数据条数；不把 JSON 估算当作已复制文件或字节。未登记副本清理前发受保护的 `snapshot-cleanup`，即使 token 已取消或观察者异常也保留原始失败/取消。
- 用户错误为清单容量说明；详细编码异常保留在 InnerException。

## 实际验证

`tests/SnapshotManifestCapacity.test.ps1` 编译生产源并在 .NET Framework x64 进程执行，使用只含元数据的大模型与独立临时小副本：

| 项数 | schema2 UTF-8 字节 | 复制前估算字节 | 完整估算/编码/解码约耗时 |
| --- | ---: | ---: | ---: |
| 163,688 | 59,660,422 | 81,153,149 | 6 秒 |
| 250,000 | 86,714,102 | 120,776,069 | 9 秒 |

断言全部冻结行、独立游戏来源哈希、每个模组来源行及稀疏原始时间戳精确往返；schema1、未知格式拒绝、250,001 项拒绝、运行中 record 不变也通过。模型进程峰值工作集约 0.7–1.2 GB，仍使用有界内存编码，未引入无限长度或新的序列化依赖。小副本实际完成 schema2 写入并从登记索引重读，另实际验证准备失败和已取消 token 下清理事件及异常保留。

回归证据：

- `artifacts/snapshot-manifest-capacity-frozen`：容量、兼容、不可变性和保存/清理事件测试。
- `artifacts/snapshot-manifest-save-final`：保存/读取/来源比较 123 assertions。
- `artifacts/snapshot-manifest-runtime-final`：运行环境 321 assertions。

本组没有再次复制真实 16 GB 游戏或启动实际游戏。真实来源的 schema2 metadata-only 探针与之后的正式副本/游戏验收由主任务 QA 独立完成；上述元数据模型和小副本测试不代替它们。
