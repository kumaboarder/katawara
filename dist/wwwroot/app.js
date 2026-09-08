const state = {
  root: "",
  recent: [],
  canBrowse: false,
  tree: [],
  query: "",
  selected: null,
  focus: null,
  file: null,
  expanded: new Set(),
  error: null,
  previewError: null,
  busy: false,
  strict: true,
  sourceView: false,
  previewCtx: { text: "", href: "" },
}

const el = {
  path: document.getElementById("path"),
  form: document.getElementById("open-form"),
  browse: document.getElementById("browse"),
  recents: document.getElementById("recents"),
  error: document.getElementById("error"),
  query: document.getElementById("query"),
  tree: document.getElementById("tree"),
  preview: document.getElementById("preview"),
  expandAll: document.getElementById("expand-all"),
  collapseAll: document.getElementById("collapse-all"),
  split: document.getElementById("split"),
  explorer: document.getElementById("explorer"),
  handle: document.getElementById("handle"),
  status: document.getElementById("status"),
  statusText: document.getElementById("status-text"),
  treeBusy: document.getElementById("tree-busy"),
  treeBusyText: document.getElementById("tree-busy-text"),
  submit: document.querySelector("#open-form [type=submit]"),
  modeBanner: document.getElementById("mode-banner"),
  ctx: document.getElementById("ctx-menu"),
  ctxPreview: document.getElementById("ctx-preview"),
  toast: document.getElementById("copy-toast"),
}

function flattenFiles(nodes) {
  const files = []
  const visit = (list) => {
    for (const node of list || []) {
      if (node.kind === "dir") visit(node.children || [])
      else files.push(node)
    }
  }
  visit(nodes)
  return files
}

function preferredFile(files) {
  const html = files.filter((file) => file.kind === "html")
  const index = html.find((file) => /(^|\/)index\.html$/i.test(file.relPath))
  return index || html[0] || files[0]
}

function filterTree(nodes, query) {
  const q = query.trim().toLowerCase()
  if (!q) return nodes
  const filterList = (list) => {
    const next = []
    for (const node of list || []) {
      if (node.kind === "dir") {
        const children = filterList(node.children || [])
        if (children.length > 0 || node.name.toLowerCase().includes(q)) {
          next.push({ ...node, children })
        }
        continue
      }
      if (node.name.toLowerCase().includes(q) || node.relPath.toLowerCase().includes(q)) {
        next.push(node)
      }
    }
    return next
  }
  return filterList(nodes)
}

function compactTree(nodes) {
  return (nodes || []).map(compactNode)
}

function compactNode(node) {
  if (node.kind !== "dir") return node
  const names = [node.name]
  let relPath = node.relPath
  let children = node.children || []
  while (children.length === 1 && children[0].kind === "dir") {
    names.push(children[0].name)
    relPath = children[0].relPath
    children = children[0].children || []
  }
  return {
    name: names.join(" / "),
    relPath,
    kind: "dir",
    children: children.map(compactNode),
  }
}

function collectDirPaths(nodes) {
  const paths = []
  const visit = (list) => {
    for (const node of list || []) {
      if (node.kind !== "dir") continue
      if (node.relPath) paths.push(node.relPath)
      visit(node.children || [])
    }
  }
  visit(nodes)
  return paths
}

function collectSubtreeDirPaths(node) {
  if (node.kind !== "dir") return []
  return collectDirPaths([node])
}

function visibleTree() {
  return compactTree(filterTree(state.tree, state.query))
}

function folderName(item) {
  return item.replace(/[\\/]+$/, "").split(/[\\/]/).pop() || item
}

function icon(kind, open) {
  if (kind === "dir") {
    return `<svg class="gold" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.7-.9L9.4 3.9A2 2 0 0 0 7.7 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2z"/></svg>`
  }
  if (kind === "md") {
    return `<svg class="forest" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/><path d="M8 13h8"/><path d="M8 17h5"/></svg>`
  }
  return `<svg class="primary" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/><path d="m9 13 2 2 4-4"/></svg>`
}

function chevron(open) {
  return open
    ? `<svg class="muted" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6"/></svg>`
    : `<svg class="muted" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="m9 6 6 6-6 6"/></svg>`
}

