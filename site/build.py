#!/usr/bin/env python3
"""Builds a static website from the refactoring catalog.

    python3 site/build.py [--catalog Catalog] [--out _site]

Only the standard library is used, so the site builds anywhere Python 3.9+
runs. The output is plain HTML, CSS and a little JavaScript for filtering.
"""

import argparse
import difflib
import html
import json
import re
import shutil
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO_URL = "https://github.com/dave-hillier/refactor-mcp"
TIERS = [
    ("primitives", "Primitives", "Single, behaviour-preserving transformations."),
    ("composites", "Composites", "Refactorings built as a recipe of primitive steps."),
    ("generators", "Generators", "Refactorings that generate new structure around existing code."),
]


# --------------------------------------------------------------------------
# Markdown: the subset the catalog READMEs use.

def inline_md(text, link_names=None):
    parts = re.split(r"(`[^`]+`)", text)
    out = []
    for part in parts:
        if part.startswith("`") and part.endswith("`") and len(part) > 1:
            code = part[1:-1]
            if link_names and code in link_names:
                out.append(f'<a href="{link_names[code]}"><code>{html.escape(code)}</code></a>')
            else:
                out.append(f"<code>{html.escape(code)}</code>")
            continue
        s = html.escape(part, quote=False)
        s = re.sub(r"\[([^\]]+)\]\(([^)]+)\)", r'<a href="\2">\1</a>', s)
        s = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", s)
        s = re.sub(r"(?<![\w*])\*([^*\s][^*]*)\*(?![\w*])", r"<em>\1</em>", s)
        s = re.sub(r"(?<![\w_])_([^_\s][^_]*)_(?![\w_])", r"<em>\1</em>", s)
        out.append(s)
    return "".join(out)


def render_markdown(text, link_names=None, skip_title=True):
    lines = text.splitlines()
    out = []
    i = 0
    il = lambda t: inline_md(t, link_names)

    def is_block_start(line):
        return (line.startswith("#") or line.startswith("```") or line.startswith("|")
                or re.match(r"^\s*([-*]|\d+\.)\s", line) is not None)

    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        m = re.match(r"^(#{1,6})\s+(.*)$", line)
        if m:
            level = len(m.group(1))
            if not (level == 1 and skip_title):
                slug = re.sub(r"[^a-z0-9]+", "-", m.group(2).lower()).strip("-")
                out.append(f'<h{level + 1} id="about-{slug}">{il(m.group(2))}</h{level + 1}>')
            i += 1
            continue
        if line.startswith("```"):
            lang = line[3:].strip()
            i += 1
            code = []
            while i < len(lines) and not lines[i].startswith("```"):
                code.append(lines[i])
                i += 1
            i += 1
            body = highlight_block("\n".join(code), lang)
            out.append(f'<pre class="code-block"><code>{body}</code></pre>')
            continue
        if line.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].startswith("|"):
                rows.append(lines[i])
                i += 1
            cells = [split_row(r) for r in rows if not re.match(r"^\|[\s:|-]+\|?\s*$", r)]
            if cells:
                head, *body = cells
                t = ['<div class="table-wrap"><table><thead><tr>']
                t += [f"<th>{il(c)}</th>" for c in head]
                t.append("</tr></thead><tbody>")
                for r in body:
                    t.append("<tr>" + "".join(f"<td>{il(c)}</td>" for c in r) + "</tr>")
                t.append("</tbody></table></div>")
                out.append("".join(t))
            continue
        m = re.match(r"^(\s*)([-*]|\d+\.)\s+(.*)$", line)
        if m:
            ordered = m.group(2)[0].isdigit()
            tag = "ol" if ordered else "ul"
            items = []
            while i < len(lines):
                m = re.match(r"^(\s*)([-*]|\d+\.)\s+(.*)$", lines[i])
                if m:
                    items.append(m.group(3))
                    i += 1
                elif lines[i].startswith(" ") and lines[i].strip() and items:
                    items[-1] += " " + lines[i].strip()
                    i += 1
                else:
                    break
            out.append(f"<{tag}>" + "".join(f"<li>{il(x)}</li>" for x in items) + f"</{tag}>")
            continue
        para = []
        while i < len(lines) and lines[i].strip() and not (para and is_block_start(lines[i])):
            para.append(lines[i].strip())
            i += 1
        out.append(f"<p>{il(' '.join(para))}</p>")
    return "\n".join(out)


