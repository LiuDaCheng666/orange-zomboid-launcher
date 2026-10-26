/* Names, descriptions and all remote strings are rendered as plain text. */
"use strict";
const $ = (id) => document.getElementById(id);
let csrf = "", state = null, selected = "", busy = false;
const form = $("server-form");
function notice(message, error = false) { const box = $("notice"); box.textContent = message; box.classList.toggle("error", error); box.hidden = !message; }
async function api(path, method = "GET", body) {
  const response = await fetch(path, { method, credentials: "same-origin", headers: { "Content-Type": "application/json", "X-CSRF-Token": csrf }, body: body === undefined ? undefined : JSON.stringify(body) });
  const result = await response.json();
  if (!response.ok) { if (result.saved && result.document) { state = result.document; edit(null); render(); } const error = new Error(result.error || "操作失败，请重试。"); error.status = response.status; throw error; }
  return result;
}
async function action(run) {
  if (busy) return;
  busy = true; document.querySelectorAll("button").forEach((button) => { button.disabled = true; });
  try { await run(); } catch (error) { notice(error.message || "无法连接服务，请确认本地管理服务已启动。", true); if (error.status === 401) showLogin(); }
  finally { busy = false; document.querySelectorAll("button").forEach((button) => { button.disabled = false; }); if (state) setOrderButtons(); }
}
function showLogin() { $("login-panel").hidden = false; $("workspace").hidden = true; $("logout").hidden = true; csrf = ""; state = null; }
function showWorkspace() { $("login-panel").hidden = true; $("workspace").hidden = false; $("logout").hidden = false; }
function element(tag, className, value) { const item = document.createElement(tag); if (className) item.className = className; if (value !== undefined) item.textContent = value; return item; }
function button(label, className, run) { const item = element("button", className, label); item.type = "button"; item.addEventListener("click", run); return item; }
function setOrderButtons() { document.querySelectorAll("[data-order]").forEach((item) => { item.disabled = item.dataset.order === "first" || item.dataset.order === "last"; }); }
function edit(row) {
  selected = row ? row.id : "";
  form.reset();
  for (const name of ["id", "name", "host", "port", "queryPort", "version", "description", "cover", "order"]) form.elements[name].value = row ? row[name] : ({ port: 16261, queryPort: 0, order: state ? state.servers.length : 0 }[name] ?? "");
  form.elements.tags.value = row ? row.tags.join("，") : "";
  form.elements.published.checked = row ? row.published : false;
  form.elements.useSteamRelay.checked = row ? row.useSteamRelay : false;
  $("editor-title").textContent = row ? "编辑服务器" : "添加服务器";
  $("editor").hidden = false;
  renderList();
}
function renderList() {
  const list = $("server-list"); list.replaceChildren();
  if (!state || state.servers.length === 0) { const empty = element("div", "empty-list"); empty.append(element("strong", "", "还没有推荐服务器"), element("p", "", "添加第一个服务器，整理好信息后再发布。")); list.append(empty); return; }
  state.servers.forEach((row, index) => {
    const card = element("article", "server-card" + (row.id === selected ? " selected" : ""));
    const top = element("div", "card-top"), mark = element("div", "server-mark", row.name.slice(0, 1));
    if (row.cover) { const image = document.createElement("img"); image.src = row.cover; image.alt = ""; image.referrerPolicy = "no-referrer"; image.addEventListener("error", () => { image.remove(); mark.textContent = row.name.slice(0, 1); }); mark.replaceChildren(image); }
    const info = element("div", "server-info"); info.append(element("div", "server-name", row.name), element("div", "server-address", (row.host.includes(":") ? "[" + row.host + "]" : row.host) + ":" + row.port));
    top.append(mark, info, element("span", "badge" + (row.published ? "" : " draft"), row.published ? "已发布" : "草稿"));
    card.append(top);
    if (row.description) card.append(element("p", "description", row.description));
    const tags = element("div", "tags"); if (row.version) tags.append(element("span", "tag", "版本 " + row.version)); row.tags.forEach((tag) => tags.append(element("span", "tag", tag))); card.append(tags);
    const footer = element("div", "card-footer"), ordering = element("div", "order-actions"), actions = element("div", "card-actions");
    const up = button("↑", "", () => reorder(index, -1)), down = button("↓", "", () => reorder(index, 1)); up.setAttribute("aria-label", "上移 " + row.name); down.setAttribute("aria-label", "下移 " + row.name); up.dataset.order = index === 0 ? "first" : "middle"; down.dataset.order = index === state.servers.length - 1 ? "last" : "middle"; ordering.append(up, down);
    actions.append(button("编辑", "text-button", () => { edit(row); $("editor").scrollIntoView({ behavior: "smooth", block: "nearest" }); }), button(row.published ? "撤下推荐" : "发布推荐", "text-button", () => action(async () => { state = await api("/api/admin/publish", "POST", { revision: state.revision, id: row.id, published: !row.published }); render(); notice(row.published ? "服务器已撤下。" : "服务器已发布到推荐列表。"); })), button("删除", "text-button danger", () => { if (confirm("删除「" + row.name + "」？此操作会移除这条推荐信息。")) action(async () => { state = await api("/api/admin/servers/" + row.id, "DELETE", { revision: state.revision }); if (selected === row.id) edit(null); render(); notice("服务器已删除。"); }); }));
    footer.append(ordering, actions); card.append(footer); list.append(card);
  });
  setOrderButtons();
}
function render() { const publication = state.publication; $("publication-panel").hidden = !publication || !publication.pending; $("publication-status").textContent = publication && publication.pending ? "列表已保存，公开列表仍为上次同步的内容。请重试公开同步。" : ""; $("total-count").textContent = state.servers.length; const count = state.servers.filter((row) => row.published).length; $("published-count").textContent = count; $("draft-count").textContent = state.servers.length - count; const updated = new Date(state.updatedUtc); $("updated-at").textContent = Number.isNaN(updated.getTime()) ? "—" : updated.toLocaleString("zh-CN", { month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" }); renderList(); }
function reorder(index, direction) { action(async () => { const ids = state.servers.map((row) => row.id); const other = index + direction; if (other < 0 || other >= ids.length) return; [ids[index], ids[other]] = [ids[other], ids[index]]; state = await api("/api/admin/order", "POST", { revision: state.revision, ids }); render(); notice("推荐顺序已保存。"); }); }
$("login-form").addEventListener("submit", (event) => { event.preventDefault(); action(async () => { const data = new FormData(event.target); const session = await api("/api/admin/login", "POST", { username: data.get("username"), password: data.get("password") }); csrf = session.csrf; event.target.elements.password.value = ""; state = await api("/api/admin/servers"); showWorkspace(); edit(null); render(); notice(""); }); });
form.addEventListener("submit", (event) => { event.preventDefault(); action(async () => { const data = new FormData(form); const row = {}; for (const field of ["id", "name", "host", "version", "description", "cover"]) row[field] = data.get(field).trim(); for (const field of ["port", "queryPort", "order"]) row[field] = Number(data.get(field)); row.tags = data.get("tags").split(/[,，]/).map((tag) => tag.trim()).filter(Boolean); row.published = form.elements.published.checked; row.useSteamRelay = form.elements.useSteamRelay.checked; state = await api("/api/admin/servers", "POST", { revision: state.revision, server: row }); edit(null); render(); notice(row.published ? "服务器已保存并发布。" : "草稿已保存。"); }); });
$("add").addEventListener("click", () => { edit(null); form.elements.name.focus(); $("editor").scrollIntoView({ behavior: "smooth", block: "nearest" }); });
for (const id of ["close-editor", "cancel-edit"]) $(id).addEventListener("click", () => { selected = ""; $("editor").hidden = true; renderList(); });
$("refresh").addEventListener("click", () => action(async () => { state = await api("/api/admin/servers"); edit(null); render(); notice("已获取最新列表。"); }));
$("retry-export").addEventListener("click", () => action(async () => { state = await api("/api/admin/export", "POST", {}); render(); notice("公开列表已同步。"); }));
$("logout").addEventListener("click", () => action(async () => { await api("/api/admin/logout", "POST", {}); showLogin(); notice("已退出登录。"); }));
(async () => { try { const session = await api("/api/admin/session"); csrf = session.csrf; state = await api("/api/admin/servers"); showWorkspace(); edit(null); render(); } catch (error) { showLogin(); if (error.status !== 401) notice("无法连接服务，请确认本地管理服务已启动。", true); } })();