function setError(message) {
  state.error = message
  el.error.hidden = !message
  el.error.textContent = message || ""
}

function setBusy(busy, message, overlay = true) {
  state.busy = busy
  const text = message || "読み込み中…"
  el.status.hidden = !busy
  el.statusText.textContent = text
  el.treeBusy.hidden = !(busy && overlay)
  el.treeBusyText.textContent = text
  if (el.submit) el.submit.disabled = busy
  if (el.browse) el.browse.disabled = busy
  document.querySelectorAll("#recents button").forEach((button) => {
    button.disabled = busy
  })
}

function previewLoading(message) {
  el.preview.innerHTML = `<div class="center loading"><span class="spinner" aria-hidden="true"></span><span>${escapeHtml(message || "ファイルを開いています…")}</span></div>`
}

function renderRecents() {
  const items = state.recent.filter((item) => item !== state.root)
  el.recents.innerHTML = items
    .map(
      (item) =>
        `<button type="button" class="btn xs outline" data-root="${escapeAttr(item)}" title="${escapeAttr(item)}">${escapeHtml(folderName(item))}</button>`,
    )
    .join("")
}

function renderTree() {
  const nodes = visibleTree()
  if (!nodes.length) {
    el.tree.innerHTML = `<p class="empty">Markdown と HTML がありません。</p>`
    return
  }
  el.tree.innerHTML = `<ul class="tree">${nodes.map((node) => renderItem(node, 0)).join("")}</ul>`
}

function renderItem(node, depth) {
  const isDir = node.kind === "dir"
  const forceOpen = state.query.trim().length > 0
  const isOpen = isDir && (forceOpen || node.relPath === "" || state.expanded.has(node.relPath))
  const active = !isDir && state.selected === node.relPath
  const focused = state.focus && state.focus.relPath === node.relPath
  const pad = 4 + depth * 10
  const title = isDir ? `${node.relPath || node.name}（Alt+クリックで配下も）` : node.relPath
  let html = `<li>
    <div class="row ${active ? "active" : ""} ${focused ? "focused" : ""}" style="padding-left:${pad}px">
      <button type="button" class="row-main" data-rel="${escapeAttr(node.relPath)}" data-kind="${node.kind}" title="${escapeAttr(title)}">
        ${isDir ? chevron(isOpen) : `<span class="lead"></span>`}
        ${icon(node.kind)}
        <span class="truncate">${escapeHtml(node.name)}</span>
      </button>
      ${
        isDir
          ? `<button type="button" class="expand-sub" data-expand="${escapeAttr(node.relPath)}" title="配下をすべて開く">${doubleDown()}</button>`
          : ""
      }
    </div>`
  if (isDir && isOpen && node.children) {
    html += `<ul>${node.children.map((child) => renderItem(child, depth + 1)).join("")}</ul>`
  }
  return html + "</li>"
}

function doubleDown() {
  return `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="m7 6 5 5 5-5"/><path d="m7 13 5 5 5-5"/></svg>`
}

const pending = new Map()
let rpcSeq = 0

function onHostMessage(raw) {
  let msg
  try {
    msg = JSON.parse(raw)
  } catch {
    return
  }
  if (msg.push === "watch") {
    if (msg.op === "hello") return
    setBusy(true, "変更を反映しています…", false)
    void loadTree()
      .then(() => {
        renderTree()
        if (state.selected) return loadFile(state.selected)
      })
      .catch(() => undefined)
      .finally(() => setBusy(false))
    return
  }
  const waiter = pending.get(msg.id)
  if (!waiter) return
  pending.delete(msg.id)
  if (msg.ok) waiter.resolve(msg.data)
  else waiter.reject(new Error(msg.error || "失敗しました"))
}

if (window.external && typeof window.external.receiveMessage === "function") {
  window.external.receiveMessage(onHostMessage)
}

function rpc(cmd, payload = {}) {
  const id = String(++rpcSeq)
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject })
    if (!window.external || typeof window.external.sendMessage !== "function") {
      pending.delete(id)
      reject(new Error("デスクトップアプリから開いてください"))
      return
    }
    window.external.sendMessage(JSON.stringify({ id, cmd, ...payload }))
  })
}

