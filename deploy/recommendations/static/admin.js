/* Remote fields stay text; detailed HTML uses the shared display-only formatter. */
"use strict";
const $ = (id) => document.getElementById(id);
let csrf = "", state = null, selected = "", busy = false;
const form = $("server-form");
let richDirty = false, richRange = null, richMode = "visual";
// BEGIN server description formatter
function cleanServerDescriptionHtml(value){
 const input=document.createElement('template');input.innerHTML=typeof value==='string'?value:'';
 const allowed=new Set('h1 h2 h3 h4 h5 h6 p div span b strong i em u s mark sub sup ul ol li blockquote pre code a table thead tbody tfoot tr th td hr br'.split(' '));
 const blocked=new Set('script style iframe object embed form input button textarea select option svg math template link meta base img video audio source canvas'.split(' '));
 const styles=new Set('font-family font-size font-weight font-style line-height letter-spacing color background-color text-align text-decoration white-space border border-width border-style border-color border-radius padding margin table-layout border-collapse border-spacing'.split(' '));
 for(const side of ['top','right','bottom','left']){styles.add('padding-'+side);styles.add('margin-'+side);styles.add('border-'+side);for(const suffix of ['width','style','color'])styles.add('border-'+side+'-'+suffix);}
 const lengthList=(value,maximum,allowAuto=false)=>{const parts=value.trim().split(/\s+/);if(parts.length>4)return '';const result=[];for(const part of parts){if(allowAuto&&part==='auto'){result.push(part);continue;}const match=/^(\d+(?:\.\d+)?)(px|pt|em|rem|%)?$/.exec(part);if(!match)return '';const scale=({px:1,pt:4/3,em:14,rem:14,'%':.14})[match[2]||'px'];result.push(Math.min(maximum,Number(match[1])*scale)+'px');}return result.join(' ');};
 const safeStyle=(name,value)=>{
  if(!styles.has(name)||/url\s*\(|expression\s*\(|var\s*\(|@|[<>\\]/i.test(value))return '';
  if(name==='font-size'){const sizes={'xx-small':12,'x-small':12,small:12,medium:14,large:18,'x-large':24,'xx-large':32};if(sizes[value])return sizes[value]+'px';const size=lengthList(value,48);return size&&!size.includes(' ')?Math.max(12,parseFloat(size))+'px':'';}
  if(/^(?:padding|margin)(?:-|$)/.test(name)||name==='border-spacing')return lengthList(value,64,name.startsWith('margin'));
  if(name==='line-height'){if(/^\d+(?:\.\d+)?$/.test(value))return String(Math.min(3,Math.max(1,Number(value))));return lengthList(value,72);}
  if(name==='letter-spacing')return lengthList(value,12);
  if(name==='border-radius')return lengthList(value,32);
  if(name==='border-width'||/^border-(?:top|right|bottom|left)-width$/.test(name))return lengthList(value,16)||(['thin','medium','thick'].includes(value)?value:'');
  const parser=document.createElement('span').style;parser.setProperty(name,value);
  if(name==='border'||/^border-(?:top|right|bottom|left)$/.test(name)){const property=name==='border'?'border-top-width':name+'-width',width=lengthList(parser.getPropertyValue(property),16);if(width)parser.setProperty(name==='border'?'border-width':property,width);}
  return parser.getPropertyValue(name);
 };
 const clean=node=>{
  if(node.nodeType===Node.TEXT_NODE)return document.createTextNode(node.nodeValue||'');
  if(node.nodeType!==Node.ELEMENT_NODE)return document.createDocumentFragment();
  const tag=node.tagName.toLowerCase();if(blocked.has(tag)||node.namespaceURI!=='http://www.w3.org/1999/xhtml')return document.createDocumentFragment();
  const output=allowed.has(tag)?document.createElement(tag):document.createDocumentFragment();
  if(output.nodeType===Node.ELEMENT_NODE){
   for(const name of node.style){const value=safeStyle(name,node.style.getPropertyValue(name));if(value)output.style.setProperty(name,value);}
   if(node.hasAttribute('title'))output.setAttribute('title',node.getAttribute('title'));
   if(tag==='a'){try{const url=new URL(node.getAttribute('href')||'');if(['http:','https:'].includes(url.protocol)&&!url.username&&!url.password){output.setAttribute('href',url.href);output.setAttribute('target','_blank');output.setAttribute('rel','noopener noreferrer');}}catch{}}
   if(['td','th'].includes(tag))for(const key of ['colspan','rowspan']){const number=Number(node.getAttribute(key));if(Number.isInteger(number)&&number>=1&&number<=24)output.setAttribute(key,String(number));}
   if(tag==='ol'){const start=Number(node.getAttribute('start'));if(Number.isInteger(start)&&start>=1&&start<=99999)output.setAttribute('start',String(start));}
  }
  for(const child of node.childNodes)output.append(clean(child));return output;
 };
 const result=document.createElement('div');for(const node of input.content.childNodes)result.append(clean(node));return result.innerHTML;
}
// END server description formatter
function textHtml(value) { const block = document.createElement("div"); block.textContent = value || ""; return block.innerHTML.replace(/\r?\n/g, "<br>"); }
function richDisplay(value, fallback) { const content = document.createElement("template"); content.innerHTML = cleanServerDescriptionHtml(value); return content.content.textContent.trim() ? content.innerHTML : textHtml(fallback); }
function plainDescription(html) {
  const template = document.createElement("template"); template.innerHTML = html;
  const block = new Set("p div h1 h2 h3 h4 h5 h6 li blockquote pre tr hr".split(" "));
  const read = node => node.nodeType === Node.TEXT_NODE ? node.nodeValue : node.nodeType !== Node.ELEMENT_NODE ? "" : node.tagName === "BR" ? "\n" : [...node.childNodes].map(read).join("") + (block.has(node.tagName.toLowerCase()) ? "\n" : ["TD", "TH"].includes(node.tagName) ? "\t" : "");
  return Array.from([...template.content.childNodes].map(read).join("").replace(/[ \t]+\n/g, "\n").replace(/\n{3,}/g, "\n\n").trim()).slice(0, 8000).join("");
}
function richCount(html) { const count = Array.from(html).length; $("rich-count").textContent = count + " / 32768"; $("rich-count").classList.toggle("over-limit", count > 32768); }
function richReset(row) {
  richDirty = false; richRange = null;
  const original = row?.descriptionHtml || "", display = richDisplay(original, form.elements.extendedDescription.value);
  form.elements.descriptionHtml.value = original;
  $("description-rich").innerHTML = display;
  $("description-source").value = original || display;
  $("description-preview").innerHTML = display;
  form.elements.extendedDescription.readOnly = !!original;
  $("rich-link-panel").hidden = true;
  richCount(original); richTab("visual");
}
function richUpdate(html, source = false) {
  const clean = cleanServerDescriptionHtml(html);
  richDirty = true; form.elements.descriptionHtml.value = source ? html : clean;
  form.elements.extendedDescription.value = plainDescription(clean);
  form.elements.extendedDescription.readOnly = true;
  $("description-preview").innerHTML = clean;
  if (source) { $("description-rich").innerHTML = clean; richRange = null; }
  else $("description-source").value = clean;
  richCount(form.elements.descriptionHtml.value);
}
function richTab(mode) {
  richMode = mode;
  $("rich-visual-panel").hidden = mode !== "visual"; $("rich-source-panel").hidden = mode !== "source";
  $("rich-visual-tab").setAttribute("aria-selected", String(mode === "visual")); $("rich-source-tab").setAttribute("aria-selected", String(mode === "source"));
  if (mode === "source") $("description-preview").innerHTML = richDisplay($("description-source").value, form.elements.extendedDescription.value);
}
function rememberRichRange() {
  const selection = window.getSelection(); if (!selection?.rangeCount) return;
  const range = selection.getRangeAt(0), editor = $("description-rich");
  if (editor.contains(range.commonAncestorContainer)) richRange = range.cloneRange();
}
function restoreRichRange() {
  const editor = $("description-rich"); editor.focus(); const selection = window.getSelection(); selection.removeAllRanges();
  if (richRange && editor.contains(richRange.commonAncestorContainer)) selection.addRange(richRange);
  else { const range = document.createRange(); range.selectNodeContents(editor); range.collapse(false); selection.addRange(range); }
}
function richCommand(command, value) { restoreRichRange(); document.execCommand("styleWithCSS", false, true); document.execCommand(command, false, value); richUpdate($("description-rich").innerHTML); rememberRichRange(); }
function richInsert(html) { richCommand("insertHTML", cleanServerDescriptionHtml(html)); }
function richCallout() {
  restoreRichRange(); const selection = window.getSelection(), range = selection.getRangeAt(0), box = document.createElement("div");
  box.setAttribute("style", "border-left:4px solid #ec994c;padding:12px;border-radius:4px");
  box.append(range.collapsed ? document.createTextNode("在这里填写提示内容") : range.extractContents());
  range.insertNode(box); range.selectNodeContents(box); selection.removeAllRanges(); selection.addRange(range);
  richUpdate($("description-rich").innerHTML); rememberRichRange();
}
document.addEventListener("selectionchange", rememberRichRange);
$("description-rich").addEventListener("input", () => richUpdate($("description-rich").innerHTML));
for (const eventName of ["paste", "drop"]) $("description-rich").addEventListener(eventName, event => {
  event.preventDefault(); const data = event.clipboardData || event.dataTransfer;
  if (!data) return; const html = data.getData("text/html");
  if (html) richInsert(html); else richCommand("insertText", data.getData("text/plain"));
});
$("description-source").addEventListener("input", () => richUpdate($("description-source").value, true));
$("rich-visual-tab").addEventListener("click", () => richTab("visual")); $("rich-source-tab").addEventListener("click", () => richTab("source"));
$("rich-theme-toggle").addEventListener("click", () => { const dark = $("rich-theme-toggle").getAttribute("aria-pressed") !== "true"; $("rich-theme-toggle").setAttribute("aria-pressed", String(dark)); $("rich-theme-toggle").textContent = dark ? "浅色预览" : "深色预览"; document.querySelector(".rich-editor").dataset.previewTheme = dark ? "dark" : "light"; });
document.querySelectorAll(".rich-tools button").forEach(button => button.addEventListener("mousedown", event => event.preventDefault()));
document.querySelectorAll("[data-rich-command]").forEach(button => button.addEventListener("click", () => richCommand(button.dataset.richCommand)));
$("rich-block").addEventListener("change", () => richCommand("formatBlock", $("rich-block").value));
$("rich-align").addEventListener("change", () => richCommand($("rich-align").value));
$("rich-font-size").addEventListener("change", () => {
  const size = $("rich-font-size").value; if (!size) return;
  restoreRichRange(); document.execCommand("styleWithCSS", false, true); document.execCommand("fontSize", false, "7");
  $("description-rich").querySelectorAll('font[size="7"]').forEach(font => { const span = document.createElement("span"); span.style.cssText = font.style.cssText; span.style.fontSize = size + "px"; span.append(...font.childNodes); font.replaceWith(span); });
  $("description-rich").querySelectorAll("span").forEach(span => { if (span.style.fontSize === "xxx-large") span.style.fontSize = size + "px"; });
  richUpdate($("description-rich").innerHTML); rememberRichRange();
});
$("rich-color").addEventListener("input", () => richCommand("foreColor", $("rich-color").value));
$("rich-background").addEventListener("input", () => richCommand("hiliteColor", $("rich-background").value));
$("rich-callout").addEventListener("click", richCallout);
$("rich-table").addEventListener("click", () => richInsert('<table style="border-collapse:collapse;table-layout:fixed"><tbody><tr><th>项目</th><th>说明</th></tr><tr><td>填写内容</td><td>填写内容</td></tr></tbody></table><p><br></p>'));
$("rich-link").addEventListener("click", () => { $("rich-link-panel").hidden = false; $("rich-link-url").value = ""; $("rich-link-url").focus(); });
$("rich-link-cancel").addEventListener("click", () => { $("rich-link-panel").hidden = true; restoreRichRange(); });
$("rich-link-apply").addEventListener("click", () => { try { const url = new URL($("rich-link-url").value); if (!["http:", "https:"].includes(url.protocol) || url.username || url.password) throw new Error(); restoreRichRange(); if (window.getSelection().isCollapsed) { const link = document.createElement("a"); link.href = url.href; link.textContent = url.href; richInsert(link.outerHTML); } else richCommand("createLink", url.href); $("rich-link-panel").hidden = true; } catch { notice("请填写完整的 HTTP 或 HTTPS 链接。", true); } });
$("description-preview").addEventListener("click", event => { if (event.target.closest("a")) event.preventDefault(); });
form.elements.extendedDescription.addEventListener("input", () => { if (!richDirty && !form.elements.descriptionHtml.value) { $("description-rich").innerHTML = textHtml(form.elements.extendedDescription.value); $("description-source").value = $("description-rich").innerHTML; } });
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
  for (const name of ["id", "name", "host", "port", "queryPort", "version", "description", "extendedDescription", "cover", "order"]) form.elements[name].value = row ? (row[name] ?? "") : ({ port: 16261, queryPort: 0, order: state ? state.servers.length : 0 }[name] ?? "");
  form.elements.screenshots.value = row ? (row.screenshots || []).join("\n") : "";
  form.elements.tags.value = row ? row.tags.join("，") : "";
  form.elements.published.checked = row ? row.published : false;
  form.elements.useSteamRelay.checked = row ? row.useSteamRelay : false;
  $("editor-title").textContent = row ? "编辑服务器" : "添加服务器";
  $("editor").hidden = false;
  richReset(row);
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
form.addEventListener("submit", (event) => { event.preventDefault(); action(async () => { if (Array.from(form.elements.descriptionHtml.value).length > 32768) throw new Error("详细介绍超过 32768 字，请缩短后再保存。"); const data = new FormData(form); const row = {}; for (const field of ["id", "name", "host", "version", "description", "cover"]) row[field] = data.get(field).trim(); row.extendedDescription = data.get("extendedDescription"); row.descriptionHtml = data.get("descriptionHtml"); row.screenshots = data.get("screenshots").split(/\r?\n/).map(value => value.trim()).filter(Boolean); for (const field of ["port", "queryPort", "order"]) row[field] = Number(data.get(field)); row.tags = data.get("tags").split(/[,，]/).map((tag) => tag.trim()).filter(Boolean); row.published = form.elements.published.checked; row.useSteamRelay = form.elements.useSteamRelay.checked; state = await api("/api/admin/servers", "POST", { revision: state.revision, server: row }); edit(null); render(); notice(row.published ? "服务器已保存并发布。" : "草稿已保存。"); }); });
$("add").addEventListener("click", () => { edit(null); form.elements.name.focus(); $("editor").scrollIntoView({ behavior: "smooth", block: "nearest" }); });
for (const id of ["close-editor", "cancel-edit"]) $(id).addEventListener("click", () => { selected = ""; $("editor").hidden = true; renderList(); });
$("refresh").addEventListener("click", () => action(async () => { state = await api("/api/admin/servers"); edit(null); render(); notice("已获取最新列表。"); }));
$("retry-export").addEventListener("click", () => action(async () => { state = await api("/api/admin/export", "POST", {}); render(); notice("公开列表已同步。"); }));
$("logout").addEventListener("click", () => action(async () => { await api("/api/admin/logout", "POST", {}); showLogin(); notice("已退出登录。"); }));
(async () => { try { const session = await api("/api/admin/session"); csrf = session.csrf; state = await api("/api/admin/servers"); showWorkspace(); edit(null); render(); } catch (error) { showLogin(); if (error.status !== 401) notice("无法连接服务，请确认本地管理服务已启动。", true); } })();
