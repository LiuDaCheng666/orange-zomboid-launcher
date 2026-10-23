'use strict';
(() => {
  const repository = 'https://github.com/LiuDaCheng666/orange-zomboid-launcher';
  const latest = repository + '/releases/latest';
  const status = document.querySelector('[data-release-status]');
  document.querySelector('[data-copy-group]')?.addEventListener('click', async () => {
    const message = document.querySelector('[data-copy-status]');
    try {
      await navigator.clipboard.writeText('1124858561');
      message.textContent = '已复制群号，可在 QQ 中搜索加入';
    } catch {
      message.textContent = '请手动复制群号 1124858561，在 QQ 中搜索加入';
    }
  });
  if (!status) return;
  const features = {
    home: ['启动首页', '确认游戏目录与组件状态，选择启动方式，查看最近使用的存档。'],
    components: ['组件管理', '查看 Buddy、Viewpoint 与中文适配的版本和状态，按需安装或更新。'],
    mods: ['模组清单', '查看模组名称与依赖，搜索和整理启用顺序，保存自己的配置。缺少封面的演示条目使用中性占位。'],
    servers: ['服务器', '管理收藏的服务器，查看连接信息，选择要连接的目标。'],
    maintenance: ['维护工具', '查看缓存与工坊维护入口、任务进度及操作日志；执行前阅读提示。']
  };
  const tabs = [...document.querySelectorAll('[role="tab"]')];
  const panel = document.querySelector('[role="tabpanel"]');
  const theme = document.querySelector('select[aria-label="预览主题"]');
  const screenshot = document.querySelector('[data-feature-image]');
  let selected = 'home';
  function selectFeature(id) {
    selected = id;
    tabs.forEach(tab => {
      const active = tab.dataset.feature === id;
      tab.setAttribute('aria-selected', String(active));
      tab.tabIndex = active ? 0 : -1;
    });
    panel.setAttribute('aria-labelledby', 'tab-' + id);
    document.querySelector('[data-feature-title]').textContent = features[id][0];
    document.querySelector('[data-feature-description]').textContent = features[id][1];
    screenshot.src = 'assets/launcher-' + id + '-' + theme.value + '.png';
    screenshot.alt = features[id][0] + (theme.value === 'dark' ? '深色' : '浅色') + '界面';
  }
  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => selectFeature(tab.dataset.feature));
    tab.addEventListener('keydown', event => {
      const next = { ArrowRight: (index + 1) % tabs.length, ArrowLeft: (index + tabs.length - 1) % tabs.length, Home: 0, End: tabs.length - 1 }[event.key];
      if (next === undefined) return;
      event.preventDefault(); tabs[next].focus(); selectFeature(tabs[next].dataset.feature);
    });
  });
  theme.addEventListener('change', () => selectFeature(selected));
  const dialog = document.querySelector('dialog');
  const enlarge = document.querySelector('[data-enlarge]');
  enlarge.addEventListener('click', () => {
    const image = document.querySelector('[data-dialog-image]');
    image.src = screenshot.src; image.alt = screenshot.alt;
    document.querySelector('[data-dialog-title]').textContent = screenshot.alt;
    dialog.showModal();
  });
  document.querySelector('[data-close]').addEventListener('click', () => dialog.close());
  dialog.addEventListener('close', () => enlarge.focus());
  function validate(data) {
    if (!data || !/^\d+\.\d+\.\d+$/.test(data.version) || !/^\d{4}-\d{2}-\d{2}$/.test(data.date)) throw Error('Invalid release');
    for (const [kind, filename] of Object.entries({ setup: `orange-launcher-setup-v${data.version}.exe`, portable: `orange-launcher-v${data.version}-win-x64.zip` })) {
      const file = data[kind];
      const relative = `/releases/v${data.version}/${filename}`;
      const github = `${repository}/releases/download/v${data.version}/${filename}`;
      if (!file || file.url !== relative || file.github !== github || !Number.isSafeInteger(file.length) || file.length <= 0 || !/^[a-fA-F0-9]{64}$/.test(file.sha256)) throw Error('Invalid download');
    }
    if (!Array.isArray(data.notes) || data.notes.length > 15 || data.notes.some(note => typeof note !== 'string' || note.length > 1000)) throw Error('Invalid notes');
    return data;
  }
  function show(data) {
    for (const kind of ['setup', 'portable']) {
      const file = data[kind];
      document.querySelectorAll(`[data-download="${kind}"]`).forEach(link => { link.href = file.url; });
      document.querySelector(`[data-github="${kind}"]`).href = file.github;
      document.querySelector(`[data-file-meta="${kind}"]`).textContent = `v${data.version} · ${(file.length / 1048576).toFixed(1)} MiB · Windows x64`;
      document.querySelector(`[data-hash="${kind}"]`).textContent = file.sha256.toUpperCase();
    }
    document.querySelector('[data-version]').textContent = `v${data.version} 稳定版`;
    document.querySelector('[data-release-date]').textContent = data.date;
    document.querySelector('[data-release-date]').dateTime = data.date;
    const notes = document.querySelector('[data-release-notes]'); notes.replaceChildren();
    for (const text of data.notes) { const li = document.createElement('li'); li.textContent = text; notes.append(li); }
    status.textContent = `v${data.version} · Windows x64 · 免费使用`;
  }
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 8000);
  fetch('release.json', { cache: 'no-cache', signal: controller.signal })
    .then(response => { if (!response.ok) throw Error('Release unavailable'); return response.json(); })
    .then(validate).then(show)
    .catch(() => {
      document.querySelectorAll('[data-download]').forEach(link => { link.href = latest; });
      status.textContent = '版本信息暂不可用 · 可从 GitHub 下载';
      document.querySelectorAll('[data-hash]').forEach(item => { item.textContent = '请在 GitHub 发布页查看 SHA-256'; });
    }).finally(() => clearTimeout(timeout));
})();
