function highlightSource(kind, text) {
  const src = String(text ?? "")
  if (kind === "html") return highlightHtml(src)
  return highlightMarkdown(src)
}

function esc(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
}

function tok(cls, value) {
  return `<span class="${cls}">${esc(value)}</span>`
}

function highlightHtml(src) {
  let i = 0
  const n = src.length
  let out = ""
  while (i < n) {
    if (src.startsWith("<!--", i)) {
      const end = src.indexOf("-->", i + 4)
      const j = end < 0 ? n : end + 3
      out += tok("tok-com", src.slice(i, j))
      i = j
      continue
    }
    if (src.startsWith("<!", i) || src.startsWith("<?", i)) {
      const end = src.indexOf(">", i)
      const j = end < 0 ? n : end + 1
      out += tok("tok-doctype", src.slice(i, j))
      i = j
      continue
    }
    if (src[i] === "<") {
      const close = src[i + 1] === "/"
      let j = i + (close ? 2 : 1)
      while (j < n && /[A-Za-z0-9:_-]/.test(src[j])) j++
      const name = src.slice(i + (close ? 2 : 1), j)
      out += tok("tok-punct", src.slice(i, close ? i + 2 : i + 1))
      if (name) out += tok("tok-tag", name)
      while (j < n && src[j] !== ">") {
        if (src[j] === "/" && src[j + 1] === ">") break
        if (/\s/.test(src[j])) {
          const s = j
          while (j < n && /\s/.test(src[j])) j++
          out += src.slice(s, j)
          continue
        }
        const a0 = j
        while (j < n && /[A-Za-z0-9:_-]/.test(src[j])) j++
        if (j === a0) {
          out += tok("tok-punct", src[j])
          j++
          continue
        }
        out += tok("tok-attr", src.slice(a0, j))
        while (j < n && /\s/.test(src[j])) {
          out += src[j]
          j++
        }
        if (src[j] === "=") {
          out += tok("tok-punct", "=")
          j++
          while (j < n && /\s/.test(src[j])) {
            out += src[j]
            j++
          }
          if (src[j] === '"' || src[j] === "'") {
            const q = src[j]
            let k = j + 1
            while (k < n && src[k] !== q) k++
            if (k < n) k++
            out += tok("tok-str", src.slice(j, k))
            j = k
          } else {
            const v0 = j
            while (j < n && !/\s|>/.test(src[j])) j++
            out += tok("tok-str", src.slice(v0, j))
          }
        }
      }
      if (src.startsWith("/>", j)) {
        out += tok("tok-punct", "/>")
        j += 2
      } else if (src[j] === ">") {
        out += tok("tok-punct", ">")
        j++
      }
      const lower = name.toLowerCase()
      if (!close && (lower === "script" || lower === "style")) {
        const closeTag = `</${lower}`
        const raw = src.slice(j)
        const found = raw.toLowerCase().indexOf(closeTag)
        if (found >= 0) {
          out += tok("tok-code", src.slice(j, j + found))
          j += found
        }
      }
      i = j
      continue
    }
    const next = src.indexOf("<", i)
    const j = next < 0 ? n : next
    out += esc(src.slice(i, j))
    i = j
  }
  return out
}

function highlightMarkdown(src) {
  const lines = src.split(/\n/)
  const out = []
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const fence = line.match(/^(\s*)(```|~~~)(.*)$/)
    if (fence) {
      const mark = fence[2]
      const lang = fence[3].trim().toLowerCase()
      const body = [line]
      i++
      while (i < lines.length) {
        body.push(lines[i])
        if (lines[i].trim().startsWith(mark)) break
        i++
      }
      const joined = body.join("\n")
      const innerStart = line.length + 1
      const innerEnd = joined.lastIndexOf("\n")
      const open = joined.slice(0, Math.min(innerStart, joined.length))
      let inner = ""
      let close = ""
      if (innerEnd > innerStart) {
        inner = joined.slice(innerStart, innerEnd)
        close = joined.slice(innerEnd)
      }
      const painted =
        lang === "html" || lang === "xml" || lang === "svg"
          ? highlightHtml(inner)
          : tok("tok-code", inner)
      out.push(tok("tok-fence", open) + (inner ? painted : "") + (close ? tok("tok-fence", close) : ""))
      continue
    }
    if (/^\s*#/.test(line)) {
      const m = line.match(/^(\s*)(#{1,6})(\s*)(.*)$/)
      if (m) {
        out.push(esc(m[1]) + tok("tok-head", m[2]) + esc(m[3]) + paintInline(m[4]))
        continue
      }
    }
    if (/^\s*>/.test(line)) {
      const m = line.match(/^(\s*)(>+)(\s?)(.*)$/)
      if (m) {
        out.push(esc(m[1]) + tok("tok-quote", m[2]) + esc(m[3]) + paintInline(m[4]))
        continue
      }
    }
    out.push(paintInline(line))
  }
  return out.join("\n")
}

function paintInline(line) {
  let i = 0
  const n = line.length
  let out = ""
  while (i < n) {
    if (line[i] === "`") {
      let j = i + 1
      while (j < n && line[j] !== "`") j++
      if (j < n) j++
      out += tok("tok-code", line.slice(i, j))
      i = j
      continue
    }
    if (line.startsWith("**", i) || line.startsWith("__", i)) {
      const m = line.slice(i, i + 2)
      const end = line.indexOf(m, i + 2)
      if (end >= 0) {
        out += tok("tok-em", line.slice(i, end + 2))
        i = end + 2
        continue
      }
    }
    if (line[i] === "[") {
      const mid = line.indexOf("](", i)
      const end = mid >= 0 ? line.indexOf(")", mid + 2) : -1
      if (mid >= 0 && end >= 0) {
        out += tok("tok-link", line.slice(i, end + 1))
        i = end + 1
        continue
      }
    }
    if (line[i] === "<" && /[A-Za-z/!?]/.test(line[i + 1] || "")) {
      const end = line.indexOf(">", i)
      if (end >= 0) {
        out += highlightHtml(line.slice(i, end + 1))
        i = end + 1
        continue
      }
    }
    const next = nextSpecial(line, i + 1)
    out += esc(line.slice(i, next))
    i = next
  }
  return out
}

function nextSpecial(line, from) {
  for (let i = from; i < line.length; i++) {
    const c = line[i]
    if (c === "`" || c === "[" || c === "<") return i
    if ((c === "*" || c === "_") && line[i + 1] === c) return i
  }
  return line.length
}

if (typeof window !== "undefined") window.highlightSource = highlightSource
if (typeof module !== "undefined") module.exports = { highlightSource }
