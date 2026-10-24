'use strict';
(() => {
  const repository = 'https://github.com/LiuDaCheng666/orange-zomboid-launcher';
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
  function enter(target, duration = 360) {
    stop(target);
    if (reducedMotion.matches || document.hidden || !target.animate) return;
    const effect = target.animate([
      { opacity: 0.2, transform: 'translateY(20px)' },
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
        enter(entry.target, 560);
        entrance.unobserve(entry.target);
      }
    }, { threshold: 0.08 });
    document.querySelectorAll('[data-reveal]').forEach(section => entrance.observe(section));
  }
  if (!status) return;
  const features = {
    home: ['每个存档，都有自己的模组清单', '在启动器里选择要继续的存档，查看并修改它使用的模组。不同存档可以保留不同的模组组合。', [
      '查看存档封面和游玩记录，选中存档后直接启动',
      '为所选存档添加、移除模组或调整加载顺序，单独保存清单',
      '删除不再使用的存档时，将其移入系统回收站'
    ]],
    components: ['3D 模组，一键启用或卸载', '在组件页面安装并启用 Viewpoint 3D；不想使用时，点击卸载即可关闭。汉化和其他组件也能分别管理。', [
      '一键安装并启用 Viewpoint 3D，按提示补齐所需的 ZombieBuddy 框架',
      '一键卸载 3D 模组；中文汉化可单独安装，移除 Viewpoint 后保留 Buddy',
      '查看各组件的安装状态和版本，按需更新或卸载'
    ]],
    mods: ['保存多套模组清单，换玩法时直接选', '把常用的模组组合保存为命名配置，按玩法选择要启用的清单，并为不同存档整理各自的模组。', [
      '搜索本地模组，查看封面、介绍、作者和前置依赖',
      '添加或移除清单中的模组，检查缺失依赖，预览自动排序或手动调整',
      '保存多套启用配置，切换玩法时选择并应用，不用逐个重新勾选'
    ]],
    servers: ['选服直连，提前下载服务器模组', '在启动器里选择服务器即可发起连接。联机时客户端必须按服务器下发的模组清单和顺序加载，普通单机模组会临时排除。', [
      '保存常玩的服务器和登录信息，或在公开大厅按人数、延迟筛选',
      '第一次连接：记录服务器下发的工坊模组清单，供下次下载使用',
      '下次进服前：打开该服务器的“模组清单”，点击“订阅并下载”，提前下载清单内的全部工坊模组',
      '连接时保留已启用的 3D 和汉化；服务器清单有变化时，以本次连接下发的最新清单为准'
    ]],
    maintenance: ['修改内存、清缓存、重下模组，一处完成', '首页“常用工具”集中提供三个入口。内存修改保存后在下次启动生效；清理和重下载会先说明处理范围，再确认执行。', [
      '一键打开内存设置，选择游戏可用的内存大小并保存，可参考按系统总内存计算的推荐值',
      '一键清除多人地图缓存，重新获取地图区块，保留角色与车辆数据库',
      '一键重新下载工坊模组：清除本游戏的工坊模组文件后，由 Steam 重下已订阅项目，保留订阅；进度和结果可在任务中心查看'
    ]]
  };
  const helpSections = { home: 'saves', components: 'tools', mods: 'saves', servers: 'servers', maintenance: 'tools' };
  const tabs = [...document.querySelectorAll('[role="tab"]')];
  const panel = document.querySelector('[role="tabpanel"]');
  const theme = document.querySelector('select[aria-label="预览主题"]');
  let screenshot = document.querySelector('[data-feature-image]');
  const enlarge = document.querySelector('[data-enlarge]');
  const previewError = document.querySelector('[data-preview-error]');
  const retryImage = document.querySelector('[data-retry-image]');
  function watchScreenshot(image) {
    const settle = () => {
      // Each request owns its image node. Late events from removed images cannot
      // overwrite a newer feature's loading/error state.
      if (image !== screenshot) return;
      const failed = image.naturalWidth === 0;
      previewError.hidden = !failed;
      image.style.visibility = failed ? 'hidden' : '';
      enlarge.disabled = failed;
    };
    image.addEventListener('load', settle, { once: true });
    image.addEventListener('error', settle, { once: true });
    if (image.complete && image.getAttribute('src')) settle();
  }
  watchScreenshot(screenshot);
  let selected = 'home';
  function selectFeature(id) {
    selected = id;
    tabs.forEach(tab => {
      stop(tab);
      const active = tab.dataset.feature === id;
      tab.setAttribute('aria-selected', String(active));
      tab.tabIndex = active ? 0 : -1;
    });
    enter(tabs.find(tab => tab.dataset.feature === id), 260);
    panel.setAttribute('aria-labelledby', 'tab-' + id);
    document.querySelector('[data-feature-title]').textContent = features[id][0];
    document.querySelector('[data-feature-description]').textContent = features[id][1];
    document.querySelector('[data-feature-help]').href = '/help#' + helpSections[id];
    const points = document.querySelector('[data-feature-points]');
    points.replaceChildren(...features[id][2].map(text => {
      const item = document.createElement('li'); item.textContent = text; return item;
    }));
    const nextImage = document.createElement('img');
    for (const name of ['class', 'width', 'height', 'data-feature-image']) nextImage.setAttribute(name, screenshot.getAttribute(name));
    nextImage.alt = tabs.find(tab => tab.dataset.feature === id).querySelector('.feature-name').textContent + (theme.value === 'dark' ? '深色' : '浅色') + '界面';
    screenshot.replaceWith(nextImage);
    screenshot = nextImage;
    previewError.hidden = true;
    enlarge.disabled = false;
    watchScreenshot(nextImage);
    nextImage.src = 'assets/launcher-' + id + '-' + theme.value + '.png?v=20261024-c-glass';
    enter(panel);
  }
  retryImage.addEventListener('click', () => {
    selectFeature(selected);
    // The retry control becomes hidden while loading. Keep keyboard focus usable.
    tabs.find(tab => tab.dataset.feature === selected).focus({ preventScroll: true });
  });
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
  enlarge.addEventListener('click', () => {
    const image = document.querySelector('[data-dialog-image]');
    image.src = screenshot.src; image.alt = screenshot.alt;
    document.querySelector('[data-dialog-title]').textContent = screenshot.alt;
    dialog.showModal();
    enter(dialog, 200);
  });
  document.querySelector('[data-close]').addEventListener('click', () => { stop(dialog); dialog.close(); });
  dialog.addEventListener('cancel', () => stop(dialog));
  dialog.addEventListener('close', () => {
    // A queued close event may belong to an earlier opening.
    if (dialog.open) return;
    stop(dialog); enlarge.focus();
  });
  function validate(data) {
    if (!data || !/^\d+\.\d+\.\d+$/.test(data.version) || !/^\d{4}-\d{2}-\d{2}$/.test(data.date)) throw Error('Invalid release');
    if (data.distribution !== undefined && !['legacy', 'velopack'].includes(data.distribution)) throw Error('Invalid distribution');
    const filenames = data.distribution === 'velopack'
      ? { setup: 'OrangeZomboidLauncher-win-Setup.exe', portable: 'OrangeZomboidLauncher-win-Portable.zip' }
      : { setup: `orange-launcher-setup-v${data.version}.exe`, portable: `orange-launcher-v${data.version}-win-x64.zip` };
    for (const [kind, filename] of Object.entries(filenames)) {
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
      status.textContent = '版本信息暂不可用，可继续下载页面提供的版本，或稍后刷新重试。';
      document.querySelectorAll('[data-hash]').forEach(item => { item.textContent = '校验信息暂不可用，请稍后刷新重试。'; });
    }).finally(() => clearTimeout(timeout));
})();
