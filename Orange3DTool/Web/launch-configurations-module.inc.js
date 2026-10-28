// Source mirrored inline in app.js; no separate runtime resource is required.
const launchConfigurationUi = (() => {
  const model = {
    list: null,
    versions: null,
    selected: null,
    selecting: "",
    cover: null,
    epoch: 0,
    reading: null,
    error: "",
    editor: null,
    jobs: new Map(),
  };
  const raw = request,
    selectedId = () => model.selecting || model.list?.selectedId || "launcher";
  const owned = new Set([
    "savesList",
    "serverList",
    "status",
    "modCatalog",
    "modDetail",
    "modCover",
    "saveGet",
    "modProfiles",
    "memoryGet",
    "memorySave",
    "saveSave",
    "javaList",
    "javaSave",
    "javaComponentsList",
    "javaComponentsInstall",
    "javaComponentsEnable",
    "javaComponentsPreview",
    "javaComponentsApply",
    "javaComponentsRecovery",
    "javaComponentsRecover",
    "javaComponentsReconcile",
    "javaComponentsInspectDownloaded",
    "componentsAction",
    "modSort",
    "steamDownload",
    "forceTakeoverPreview",
    "forceTakeoverApply",
    "buddyPreview",
    "buddyTakeover",
  ]);
  for (const action of [
    "configurationRequest",
    "configurationCreate",
    "configurationRename",
    "configurationVersion",
    "configurationDelete",
    "configurationSaveOrdinary",
    "frozenConfigurationSave",
    "frozenConfigurationApprove",
    "frozenConfigurationUninstall",
    "frozenConfigurationRecover",
  ])
    mutationActions.add(action);
  const pureReads = new Set([
    "status",
    "modCatalog",
    "modDetail",
    "modCover",
    "saveGet",
    "modProfiles",
    "memoryGet",
    "javaList",
    "javaComponentsList",
    "javaComponentsRecovery",
    "modSort",
    "savesList",
    "serverList",
  ]);
  const readId = () => crypto.randomUUID().replaceAll("-", "");
  async function ownerRequest(id, action, payload = {}, editor = null) {
    const token = pureReads.has(action) ? readId() : "";
    if (editor && token) editor.readRequests.add(token);
    let result;
    try {
      result = await raw("configurationRequest", {
        id,
        action,
        payload,
        ...(token ? { readId: token } : {}),
      });
    } finally {
      if (editor && token) editor.readRequests.delete(token);
    }
    if (result.configurationId !== id)
      throw Error("配置回复不匹配，请重新读取。");
    return result.data;
  }
  async function readView(editor) {
    const token = readId();
    editor.readRequests.add(token);
    try { return await raw("configurationRead", { id: editor.id, readId: token }); }
    finally { editor.readRequests.delete(token); }
  }
  request = async function (action, payload = {}) {
    if (!owned.has(action)) return raw(action, payload);
    if (!model.list) await reload();
    if (!model.list) throw Error(model.error || "启动配置尚未读取，请重新读取。");
    const id = selectedId(),
      epoch = model.epoch;
    const data = await ownerRequest(id, action, payload);
    if ((epoch !== model.epoch || id !== selectedId()) && pureReads.has(action)) {
      const superseded=Error("配置选择已变化，已忽略之前的读取。");
      superseded.supersededConfiguration=true;
      throw superseded;
    }
    return data;
  };
  const kind = (row) =>
    row?.kind === "frozen"
      ? "快照配置"
      : row?.kind === "live"
        ? "普通配置"
        : "内置配置";
  const description = (row) =>
    row?.id === "steam"
      ? "使用 Steam 与原游戏的启动设置，可按需接管。"
      : row?.id === "vanilla"
        ? "使用原版游戏，不加载普通模组和可选 Java 组件。"
        : row?.kind === "frozen"
          ? "固定游戏与模组副本；工坊更新不会改变这份快照。"
          : "使用所选游戏版本和当前模组文件。";
  const versionOptions = (id) =>
    (model.versions?.versions || [])
      .filter((v) => v.installed)
      .map(
        (v) =>
          `<option value="${esc(v.id)}" ${v.id === id ? "selected" : ""}>${esc(v.name)} · ${esc(v.version)}</option>`,
      )
      .join("");
  async function readCover(id, epoch, intentionalSelection = false) {
    const row = model.list?.configurations.find((r) => r.id === id);
    model.selected = row;
    model.cover = null;
    if (!row?.available) return;
    const [statusResult,memoryResult]=await Promise.allSettled([ownerRequest(id,"status"),ownerRequest(id,"memoryGet")]);
    if(epoch!==model.epoch||id!==selectedId())return;
    if(statusResult.status==='fulfilled'){
      const status=statusResult.value,memory=memoryResult.status==='fulfilled'?memoryResult.value:null;
      if(status.components&&independentCore(status.components))status.components=status.components.filter(c=>c.id!=='base');
      model.cover={status,memory};
      if(memory)state.actual.memory=memory;else delete state.actual.memory;
      acceptStatus(status,{intentionalSelection:intentionalSelection?id:null});
    }
    if(statusResult.status==='rejected')throw statusResult.reason;
    if(memoryResult.status==='rejected')throw memoryResult.reason;
  }
  async function reload(intentionalSelection = false) {
    if (model.reading) return model.reading;
    const epoch = model.epoch;
    model.error = "";
    model.reading = (async () => {
      try {
        const [list, versions] = await Promise.all([
          raw("launchConfigurations"),
          raw("versionList"),
        ]);
        if (epoch !== model.epoch) return;
        model.list = list;
        model.versions = versions;
        await readCover(list.selectedId, epoch, intentionalSelection);
      } catch (error) {
        if (epoch === model.epoch) model.error = error.message;
      } finally {
        model.reading = null;
        if (state.route === "home" || state.route === "components") render();
      }
    })();
    return model.reading;
  }
  async function select(id) {
    state.notice='';state.noticeRoute='';
    const epoch = ++model.epoch;
    const oldSelected=model.selected,oldCover=model.cover,oldList=model.list,oldId=selectedId();
    const draftKeys=['modProfile','modProfileMods','modProfileDirty','modProfileName','modProfileSaveFeedback','modProfileSaveError','modSortReport'];
    let selectionCommitted=false;
    const before={};for(const key of [...draftKeys,'modProfiles','modCatalog','java','memory','actual','playProfiles','playPreview','preflight'])before[key]=state[key];
    model.modWorkspaces ||= new Map();model.modWorkspaces.set(oldId,Object.fromEntries(draftKeys.map(key=>[key,state[key]])));
    model.selecting = id;
    libraryUi.invalidateJavaComponents();
    model.selected = model.list?.configurations.find((row) => row.id === id);
    model.cover = null;
    render();
    model.error = "";
    cancelLaunchAttempt();
    state.playProfiles = null;
    state.playPreview = null;
    state.preflight = null;
    state.actual = {};
    state.java = null;
    state.memory = null;
    state.modCatalog = null;
    state.modProfiles = null;
    state.modProfile = null;
    state.modProfileMods = [];
    state.modProfileDirty = false;
    state.modProfileReadSequence++;
    state.modSortReport = null;
    state.modReadRevision=(state.modReadRevision||0)+1;
    state.javaRevision=(state.javaRevision||0)+1;
    state.loading.modProfiles=false;state.loading.modCatalog=false;state.loading.modProfile=false;state.loading.java=false;
    const cached=model.modWorkspaces.get(id);if(cached)Object.assign(state,cached);
    try {
      const list = await raw("launchConfigurationSelect", { id });
      if (epoch !== model.epoch) return;
      selectionCommitted=true;
      model.list = list;
      model.selected = list.configurations.find((r) => r.id === id);
      render();
      await readCover(id, epoch, true);
      if(epoch===model.epoch&&state.route==='mods'){void load('modProfiles',true);void load('modCatalog',true);}
    } catch (error) {
      if (epoch === model.epoch) {model.error=error.message;if(!selectionCommitted){model.selected=oldSelected;model.cover=oldCover;model.list=oldList;Object.assign(state,before);}}
    } finally {
      if (epoch === model.epoch) {
        model.selecting = "";
        render();
      }
    }
  }
  const previousEnsure = ensureExperience;
  ensureExperience = function () {
    if (!model.list && !model.reading && !model.error) void reload();
    for (const [key, action] of [
      ["memory", "memoryGet"],
      ["saves", "savesList"],
      ["servers", "serverList"],
    ])
      if (
        !state.actual[key] &&
        !state.experienceReads[key] &&
        !state.experienceErrors[key]
      )
        experienceRead(key, action);
  };
  function displayVersion(row, status) {
    const known = value => value && value !== "未知";
    if (status?.gameVersion?.state === "known" && known(status.gameVersion.value))
      return status.gameVersion.value;
    return known(row?.version) ? row.version : "未知";
  }
  function selectedDisplay() {
    const id = selectedId(),
      row = model.list?.configurations.find(r => r.id === id),
      cover = model.cover?.status.configurationId === id ? model.cover : null,
      status = state.status?.configurationId === id ? state.status : cover?.status;
    return { row, cover, status, memory: cover ? state.actual.memory : null,
      version: displayVersion(row, status),
      components: status?.components?.filter(c => c.configured) || [] };
  }
  const previousHome = home;
  home = function () {
    const container = document.createElement("div");
    container.innerHTML = previousHome();
    const display = selectedDisplay(), { row, cover, memory, version, components } = display;
    container.querySelector(".launch-config").innerHTML =
      `<label for="launch-configuration-select">启动配置</label><select id="launch-configuration-select" ${!model.list ? "disabled" : ""}>${(model.list?.configurations || [{ id: "launcher", name: "启动器配置", kind: "builtin" }]).map((r) => `<option value="${esc(r.id)}" ${r.id === selectedId() ? "selected" : ""}>${esc(r.name)} · ${kind(r)}${r.version ? " · " + esc(r.id === selectedId() ? version : r.version) : ""}${r.available === false ? " · 版本不可用" : ""}</option>`).join("")}</select><p data-configuration-description>${esc(description(row))}</p>${row?.available === false ? '<p class="status-warning">' + esc(row.message) + "</p>" : ""}${model.error ? '<p class="status-warning">' + esc(model.error) + "</p>" : ""}<div class="actions"><button data-configuration-create>新建配置</button><button data-configuration-edit ${!row ? "disabled" : ""}>编辑配置</button><button data-configuration-reload>重新读取</button>${row?.id === "steam" ? "<button data-configuration-steam>接管 Steam 配置</button><button data-force-takeover>强制接管</button>" : ""}</div>`;
    const summary = container.querySelector("[data-current-config]");
    summary.textContent = cover
      ? `${memory ? memory.currentGiB + " GiB Java 堆" : "内存待读取"} · ${components.length} 个组件 · ${version}`
      : "正在读取所选配置…";
    container.querySelector(".current-config h2").textContent = "下次启动配置";
    const detail = container.querySelector(".current-config details");
    if (detail)
      detail.innerHTML =
        "<summary>配置详情</summary><p>" +
        esc(components.map((c) => c.title).join(" · ") || "未启用组件") +
        "</p>";
    const launch = container.querySelector("#launch");
    if (!row?.available || !cover || model.selecting) launch.disabled = true;
    container.insertAdjacentHTML(
      "beforeend",
      '<div class="configuration-jobs" data-configuration-jobs>' +
        jobsMarkup() +
        "</div>",
    );
    if (
      cover?.status.runtimeConfigurationName &&
      (cover.status.running || cover.status.startup?.pending) &&
      cover.status.runtimeConfigurationId !== row?.id
    )
      container.insertAdjacentHTML(
        "beforeend",
        '<p class="small" data-configuration-runtime>正在运行：' +
          esc(cover.status.runtimeConfigurationName) +
          " · 下次启动：" +
          esc(row?.name) +
          "</p>",
      );
    if (
      row?.id === "steam" &&
      (libraryUi.model.forceResult || libraryUi.model.forceError)
    )
      container.insertAdjacentHTML(
        "beforeend",
        '<section class="card section" data-configuration-steam-result><h3>Steam 配置接管结果</h3><p>' +
          esc(
            libraryUi.model.forceResult?.message || libraryUi.model.forceError,
          ) +
          "</p></section>",
      );
    return container.innerHTML;
  };
  function jobsMarkup() {
    return [...model.jobs.values()]
      .map(
        (job) =>
          `<section class="card section" data-configuration-job-owner="${esc(job.id)}"><h3>${esc(job.name)} · ${job.active ? "正在处理" : "操作结果"}</h3><p role="status">${esc(job.message)}</p>${job.active ? '<progress aria-label="' + esc(job.name) + '操作进度"></progress><button data-configuration-job-cancel="' + esc(job.id) + '">取消此配置任务</button>' : '<button data-configuration-job-dismiss="' + esc(job.id) + '">关闭结果</button>'}</section>`,
      )
      .join("");
  }
  function paintJobs() {
    const root = document.querySelector("[data-configuration-jobs]");
    if (root) root.innerHTML = jobsMarkup();
  }
  actualCover = function () {
    const { row, cover, memory, version, components } = selectedDisplay();
    return `<aside class="scene-status" data-home-actual aria-label="所选启动配置摘要"><h3>${esc(row?.name || "启动配置")}</h3><dl><dt>游戏版本</dt><dd>${esc(version)}</dd><dt>启动内存</dt><dd>${memory ? esc(memory.currentGiB) + " GiB" : "待读取"}</dd><dt>下次启动启用组件</dt><dd>${cover ? components.length + " 项" : "待读取"}</dd></dl><p class="configured-names">${esc(components.map((c) => c.title).join(" · ") || (cover ? "未启用组件" : "正在读取"))}</p></aside>`;
  };
  function editorStatus(editor, text, busy = false) {
    if (model.editor !== editor) return;
    editor.message = text;
    const root = editor.element,
      feedback = root.querySelector("[data-configuration-editor-status]");
    feedback.textContent = text;
    root.querySelector("[data-configuration-progress]").hidden = !busy;
    root.querySelector("[data-configuration-cancel]").hidden = !busy;
    root
      .querySelectorAll(
        "[data-configuration-write],[data-configuration-up],[data-configuration-down],[data-configuration-remove],[data-configuration-add]",
      )
      .forEach((b) => {
        if (busy) {
          if (!b.disabled) {
            b.dataset.configurationLock = "1";
            b.disabled = true;
          }
        } else if (b.dataset.configurationLock) {
          delete b.dataset.configurationLock;
          b.disabled = false;
        }
      });
  }
  const frozenPayload = (editor) => ({
    snapshotId: editor.id.slice(9),
    manifestHash: editor.view.frozen.manifestHash,
  });
  function meta(editor, id) {
    return (
      [...(editor.catalog || []), ...(editor.sources || [])].find(
        (r) => r.id === id,
      ) || { id, name: id }
    );
  }
  function paintMods() {
    const editor = model.editor;
    if (!editor) return;
    const root = editor.element,
      query = editor.search.toLowerCase(),
      enabledQuery = (editor.enabledSearch || "").toLowerCase();
    root.querySelector("[data-configuration-mods]").closest(".mod-pane").querySelector("h2").textContent=editor.dirty?"待保存清单":editor.frozen?"快照已启用":"已启用";
    root.querySelector("[data-configuration-available]").closest(".mod-pane").querySelector("h2").textContent=editor.frozen?"当前来源":"全部模组";
    root.querySelector("[data-configuration-mods]").innerHTML =
      editor.ids
        .filter((id) =>
          `${meta(editor, id).name} ${id}`.toLowerCase().includes(enabledQuery),
        )
        .map((id) => {
          const rawRow = meta(editor, id),row=rawRow.mod||rawRow,
            index = editor.ids.indexOf(id);
          return `<li data-configuration-mod="${esc(id)}" data-mod-row="${esc(id)}"><button class="mod-drag-handle" data-mod-drag="${esc(id)}" data-mod-drag-kind="configuration" aria-label="拖动 ${esc(row.name || id)} 调整顺序" ${editor.saving ? "disabled" : ""}>⠿</button><span class="order-number">${index + 1}.</span><div class="ordered-info"><div><h3>${esc(row.name || id)}</h3>${buddyDependencyBadge(row,[...(editor.catalog||[]),...(editor.sources||[])])}${modDependencyStates(row,editor.ids,[...(editor.catalog||[]),...(editor.sources||[])],editor.status)}<span class="small">${esc(id)}</span>${row.workshopId ? `<button data-mod-workshop="${esc(row.workshopId)}">工坊原页 ↗</button>` : ""}</div></div><div class="actions"><button data-configuration-up="${esc(id)}" ${index === 0 || editor.saving ? "disabled" : ""} aria-label="上移 ${esc(row.name || id)}">↑</button><button data-configuration-down="${esc(id)}" ${index === editor.ids.length - 1 || editor.saving ? "disabled" : ""} aria-label="下移 ${esc(row.name || id)}">↓</button><button data-configuration-remove="${esc(id)}" ${editor.saving ? "disabled" : ""}>移除</button></div>${modDetailsMarkup(row,editor.frozen&&!editor.catalog.some(item=>item.id===id)?'configuration-source':'configuration-enabled')}</li>`;
        })
        .join("") || "<li>没有符合搜索条件的模组。</li>";
    root.querySelector("[data-configuration-available]").innerHTML =
      (editor.sources || editor.catalog || [])
        .filter(
          (r) =>
            r.installationComponent !== true && r.id !== "ZombieBuddy",
        )
        .filter((r) => `${r.name} ${r.id}`.toLowerCase().includes(query))
        .map(
          (r, index) =>
            `<div class="configuration-source" data-mod-row="${esc(r.id)}"><span>${esc(r.name || r.id)} ${buddyDependencyBadge(r,[...(editor.catalog||[]),...(editor.sources||[])])}<small>${esc({ user: "本机模组", workshop: "工坊", local: "本机模组" }[r.sourceKind || r.source] || "当前来源")}${r.workshopId ? " · 工坊 " + esc(r.workshopId) : ""}</small></span><button data-configuration-add="${index}" data-source-key="${esc(r.token || r.id)}" ${editor.saving || editor.ids.includes(r.id) ? "disabled" : ""}>${editor.ids.includes(r.id) ? editor.dirty?"已选":"已启用" : editor.frozen ? "复制到快照" : "添加"}</button>${modDetailsMarkup(r.mod||r,'configuration-source')}</div>`,
        )
        .join("") || '<p class="small">没有可添加的匹配项。</p>';
    applyRichDescription(root);observeCovers();
  }
  async function readComponents(editor, epoch = editor.epoch) {
    const [inventory, approvals, status] = await Promise.all([
      ownerRequest(editor.id, "javaComponentsList", {}, editor),
      ownerRequest(editor.id, "javaList", {}, editor),
      ownerRequest(editor.id, "status", {}, editor),
    ]);
    if (model.editor !== editor || epoch !== editor.epoch) return;
    editor.inventory = inventory;
    libraryUi.acceptJavaComponents(editor.id, inventory, approvals);
    editor.approvals = approvals;
    editor.status = status;
    paintComponents(editor);
  }
  function paintComponents(editor) {
    if(model.editor!==editor)return;
    const all=editor.inventory?.rows||[],isFramework=row=>row.installationComponent===true||row.componentKind==='framework'||row.modId==='ZombieBuddy',rows=all.filter(isFramework),approvals=editor.approvals?.jars||[],root=editor.element.querySelector('[data-configuration-components]');
    const rowMarkup=row=>{const index=all.indexOf(row),approval=row.enabled?approvals.find(j=>j.id===row.modId):null,key=Object.keys(componentModIds).find(key=>componentModIds[key]===row.modId),item=row.workshopId||editor.status?.components?.find(c=>c.id===key)?.item;
      return `<article class="configuration-component-row" data-configuration-component="${esc(row.modId)}" data-configuration-source-token="${esc(row.token)}">${item?componentCover(key,item,row.name):'<span class="configuration-component-symbol" aria-hidden="true">⚙</span>'}<div class="configuration-component-info"><h3>${esc(knownComponentDetails[key]?.[0]||row.name||row.modId)}</h3><p class="small">${esc(row.modId)}${row.workshopId?' · 工坊 '+esc(row.workshopId):''}</p><p class="small ${row.enabled?'status-positive':''}">${row.enabled?'已启用':'未启用'}${row.jarState==='missing'?' · 文件缺失':''}</p>${approval?`<details data-configuration-java-source><summary>Java 文件与授权</summary><p class="small">Java 包 ${esc(row.packageName||'未声明')}</p><p class="key">SHA-256 ${esc(approval.sha256)}</p></details>`:''}${row.limitations?.length?'<p class="small status-warning">'+row.limitations.map(esc).join(' · ')+'</p>':''}</div><div class="actions">${approval?`<button data-configuration-approve="${esc(approval.approvalToken||approval.sha256)}" data-configuration-decision="${approval.decision!==true}" data-configuration-write>${approval.decision===true?'拒绝 Java 文件':'允许此 Java 文件'}</button>`:''}<button data-configuration-component-toggle="${index}" data-configuration-write ${row.enabled?'':'class="primary"'}>${row.enabled?'停用':'启用'}</button>${row.canUninstall?`<button data-configuration-component-remove="${index}" data-configuration-write>${editor.frozen?'移除副本':'从此配置移除'}</button>`:''}</div></article>`;
    };
    const missing=(editor.status?.components||[]).filter(c=>c.installationComponent===true||c.componentKind==='framework'||c.id==='buddy').filter(c=>!rows.some(row=>row.modId===componentModIds[c.id]));
    if(!root)return;
    root.innerHTML=`<div class="configuration-component-toolbar"><div><strong>加载框架与安装组件</strong><p class="small">这里只管理需要单独安装的框架。依赖 Buddy 的模组在模组清单中启用和排序。</p></div></div>${rows.map(rowMarkup).join('')}${missing.map(c=>`<article class="configuration-component-row">${componentCover(c.id,c.item,c.title)}<div class="configuration-component-info"><h3>${esc(knownComponentDetails[c.id]?.[0]||c.title)}</h3><p class="small">${c.configured?'已启用':'尚未安装'}</p></div><button data-configuration-known="${esc(c.id)}" data-configuration-write>${c.configured?'重新应用':'安装并启用'}</button></article>`).join('')}${!rows.length&&!missing.length?'<p class="small">此配置没有需要单独安装的框架。</p>':''}`;
    if(editor.saving)editorStatus(editor,editor.message,true);
  }
  async function openEditor(id = selectedId(), tab = "mods") {
    if (model.editor) {
      if (model.editor.element.isConnected && model.editor.element.open) return;
      releaseEditor(model.editor);
    }
    const element = document.createElement("dialog");
    element.id = "configuration-editor";
    element.className = "configuration-editor";
    const editor = {
      id,
      element,
      epoch: 0,
      tab: "mods",
      reading: true,
      readRequests: new Set(),
      ids: [],
      catalog: [],
      sources: null,
      tokens: new Map(),
      search: "",
      dirty: false,
      saving: false,
      view: null,
    };
    model.editor = editor;
    element.innerHTML =
      '<div class="row"><h2>正在读取配置…</h2><div class="actions"><button data-configuration-editor-reload>重新读取</button><button data-configuration-close>关闭</button></div></div><p data-configuration-editor-status role="status">正在读取此配置…</p><progress data-configuration-progress aria-label="配置操作进度"></progress><button data-configuration-cancel>取消此操作</button><div data-configuration-editor-body></div>';
    document.body.append(element);
    element.addEventListener("cancel", (event) => {
      event.preventDefault();
      void closeEditor();
    });
    element.addEventListener("close", () => releaseEditor(editor));
    try {
      element.showModal();
    } catch (error) {
      releaseEditor(editor);
      notify(error.message);
      return;
    }
    try {
      const view = await readView(editor);
      if (model.editor !== editor) return;
      editor.view = view;
      editor.frozen = view.kind === "frozen";
      editor.ids = [...view.ordinaryIds];
      const named = view.kind === "live";
      element.querySelector("h2").textContent = (editor.frozen ? "快照 " + view.name + " 的模组" : view.name + " · " + kind(view));
      element.querySelector("[data-configuration-editor-body]").innerHTML =
        `${named ? `<div class="configuration-fields"><label>配置名称<input data-configuration-name maxlength="100" value="${esc(view.name)}"></label><button data-configuration-rename data-configuration-write>保存名称</button><label>游戏版本<select data-configuration-version>${versionOptions(view.versionId)}</select></label><button data-configuration-set-version data-configuration-write>保存版本</button></div>` : `<p>游戏版本：${esc(view.frozen?.gameVersion || model.selected?.version || view.versionId)}${editor.frozen ? " · 副本版本已固定" : ""}</p>`}${!view.available ? `<p class="status-warning">${esc(view.message)}</p>` : ""}<section data-configuration-area="mods"><p>${editor.frozen ? "右侧使用此快照保存的副本；左侧为当前来源，新增时复制到此快照。启动继续使用该副本。" : "勾选要使用的模组，在右侧调整顺序后保存。"}</p><div class="mod-dual-pane configuration-dual-pane"><section class="mod-pane"><div class="mod-pane-heading"><h2>全部模组</h2></div><input type="search" data-configuration-search aria-label="搜索全部模组" placeholder="搜索名称或 ID"><div class="configuration-source-list" data-configuration-available></div></section><section class="mod-pane"><div class="mod-pane-heading"><h2>已启用</h2><button data-configuration-sort data-configuration-write ${!view.available ? "disabled" : ""}>自动排序</button></div><input type="search" data-configuration-enabled-search aria-label="搜索已启用模组" placeholder="在已启用模组中搜索"><ol class="enabled-mods configuration-mods" data-configuration-mods></ol></section></div><div class="actions"><button class="primary" data-configuration-save data-configuration-write ${!view.available || id === "steam" || id === "vanilla" ? "disabled" : ""}>保存模组清单</button></div></section>${named ? '<button class="danger" data-configuration-delete data-configuration-write>删除此配置</button>' : ""}`;
      const saveActions = element
        .querySelector("[data-configuration-save]")
        ?.closest(".actions");
      if (saveActions) {
        saveActions.classList.add("configuration-editor-actions");
        saveActions.hidden = false;
        element.append(saveActions);
      }
      if (!view.available) {
        editorStatus(editor, "此版本不可用；仍可修改名称或选择已安装版本。");
        return;
      }
      if (editor.frozen) {
        editor.catalog = view.frozen.copied;
        const sources = await raw("frozenConfigurationSources", {
          ...frozenPayload(editor),
          readId: (editor.sourceReadId = readId()),
        });
        if (model.editor !== editor) return;
        editor.sources = sources.sources;
      } else {
        const catalog = await ownerRequest(id, "modCatalog", {}, editor);
        if (model.editor !== editor) return;
        editor.catalog = catalog.mods || [];
      }
      paintMods();
      observeCovers();
      editor.status = await ownerRequest(editor.id, "status", {}, editor);
      if (model.editor !== editor) return;
      paintMods();
      editorStatus(editor, "模组清单尚未修改。");
    } catch (error) {
      if (model.editor === editor) {
        if (!editor.view) element.querySelector("h2").textContent = /取消/.test(error.message) ? "读取已取消" : "配置读取未完成";
        editorStatus(editor, error.message);
      }
    } finally {
      if (model.editor === editor) {
        editor.reading = false;
        element.querySelector("[data-configuration-progress]").hidden = true;
        element.querySelector("[data-configuration-cancel]").hidden = true;
      }
    }
  }
  async function reloadEditor(editor) {
    if (editor.reading || editor.saving) return;
    if (!editor.view) { const { id, tab } = editor; await closeEditor(); return openEditor(id, tab); }
    const epoch = ++editor.epoch;
    editor.reading = true;
    editorStatus(editor, "正在读取此配置，模组草稿会保留…", true);
    try {
      const view = await readView(editor);
      let catalog, sources;
      if (editor.frozen) {
        const result = await raw("frozenConfigurationSources", {
          snapshotId: editor.id.slice(9),
          manifestHash: view.frozen.manifestHash,
          readId: (editor.sourceReadId = readId()),
        });
        catalog = view.frozen.copied;
        sources = result.sources;
      } else {
        catalog = (
          await ownerRequest(editor.id, "modCatalog", { refresh: true }, editor)
        ).mods;
      }
      if (model.editor !== editor || epoch !== editor.epoch) return;
      editor.view = view;
      editor.catalog = catalog;
      editor.sources = sources;
      if (!editor.dirty) editor.ids = [...view.ordinaryIds];
      editor.status = await ownerRequest(editor.id, "status", {}, editor);
      if (model.editor === editor && epoch === editor.epoch) {
        paintMods();
        editorStatus(editor, "已重新读取，未保存的模组草稿保留。");
      }
    } catch (error) {
      if (model.editor === editor && epoch === editor.epoch)
        editorStatus(editor, error.message + " 模组草稿已保留。");
    } finally {
      if (model.editor === editor && epoch === editor.epoch)
        editor.reading = false;
    }
  }
  function releaseEditor(editor) {
    if (editor.released) return;
    editor.released = true;
    if (model.editor === editor) model.editor = null;
    for (const token of editor.readRequests)
      void raw("configurationReadCancel", {
        id: editor.id,
        readId: token,
      }).catch(() => {});
    if (editor.reading && editor.sourceReadId)
      void raw("frozenConfigurationReadCancel", {
        snapshotId: editor.id.slice(9),
        readId: editor.sourceReadId,
      }).catch(() => {});
    editor.element.remove();
    if (editor.saving) {
      paintJobs();
      notify(editor.view.name + "的操作继续进行，可在首页查看或取消。");
    }
  }
  async function closeEditor() {
    const editor = model.editor;
    if (!editor) return;
    if (
      !editor.saving &&
      editor.dirty &&
      !(await confirm(
        "放弃未保存的模组清单？",
        "尚未保存的模组选择和顺序将被放弃。",
      ))
    )
      return;
    if (model.editor !== editor) return;
    editor.element.close();
    releaseEditor(editor);
  }
  async function write(editor, operation, message, options = {}) {
    if (editor.saving) return;
    editor.saving = true;
    editor.epoch++;
    const job = {
      id: editor.id,
      name: editor.view.name,
      active: true,
      message,
      hash: editor.view.frozen?.manifestHash,
    };
    model.jobs.set(editor.id, job);
    paintJobs();
    editorStatus(editor, message, true);
    let completedMessage = "";
    try {
      const result = await operation();
      if(model.componentView?.id===editor.id)model.componentView=null;
      editor.message = result?.message || "此配置的操作已完成。";
      completedMessage = editor.message;
      if (result?.configuration && editor.frozen)
        editor.view.frozen = result.configuration;
      if (model.editor === editor && options.components !== false) {
        if (editor.frozen) {
          const current = await raw("frozenConfigurationRead", {
            snapshotId: editor.id.slice(9),
          });
          editor.view.frozen = current;
        }
        await readComponents(editor);
      }
      if (editor.id === selectedId() && options.cover !== false)
        await readCover(editor.id, model.epoch);
    } catch (error) {
      editor.message = completedMessage
        ? completedMessage + " 状态读取未完成，请重新读取。"
        : (/^无法原子保存快照元数据/.test(error.message)
            ? "快照配置文件未能写入，文件可能被占用。"
            : error.message) + (editor.dirty ? " 模组草稿已保留，可重试。" : "");
    } finally {
      editor.saving = false;
      job.active = false;
      job.message = editor.message;
      paintJobs();
      if (model.editor === editor) {
        editorStatus(editor, editor.message);
        paintMods();
      }
    }
  }
  async function save(editor) {
    await write(
      editor,
      async () => {
        let result;
        if (editor.frozen) {
          result = await raw("frozenConfigurationSave", {
            ...frozenPayload(editor),
            ordinaryIds: editor.ids,
            componentIds: editor.view.frozen.componentIds,
            sourceTokens: [...editor.tokens.values()],
          });
          editor.view.frozen = result.configuration;
          editor.catalog = result.configuration.copied;
        } else {
          const token = (editor.writeId = readId());
          model.jobs.get(editor.id).readId = token;
          try { result = await raw("configurationSaveOrdinary", { id: editor.id, ordinaryIds: editor.ids, readId: token }); }
          finally { if (editor.writeId === token) editor.writeId = ""; }
        }
        editor.dirty = false;
        editor.tokens.clear();
        return { ...result, message: "模组清单已保存；组件选择独立保留。" };
      },
      editor.frozen
        ? "正在核对并复制所选当前文件…"
        : "正在保存此配置的模组清单…",
      { components: false, cover: true },
    );
  }
  async function componentAction(editor, row, operation) {
    await write(
      editor,
      async () => {
        if (editor.frozen) {
          let result;
          if (operation === "uninstall")
            result = await raw("frozenConfigurationUninstall", {
              ...frozenPayload(editor),
              componentIds: [row.modId],
            });
          else
            result = await raw("frozenConfigurationSave", {
              ...frozenPayload(editor),
              ordinaryIds: editor.view.frozen.ordinaryIds.filter(
                (id) => operation !== "enable" || id !== row.modId,
              ),
              componentIds:
                operation === "enable"
                  ? [
                      ...new Set([
                        ...editor.view.frozen.componentIds,
                        row.modId,
                      ]),
                    ]
                  : editor.view.frozen.componentIds.filter(
                      (id) => id !== row.modId,
                    ),
              sourceTokens: [],
            });
          editor.view.frozen = result.configuration;
          if (operation === "enable")
            editor.ids = editor.ids.filter((id) => id !== row.modId);
          return {
            ...result,
            message:
              operation === "disable"
                ? "组件已停用，副本保留。"
                : result.message,
          };
        }
        const plan = await ownerRequest(editor.id, "javaComponentsPreview", {
          token: row.token,
          operation,
          stamp: editor.inventory.stamp,
          receiptId: "",
        });
        return ownerRequest(editor.id, "javaComponentsApply", {
          previewStamp: plan.previewStamp,
          confirmed: true,
        });
      },
      "正在保存此配置的组件更改…",
    );
  }
  async function create() {
    if (document.querySelector("#configuration-create")) return;
    const dialog = document.createElement("dialog");
    dialog.id = "configuration-create";
    dialog.className = "configuration-editor";
    dialog.innerHTML = `<h2>新建普通启动配置</h2><label>配置名称<input data-configuration-new-name maxlength="100" autofocus></label><label>游戏版本<select data-configuration-new-version>${versionOptions("steam")}</select></label><p>引用当前游戏与模组文件，不复制完整游戏。</p><p data-create-status role="status"></p><div class="actions"><button data-create-cancel>取消</button><button data-create-confirm class="primary">创建并编辑</button></div>`;
    document.body.append(dialog);
    dialog.addEventListener("close", () => dialog.remove());
    dialog.querySelector("[data-create-cancel]").onclick = () => dialog.close();
    dialog.querySelector("[data-create-confirm]").onclick = async () => {
      const button = dialog.querySelector("[data-create-confirm]");
      button.disabled = true;
      try {
        const row = await raw("configurationCreate", {
          name: dialog.querySelector("input").value,
          versionId: dialog.querySelector("select").value,
        });
        dialog.close();
        await select(row.id);
        model.list = null;
        await reload();
        await openEditor(row.id);
      } catch (error) {
        dialog.querySelector("[data-create-status]").textContent =
          error.message;
      } finally {
        button.disabled = false;
      }
    };
    dialog.showModal();
  }
  document.addEventListener("input", (event) => {
    const editor = model.editor;
    if (!editor || !event.target.closest("#configuration-editor")) return;
    if (event.target.matches("[data-configuration-enabled-search]")) {
      editor.enabledSearch = event.target.value;
      paintMods();
    }
    if (event.target.matches("[data-configuration-search]")) {
      editor.search = event.target.value;
      paintMods();
    }
  });
  document.addEventListener("change", (event) => {
    if (event.target.id === "launch-configuration-select")
      void select(event.target.value);
  });
  document.addEventListener("click", async (event) => {
    const button = event.target.closest("button");
    if (!button || button.disabled) return;
    const editor = model.editor;
    try {
      if (button.hasAttribute("data-configuration-create"))
        return void create();
      if (button.hasAttribute("data-configuration-edit"))
        return void openEditor();
      if (button.hasAttribute("data-configuration-components-open"))
        return void openEditor(selectedId(), "mods");
      if (button.hasAttribute("data-configuration-reload"))
        return void reload();
      if (button.dataset.configurationJobDismiss) {
        model.jobs.delete(button.dataset.configurationJobDismiss);
        paintJobs();
        return;
      }
      if (button.dataset.configurationJobCancel) {
        const id = button.dataset.configurationJobCancel, token = model.jobs.get(id)?.readId;
        await raw(
          token ? "configurationReadCancel" : id.startsWith("snapshot:") ? "frozenConfigurationCancel" : "configurationCancel",
          token ? { id, readId: token } : id.startsWith("snapshot:") ? { snapshotId: id.slice(9) } : { id },
        );
        return;
      }
      if (button.hasAttribute("data-configuration-steam")) {
        route("components");
        await scanBuddy();
        return;
      }
      if (!editor || !button.closest("#configuration-editor")) return;
      if (button.hasAttribute("data-configuration-editor-reload"))
        return void reloadEditor(editor);
      if (button.hasAttribute("data-configuration-close"))
        return void closeEditor();
      if (button.hasAttribute("data-configuration-cancel")) {
        if (editor.reading && !editor.saving) {
          await Promise.all([
            ...[...editor.readRequests].map(token => raw("configurationReadCancel", { id: editor.id, readId: token })),
            ...(editor.sourceReadId ? [raw("frozenConfigurationReadCancel", { snapshotId: editor.id.slice(9), readId: editor.sourceReadId })] : []),
          ]);
          return;
        }
        if (editor.writeId) {
          await raw("configurationReadCancel", { id: editor.id, readId: editor.writeId });
          return;
        }
        await raw(
          editor.frozen ? "frozenConfigurationCancel" : "configurationCancel",
          editor.frozen
            ? { snapshotId: editor.id.slice(9) }
            : { id: editor.id },
        );
        return;
      }
      if (button.dataset.configurationTab) {
        editor.tab = button.dataset.configurationTab;
        editor.element.querySelector(".configuration-editor-actions").hidden =
          button.dataset.configurationTab !== "mods";
        for (const area of editor.element.querySelectorAll(
          "[data-configuration-area]",
        ))
          area.hidden =
            area.dataset.configurationArea !== button.dataset.configurationTab;
        for (const tab of editor.element.querySelectorAll(
          "[data-configuration-tab]",
        ))
          tab.setAttribute("aria-pressed", String(tab === button));
        if (
          button.dataset.configurationTab === "components" &&
          !editor.inventory &&
          !editor.reading
        ) {
          editor.reading = true;
          editorStatus(editor, "正在核对组件来源…");
          try {
            await readComponents(editor);
            if (model.editor === editor)
              editorStatus(editor, "组件更改会立即保存到此配置。");
          } catch (error) {
            if (model.editor === editor) editorStatus(editor, error.message);
          } finally {
            if (model.editor === editor) editor.reading = false;
          }
        }
        return;
      }
      if (button.hasAttribute("data-configuration-save"))
        return void save(editor);
      if (button.hasAttribute("data-configuration-rename"))
        return void write(
          editor,
          async () => {
            const view = await raw("configurationRename", {
              id: editor.id,
              name: editor.element.querySelector("[data-configuration-name]")
                .value,
            });
            editor.view.name = view.name;
            editor.element.querySelector("h2").textContent =
              (editor.frozen ? "快照 " + view.name + " 的模组" : view.name + " · " + kind(view));
            model.list = null;
            await reload();
            return { message: "配置名称已保存。" };
          },
          "正在保存名称…",
          { components: false, cover: false },
        );
      if (button.hasAttribute("data-configuration-set-version"))
        return void write(
          editor,
          async () => {
            const view = await raw("configurationVersion", {
              id: editor.id,
              versionId: editor.element.querySelector(
                "[data-configuration-version]",
              ).value,
            });
            editor.view = view;
            if (!editor.dirty) editor.ids = [...view.ordinaryIds];
            const catalog = await ownerRequest(
              editor.id,
              "modCatalog",
              {},
              editor,
            );
            editor.catalog = catalog.mods || [];
            editor.element.querySelector("[data-configuration-save]").disabled =
              false;
            editor.element.querySelector("[data-configuration-sort]").disabled =
              false;
            editor.element.querySelector(".status-warning")?.remove();
            model.list = null;
            await reload(true);
            return {
              message: "游戏版本已保存，已读取该版本的当前模组。",
            };
          },
          "正在保存版本…",
          { cover: false },
        );
      if (button.hasAttribute("data-configuration-delete")) {
        if (
          !(await confirm(
            "删除此启动配置？",
            "仅删除此普通配置的登记；原游戏、工坊和存档保留。",
          ))
        )
          return;
        await raw("configurationDelete", { id: editor.id });
        editor.dirty = false;
        await closeEditor();
        model.list = null;
        await reload();
        return;
      }
      if (button.hasAttribute("data-configuration-sort"))
        return void write(
          editor,
          async () => {
            const sorted = await ownerRequest(editor.id, "modSort", {
              mods: editor.ids,
            });
            const valid =
              !(sorted.errors || []).length &&
              Array.isArray(sorted.mods) &&
              sorted.mods.length === editor.ids.length &&
              editor.ids.every((id) => sorted.mods.includes(id));
            if (
              valid &&
              sorted.mods.some((id, index) => id !== editor.ids[index])
            ) {
              editor.ids = [...sorted.mods];
              editor.dirty = true;
            }
            return {
              message:
                (valid ? "排序建议已载入草稿；" : "保留当前模组顺序；") +
                [
                  ...(sorted.warnings || []),
                  ...(sorted.errors || []),
                  ...(sorted.violations || []),
                ].join("；") +
                " 可调整后保存。",
            };
          },
          "正在读取作者排序建议…",
          { components: false, cover: false },
        );
      if (
        button.hasAttribute("data-configuration-component-toggle") ||
        button.hasAttribute("data-configuration-component-remove")
      ) {
        const row =
            editor.inventory.rows[
              Number(
                button.dataset.configurationComponentToggle ??
                  button.dataset.configurationComponentRemove,
              )
            ],
          operation = button.hasAttribute("data-configuration-component-remove")
            ? "uninstall"
            : row.enabled
              ? "disable"
              : "enable";
        if (
          operation === "uninstall" &&
          !(await confirm(
            editor.frozen ? "移除这个组件副本？" : "从此配置移除组件？",
            editor.frozen
              ? "最后一个未被引用的副本将直接删除；被其他模组依赖的文件仍保留。"
              : "原工坊文件保留，其他配置不受影响。",
          ))
        )
          return;
        return void componentAction(editor, row, operation);
      }
      if (button.dataset.configurationApprove)
        return void write(
          editor,
          async () => {
            const decision = button.dataset.configurationDecision === "true";
            if (editor.frozen) {
              const result = await raw("frozenConfigurationApprove", {
                ...frozenPayload(editor),
                approvalToken: button.dataset.configurationApprove,
                decision,
              });
              editor.view.frozen = result.configuration;
              return { ...result, message: "此副本的 Java 决定已保存。" };
            }
            await ownerRequest(editor.id, "javaSave", {
              stamp: editor.approvals.stamp,
              hash: button.dataset.configurationApprove,
              decision,
            });
            return { message: "此来源的当前 Java 文件决定已保存。" };
          },
          "正在保存 Java 文件授权…",
        );
      if (button.hasAttribute("data-configuration-known"))
        return void write(
          editor,
          () =>
            ownerRequest(editor.id, "componentsAction", {
              component: button.dataset.configurationKnown,
              operation: "install",
              confirmed: true,
            }),
          "正在核对、下载并选择组件…",
        );
      if (button.hasAttribute("data-configuration-download"))
        return void write(
          editor,
          () =>
            ownerRequest(editor.id, "javaComponentsInstall", {
              id: editor.element.querySelector("[data-configuration-workshop]")
                .value,
              confirmed: true,
            }),
          "正在下载工坊组件…",
        );
      if (editor.saving) return;
      const up = button.dataset.configurationUp,
        down = button.dataset.configurationDown,
        remove = button.dataset.configurationRemove;
      if (up || down) {
        const index = editor.ids.indexOf(up || down),
          other = index + (up ? -1 : 1);
        [editor.ids[index], editor.ids[other]] = [
          editor.ids[other],
          editor.ids[index],
        ];
        editor.dirty = true;
        paintMods();
      } else if (remove) {
        editor.ids = editor.ids.filter((id) => id !== remove);
        editor.tokens.delete(remove);
        editor.dirty = true;
        paintMods();
      } else if (button.hasAttribute("data-configuration-add")) {
        const key = button.dataset.sourceKey,
          row = (editor.sources || editor.catalog).find(
            (r) => (r.token || r.id) === key,
          );
        if (!row || row.installationComponent === true || row.id === "ZombieBuddy") return;
        editor.ids.push(row.id);
        if (
          row.token &&
          !editor.view.frozen?.copied.some((c) => c.id === row.id)
        )
          editor.tokens.set(row.id, row.token);
        editor.dirty = true;
        paintMods();
      }
    } catch (error) {
      if (editor) editorStatus(editor, error.message);
      else notify(error.message);
    }
  });
  function progressLabel(progress) {
    const count =
      progress.completedFiles > 0 || progress.totalFiles > 0
        ? "文件 " +
          progress.completedFiles +
          (progress.totalFiles > 0 ? " / " + progress.totalFiles : "")
        : "";
    const size =
      progress.completedBytes > 0 || progress.totalBytes > 0
        ? formatBytes(progress.completedBytes) +
          (progress.totalBytes > 0
            ? " / " + formatBytes(progress.totalBytes)
            : "")
        : "";
    return [progress.text, count, size].filter(Boolean).join(" · ");
  }
  function formatBytes(value) {
    return value >= 1048576
      ? (value / 1048576).toFixed(1) + " MiB"
      : value >= 1024
        ? (value / 1024).toFixed(1) + " KiB"
        : value + " B";
  }
  document.addEventListener("ozl-configuration-progress", (event) => {
    const editor = model.editor,
      data = event.detail;
    const progress = data.progress || data;
    if (data.readId) {
      const job = model.jobs.get(data.configurationId);
      if (job?.active && job.readId === data.readId) { job.message = progressLabel(progress); paintJobs(); }
      if (editor?.saving && editor.id === data.configurationId && editor.writeId === data.readId) editorStatus(editor, progressLabel(progress), true);
      if (
        editor?.reading &&
        (editor.readRequests.has(data.readId) ||
          editor.sourceReadId === data.readId)
      )
        editorStatus(editor, progressLabel(progress), true);
      return;
    }
    const ownedJob = model.jobs.get(data.configurationId);
    if (
      ownedJob?.hash &&
      data.manifestHash &&
      ownedJob.hash !== data.manifestHash
    )
      return;
    const job = model.jobs.get(data.configurationId);
    if (job?.active) {
      job.message = progressLabel(progress) || job.message;
      paintJobs();
    }
    if (!editor || data.configurationId !== editor.id || !editor.saving) return;
    editorStatus(editor, progressLabel(progress) || "正在处理此配置…", true);
    const bar = editor.element.querySelector("[data-configuration-progress]");
    if (progress.value >= 0) {
      bar.max = 100;
      bar.value = progress.value;
    } else bar.removeAttribute("value");
  });
  const previousDrafts = hasUnsavedDrafts;
  hasUnsavedDrafts = (options) =>
    previousDrafts(options) ||
    !!model.editor?.dirty ||
    [...model.jobs.values()].some(
      (job) => job.active && job.id === selectedId(),
    );
  const previousComponents = components;
  components = function () {
    const id=selectedId(),row=model.list?.configurations.find(row=>row.id===id),name=row?.name||'启动配置';
    const label=`<section class="component-configuration-label" data-component-configuration><label for="launch-configuration-select">启动配置<select id="launch-configuration-select" ${!model.list?'disabled':''}>${(model.list?.configurations||[{id:'launcher',name:'启动器配置'}]).map(option=>`<option value="${esc(option.id)}" ${option.id===id?'selected':''}>${esc(option.name)}${option.version?' · '+esc(option.version):''}${option.available===false?' · 版本不可用':''}</option>`).join('')}</select></label><button data-page="mods">查看模组配置</button></section>`;
    if(state.status?.configurationId!==id)return heading()+label+'<p class="read-pending">正在读取当前配置…</p>';
    return previousComponents().replace('<div data-quick-config-slot></div>',label);
  };
  return {
    model,
    reload,
    select,
    openEditor,
    ownerRequest,
    paintMods,
    editor: () => model.editor,
    dragIds: () => model.editor?.ids || [],
    dragCommit(ids) {
      if (!model.editor) return;
      model.editor.ids = ids;
      model.editor.dirty = true;
    },
  };
})();