function sanitizeMarkdown(html) {
  if (window.DOMPurify) {
    return window.DOMPurify.sanitize(html, {
      USE_PROFILES: { html: true },
      FORBID_TAGS: ["script", "iframe", "object", "embed", "form", "base", "link", "meta", "svg", "math"],
      FORBID_ATTR: ["onerror", "onload", "onclick", "onmouseover", "onfocus", "oninput"],
    })
  }
  const box = document.createElement("div")
  box.textContent = html
  return `<pre>${box.innerHTML}</pre>`
}

function renderPreview() {
  if (state.previewError) {
    el.preview.innerHTML = `<div class="center bad">${escapeHtml(state.previewError)}</div>`
    return
  }
  if (!state.file) {
    el.preview.innerHTML = `<div class="center">左の一覧から選ぶ</div>`
    return
  }
  const { kind, relPath, content, mtime } = state.file
  const open =
    kind === "html" && !state.sourceView
      ? `<button type="button" class="btn xs outline" data-open-external="${escapeAttr(relPath)}">ブラウザで開く</button>`
      : ""
  const back = state.sourceView
    ? `<button type="button" class="btn xs outline" data-view="preview">プレビューに戻る</button>`
    : ""
  let body = ""
  if (state.sourceView) {
    body = `<pre class="source-view" data-lang="${escapeAttr(kind)}">${typeof highlightSource === "function" ? highlightSource(kind, content) : escapeHtml(content)}</pre>`
  } else if (kind === "md") {
    const parsed = window.marked.parse(content, { gfm: true, breaks: false })
    const html = state.strict ? sanitizeMarkdown(parsed) : parsed
    body = `<div class="md-preview">${html}</div>`
  } else {
    const sandbox = state.strict
      ? ` sandbox="allow-scripts allow-modals" referrerpolicy="no-referrer"`
      : ""
    body = `<iframe title="${escapeAttr(relPath)}"${sandbox}></iframe>`
  }
  el.preview.innerHTML = `
    <div class="preview-bar">
      <span class="kind">${escapeHtml(kind)}${state.sourceView ? " source" : ""}</span>
      <span class="rel">${escapeHtml(relPath)}</span>
      ${open}
      ${back}
    </div>
    <div class="preview-body">${body}</div>`
  if (kind === "html" && !state.sourceView) {
    const iframe = el.preview.querySelector("iframe")
    if (iframe) iframe.srcdoc = state.file.previewHtml || content || ""
  }
  if (kind === "md" && !state.sourceView) {
    void renderMermaid(el.preview.querySelector(".md-preview"))
  }
  void mtime
}

document.addEventListener("click", (event) => {
  const back = event.target.closest("[data-view=preview]")
  if (back) {
    event.preventDefault()
    state.sourceView = false
    renderPreview()
    return
  }
  const button = event.target.closest("[data-open-external]")
  if (!button) return
  event.preventDefault()
  void rpc("openExternal", { path: button.getAttribute("data-open-external") }).catch((caught) => {
    setError(caught instanceof Error ? caught.message : "ブラウザで開けません")
  })
})

let mermaidReady = false

function ensureMermaid() {
  if (mermaidReady || !window.mermaid) return
  window.mermaid.initialize({
    startOnLoad: false,
    securityLevel: "strict",
    theme: "base",
    themeVariables: {
      background: "#fbf7ef",
      primaryColor: "#e7dcc8",
      primaryTextColor: "#1c1814",
      primaryBorderColor: "#d9cbb6",
      lineColor: "#5c5348",
      secondaryColor: "#efe6d6",
      tertiaryColor: "#fbf7ef",
      fontFamily: "inherit",
    },
  })
  mermaidReady = true
}

async function renderMermaid(root) {
  ensureMermaid()
  if (!window.mermaid || !root) return
  const nodes = []
  root.querySelectorAll("pre > code").forEach((code) => {
    const lang = (code.className || "").replace(/^language-/, "").trim().toLowerCase()
    if (lang !== "mermaid") return
    const block = document.createElement("pre")
    block.className = "mermaid"
    block.textContent = code.textContent
    code.parentElement.replaceWith(block)
    nodes.push(block)
  })
  if (nodes.length === 0) return
  try {
    await window.mermaid.run({ nodes })
  } catch {
    // 不正な図はそのまま残す
  }
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
}

