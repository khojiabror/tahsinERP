# -*- coding: utf-8 -*-
"""
Markdown boblaridan bitta PDF (Chrome/Edge headless "print to PDF") — tashqi kutubxonasiz.

    python md2pdf.py --title "Sarlavha" --out kitob.pdf bob1.md bob2.md ...

Qo'llab-quvvatlanadi: # sarlavhalar, jadvallar (|), ro'yxatlar (-, 1.), ```kod```, > iqtibos, **qalin**, *kursiv*, `kod`, --- chiziq.
Mundarija h1/h2 bo'yicha avtomatik.
"""
import argparse
import html
import os
import re
import subprocess
import sys
import tempfile

CSS = """
@page { size: A4; margin: 16mm 14mm 18mm 14mm; }
body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 10.5pt; line-height: 1.45; color: #1f2937; }
h1 { font-size: 20pt; color: #0f3d6e; border-bottom: 2px solid #0f3d6e; padding-bottom: 4px; page-break-before: always; margin-top: 0; }
h1.first { page-break-before: avoid; }
h2 { font-size: 14pt; color: #0f3d6e; margin-top: 18px; page-break-after: avoid; }
h3 { font-size: 12pt; color: #1e5aa0; margin-top: 14px; page-break-after: avoid; }
h4 { font-size: 11pt; page-break-after: avoid; }
table { border-collapse: collapse; width: 100%; margin: 6px 0 10px; font-size: 9.5pt; page-break-inside: auto; }
tr { page-break-inside: avoid; }
th, td { border: 1px solid #cbd5e1; padding: 4px 6px; vertical-align: top; text-align: left; }
th { background: #e8eef6; }
code { font-family: Consolas, monospace; font-size: 9pt; background: #f1f5f9; padding: 0 3px; border-radius: 3px; }
pre { background: #f8fafc; border: 1px solid #e2e8f0; padding: 8px; font-size: 8.5pt; line-height: 1.25; white-space: pre-wrap; page-break-inside: avoid; }
pre code { background: none; padding: 0; }
blockquote { border-left: 4px solid #f59e0b; background: #fffbeb; margin: 8px 0; padding: 6px 10px; }
ul, ol { margin: 4px 0 8px 20px; padding-left: 10px; }
li { margin: 2px 0; }
hr { border: 0; border-top: 1px solid #cbd5e1; margin: 12px 0; }
.cover { height: 250mm; display: flex; flex-direction: column; justify-content: center; text-align: center; }
.cover h1 { border: 0; font-size: 28pt; page-break-before: avoid; }
.cover .sub { font-size: 13pt; color: #475569; margin-top: 8px; }
.toc { page-break-after: always; }
.toc h2 { margin-top: 0; }
.toc ul { list-style: none; margin-left: 0; padding-left: 0; }
.toc li.l2 { margin-left: 18px; font-size: 9.5pt; }
.toc a { color: #1f2937; text-decoration: none; }
.test { page-break-inside: avoid; }
"""


def inline(s):
    s = html.escape(s, quote=False)
    parts = re.split(r'(`[^`]+`)', s)
    out = []
    for p in parts:
        if p.startswith('`') and p.endswith('`') and len(p) > 1:
            out.append('<code>' + p[1:-1] + '</code>')
        else:
            p = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', p)
            p = re.sub(r'(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])', r'<i>\1</i>', p)
            p = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'\1', p)
            out.append(p)
    return ''.join(out)


def split_row(line):
    line = line.strip()
    if line.startswith('|'):
        line = line[1:]
    if line.endswith('|'):
        line = line[:-1]
    cells, cur, in_code = [], '', False
    for ch in line:
        if ch == '`':
            in_code = not in_code
        if ch == '|' and not in_code:
            cells.append(cur)
            cur = ''
        else:
            cur += ch
    cells.append(cur)
    return [c.strip() for c in cells]


