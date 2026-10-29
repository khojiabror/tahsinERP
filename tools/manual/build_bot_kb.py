"""Qo'llanma HTML'idan (tahsinERP/ManualData/{uz,ru}) Telegram bot bilim bazasini quradi.

    python tools/manual/build_bot_kb.py

Natija: tahsinERP.ControlPanel/Bot/Content/kb.{uz,ru}.json (EmbeddedResource). Har modul — sarlavha, qisqa tavsif
(1.1 bo'lim) va h2 darajadagi bo'limlar (matn Telegram HTML'ida: <b>, oddiy qatorlar). ManualData o'zgarsa
(convert_docx.py qayta ishga tushirilsa) shu skriptni ham qayta ishga tushirib natijani commit qiling.
"""
import html
import json
import os
import re
import sys
from html.parser import HTMLParser

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "tahsinERP", "ManualData")
DST = os.path.join(ROOT, "tahsinERP.ControlPanel", "Bot", "Content")
SUMMARY_MAX = 900


def bold_classes(source):
    """`.wm-page .wm-doc .sN{...font-weight:700...}` — qalin matn klasslari (fayl bo'yicha har xil)."""
    found = set()
    for m in re.finditer(r"\.wm-doc \.(s\d+)\{([^}]*)\}", source):
        if "font-weight:700" in m.group(2):
            found.add(m.group(1))
    return found


class ManualParser(HTMLParser):
    """Bloklar ketma-ketligi: ("h1"|"h2"|"h3"|"p"|"li"|"row", matn)."""

    def __init__(self, bold):
        super().__init__(convert_charrefs=True)
        self.bold = bold
        self.blocks = []
        self.buf = None          # joriy blok matni (list)
        self.kind = None
        self.span_stack = []     # har ochilgan span qalinmi
        self.in_table = 0
        self.row = None          # jadval qatori katakchalari
        self.cell = None
        self.header = False
        self.skip = 0            # <style>, <a class=wm-bm> ichi

    def _flush(self):
        if self.buf is not None:
            text = norm("".join(self.buf))
            if text:
                self.blocks.append((self.kind, text))
        self.buf = None
        self.kind = None

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "style":
            self.skip += 1
            return
        if tag in ("h1", "h2", "h3"):
            self._flush()
            self.kind, self.buf = tag, []
        elif tag == "table":
            self._flush()
            self.in_table += 1
        elif tag == "thead":
            self.header = True
        elif tag == "tbody":
            self.header = False
        elif tag == "tr" and self.in_table:
            self.row = []
        elif tag == "td" and self.in_table:
            self.cell = []
        elif tag == "p" and not self.in_table:
            self._flush()
            self.kind, self.buf = "p", []
        elif tag == "p" and self.cell is not None and self.cell:
            self.cell.append(" ")
        elif tag == "br":
            self._text("\n")
        elif tag == "span":
            cls = (a.get("class") or "").split()
            b = any(c in self.bold for c in cls) and not self.in_table and self.kind == "p"
            self.span_stack.append(b)
            if b:
                self._text("\x01")
        elif tag == "img":
            # Rasm paragraflari matnga kirmaydi.
            if self.kind == "p":
                self.kind = "img"

    def handle_endtag(self, tag):
        if tag == "style":
            self.skip -= 1
        elif tag in ("h1", "h2", "h3"):
            self._flush()
        elif tag == "p" and not self.in_table:
            self._flush()
        elif tag == "span":
            if self.span_stack and self.span_stack.pop():
                self._text("\x02")
        elif tag == "td" and self.cell is not None:
            self.row.append(norm("".join(self.cell)))
            self.cell = None
        elif tag == "tr" and self.row is not None:
            cells = [c for c in self.row]
            if any(cells):
                self.blocks.append(("th" if self.header else "row", cells))
            self.row = None
        elif tag == "table":
            self.in_table -= 1

    def _text(self, data):
        if self.cell is not None:
            self.cell.append(data)
        elif self.buf is not None:
            self.buf.append(data)

    def handle_data(self, data):
        if self.skip:
            return
        self._text(data)


