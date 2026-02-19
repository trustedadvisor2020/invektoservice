"""
Generic Markdown → PDF Converter for Invekto Scenario Documents
Handles: headers, tables, blockquotes, aha moments, bullet/numbered lists,
bold/italic inline, code blocks, horizontal rules.
"""

import re
import os
import sys

from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.colors import HexColor, white
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_JUSTIFY
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, HRFlowable, KeepTogether
)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

# ─── Colors ────────────────────────────────────────────────
PRIMARY       = HexColor("#1a56db")
PRIMARY_LIGHT = HexColor("#e8f0fe")
ACCENT        = HexColor("#f59e0b")
ACCENT_LIGHT  = HexColor("#fef3c7")
ACCENT_DARK   = HexColor("#b45309")
RED           = HexColor("#dc2626")
RED_LIGHT     = HexColor("#fef2f2")
GREEN         = HexColor("#059669")
GREEN_LIGHT   = HexColor("#ecfdf5")
GRAY_50       = HexColor("#f9fafb")
GRAY_100      = HexColor("#f3f4f6")
GRAY_200      = HexColor("#e5e7eb")
GRAY_500      = HexColor("#6b7280")
GRAY_700      = HexColor("#374151")
GRAY_900      = HexColor("#111827")

# ─── Fonts ─────────────────────────────────────────────────
font_map = [
    ("C:/Windows/Fonts/segoeui.ttf",  "Segoe"),
    ("C:/Windows/Fonts/segoeuib.ttf", "Segoe-Bold"),
    ("C:/Windows/Fonts/segoeuii.ttf", "Segoe-Italic"),
    ("C:/Windows/Fonts/segoeuiz.ttf", "Segoe-BoldItalic"),
]
ok = all(os.path.exists(p) for p, _ in font_map)
for p, n in font_map:
    if os.path.exists(p):
        pdfmetrics.registerFont(TTFont(n, p))

F    = "Segoe"       if ok else "Helvetica"
FB   = "Segoe-Bold"  if ok else "Helvetica-Bold"
FI   = "Segoe-Italic" if ok else "Helvetica-Oblique"
FBI  = "Segoe-BoldItalic" if ok else "Helvetica-BoldOblique"


# ─── Styles ────────────────────────────────────────────────
def mkstyles():
    return {
        "title":     ParagraphStyle("title",    fontName=FB, fontSize=22, leading=28, textColor=PRIMARY,   spaceAfter=4*mm),
        "subtitle":  ParagraphStyle("subtitle", fontName=F,  fontSize=11, leading=16, textColor=GRAY_500,  spaceAfter=6*mm),
        "h2":        ParagraphStyle("h2",       fontName=FB, fontSize=15, leading=21, textColor=GRAY_900,  spaceBefore=6*mm, spaceAfter=3*mm),
        "h3":        ParagraphStyle("h3",       fontName=FB, fontSize=13, leading=18, textColor=PRIMARY,   spaceBefore=5*mm, spaceAfter=3*mm),
        "h4":        ParagraphStyle("h4",       fontName=FB, fontSize=11, leading=15, textColor=GRAY_700,  spaceBefore=3*mm, spaceAfter=2*mm),
        "body":      ParagraphStyle("body",     fontName=F,  fontSize=10, leading=15, textColor=GRAY_700,  spaceAfter=3*mm, alignment=TA_JUSTIFY),
        "body_b":    ParagraphStyle("body_b",   fontName=FB, fontSize=10, leading=15, textColor=GRAY_900,  spaceAfter=3*mm),
        "bullet":    ParagraphStyle("bullet",   fontName=F,  fontSize=10, leading=15, textColor=GRAY_700,  leftIndent=10*mm, bulletIndent=5*mm, spaceAfter=1.5*mm),
        "num":       ParagraphStyle("num",      fontName=F,  fontSize=10, leading=15, textColor=GRAY_700,  leftIndent=10*mm, bulletIndent=5*mm, spaceAfter=1.5*mm),
        "meta_l":    ParagraphStyle("ml",       fontName=FB, fontSize=9,  leading=13, textColor=PRIMARY),
        "meta_v":    ParagraphStyle("mv",       fontName=F,  fontSize=9,  leading=13, textColor=GRAY_700),
        "th":        ParagraphStyle("th",       fontName=FB, fontSize=9,  leading=13, textColor=white),
        "tc":        ParagraphStyle("tc",       fontName=F,  fontSize=9,  leading=13, textColor=GRAY_700),
        "tcb":       ParagraphStyle("tcb",      fontName=FB, fontSize=9,  leading=13, textColor=GRAY_900),
        "aha_title": ParagraphStyle("at",       fontName=FB, fontSize=11, leading=16, textColor=ACCENT_DARK, spaceAfter=2*mm),
        "aha_text":  ParagraphStyle("atx",      fontName=F,  fontSize=10, leading=15, textColor=GRAY_700,  spaceAfter=1.5*mm),
        "aha_bold":  ParagraphStyle("ab",       fontName=FB, fontSize=10, leading=15, textColor=GRAY_900,  spaceAfter=1.5*mm),
        "aha_quote": ParagraphStyle("aq",       fontName=FI, fontSize=10, leading=15, textColor=ACCENT_DARK, leftIndent=6*mm, spaceAfter=1.5*mm),
        "aha_result":ParagraphStyle("ar",       fontName=FB, fontSize=11, leading=16, textColor=GREEN,     spaceAfter=1.5*mm),
        "section_label": ParagraphStyle("sl",   fontName=FB, fontSize=10, leading=14, textColor=PRIMARY,   spaceBefore=2*mm, spaceAfter=2*mm),
        "quote":     ParagraphStyle("quote",    fontName=FI, fontSize=10, leading=15, textColor=GRAY_500,  leftIndent=8*mm, spaceAfter=2*mm),
        "code":      ParagraphStyle("code",     fontName="Courier", fontSize=9, leading=13, textColor=GRAY_700, leftIndent=6*mm, spaceAfter=1*mm),
    }