function escapeAttr(value) {
  return escapeHtml(value)
}

function findNode(nodes, relPath) {
  for (const node of nodes || []) {
    if (node.relPath === relPath) return node
    if (node.children) {
      const found = findNode(node.children, relPath)
      if (found) return found
    }
  }
  return null
}

function focusedRel() {
  return (state.focus && state.focus.relPath) || state.selected || ""
}

function setFocus(relPath, kind) {
  state.focus = relPath ? { relPath, kind: kind || "file" } : null
  el.tree.querySelectorAll(".row.focused").forEach((row) => row.classList.remove("focused"))
  if (!relPath) return
  const button = Array.from(el.tree.querySelectorAll(".row-main")).find(
    (node) => node.getAttribute("data-rel") === relPath,
  )
  button?.closest(".row")?.classList.add("focused")
}

function hideCtxMenu() {
  if (el.ctx) el.ctx.hidden = true
  if (el.ctxPreview) el.ctxPreview.hidden = true
}

function placeMenu(menu, x, y) {
  menu.style.left = "0px"
  menu.style.top = "0px"
  menu.hidden = false
  const rect = menu.getBoundingClientRect()
  const left = Math.min(x, window.innerWidth - rect.width - 8)
  const top = Math.min(y, window.innerHeight - rect.height - 8)
  menu.style.left = `${Math.max(8, left)}px`
  menu.style.top = `${Math.max(8, top)}px`
}

function showCtxMenu(x, y) {
  hideCtxMenu()
  const menu = el.ctx
  if (!menu || !focusedRel()) return
  placeMenu(menu, x, y)
}

function previewSelectionText() {
  const sel = window.getSelection()
  if (sel && !sel.isCollapsed && el.preview.contains(sel.anchorNode)) return String(sel)
  return (state.previewCtx && state.previewCtx.text) || ""
}

function linkFromTarget(target) {
  const a = target && target.closest ? target.closest("a[href]") : null
  if (!a) return ""
  const raw = a.getAttribute("href") || ""
  if (/^(https?:|mailto:|preview:)/i.test(raw)) return a.href || raw
  return raw
}