def norm(s):
    s = s.replace("\xa0", " ")
    s = re.sub(r"[ \t\r\f\v]+", " ", s)
    s = re.sub(r" *\n *", "\n", s)
    s = re.sub(r"\x01\s*\x02", "", s)
    return s.strip()


def to_html(text):
    """\x01..\x02 — qalin; qolgani Telegram HTML uchun escape."""
    out = html.escape(text, quote=False)
    out = out.replace("\x01", "<b>").replace("\x02", "</b>").replace("</b><b>", "")
    # Ochilib yopilmagan teglar qolmasin
    if out.count("<b>") != out.count("</b>"):
        out = out.replace("<b>", "").replace("</b>", "")
    return out


def plain(text):
    return text.replace("\x01", "").replace("\x02", "")


CAPTION = re.compile(r"^(Rasm|Jadval|Рис\.|Рисунок|Таблица)\s*\d+[.:]", re.I)
HEAD_NUM = re.compile(r"^(\d+(?:\.\d+)*)\.?\s+(.*)$")


def build_module(path, title):
    source = open(path, encoding="utf-8").read()
    p = ManualParser(bold_classes(source))
    p.feed(source)
    p._flush()

    sections = []
    cur = None
    h1 = None
    summary = ""

    def start(head):
        nonlocal cur
        m = HEAD_NUM.match(head)
        num, name = (m.group(1), m.group(2)) if m else ("", head)
        cur = {"n": num, "t": name, "lines": []}
        sections.append(cur)

    for kind, value in p.blocks:
        if kind == "h1":
            h1 = value
            start(value)
        elif kind == "h2":
            # h1 ostida o'z matni bo'lmasa — bo'sh h1 bo'limini olib tashlaymiz
            if cur is not None and not cur["lines"] and cur["t"] == HEAD_NUM.sub(r"\2", h1 or ""):
                sections.pop()
            start(value)
        elif cur is None:
            continue
        elif kind == "h3":
            cur["lines"].append("<b>" + html.escape(value, quote=False) + "</b>")
        elif kind == "p":
            if CAPTION.match(plain(value)):
                continue
            cur["lines"].append(to_html(value))
        elif kind == "th":
            cur["lines"].append("<b>" + html.escape(" | ".join(c for c in value if c), quote=False) + "</b>")
        elif kind == "row":
            cells = [c for c in value if c]
            cur["lines"].append("• " + html.escape(" — ".join(cells), quote=False))

    out_sections = []
    for i, s in enumerate(sections):
        body = "\n".join(s["lines"]).strip()
        if not body:
            continue
        out_sections.append({"n": s["n"], "t": s["t"], "body": body})
        if not summary and s["n"] in ("1.1", "1"):
            summary = re.sub(r"<[^>]+>", "", body)
    if not summary and out_sections:
        summary = re.sub(r"<[^>]+>", "", out_sections[0]["body"])
    summary = html.unescape(summary)
    para = summary.split("\n")[0]
    if len(para) > SUMMARY_MAX:
        para = para[:SUMMARY_MAX].rsplit(" ", 1)[0] + "…"
    return {"summary": para, "sections": out_sections}


def main():
    manifest = json.load(open(os.path.join(SRC, "manifest.json"), encoding="utf-8"))
    os.makedirs(DST, exist_ok=True)
    for lang in ("uz", "ru"):
        modules = []
        for m in manifest["modules"]:
            path = os.path.join(SRC, lang, m["id"] + ".html")
            if not os.path.exists(path):
                print("yo'q:", path, file=sys.stderr)
                continue
            title = re.sub(r"^\d+\.\s*", "", m["titles"][lang])
            data = build_module(path, title)
            modules.append({"id": m["id"], "num": m["num"], "title": title, **data})
        out = os.path.join(DST, f"kb.{lang}.json")
        with open(out, "w", encoding="utf-8", newline="\n") as f:
            json.dump({"lang": lang, "modules": modules}, f, ensure_ascii=False, indent=1)
        n = sum(len(x["sections"]) for x in modules)
        print(f"{out}: {len(modules)} modul, {n} bo'lim, {os.path.getsize(out) // 1024} KB")


if __name__ == "__main__":
    main()