S = mkstyles()
W = A4[0] - 40*mm  # available width with 20mm margins each side


# ─── Inline markdown → reportlab XML ──────────────────────
def md_inline(text):
    """Convert markdown bold/italic to reportlab XML tags."""
    # Escape XML entities first
    text = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    # Bold+italic ***text*** or ___text___
    text = re.sub(r'\*\*\*(.+?)\*\*\*', r'<b><i>\1</i></b>', text)
    # Bold **text**
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    # Italic *text* (but not **)
    text = re.sub(r'(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)', r'<i>\1</i>', text)
    # Inline code `text`
    text = re.sub(r'`(.+?)`', r'<font face="Courier" size="9">\1</font>', text)
    # Links [text](url) — just show text
    text = re.sub(r'\[([^\]]+)\]\([^\)]+\)', r'\1', text)
    return text


# ─── Component builders ───────────────────────────────────

def hr():
    return HRFlowable(width="100%", thickness=0.5, color=GRAY_200,
                       spaceBefore=2*mm, spaceAfter=2*mm)

def thick_hr():
    return HRFlowable(width="100%", thickness=1.5, color=PRIMARY,
                       spaceBefore=3*mm, spaceAfter=3*mm)

def meta_card(items):
    """Blue metadata card."""
    rows = []
    for label, value in items:
        rows.append([
            Paragraph(label, S["meta_l"]),
            Paragraph(md_inline(value), S["meta_v"]),
        ])
    t = Table(rows, colWidths=[W*0.28, W*0.72])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0,0),(0,-1), PRIMARY_LIGHT),
        ("BACKGROUND", (1,0),(1,-1), GRAY_50),
        ("BOX",        (0,0),(-1,-1), 0.5, GRAY_200),
        ("INNERGRID",  (0,0),(-1,-1), 0.5, GRAY_200),
        ("VALIGN",     (0,0),(-1,-1), "MIDDLE"),
        ("TOPPADDING", (0,0),(-1,-1), 4),
        ("BOTTOMPADDING",(0,0),(-1,-1),4),
        ("LEFTPADDING",(0,0),(-1,-1), 6),
        ("RIGHTPADDING",(0,0),(-1,-1),6),
    ]))
    return t

