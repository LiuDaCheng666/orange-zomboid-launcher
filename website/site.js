'use strict';
(() => {
  const repository = 'https://github.com/LiuDaCheng666/orange-zomboid-launcher';
  const latest = repository + '/releases/latest';
  const status = document.querySelector('[data-release-status]');
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
