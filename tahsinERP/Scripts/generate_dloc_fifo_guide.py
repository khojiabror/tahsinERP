# -*- coding: utf-8 -*-
"""Generate Docs/DLOC_FIFO_User_Guide.docx and .pdf from markdown source."""
import os
import re
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(ROOT, "Docs")
MD_PATH = os.path.join(DOCS, "DLOC_FIFO_User_Guide.md")
DOCX_PATH = os.path.join(DOCS, "DLOC_FIFO_User_Guide.docx")
PDF_PATH = os.path.join(DOCS, "DLOC_FIFO_User_Guide.pdf")
FONT_DIR = os.path.join(ROOT, "Scripts", "fonts")
FONT_PATH = os.path.join(FONT_DIR, "DejaVuSans.ttf")
FONT_URL = "https://raw.githubusercontent.com/dejavu-fonts/dejavu-fonts/master/ttf/DejaVuSans.ttf"


def read_md():
    with open(MD_PATH, "r", encoding="utf-8") as f:
        return f.read()


def parse_blocks(text):
    lines = text.splitlines()
    blocks = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if line.startswith("# "):
            blocks.append(("h1", line[2:].strip()))
            i += 1
        elif line.startswith("## "):
            blocks.append(("h2", line[3:].strip()))
            i += 1
        elif line.startswith("### "):
            blocks.append(("h3", line[4:].strip()))
            i += 1
        elif line.startswith("|") and i + 1 < len(lines) and lines[i + 1].startswith("|"):
            table_lines = []
            while i < len(lines) and lines[i].startswith("|"):
                table_lines.append(lines[i])
                i += 1
            blocks.append(("table", table_lines))
        elif line.strip().startswith("```"):
            i += 1
            code = []
            while i < len(lines) and not lines[i].strip().startswith("```"):
                code.append(lines[i])
                i += 1
            if i < len(lines):
                i += 1
            blocks.append(("code", "\n".join(code)))
        elif line.strip() == "---":
            blocks.append(("hr", ""))
            i += 1
        elif line.strip().startswith("- ") or line.strip().startswith("* "):
            items = []
            while i < len(lines) and (lines[i].strip().startswith("- ") or lines[i].strip().startswith("* ")):
                items.append(re.sub(r"^[\-\*]\s+", "", lines[i].strip()))
                i += 1
            blocks.append(("ul", items))
        elif re.match(r"^\d+\.\s", line.strip()):
            items = []
            while i < len(lines) and re.match(r"^\d+\.\s", lines[i].strip()):
                items.append(re.sub(r"^\d+\.\s+", "", lines[i].strip()))
                i += 1
            blocks.append(("ol", items))
        elif line.strip():
            para = [line.strip()]
            i += 1
            while i < len(lines) and lines[i].strip() and not lines[i].startswith("#") and not lines[i].startswith("|") and not lines[i].strip().startswith("- "):
                para.append(lines[i].strip())
                i += 1
            blocks.append(("p", " ".join(para)))
        else:
            i += 1
    return blocks


def strip_md_inline(s):
    s = re.sub(r"\*\*(.+?)\*\*", r"\1", s)
    s = re.sub(r"`(.+?)`", r"\1", s)
    return s


def build_docx(blocks):
    from docx import Document
    from docx.shared import Pt, Inches
    from docx.enum.text import WD_ALIGN_PARAGRAPH

    doc = Document()
    style = doc.styles["Normal"]
    style.font.name = "Calibri"
    style.font.size = Pt(11)

    for kind, content in blocks:
        if kind == "h1":
            doc.add_heading(strip_md_inline(content), level=0)
        elif kind == "h2":
            doc.add_heading(strip_md_inline(content), level=1)
        elif kind == "h3":
            doc.add_heading(strip_md_inline(content), level=2)
        elif kind == "p":
            doc.add_paragraph(strip_md_inline(content))
        elif kind == "hr":
            doc.add_paragraph()
        elif kind == "code":
            p = doc.add_paragraph(content)
            p.style = "Intense Quote"
        elif kind == "ul":
            for item in content:
                doc.add_paragraph(strip_md_inline(item), style="List Bullet")
        elif kind == "ol":
            for item in content:
                doc.add_paragraph(strip_md_inline(item), style="List Number")
        elif kind == "table":
            rows = []
            for tl in content:
                cells = [c.strip() for c in tl.strip("|").split("|")]
                if all(set(c) <= set("-: ") for c in cells):
                    continue
                rows.append([strip_md_inline(c) for c in cells])
            if rows:
                t = doc.add_table(rows=len(rows), cols=len(rows[0]))
                t.style = "Table Grid"
                for ri, row in enumerate(rows):
                    for ci, cell in enumerate(row):
                        t.rows[ri].cells[ci].text = cell

    doc.save(DOCX_PATH)
    print("DOCX:", DOCX_PATH)


