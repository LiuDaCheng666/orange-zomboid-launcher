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
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  const effects = new Map();
  function stop(target) {
    effects.get(target)?.cancel();
    effects.delete(target);
  }
  function enter(target, duration = 240) {
    stop(target);
    if (reducedMotion.matches || document.hidden || !target.animate) return;
    const effect = target.animate([
      { opacity: 0.55, transform: 'translateY(8px)' },
      { opacity: 1, transform: 'translateY(0)' }
    ], { duration, easing: 'cubic-bezier(.2,.7,.2,1)' });
    effects.set(target, effect);
    const release = () => { if (effects.get(target) === effect) effects.delete(target); };
    effect.finished.then(release, release);
  }
  function stopAll() { for (const target of effects.keys()) stop(target); }
  reducedMotion.addEventListener('change', () => { if (reducedMotion.matches) stopAll(); });
  document.addEventListener('visibilitychange', () => { if (document.hidden) stopAll(); });
  window.addEventListener('pagehide', stopAll);
  if ('IntersectionObserver' in window) {
    const entrance = new IntersectionObserver(entries => {
      for (const entry of entries) if (entry.isIntersecting) {
        enter(entry.target, 360);
        entrance.unobserve(entry.target);
      }
    }, { threshold: 0.08 });
    document.querySelectorAll('[data-reveal]').forEach(section => entrance.observe(section));
  }
  if (!status) return;
  const features = {
    home: ['启动游戏与管理存档', '启动前选好配置和目标，不用每次重新整理模组。', [
      '进入主菜单，或选择指定存档、服务器启动',
      '查看存档封面、游玩记录与各存档的模组清单',
      '删除不用的存档，移到系统回收站'
    ]],
    components: ['核心组件安装与更新', '三个核心组件放在一起，其他扩展按你的需要选择。', [
      '安装 ZombieBuddy 框架、Viewpoint 3D 和中文汉化',
      '查看已装版本、启用状态与可用更新',
      '按组件安装、卸载，或一键恢复全部组件'
    ]],
    mods: ['模组配置与依赖排序', '看封面、看介绍、查依赖，为不同玩法保存不同配置。', [
      '搜索本地模组，查看封面、作者、版本与前置依赖',
      '检查缺失依赖，预览自动排序，也能手动调整顺序',
      '保存多套配置，为不同存档整理各自的模组清单'
    ]],
    servers: ['服务器列表与公开大厅', '个人列表、游戏收藏与公开大厅，放在同一个地方。', [
      '保存服务器地址、端口与角色登录信息',
      '查看在线人数与延迟，搜索、筛选和排序公开大厅',
      '选择服务器启动，连接时使用服务器下发的模组清单'
    ]],
    maintenance: ['内存设置与工坊维护', '任务进行到哪一步、哪里出错，在进度和日志里查看。', [
      '自动读取总内存与当前设置，调整 Java 启动内存',
      '清理缓存，调用 Steam 重新下载工坊模组',
      '跟踪操作进度、完成结果与详细日志'
    ]]
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
    const points = document.querySelector('[data-feature-points]');
    points.replaceChildren(...features[id][2].map(text => {
      const item = document.createElement('li'); item.textContent = text; return item;
    }));
    screenshot.src = 'assets/launcher-' + id + '-' + theme.value + '.png';
    screenshot.alt = tabs.find(tab => tab.dataset.feature === id).querySelector('.feature-name').textContent + (theme.value === 'dark' ? '深色' : '浅色') + '界面';
    enter(panel);
  }
  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => selectFeature(tab.dataset.feature));
    tab.addEventListener('keydown', event => {
      const next = { ArrowDown: (index + 1) % tabs.length, ArrowUp: (index + tabs.length - 1) % tabs.length, ArrowRight: (index + 1) % tabs.length, ArrowLeft: (index + tabs.length - 1) % tabs.length, Home: 0, End: tabs.length - 1 }[event.key];
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
    enter(dialog, 200);
  });
  document.querySelector('[data-close]').addEventListener('click', () => { stop(dialog); dialog.close(); });
  dialog.addEventListener('cancel', () => stop(dialog));
  dialog.addEventListener('close', () => { stop(dialog); enlarge.focus(); });
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
      document.querySelector(`[data-file-meta="${kind}"]`).textContent = `${(file.length / 1048576).toFixed(1)} MiB · Windows x64`;
      document.querySelector(`[data-hash="${kind}"]`).textContent = file.sha256.toUpperCase();
    }
    document.querySelector('[data-version]').textContent = `v${data.version}`;
    document.querySelector('[data-release-date]').textContent = data.date;
    document.querySelector('[data-release-date]').dateTime = data.date;
    const notes = document.querySelector('[data-release-notes]'); notes.replaceChildren();
    for (const text of data.notes) { const li = document.createElement('li'); li.textContent = text; notes.append(li); }
    status.textContent = '';
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
