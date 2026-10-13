# DLSS 5 实验接入准备（未安装）

2026-10-04。此目录只有关闭态模板；不含 DLL、安装器、驱动改动或可运行的 DLSS 组件，不会被游戏读取。模板不是启用功能。

NVIDIA 的 DLSS 5 是神经渲染阶段，需要当前画面和运动向量；RTX 50 系列是硬件条件，不是任意游戏均可启用的承诺。[官方研究说明](https://research.nvidia.com/labs/adlr/DLSS5/)

第三方 [DLSS5-Feeder 原始文档](https://github.com/jlrouzies-fr/DLSS5-Feeder) 宣称提供 ReShade/OpenGL 路径；需要深度、运动估计、Feeder 和神经渲染组件。它属于实验方案，并非 NVIDIA App 的通用开关。模板的 enabled=0 只是停用态参数，缺少组件时无作用。

本机指定 Steam 库中没有发现 Viewpoint 工坊目录，无法在本次验证游戏内联用。未运行第三方安装器，未加入安全软件排除项，也未修改游戏、驱动和服务器。

后续接入应先确认组件来源、版本和再分发条件，在单机中检查真实输出、画质、帧时间和启停日志。先验证清洁 Viewpoint，再加单个光影，再验证神经渲染；不要假设三个渲染层天然兼容。游戏根目录可能涉及 opengl32.dll、ReShade 配置、addon 文件、NGX 运行库、reshade-shaders 和日志；最终清单须依据实际安装，而非对整个目录盲删。既有 ReShade 必须先备份，不能覆盖后再宣称可恢复。

DLSS 4/4.5 的驱动覆盖也有游戏内已有相关功能的前提。[NVIDIA 支持说明](https://nvidia.custhelp.com/app/answers/detail/a_id/5620)

Smooth Motion 是另一项功能，官方列举兼容 DirectX 11/12 和 Vulkan，不能把它当成 Viewpoint 的 OpenGL DLSS 5 开关。[NVIDIA 说明](https://www.nvidia.com/en-us/geforce/news/nvidia-app-global-dlss-overrides-rtx-40-series-smooth-motion/)