def data_table(headers, rows_data, col_ratios=None, width_pct=1.0):
    """Styled data table."""
    w = W * width_pct
    n = len(headers)
    if col_ratios is None:
        col_ratios = [1.0/n]*n
    cw = [w*r for r in col_ratios]

    hrow = [Paragraph(h, S["th"]) for h in headers]
    drows = []
    for row in rows_data:
        cells = []
        for c in row:
            txt = str(c)
            st = S["tcb"] if txt.startswith("<b>") or "**" in txt else S["tc"]
            cells.append(Paragraph(md_inline(txt), st))
        drows.append(cells)

    t = Table([hrow]+drows, colWidths=cw)
    cmds = [
        ("BACKGROUND",(0,0),(-1,0), PRIMARY),
        ("TEXTCOLOR", (0,0),(-1,0), white),
        ("VALIGN",    (0,0),(-1,-1),"MIDDLE"),
        ("TOPPADDING",(0,0),(-1,-1),5),
        ("BOTTOMPADDING",(0,0),(-1,-1),5),
        ("LEFTPADDING",(0,0),(-1,-1),6),
        ("RIGHTPADDING",(0,0),(-1,-1),6),
        ("BOX",       (0,0),(-1,-1),0.5,GRAY_200),
        ("INNERGRID", (0,0),(-1,-1),0.5,GRAY_200),
    ]
    for i in range(1, len(drows)+1):
        if i % 2 == 0:
            cmds.append(("BACKGROUND",(0,i),(-1,i),GRAY_50))
    t.setStyle(TableStyle(cmds))
    return t

def pain_box(text):
    """Red pain/problem box."""
    c = [[Paragraph(md_inline(text), ParagraphStyle("p",fontName=FI,fontSize=10,leading=15,textColor=RED))]]
    t = Table(c, colWidths=[W-12*mm])
    t.setStyle(TableStyle([
        ("BACKGROUND",(0,0),(-1,-1),RED_LIGHT),
        ("BOX",(0,0),(-1,-1),0.5,RED),
        ("LEFTPADDING",(0,0),(-1,-1),8),("RIGHTPADDING",(0,0),(-1,-1),8),
        ("TOPPADDING",(0,0),(-1,-1),6),("BOTTOMPADDING",(0,0),(-1,-1),6),
    ]))
    return t

def aha_box(paras):
    """Amber aha moment box."""
    rows = [[p] for p in paras]
    t = Table(rows, colWidths=[W-16*mm])
    cmds = [
        ("BACKGROUND",(0,0),(-1,-1),ACCENT_LIGHT),
        ("BOX",(0,0),(-1,-1),1.5,ACCENT),
        ("LEFTPADDING",(0,0),(-1,-1),10),("RIGHTPADDING",(0,0),(-1,-1),10),
        ("TOPPADDING",(0,0),(0,0),10),
        ("BOTTOMPADDING",(0,-1),(-1,-1),10),
    ]
    for i in range(len(rows)):
        if i > 0:
            cmds.append(("TOPPADDING",(0,i),(-1,i),2))
        if i < len(rows)-1:
            cmds.append(("BOTTOMPADDING",(0,i),(-1,i),2))
    t.setStyle(TableStyle(cmds))
    return t

def result_box(text):
    """Green result box."""
    c = [[Paragraph(md_inline(text), ParagraphStyle("r",fontName=F,fontSize=10,leading=15,textColor=GREEN))]]
    t = Table(c, colWidths=[W-12*mm])
    t.setStyle(TableStyle([
        ("BACKGROUND",(0,0),(-1,-1),GREEN_LIGHT),
        ("BOX",(0,0),(-1,-1),0.5,GREEN),
        ("LEFTPADDING",(0,0),(-1,-1),8),("RIGHTPADDING",(0,0),(-1,-1),8),
        ("TOPPADDING",(0,0),(-1,-1),6),("BOTTOMPADDING",(0,0),(-1,-1),6),
    ]))
    return t

def section_label(text):
    """Blue section label bar."""
    c = [[Paragraph(text, S["section_label"])]]
    t = Table(c, colWidths=[W])
    t.setStyle(TableStyle([
        ("BACKGROUND",(0,0),(-1,-1),PRIMARY_LIGHT),
        ("LEFTPADDING",(0,0),(-1,-1),8),("RIGHTPADDING",(0,0),(-1,-1),8),
        ("TOPPADDING",(0,0),(-1,-1),4),("BOTTOMPADDING",(0,0),(-1,-1),4),
    ]))
    return t


# ─── Markdown parser ──────────────────────────────────────

def parse_table(lines):
    """Parse markdown table lines into headers and rows."""
    if len(lines) < 2:
        return None, None

    def split_row(line):
        line = line.strip()
        if line.startswith("|"):
            line = line[1:]
        if line.endswith("|"):
            line = line[:-1]
        return [c.strip() for c in line.split("|")]

    headers = split_row(lines[0])
    # Skip separator line (line with ---)
    rows = []
    for line in lines[2:]:
        if line.strip():
            rows.append(split_row(line))
    return headers, rows