def split_row(row):
    row = row.strip()
    if row.startswith("|"):
        row = row[1:]
    if row.endswith("|"):
        row = row[:-1]
    # Pipes inside code spans are rare in the catalog; split on unescaped pipes.
    return [c.strip() for c in re.split(r"(?<!\\)\|", row)]


def first_paragraph(text):
    paras = []
    for line in text.splitlines():
        if line.startswith("#"):
            if paras:
                break
            continue
        if not line.strip():
            if paras:
                break
            continue
        paras.append(line.strip())
    return " ".join(paras)


def title_of(text, fallback):
    for line in text.splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    return fallback.replace("-", " ").title()


# --------------------------------------------------------------------------
# A small C# highlighter. Tokenises line by line, carrying block comment and
# verbatim/raw string state across lines.

CS_KEYWORDS = set("""
abstract as base bool break byte case catch char checked class const continue
decimal default delegate do double else enum event explicit extern false finally
fixed float for foreach goto if implicit in int interface internal is lock long
namespace new null object operator out override params private protected public
readonly ref return sbyte sealed short sizeof stackalloc static string struct
switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual
void volatile while var async await yield record init get set add remove value
when where nameof dynamic global partial not and or with required file scoped
""".split())

TOKEN_RE = re.compile(r"""
    (?P<marker>/\*\[\*/|/\*\]\*/|/\*\^\*/)
  | (?P<comment>//.*)
  | (?P<bcomment>/\*)
  | (?P<raw>\$*\"\"\"+)
  | (?P<verbatim>\$?@\"|@\$\")
  | (?P<string>\$?\"(?:[^\"\\]|\\.)*\"?)
  | (?P<char>'(?:[^'\\]|\\.)*')
  | (?P<number>\b\d[\d_]*(?:\.\d+)?(?:[eE][+-]?\d+)?[mMdDfFlLuU]*\b|\b0x[0-9a-fA-F_]+\b)
  | (?P<preproc>^\s*\#\w+.*)
  | (?P<attr>(?<=\[)[A-Z]\w*)
  | (?P<ident>@?[A-Za-z_]\w*)
""", re.VERBOSE)


class CSharpHighlighter:
    def __init__(self):
        self.state = None  # None, ("comment",), ("verbatim",), ("raw", quotes)

    def line(self, text):
        out = []
        pos = 0
        n = len(text)
        while pos < n:
            if self.state:
                kind = self.state[0]
                if kind == "comment":
                    end = text.find("*/", pos)
                    stop = n if end < 0 else end + 2
                    out.append(span("c", text[pos:stop]))
                    if end >= 0:
                        self.state = None
                    pos = stop
                elif kind == "verbatim":
                    j = pos
                    while j < n:
                        if text[j] == '"':
                            if j + 1 < n and text[j + 1] == '"':
                                j += 2
                                continue
                            j += 1
                            self.state = None
                            break
                        j += 1
                    out.append(span("s", text[pos:j]))
                    pos = j
                else:
                    q = self.state[1]
                    end = text.find(q, pos)
                    stop = n if end < 0 else end + len(q)
                    out.append(span("s", text[pos:stop]))
                    if end >= 0:
                        self.state = None
                    pos = stop
                continue
            m = TOKEN_RE.search(text, pos)
            if not m:
                out.append(html.escape(text[pos:]))
                break
            out.append(html.escape(text[pos:m.start()]))
            kind = m.lastgroup
            tok = m.group()
            pos = m.end()
            if kind == "marker":
                out.append(span("mk", tok, "target marker"))
            elif kind == "comment":
                out.append(span("c", tok))
            elif kind == "bcomment":
                self.state = ("comment",)
                out.append(span("c", tok))
            elif kind == "raw":
                self.state = ("raw", '"' * tok.count('"'))
                out.append(span("s", tok))
            elif kind == "verbatim":
                self.state = ("verbatim",)
                out.append(span("s", tok))
            elif kind in ("string", "char"):
                out.append(span("s", tok))
            elif kind == "number":
                out.append(span("n", tok))
            elif kind == "preproc":
                out.append(span("p", tok))
            elif kind == "attr":
                out.append(span("t", tok))
            elif tok.lstrip("@") in CS_KEYWORDS and not tok.startswith("@"):
                out.append(span("k", tok))
            elif tok[0].isupper():
                out.append(span("t", tok))
            else:
                out.append(html.escape(tok))
        return "".join(out)