function displayLink(href) {
  if (!href) return ""
  const prefix = "preview://workspace/"
  if (href.startsWith(prefix)) {
    const rel = decodeURIComponent(href.slice(prefix.length).split(/[?#]/)[0])
    return PathCopyFull(rel)
  }
  return href
}

function PathCopyFull(relPath) {
  const root = String(state.root || "").replace(/[\\/]+$/, "")
  const windows = /^[A-Za-z]:[\\/]/.test(state.root || "") || (state.root || "").includes("\\")
  const sep = windows ? "\\" : "/"
  const rel = String(relPath || "").replaceAll("\\", "/").replace(/^\/+/, "").replaceAll("/", sep)
  if (!rel) return root
  return root + sep + rel
}

function showPreviewMenu(x, y, href) {
  hideCtxMenu()
  const menu = el.ctxPreview
  if (!menu || !state.selected) return
  const linkBtn = menu.querySelector("[data-preview=link]")
  const address = displayLink(href || "")
  if (linkBtn) {
    linkBtn.hidden = !address
    linkBtn.dataset.href = address
  }
  placeMenu(menu, x, y)
}

async function copyPreviewText(text) {
  hideCtxMenu()
  if (!text) return
  showCopyToast()
  await writeClipboard(text)
}

let toastTimer = 0
function showCopyToast() {
  if (!el.toast) return
  el.toast.hidden = false
  window.clearTimeout(toastTimer)
  toastTimer = window.setTimeout(() => {
    el.toast.hidden = true
  }, 1400)
}

async function writeClipboard(text) {
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await Promise.race([
        navigator.clipboard.writeText(text),
        new Promise((_, reject) => window.setTimeout(() => reject(new Error("clipboard-timeout")), 250)),
      ])
      return
    }
  } catch {
    // file:// や権限待ちでは拒否・停滞することがある
  }
  const area = document.createElement("textarea")
  area.value = text
  area.setAttribute("readonly", "")
  area.style.position = "fixed"
  area.style.left = "-9999px"
  document.body.appendChild(area)
  area.select()
  document.execCommand("copy")
  area.remove()
}

async function copyFocused(mode) {
  const rel = focusedRel()
  if (!rel) return
  hideCtxMenu()
  try {
    const data = await rpc("copyPath", { mode, path: rel })
    showCopyToast()
    if (data && data.text) void writeClipboard(data.text)
  } catch (caught) {
    setError(caught instanceof Error ? caught.message : "コピーできません")
  }
}

async function loadTree() {
  const data = await rpc("tree")
  state.tree = data.tree || []
  if (data.root) state.root = data.root
  return { files: flattenFiles(state.tree), tree: state.tree, root: data.root || null }
}

async function loadFile(relPath) {
  previewLoading("ファイルを開いています…")
  state.sourceView = false
  try {
    const data = await rpc("file", { path: relPath })
    state.previewError = null
    state.file = data
    renderPreview()
  } catch (caught) {
    state.previewError = caught instanceof Error ? caught.message : "ファイルを読めません"
    state.file = null
    renderPreview()
  }
}

async function applyWorkspace() {
  const { files, tree, root } = await loadTree()
  if (root) el.path.value = root
  const nextSelected =
    state.selected && files.some((item) => item.relPath === state.selected)
      ? state.selected
      : preferredFile(files)?.relPath || null
  state.selected = nextSelected
  state.focus = nextSelected ? { relPath: nextSelected, kind: "file" } : null
  state.expanded = new Set(collectDirPaths(compactTree(tree)))
  renderTree()
  renderRecents()
  if (nextSelected) await loadFile(nextSelected)
  else {
    state.file = null
    state.previewError = null
    renderPreview()
  }
}

async function openPath(nextRoot) {
  setBusy(true, "フォルダを読んでいます…")
  setError(null)
  try {
    const data = await rpc("setWorkspace", { root: nextRoot })
    rememberWorkspace(data)
    state.selected = null
    state.focus = null
    await applyWorkspace()
  } catch (caught) {
    setError(caught instanceof Error ? caught.message : "フォルダを開けません")
  } finally {
    setBusy(false)
  }
}

function rememberWorkspace(data) {
  if (data.root) {
    state.root = data.root
    el.path.value = data.root
  }
  if (data.recent) state.recent = data.recent
}

async function browse() {
  setBusy(true, "フォルダを選んでいます…")
  setError(null)
  try {
    const data = await rpc("browse")
    if (data.cancelled) return
    rememberWorkspace(data)
    state.selected = null
    state.focus = null
    setBusy(true, "フォルダを読んでいます…")
    await applyWorkspace()
  } catch (caught) {
    setError(caught instanceof Error ? caught.message : "フォルダを選べませんでした")
  } finally {
    setBusy(false)
  }
}

function toggleDir(node, recursive) {
  const paths = recursive ? collectSubtreeDirPaths(node) : [node.relPath]
  const allOpen = paths.every((path) => state.expanded.has(path))
  if (allOpen) paths.forEach((path) => state.expanded.delete(path))
  else paths.forEach((path) => state.expanded.add(path))
  renderTree()
}

function expandSubtree(node) {
  for (const path of collectSubtreeDirPaths(node)) state.expanded.add(path)
  renderTree()
}

el.form.addEventListener("submit", (event) => {
  event.preventDefault()
  void openPath(el.path.value)
})

el.browse.addEventListener("click", () => void browse())
el.expandAll.addEventListener("click", () => {
  state.expanded = new Set(collectDirPaths(visibleTree()))
  renderTree()
})
el.collapseAll.addEventListener("click", () => {
  state.expanded = new Set()
  renderTree()
})
el.query.addEventListener("input", () => {
  state.query = el.query.value
  renderTree()
})
el.recents.addEventListener("click", (event) => {
  const button = event.target.closest("[data-root]")
  if (!button) return
  void openPath(button.getAttribute("data-root"))
})
function handleTreeContext(event) {
  const button = event.target.closest(".row-main")
  if (!button) return false
  event.preventDefault()
  setFocus(button.getAttribute("data-rel"), button.getAttribute("data-kind") || "file")
  showCtxMenu(event.clientX, event.clientY)
  return true
}

el.tree.addEventListener("click", (event) => {
  const expand = event.target.closest("[data-expand]")
  if (expand) {
    event.preventDefault()
    const node = findNode(visibleTree(), expand.getAttribute("data-expand"))
    if (node) expandSubtree(node)
    return
  }
  const button = event.target.closest(".row-main")
  if (!button) return
  const rel = button.getAttribute("data-rel")
  const kind = button.getAttribute("data-kind")
  const node = findNode(visibleTree(), rel)
  if (!node) return
  if (kind === "dir") {
    setFocus(rel, "dir")
    toggleDir(node, event.altKey || event.metaKey)
  } else {
    setFocus(rel, "file")
    state.selected = rel
    renderTree()
    void loadFile(rel)
  }
})
el.tree.addEventListener("contextmenu", (event) => {
  handleTreeContext(event)
})
el.tree.addEventListener("pointerdown", (event) => {
  if (event.button !== 2) return
  handleTreeContext(event)
})
el.tree.addEventListener("dblclick", (event) => {
  const button = event.target.closest(".row-main")
  if (!button || button.getAttribute("data-kind") !== "dir") return
  event.preventDefault()
  const node = findNode(visibleTree(), button.getAttribute("data-rel"))
  if (node) expandSubtree(node)
})

function handlePreviewContext(event) {
  if (event.target.closest(".preview-bar button")) return false
  if (!state.selected && !state.file) return false
  event.preventDefault()
  const href = linkFromTarget(event.target)
  state.previewCtx = { text: previewSelectionText(), href }
  showPreviewMenu(event.clientX, event.clientY, href)
  return true
}

el.preview.addEventListener("contextmenu", (event) => {
  handlePreviewContext(event)
})
el.preview.addEventListener("pointerdown", (event) => {
  if (event.button !== 2) return
  handlePreviewContext(event)
})

el.ctx.addEventListener("click", (event) => {
  const button = event.target.closest("[data-copy]")
  if (!button) return
  event.preventDefault()
  void copyFocused(button.getAttribute("data-copy"))
})

el.ctxPreview.addEventListener("click", (event) => {
  const button = event.target.closest("[data-preview]")
  if (!button) return
  event.preventDefault()
  const act = button.getAttribute("data-preview")
  if (act === "copy") {
    void copyPreviewText((state.previewCtx && state.previewCtx.text) || previewSelectionText())
    return
  }
  if (act === "link") {
    void copyPreviewText(button.dataset.href || state.previewCtx.href)
    return
  }
  if (act === "source") {
    hideCtxMenu()
    state.sourceView = true
    renderPreview()
  }
})

window.addEventListener("message", (event) => {
  const data = event.data
  if (!data || typeof data !== "object") return
  if (data.kind === "soba-nav") {
    const next = resolvePreviewHref(state.selected || (state.file && state.file.relPath) || "", data.href || "")
    if (!next) return
    if (/\.(html?|md|markdown)$/i.test(next)) {
      state.selected = next
      renderTree()
      void loadFile(next)
    } else {
      void rpc("openExternal", { path: next }).catch(() => undefined)
    }
    return
  }
  if (data.kind !== "soba-ctx") return
  const iframe = el.preview.querySelector("iframe")
  const rect = iframe ? iframe.getBoundingClientRect() : { left: 0, top: 0 }
  state.previewCtx = { text: data.text || "", href: data.href || "" }
  showPreviewMenu(rect.left + (data.x || 0), rect.top + (data.y || 0), data.href || "")
})

function resolvePreviewHref(fromRel, href) {
  const cut = String(href || "").trim().split("#")[0].split("?")[0]
  if (!cut || /^(https?:|mailto:|javascript:)/i.test(cut)) return ""
  try {
    const dummy = new URL(String(fromRel || "index.html").replace(/^\/+/, ""), "https://workspace.local/")
    return decodeURIComponent(new URL(cut, dummy).pathname.replace(/^\/+/, ""))
  } catch {
    return cut.replace(/^(\.\/)+/, "")
  }
}

document.addEventListener("contextmenu", (event) => {
  event.preventDefault()
}, true)

document.addEventListener("click", (event) => {
  if (el.ctx && !el.ctx.hidden && el.ctx.contains(event.target)) return
  if (el.ctxPreview && !el.ctxPreview.hidden && el.ctxPreview.contains(event.target)) return
  hideCtxMenu()
})

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape") hideCtxMenu()
  const copyKey =
    (event.ctrlKey || event.metaKey) && !event.shiftKey && !event.altKey && event.key.toLowerCase() === "c"
  if (!copyKey) return
  if (event.target.closest("input, textarea, select")) return
  const inPreview = event.target.closest("#preview-wrap")
  if (inPreview) return
  const sel = window.getSelection()
  if (sel && !sel.isCollapsed && el.preview.contains(sel.anchorNode)) return
  if (!event.target.closest("#explorer")) return
  if (!focusedRel()) return
  event.preventDefault()
  void copyFocused("name")
})

