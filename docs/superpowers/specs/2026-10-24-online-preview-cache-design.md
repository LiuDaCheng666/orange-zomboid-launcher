# 线上截图缓存更新

用户已批准既有C质感网站并明确要求部署到orangezl.com，无需重新选择设计。现场确认Caddy对/assets/*设置max-age86400，同名十张PNG在线上仍是旧内容。保持PNG文件及全部页面布局不变，只为index初图与site.js动态截图URL统一增加`?v=20261024-fluent`。HTML/CSS/JS已有no-cache，不改服务器配置、下载清单、更新源或版本。

相比要求访客清缓存或关闭整个资源缓存，URL版本标记能直接让现有访客取得新图，且保留图片正常缓存。验证所有功能／主题、失败重试／竞态、no-JS和初始单请求继续成立。只允许index.html、site.js、website.test.cjs改动；测试先失败再实现，检查实际网络URL，独立审查后部署。无需新图片、框架、CDN或UI改版。