def span(cls, text, title=None):
    t = f' title="{title}"' if title else ""
    return f'<span class="{cls}"{t}>{html.escape(text)}</span>'


def highlight_block(code, lang):
    if lang in ("cs", "csharp", "c#"):
        h = CSharpHighlighter()
        return "\n".join(h.line(l) for l in code.split("\n"))
    if lang == "json":
        return highlight_json(code)
    return html.escape(code)


def highlight_json(code):
    s = html.escape(code, quote=False)
    s = re.sub(r'("(?:[^"\\]|\\.)*")(\s*:)', r'<span class="k">\1</span>\2', s)
    s = re.sub(r'(:\s*|\[\s*|,\s*)("(?:[^"\\]|\\.)*")', r'\1<span class="s">\2</span>', s)
    return s


# --------------------------------------------------------------------------
# Catalog model.

def read_text(path):
    return path.read_text(encoding="utf-8-sig").replace("\r\n", "\n")


def list_files(root):
    if not root.is_dir():
        return {}
    return {p.relative_to(root).as_posix(): read_text(p)
            for p in sorted(root.rglob("*")) if p.is_file()}


def load_catalog(catalog):
    tiers = []
    for tier_id, tier_name, tier_blurb in TIERS:
        tdir = catalog / tier_id
        if not tdir.is_dir():
            continue
        refs = []
        for rdir in sorted(p for p in tdir.iterdir() if p.is_dir()):
            readme_path = rdir / "README.md"
            readme = read_text(readme_path) if readme_path.exists() else ""
            cases = []
            for cdir in sorted(p for p in rdir.iterdir() if p.is_dir()):
                cj = cdir / "case.json"
                if not cj.exists():
                    continue
                spec = json.loads(read_text(cj))
                cases.append({
                    "id": cdir.name,
                    "spec": spec,
                    "before": list_files(cdir / "before"),
                    "after": list_files(cdir / "after") if (cdir / "after").is_dir() else None,
                })
            refs.append({
                "id": rdir.name,
                "tier": tier_id,
                "title": title_of(readme, rdir.name),
                "summary": first_paragraph(readme),
                "readme": readme,
                "cases": cases,
            })
        tiers.append({"id": tier_id, "name": tier_name, "blurb": tier_blurb, "refs": refs})
    return tiers


def case_is_error(case):
    return case["spec"].get("expect") == "error"


# --------------------------------------------------------------------------
# Rendering.

def page(title, body, depth, description=""):
    root = "../" * depth
    desc = html.escape(description or "A catalog of C# refactorings, specified by example.")
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{html.escape(title)}</title>
<meta name="description" content="{desc}">
<link rel="stylesheet" href="{root}assets/style.css">
<script>try{{var t=localStorage.getItem('theme');if(t)document.documentElement.dataset.theme=t}}catch(e){{}}</script>
</head>
<body>
<header class="topbar">
  <a class="brand" href="{root}index.html"><span class="logo">⟲</span> RefactorMCP <span class="muted">catalog</span></a>
  <nav>
    <a href="{root}index.html#primitives">Primitives</a>
    <a href="{root}index.html#composites">Composites</a>
    <a href="{root}index.html#generators">Generators</a>
    <a href="{REPO_URL}">GitHub</a>
    <button class="theme-toggle" type="button" aria-label="Toggle dark mode"><svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true"><circle cx="8" cy="8" r="6.5" fill="none" stroke="currentColor" stroke-width="1.5"/><path d="M8 1.5a6.5 6.5 0 0 1 0 13z" fill="currentColor"/></svg></button>
  </nav>
</header>
{body}
<footer class="footer">
  Generated from <a href="{REPO_URL}/tree/main/Catalog"><code>Catalog/</code></a>.
  The catalog is the specification; the implementation is measured against it.