def ensure_font():
    os.makedirs(FONT_DIR, exist_ok=True)
    if not os.path.isfile(FONT_PATH):
        print("Downloading DejaVuSans.ttf for PDF Unicode...")
        urllib.request.urlretrieve(FONT_URL, FONT_PATH)


def build_pdf(blocks):
    ensure_font()
    from fpdf import FPDF

    class PDF(FPDF):
        def header(self):
            self.set_font("DejaVu", "", 9)
            self.set_text_color(100, 100, 100)
            self.cell(0, 8, "tahsinERP — DLOC va FIFO qo'llanmasi v1.0", align="C", new_x="LMARGIN", new_y="NEXT")
            self.ln(2)

        def footer(self):
            self.set_y(-12)
            self.set_font("DejaVu", "", 8)
            self.set_text_color(120, 120, 120)
            self.cell(0, 8, f"Sahifa {self.page_no()}/{{nb}}", align="C")

    pdf = PDF()
    pdf.alias_nb_pages()
    pdf.set_auto_page_break(auto=True, margin=15)
    pdf.add_font("DejaVu", "", FONT_PATH)
    pdf.add_page()
    pdf.set_font("DejaVu", "", 11)

    for kind, content in blocks:
        if kind == "h1":
            pdf.ln(4)
            pdf.set_font("DejaVu", "", 16)
            pdf.multi_cell(0, 9, strip_md_inline(content))
            pdf.set_font("DejaVu", "", 11)
            pdf.ln(2)
        elif kind == "h2":
            pdf.ln(3)
            pdf.set_font("DejaVu", "", 13)
            pdf.multi_cell(0, 8, strip_md_inline(content))
            pdf.set_font("DejaVu", "", 11)
            pdf.ln(1)
        elif kind == "h3":
            pdf.ln(2)
            pdf.set_font("DejaVu", "", 12)
            pdf.multi_cell(0, 7, strip_md_inline(content))
            pdf.set_font("DejaVu", "", 11)
        elif kind == "p":
            pdf.multi_cell(0, 6, strip_md_inline(content))
            pdf.ln(1)
        elif kind == "hr":
            pdf.ln(3)
        elif kind == "code":
            pdf.set_font("DejaVu", "", 9)
            pdf.multi_cell(0, 5, content)
            pdf.set_font("DejaVu", "", 11)
            pdf.ln(1)
        elif kind == "ul":
            for item in content:
                pdf.multi_cell(0, 6, "  • " + strip_md_inline(item))
            pdf.ln(1)
        elif kind == "ol":
            for n, item in enumerate(content, 1):
                pdf.multi_cell(0, 6, f"  {n}. " + strip_md_inline(item))
            pdf.ln(1)
        elif kind == "table":
            pdf.set_font("DejaVu", "", 9)
            for tl in content:
                if all(set(c.strip()) <= set("-: ") for c in tl.split("|") if c.strip()):
                    continue
                row = " | ".join(strip_md_inline(c.strip()) for c in tl.strip("|").split("|"))
                pdf.multi_cell(0, 5, row)
            pdf.set_font("DejaVu", "", 11)
            pdf.ln(2)

    pdf.output(PDF_PATH)
    print("PDF:", PDF_PATH)


def try_word_com_pdf():
  """Windows: Word COM orqali DOCX -> PDF."""
  if sys.platform != "win32":
    return False
  try:
    import win32com.client
    word = win32com.client.Dispatch("Word.Application")
    word.Visible = False
    doc = word.Documents.Open(os.path.abspath(DOCX_PATH))
    doc.SaveAs(os.path.abspath(PDF_PATH), FileFormat=17)
    doc.Close()
    word.Quit()
    print("PDF (Word COM):", PDF_PATH)
    return True
  except Exception as ex:
    print("Word COM skipped:", ex)
    return False


def try_docx2pdf():
    try:
        from docx2pdf import convert
        convert(DOCX_PATH, PDF_PATH)
        print("PDF (docx2pdf):", PDF_PATH)
        return True
    except Exception as ex:
        print("docx2pdf skipped:", ex)
        return False


def main():
    if not os.path.isfile(MD_PATH):
        print("Missing", MD_PATH)
        sys.exit(1)
    blocks = parse_blocks(read_md())
    build_docx(blocks)
    if not try_word_com_pdf() and not try_docx2pdf():
        build_pdf(blocks)
    print("Done.")


if __name__ == "__main__":
    main()