def is_table_separator(line):
    return bool(re.match(r'^\|[\s\-:|]+\|$', line.strip()))

def is_meta_table(headers, rows):
    """Check if table is a metadata-style key-value table (first col is bold label)."""
    if len(headers) == 2 and all(h.strip() == "" for h in headers):
        return True
    return False


def convert_md_to_pdf(md_path, pdf_path, doc_title=None):
    """Main converter: parse markdown, generate PDF."""

    with open(md_path, "r", encoding="utf-8") as f:
        content = f.read()

    lines = content.split("\n")
    story = []
    i = 0
    in_aha = False
    aha_lines = []
    in_code = False
    code_lines = []
    first_h2_seen = False
    page_title = doc_title or "Invekto Senaryolar"

    # Extract title from first line
    if lines and lines[0].startswith("# "):
        page_title = lines[0][2:].strip()

    # Footer function
    def add_footer(canvas, doc):
        canvas.saveState()
        canvas.setFont(F, 8)
        canvas.setFillColor(GRAY_500)
        pn = canvas.getPageNumber()
        canvas.drawCentredString(A4[0]/2, 12*mm, f"Invekto — {page_title}  |  Sayfa {pn}")
        canvas.restoreState()

    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        # ── Code block ───────────────────────────
        if stripped.startswith("```"):
            if in_code:
                # End code block
                in_code = False
                if code_lines:
                    for cl in code_lines:
                        story.append(Paragraph(cl.replace(" ", "&nbsp;"), S["code"]))
                    story.append(Spacer(1, 2*mm))
                code_lines = []
            else:
                in_code = True
            i += 1
            continue

        if in_code:
            code_lines.append(line.rstrip())
            i += 1
            continue

        # ── Empty line ───────────────────────────
        if not stripped:
            i += 1
            continue

        # ── Horizontal rule ──────────────────────
        if re.match(r'^-{3,}$', stripped) or re.match(r'^\*{3,}$', stripped):
            story.append(hr())
            i += 1
            continue

        # ── Aha moment block (> 💡) ──────────────
        if stripped.startswith("> 💡") or stripped.startswith(">💡") or \
           (stripped.startswith("> ") and "💡" in stripped and "Vay Be" in stripped):
            # Collect all aha blockquote lines
            aha_raw = []
            first_line = re.sub(r'^>\s*', '', stripped)
            # Remove the "💡 **Vay Be Anı:**" prefix
            first_line = re.sub(r'💡\s*\*\*Vay Be An[ıi]:\*\*\s*', '', first_line).strip()
            if first_line:
                aha_raw.append(first_line)
            i += 1
            while i < len(lines):
                l = lines[i].strip()
                if l.startswith(">"):
                    content_after = re.sub(r'^>\s*', '', l).strip()
                    # Skip nested blockquote markers for inner quotes
                    if content_after.startswith(">"):
                        content_after = re.sub(r'^>\s*', '', content_after).strip()
                        if content_after:
                            aha_raw.append("QUOTE:" + content_after)
                    elif content_after:
                        aha_raw.append(content_after)
                elif l == "":
                    i += 1
                    continue
                else:
                    break
                i += 1

            # Build aha box
            aha_paras = [Paragraph("\U0001f4a1  VAY BE ANI", S["aha_title"])]
            for ar in aha_raw:
                txt = md_inline(ar.replace("QUOTE:", ""))
                if ar.startswith("QUOTE:"):
                    aha_paras.append(Paragraph(txt, S["aha_quote"]))
                elif re.search(r'\b\d+[\.,]\d+\s*(TL|EUR|€|\$|USD)', ar) or \
                     re.search(r'toplam|Toplam|kayıp|kurtarıl|gelir|değer|fark buydu|Sonuç', ar):
                    aha_paras.append(Paragraph(txt, S["aha_result"]))
                elif ar.startswith("**") or "<b>" in txt[:10]:
                    aha_paras.append(Paragraph(txt, S["aha_bold"]))
                else:
                    aha_paras.append(Paragraph(txt, S["aha_text"]))

            story.append(Spacer(1, 2*mm))
            story.append(aha_box(aha_paras))
            story.append(Spacer(1, 3*mm))
            continue

        # ── Regular blockquote (non-aha) ─────────
        if stripped.startswith("> ") and "💡" not in stripped:
            quote_lines_buf = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                ql = re.sub(r'^>\s*', '', lines[i].strip())
                if ql:
                    quote_lines_buf.append(ql)
                i += 1

            # Check if it's header metadata (first few lines with **Label:** pattern)
            is_header_meta = all(re.match(r'\*\*[^*]+\*\*', ql) for ql in quote_lines_buf)
            if is_header_meta:
                for ql in quote_lines_buf:
                    story.append(Paragraph(md_inline(ql), S["body"]))
            else:
                for ql in quote_lines_buf:
                    story.append(Paragraph(md_inline(ql), S["quote"]))
            continue

        # ── Table ────────────────────────────────
        if stripped.startswith("|") and i+1 < len(lines) and is_table_separator(lines[i+1].strip()):
            tbl_lines = [stripped]
            i += 1
            while i < len(lines) and lines[i].strip().startswith("|"):
                tbl_lines.append(lines[i].strip())
                i += 1

            headers, rows = parse_table(tbl_lines)
            if headers and rows:
                if is_meta_table(headers, rows):
                    # Render as metadata card
                    items = []
                    for row in rows:
                        label = re.sub(r'\*\*([^*]+)\*\*', r'\1', row[0])
                        val = row[1] if len(row) > 1 else ""
                        items.append((label, val))
                    story.append(meta_card(items))
                else:
                    # Render as data table
                    clean_h = [re.sub(r'\*\*([^*]+)\*\*', r'\1', h) for h in headers]
                    clean_r = []
                    for row in rows:
                        clean_r.append([md_inline(c) for c in row])
                    story.append(data_table(clean_h, clean_r))
                story.append(Spacer(1, 3*mm))
            continue

        # ── H1 ───────────────────────────────────
        if stripped.startswith("# ") and not stripped.startswith("## "):
            title_text = stripped[2:].strip()
            story.append(Spacer(1, 12*mm))
            story.append(Paragraph(md_inline(title_text), S["title"]))
            story.append(thick_hr())
            i += 1
            continue

        # ── H2 ───────────────────────────────────
        if stripped.startswith("## ") and not stripped.startswith("### "):
            h2_text = stripped[3:].strip()
            if first_h2_seen:
                pass  # No page break for every h2 — let it flow
            first_h2_seen = True
            story.append(Spacer(1, 4*mm))
            story.append(Paragraph(md_inline(h2_text), S["h2"]))
            story.append(hr())
            i += 1
            continue

        # ── H3 ───────────────────────────────────
        if stripped.startswith("### ") and not stripped.startswith("#### "):
            h3_text = stripped[4:].strip()
            story.append(Spacer(1, 3*mm))
            story.append(Paragraph(md_inline(h3_text), S["h3"]))
            i += 1
            continue

        # ── H4 ───────────────────────────────────
        if stripped.startswith("#### "):
            h4_text = stripped[5:].strip()
            story.append(Paragraph(md_inline(h4_text), S["h4"]))
            i += 1
            continue

        # ── Bullet list ──────────────────────────
        if re.match(r'^[-*]\s', stripped):
            bullet_text = re.sub(r'^[-*]\s+', '', stripped)
            story.append(Paragraph(f"\u2022  {md_inline(bullet_text)}", S["bullet"]))
            i += 1
            continue

        # ── Numbered list ────────────────────────
        m = re.match(r'^(\d+)\.\s+(.+)', stripped)
        if m:
            num = m.group(1)
            text = m.group(2)
            # Collect continuation lines (indented)
            i += 1
            while i < len(lines) and lines[i].strip() and not re.match(r'^\d+\.', lines[i].strip()) \
                  and not lines[i].strip().startswith("#") and not lines[i].strip().startswith("|") \
                  and not lines[i].strip().startswith(">") and not lines[i].strip().startswith("-") \
                  and not lines[i].strip().startswith("*") and not lines[i].strip().startswith("```"):
                if lines[i].startswith("   ") or lines[i].startswith("\t"):
                    text += " " + lines[i].strip()
                else:
                    break
                i += 1
            story.append(Paragraph(f"<b>{num}.</b>  {md_inline(text)}", S["num"]))
            continue

        # ── Pain indicator ───────────────────────
        # Lines that are just **Acı:** or contain strong pain language
        if stripped == "**Acı:**" or stripped.startswith("**Acı:**"):
            pain_text = stripped.replace("**Acı:**", "").strip()
            story.append(Paragraph("Ac\u0131", S["h4"]))
            if pain_text:
                story.append(pain_box(pain_text))
            i += 1
            # Check if next line is the pain description
            if not pain_text and i < len(lines) and lines[i].strip() and not lines[i].strip().startswith("#"):
                pain_desc = lines[i].strip()
                story.append(pain_box(pain_desc))
                i += 1
            continue

        # ── Bold label line (**Label:** text) ────
        bold_label_match = re.match(r'^\*\*([^*]+)\*\*:?\s*(.*)', stripped)
        if bold_label_match and not stripped.startswith("|"):
            label = bold_label_match.group(1)
            rest = bold_label_match.group(2).strip()

            # Special handling for known section labels
            pain_labels = ["Acı", "Sorun", "Problem"]
            result_labels = ["Sonuç", "Sonuc", "Etki", "Getiri"]

            if label in pain_labels:
                story.append(Paragraph(md_inline(label), S["h4"]))
                if rest:
                    story.append(pain_box(rest))
                    i += 1
                    continue
            elif label in result_labels:
                if rest:
                    story.append(result_box(f"<b>{label}:</b> {rest}"))
                else:
                    story.append(Paragraph(md_inline(f"<b>{label}</b>"), S["body_b"]))
                i += 1
                continue

            # Regular bold label
            if rest:
                story.append(Paragraph(f"<b>{md_inline(label)}:</b> {md_inline(rest)}", S["body"]))
            else:
                story.append(Paragraph(f"<b>{md_inline(label)}</b>", S["body_b"]))
            i += 1
            continue

        # ── Regular paragraph ────────────────────
        para_text = stripped
        i += 1
        # Don't merge multiple lines — keep paragraphs separate for readability

        # Check if it's a "pain" line (italicized frustration)
        if para_text.startswith("*\"") or para_text.startswith("*'"):
            story.append(Paragraph(md_inline(para_text), S["quote"]))
        else:
            story.append(Paragraph(md_inline(para_text), S["body"]))
        continue

    # Build PDF
    doc = SimpleDocTemplate(
        pdf_path, pagesize=A4,
        leftMargin=20*mm, rightMargin=20*mm,
        topMargin=20*mm, bottomMargin=20*mm,
    )

    # Footer function needs to be defined with correct title
    def footer(canvas, doc):
        canvas.saveState()
        canvas.setFont(F, 8)
        canvas.setFillColor(GRAY_500)
        pn = canvas.getPageNumber()
        # Truncate title for footer
        ft = page_title[:50] + "..." if len(page_title) > 50 else page_title
        canvas.drawCentredString(A4[0]/2, 12*mm, f"Invekto \u2014 {ft}  |  Sayfa {pn}")
        canvas.restoreState()

    doc.build(story, onFirstPage=footer, onLaterPages=footer)

    # Count pages
    from pypdf import PdfReader
    r = PdfReader(pdf_path)
    return len(r.pages)


# ─── Main ─────────────────────────────────────────────────
if __name__ == "__main__":
    docs_dir = r"c:\CRMs\InvektoServices\ideas\docs"

    files = [
        "e-ticaret-senaryolari.md",
        "saglik-senaryolari.md",
        "otel-turizm-senaryolari.md",
        "guzellik-salonu-senaryolari.md",
        "egitim-senaryolari.md",
        "evrensel-senaryolar.md",
        "ai-inovasyonlar.md",
    ]

    # If specific file passed as argument
    if len(sys.argv) > 1:
        files = [sys.argv[1]]

    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8') if hasattr(sys.stdout, 'buffer') else sys.stdout

    total_pages = 0
    for fname in files:
        md_path = os.path.join(docs_dir, fname)
        pdf_path = os.path.join(docs_dir, fname.replace(".md", ".pdf"))

        if not os.path.exists(md_path):
            print(f"SKIP: {fname} not found")
            continue

        try:
            pages = convert_md_to_pdf(md_path, pdf_path)
            total_pages += pages
            print(f"OK: {fname} -> {pages} pages")
        except Exception as e:
            print(f"ERR: {fname} -> {e}")
            import traceback
            traceback.print_exc()

    print(f"\nTotal: {len(files)} files, {total_pages} pages")