</footer>
<script src="{root}assets/site.js"></script>
</body>
</html>
"""


def render_index(tiers, total_cases, total_errors):
    total_refs = sum(len(t["refs"]) for t in tiers)
    sections = []
    for t in tiers:
        cards = []
        for r in t["refs"]:
            n = len(r["cases"])
            errs = sum(1 for c in r["cases"] if case_is_error(c))
            unimpl = sum(1 for c in r["cases"] if c["spec"].get("status") == "unimplemented")
            search = " ".join([r["id"], r["title"], r["summary"]]).lower()
            badges = f'<span class="pill">{n} case{"s" if n != 1 else ""}</span>'
            if errs:
                badges += f'<span class="pill pill-err">{errs} refusal{"s" if errs != 1 else ""}</span>'
            if unimpl:
                badges += f'<span class="pill pill-todo">{unimpl} unimplemented</span>'
            cards.append(f"""<a class="card" href="{t['id']}/{r['id']}/index.html" data-search="{html.escape(search)}">
  <h3>{html.escape(r['title'])}</h3>
  <p>{inline_md(r['summary'])}</p>
  <div class="card-meta"><code>{r['id']}</code>{badges}</div>
</a>""")
        sections.append(f"""<section class="tier" id="{t['id']}" data-tier="{t['id']}">
  <div class="tier-head"><h2>{t['name']} <span class="count">{len(t['refs'])}</span></h2><p class="muted">{t['blurb']}</p></div>
  <div class="grid">{''.join(cards)}</div>
</section>""")
    body = f"""<main class="wrap">
<section class="hero">
  <h1>Refactoring catalog</h1>
  <p class="lede">Every refactoring <a href="{REPO_URL}">RefactorMCP</a> aims to support, described as behaviour
  and specified by fixtures: a compilable <em>before</em>, the operation, and the exact <em>after</em>.</p>
  <div class="stats">
    <div><strong>{total_refs}</strong><span>refactorings</span></div>
    <div><strong>{total_cases}</strong><span>cases</span></div>
    <div><strong>{total_errors}</strong><span>refusal cases</span></div>
  </div>
  <div class="filter">
    <input id="filter" type="search" placeholder="Filter refactorings… (press / to focus)" autocomplete="off" aria-label="Filter refactorings">
    <div class="chips" role="group" aria-label="Tier">
      <button type="button" class="chip active" data-tier="all">All</button>
      {''.join(f'<button type="button" class="chip" data-tier="{t["id"]}">{t["name"]}</button>' for t in tiers)}
    </div>
  </div>
  <p id="no-results" class="muted" hidden>No refactoring matches.</p>