window.addEventListener("blur", hideCtxMenu)
window.addEventListener("resize", hideCtxMenu)
document.getElementById("tree-scroll")?.addEventListener("scroll", hideCtxMenu, { passive: true })

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value))
}

function setupSplit() {
  const applySize = (vertical, size) => {
    if (vertical) {
      el.explorer.style.height = `${size}px`
      el.explorer.style.width = ""
    } else {
      el.explorer.style.width = `${size}px`
      el.explorer.style.height = ""
    }
  }

  el.handle.addEventListener("pointerdown", (event) => {
    if (event.button !== 0) return
    event.preventDefault()
    const vertical = window.matchMedia("(max-width: 1023px)").matches
    const start = vertical ? event.clientY : event.clientX
    const startSize = vertical
      ? el.explorer.getBoundingClientRect().height
      : el.explorer.getBoundingClientRect().width
    const splitSize = vertical
      ? el.split.getBoundingClientRect().height
      : el.split.getBoundingClientRect().width
    const handleSize = vertical
      ? el.handle.getBoundingClientRect().height
      : el.handle.getBoundingClientRect().width
    const minExplorer = vertical ? 140 : 200
    const minPreview = vertical ? 160 : 280
    const maxSize = Math.max(minExplorer, splitSize - handleSize - minPreview)

    document.body.classList.add("is-resizing")
    document.body.classList.toggle("is-resizing-y", vertical)
    try {
      el.handle.setPointerCapture(event.pointerId)
    } catch {
      // キャプチャできない環境でも、下の pointer-events: none で追従する
    }

    const move = (ev) => {
      const delta = vertical ? ev.clientY - start : ev.clientX - start
      applySize(vertical, clamp(startSize + delta, minExplorer, maxSize))
    }
    const up = (ev) => {
      document.body.classList.remove("is-resizing", "is-resizing-y")
      if (el.handle.hasPointerCapture(ev.pointerId)) {
        el.handle.releasePointerCapture(ev.pointerId)
      }
      el.handle.removeEventListener("pointermove", move)
      el.handle.removeEventListener("pointerup", up)
      el.handle.removeEventListener("pointercancel", up)
    }
    el.handle.addEventListener("pointermove", move)
    el.handle.addEventListener("pointerup", up)
    el.handle.addEventListener("pointercancel", up)
  })
}