def convert(md, toc, chapter):
    lines = md.replace('\r\n', '\n').split('\n')
    out = []
    i = 0
    n = len(lines)
    while i < n:
        line = lines[i]
        if line.startswith('```'):
            buf = []
            i += 1
            while i < n and not lines[i].startswith('```'):
                buf.append(lines[i])
                i += 1
            i += 1
            out.append('<pre><code>' + html.escape('\n'.join(buf)) + '</code></pre>')
            continue
        m = re.match(r'^(#{1,6})\s+(.*)$', line)
        if m:
            level = len(m.group(1))
            text = m.group(2).strip()
            anchor = 'h%d_%d' % (chapter, len(toc))
            if level <= 2:
                toc.append((level, text, anchor))
            cls = ''
            out.append('<h%d id="%s"%s>%s</h%d>' % (level, anchor, cls, inline(text), level))
            i += 1
            continue
        if re.match(r'^\s*(-{3,}|\*{3,})\s*$', line):
            out.append('<hr/>')
            i += 1
            continue
        if line.lstrip().startswith('|') and i + 1 < n and re.match(r'^\s*\|?\s*:?-{2,}', lines[i + 1]):
            head = split_row(line)
            i += 2
            rows = []
            while i < n and lines[i].lstrip().startswith('|'):
                rows.append(split_row(lines[i]))
                i += 1
            t = ['<table><thead><tr>' + ''.join('<th>%s</th>' % inline(c) for c in head) + '</tr></thead><tbody>']
            for r in rows:
                t.append('<tr>' + ''.join('<td>%s</td>' % inline(c) for c in r) + '</tr>')
            t.append('</tbody></table>')
            out.append(''.join(t))
            continue
        if line.startswith('>'):
            buf = []
            while i < n and lines[i].startswith('>'):
                buf.append(lines[i][1:].strip())
                i += 1
            out.append('<blockquote>' + '<br/>'.join(inline(b) for b in buf) + '</blockquote>')
            continue
        m = re.match(r'^(\s*)([-*]|\d+[.)])\s+(.*)$', line)
        if m:
            ordered = m.group(2)[0].isdigit()
            items = []
            while i < n:
                mm = re.match(r'^(\s*)([-*]|\d+[.)])\s+(.*)$', lines[i])
                if mm:
                    depth = len(mm.group(1).replace('\t', '    ')) // 2
                    items.append([depth, mm.group(3)])
                    i += 1
                elif lines[i].strip() and lines[i].startswith('   ') and items:
                    items[-1][1] += ' ' + lines[i].strip()
                    i += 1
                else:
                    break
            out.append(render_list(items, ordered))
            continue
        if not line.strip():
            i += 1
            continue
        buf = [line]
        i += 1
        while i < n and lines[i].strip() and not re.match(r'^(#{1,6}\s|```|>|\s*([-*]|\d+[.)])\s|\s*\|)', lines[i]):
            buf.append(lines[i])
            i += 1
        out.append('<p>' + '<br/>'.join(inline(b) for b in buf) + '</p>')
    return '\n'.join(out)


def render_list(items, ordered):
    tag = 'ol' if ordered else 'ul'
    res, stack = [], []
    for depth, text in items:
        while stack and stack[-1] > depth:
            res.append('</li></%s>' % tag)
            stack.pop()
        if not stack or depth > stack[-1]:
            res.append('<%s>' % tag)
            stack.append(depth)
        else:
            res.append('</li>')
        res.append('<li>' + inline(text))
    while stack:
        res.append('</li></%s>' % tag)
        stack.pop()
    return ''.join(res)


def find_browser():
    for p in [r'C:\Program Files\Google\Chrome\Application\chrome.exe',
              r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
              r'C:\Program Files\Microsoft\Edge\Application\msedge.exe']:
        if os.path.exists(p):
            return p
    sys.exit('Chrome yoki Edge topilmadi')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--title', required=True)
    ap.add_argument('--subtitle', default='')
    ap.add_argument('--out', required=True)
    ap.add_argument('files', nargs='+')
    a = ap.parse_args()
    toc, bodies = [], []
    for ci, f in enumerate(a.files):
        body = convert(open(f, encoding='utf-8-sig').read(), toc, ci)
        if ci == 0:
            body = body.replace('<h1 ', '<h1 class="first" ', 1)
        bodies.append(body)
    toc_html = '<div class="toc"><h2>Mundarija</h2><ul>' + ''.join(
        '<li class="l%d"><a href="#%s">%s</a></li>' % (lvl, anc, inline(t)) for lvl, t, anc in toc) + '</ul></div>'
    doc = ('<!DOCTYPE html><html lang="uz"><head><meta charset="utf-8"><title>%s</title><style>%s</style></head><body>'
           '<div class="cover"><h1>%s</h1><div class="sub">%s</div></div>%s%s</body></html>') % (
        html.escape(a.title), CSS, html.escape(a.title), html.escape(a.subtitle), toc_html, '\n'.join(bodies))
    tmp = os.path.join(tempfile.gettempdir(), 'md2pdf_%d.html' % os.getpid())
    open(tmp, 'w', encoding='utf-8').write(doc)
    out = os.path.abspath(a.out)
    subprocess.run([find_browser(), '--headless=new', '--disable-gpu', '--no-pdf-header-footer',
                    '--print-to-pdf=' + out, 'file:///' + tmp.replace('\\', '/')], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=300)
    os.remove(tmp)
    print(out, os.path.getsize(out) // 1024, 'KB')


if __name__ == '__main__':
    main()