</section>
{''.join(sections)}
</main>"""
    return page("RefactorMCP refactoring catalog", body, 0)


def render_target(target, link_names):
    if not target:
        return ""
    bits = []
    if "file" in target:
        bits.append(f"<code>{html.escape(target['file'])}</code>")
    if "symbol" in target:
        bits.append(f"symbol <code>{html.escape(target['symbol'])}</code>")
    if target.get("selection") == "marker":
        bits.append('the <span class="mk">/*[*/ … /*]*/</span> selection')
    if target.get("caret") == "marker":
        bits.append('the <span class="mk">/*^*/</span> caret')
    if "range" in target:
        bits.append(f"range <code>{html.escape(target['range'])}</code>")
    return ", ".join(bits)


def render_args(args):
    if not args:
        return ""
    rows = "".join(
        f"<tr><th><code>{html.escape(k)}</code></th><td><code>{html.escape(json.dumps(v, ensure_ascii=False))}</code></td></tr>"
        for k, v in args.items())
    return f'<table class="args">{rows}</table>'


def render_spec(spec, link_names):
    parts = []
    if spec.get("steps"):
        items = []
        for s in spec["steps"]:
            name = s["refactoring"]
            label = f"<code>{html.escape(name)}</code>"
            if name in link_names:
                label = f'<a href="{link_names[name]}">{label}</a>'
            tgt = render_target(s.get("target"), link_names)
            items.append(f"<li>{label}{' on ' + tgt if tgt else ''}{render_args(s.get('arguments'))}</li>")
        parts.append(f'<div class="spec-row"><span class="spec-k">Recipe</span><ol class="steps">{"".join(items)}</ol></div>')
    else:
        tgt = render_target(spec.get("target"), link_names)
        if tgt:
            parts.append(f'<div class="spec-row"><span class="spec-k">Target</span><span>{tgt}</span></div>')
        a = render_args(spec.get("arguments"))
        if a:
            parts.append(f'<div class="spec-row"><span class="spec-k">Arguments</span>{a}</div>')
    proj = spec.get("project")
    if proj:
        parts.append(f'<div class="spec-row"><span class="spec-k">Project</span><span>'
                     + ", ".join(f"{html.escape(k)} <code>{html.escape(str(v))}</code>" for k, v in proj.items())
                     + "</span></div>")
    if spec.get("projects"):
        ps = []
        for p in spec["projects"]:
            s = f"<code>{html.escape(p['name'])}</code>"
            if p.get("references"):
                s += " → " + ", ".join(f"<code>{html.escape(x)}</code>" for x in p["references"])
            ps.append(s)
        parts.append(f'<div class="spec-row"><span class="spec-k">Projects</span><span>{"; ".join(ps)}</span></div>')
    if spec.get("expect") == "error":
        e = f'<code class="err-code">{html.escape(spec.get("errorCode", "error"))}</code>'
        if spec.get("errorContains"):
            e += f' mentioning “{html.escape(spec["errorContains"])}”'
        parts.append(f'<div class="spec-row"><span class="spec-k">Refuses</span><span>{e}; every file is left unchanged</span></div>')
    return f'<div class="spec">{"".join(parts)}</div>'


def render_file_diff(name, before, after):
    """Full-file diff with the unchanged lines kept, since fixtures are short."""
    if before is None:
        status, a, b = "added", [], after.split("\n")
    elif after is None:
        status, a, b = "deleted", before.split("\n"), []
    else:
        a, b = before.split("\n"), after.split("\n")
        status = "unchanged" if a == b else "modified"
    for lst in (a, b):
        while lst and lst[-1] == "":
            lst.pop()
    ha, hb = CSharpHighlighter(), CSharpHighlighter()
    ra = [ha.line(l) for l in a]
    rb = [hb.line(l) for l in b]
    rows = []
    sm = difflib.SequenceMatcher(a=a, b=b, autojunk=False)
    for op, i1, i2, j1, j2 in sm.get_opcodes():
        if op == "equal":
            for k in range(i2 - i1):
                rows.append(("ctx", i1 + k + 1, j1 + k + 1, ra[i1 + k]))
        else:
            for k in range(i1, i2):
                rows.append(("del", k + 1, "", ra[k]))
            for k in range(j1, j2):
                rows.append(("add", "", k + 1, rb[k]))
    sign = {"ctx": " ", "del": "−", "add": "+"}
    body = "".join(
        f'<tr class="{c}"><td class="ln">{x}</td><td class="ln">{y}</td><td class="sg">{sign[c]}</td><td class="src">{s or " "}</td></tr>'
        for c, x, y, s in rows)
    open_attr = "" if status == "unchanged" else " open"
    return f"""<details class="file file-{status}"{open_attr}>
<summary><span class="fname">{html.escape(name)}</span><span class="fstatus">{status}</span></summary>
<div class="diff-wrap"><table class="diff">{body}</table></div>
</details>"""


def render_files_only(files):
    out = []
    for name, text in files.items():
        h = CSharpHighlighter()
        lines = text.split("\n")
        while lines and lines[-1] == "":
            lines.pop()
        body = "".join(
            f'<tr class="ctx"><td class="ln">{i}</td><td class="sg"> </td><td class="src">{h.line(l) or " "}</td></tr>'
            for i, l in enumerate(lines, 1))
        out.append(f"""<details class="file" open>
<summary><span class="fname">{html.escape(name)}</span><span class="fstatus">input</span></summary>
<div class="diff-wrap"><table class="diff">{body}</table></div>
</details>""")
    return "".join(out)


def render_case(ref, case, link_names):
    spec = case["spec"]
    is_err = case_is_error(case)
    desc = spec.get("description", "")
    badge = '<span class="pill pill-err">refusal</span>' if is_err else '<span class="pill pill-ok">success</span>'
    if spec.get("status") == "unimplemented":
        badge += '<span class="pill pill-todo">unimplemented</span>'
    if spec.get("steps"):
        badge += '<span class="pill">recipe</span>'
    if is_err or case["after"] is None:
        files = render_files_only(case["before"])
    else:
        names = sorted(set(case["before"]) | set(case["after"]),
                       key=lambda n: (case["before"].get(n) == case["after"].get(n), n))
        files = "".join(render_file_diff(n, case["before"].get(n), case["after"].get(n)) for n in names)
    src = f"{REPO_URL}/tree/main/Catalog/{ref['tier']}/{ref['id']}/{case['id']}"
    return f"""<article class="case" id="{case['id']}" data-kind="{'error' if is_err else 'success'}">
<details>
<summary class="case-head">
  <div><h3><a href="#{case['id']}" class="anchor">#</a>{html.escape(case['id'])}</h3><p>{inline_md(desc, link_names)}</p></div>
  <div class="case-badges">{badge}</div>