async function boot() {
  setBusy(true, "フォルダを読んでいます…")
  previewLoading("読み込み中…")
  try {
    const data = await rpc("workspace")
    state.root = data.root || ""
    state.recent = data.recent || []
    state.canBrowse = Boolean(data.canBrowse)
    state.strict = data.strict !== false
    el.modeBanner.hidden = state.strict
    document.title = state.strict ? "傍ら — 生成AIが書いた Markdown と HTML を見る" : "傍ら（制限なし）"
    el.path.value = state.root
    el.browse.hidden = !state.canBrowse
    await applyWorkspace()
  } catch (caught) {
    setError(caught instanceof Error ? caught.message : "フォルダを読めません")
    renderTree()
    renderPreview()
  } finally {
    setBusy(false)
    hideSplash()
  }
}

const splashAt = performance.now()
function hideSplash() {
  const splash = document.getElementById("splash")
  if (!splash || splash.classList.contains("is-leaving")) return
  const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches
  const wait = reduce ? 0 : Math.max(0, 1200 - (performance.now() - splashAt))
  window.setTimeout(() => {
    splash.classList.add("is-leaving")
    const done = () => splash.remove()
    splash.addEventListener("transitionend", done, { once: true })
    window.setTimeout(done, 700)
  }, wait)
}

setupSplit()
void boot()