</summary>
<div class="case-body">
{render_spec(spec, link_names)}
{files}
<p class="src-link"><a href="{src}">View fixture on GitHub</a></p>
</div>
</details>
</article>"""


def render_ref(ref, tier, link_names_rel, tiers):
    link_names = {k: "../../" + v for k, v in link_names_rel.items()}
    readme_html = render_markdown(ref["readme"], link_names)
    n = len(ref["cases"])
    errs = sum(1 for c in ref["cases"] if case_is_error(c))
    toc = "".join(
        f'<li class="{"err" if case_is_error(c) else "ok"}"><a href="#{c["id"]}">{html.escape(c["id"])}</a></li>'
        for c in ref["cases"])
    cases = "".join(render_case(ref, c, link_names) for c in ref["cases"])
    siblings = tier["refs"]
    idx = siblings.index(ref)
    prev_l = f'<a href="../{siblings[idx - 1]["id"]}/index.html">← {html.escape(siblings[idx - 1]["title"])}</a>' if idx > 0 else "<span></span>"
    next_l = f'<a href="../{siblings[idx + 1]["id"]}/index.html">{html.escape(siblings[idx + 1]["title"])} →</a>' if idx + 1 < len(siblings) else "<span></span>"
    body = f"""<main class="wrap ref-page">
<nav class="crumbs"><a href="../../index.html">Catalog</a> / <a href="../../index.html#{tier['id']}">{tier['name']}</a> / <code>{ref['id']}</code></nav>
<h1>{html.escape(ref['title'])}</h1>
<div class="ref-layout">
<aside class="toc">
  <h4>Cases <span class="count">{n}</span></h4>
  <div class="chips small" role="group" aria-label="Case kind">
    <button type="button" class="chip active" data-kind="all">All</button>
    <button type="button" class="chip" data-kind="success">Success</button>
    <button type="button" class="chip" data-kind="error">Refusals ({errs})</button>
  </div>
  <ul>{toc}</ul>
</aside>
<div class="ref-main">
<section class="readme">{readme_html}</section>
<section class="cases">
  <div class="cases-head"><h2 id="cases">Cases</h2>
  <div><button type="button" class="linkish" data-expand="1">Expand all</button> · <button type="button" class="linkish" data-expand="0">Collapse all</button></div></div>
  <p class="muted legend">Diffs show each file from <em>before</em> to <em>after</em>.
  <span class="mk">/*[*/ … /*]*/</span> marks a selection and <span class="mk">/*^*/</span> a caret; the runner removes them before the refactoring runs.</p>
  {cases}
</section>
<nav class="pager">{prev_l}{next_l}</nav>
</div>
</div>
</main>"""
    return page(f"{ref['title']} · RefactorMCP catalog", body, 2, ref["summary"])


def build(catalog, out):
    tiers = load_catalog(catalog)
    if out.exists():
        shutil.rmtree(out)
    (out / "assets").mkdir(parents=True)
    for asset in ("style.css", "site.js"):
        shutil.copy(HERE / asset, out / "assets" / asset)
    (out / ".nojekyll").write_text("")

    link_names = {r["id"]: f"{t['id']}/{r['id']}/index.html" for t in tiers for r in t["refs"]}
    total_cases = sum(len(r["cases"]) for t in tiers for r in t["refs"])
    total_errors = sum(1 for t in tiers for r in t["refs"] for c in r["cases"] if case_is_error(c))
    (out / "index.html").write_text(render_index(tiers, total_cases, total_errors), encoding="utf-8")
    for t in tiers:
        for r in t["refs"]:
            d = out / t["id"] / r["id"]
            d.mkdir(parents=True)
            (d / "index.html").write_text(render_ref(r, t, link_names, tiers), encoding="utf-8")

    index = [{"id": r["id"], "tier": t["id"], "title": r["title"], "summary": r["summary"],
              "cases": len(r["cases"]), "url": link_names[r["id"]]} for t in tiers for r in t["refs"]]
    (out / "catalog.json").write_text(json.dumps(index, indent=1), encoding="utf-8")
    print(f"Built {len(index)} refactorings, {total_cases} cases into {out}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--catalog", default=str(HERE.parent / "Catalog"))
    ap.add_argument("--out", default=str(HERE.parent / "_site"))
    a = ap.parse_args()
    build(Path(a.catalog), Path(a.out))
