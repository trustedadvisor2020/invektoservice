"""
Generic Apple-style Markdown → PDF Converter
Parses any Invekto scenario .md file and produces an Apple-design PDF.
Each scenario starts on a new page, aha moments inline.
"""

import os, sys, re, io
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.colors import HexColor, white, Color
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, HRFlowable, KeepTogether, Flowable
)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

# ─── Palette ────────────────────────────────────────────
BLACK  = HexColor("#1d1d1f")
GRAY   = HexColor("#86868b")
GLIGHT = HexColor("#a1a1a6")
BLUE   = HexColor("#0071e3")
GREEN  = HexColor("#30d158")
AMBER  = HexColor("#ff9f0a")
DIVIDER= HexColor("#d2d2d7")
DANGER = HexColor("#ff3b30")
AHA_BG = HexColor("#fffbf0")
AI_BG  = HexColor("#f0f7ff")

# ─── Fonts ──────────────────────────────────────────────
for p, n in [
    ("C:/Windows/Fonts/segoeui.ttf",  "SF"),
    ("C:/Windows/Fonts/segoeuib.ttf", "SF-B"),
    ("C:/Windows/Fonts/segoeuii.ttf", "SF-I"),
    ("C:/Windows/Fonts/segoeuiz.ttf", "SF-BI"),
    ("C:/Windows/Fonts/segoeuil.ttf", "SF-L"),
    ("C:/Windows/Fonts/seguisb.ttf",  "SF-S"),
]:
    if os.path.exists(p):
        pdfmetrics.registerFont(TTFont(n, p))

F  = "SF"
FB = "SF-B"
FI = "SF-I"
FL = "SF-L" if os.path.exists("C:/Windows/Fonts/segoeuil.ttf") else F
FS = "SF-S"  if os.path.exists("C:/Windows/Fonts/seguisb.ttf")  else FB

PW, PH = A4
ML, MR, MT, MB = 28*mm, 28*mm, 22*mm, 20*mm
W = PW - ML - MR


# ─── Gradient bar flowable ──────────────────────────────
class GradientBar(Flowable):
    def __init__(self, width, height=2.5, c1=BLUE, c2=GREEN):
        Flowable.__init__(self)
        self.width, self.height, self.c1, self.c2 = width, height, c1, c2
    def wrap(self, aw, ah): return (self.width, self.height)
    def draw(self):
        for i in range(50):
            t = i / 50
            r = self.c1.red   + (self.c2.red   - self.c1.red)   * t
            g = self.c1.green + (self.c2.green - self.c1.green) * t
            b = self.c1.blue  + (self.c2.blue  - self.c1.blue)  * t
            sw = self.width / 50
            self.canv.setFillColor(Color(r, g, b))
            self.canv.rect(i*sw, 0, sw+0.5, self.height, fill=1, stroke=0)


# ─── Styles ─────────────────────────────────────────────
def S(name, **kw):
    return ParagraphStyle(name, **kw)

st = {
    "hero":    S("hero",    fontName=FB, fontSize=34, leading=40, textColor=BLACK, spaceAfter=3*mm),
    "hero_s":  S("hero_s",  fontName=FL, fontSize=14, leading=20, textColor=GRAY,  spaceAfter=6*mm),
    "code":    S("code",    fontName=FS, fontSize=9,  leading=12, textColor=BLUE,  spaceAfter=1*mm),
    "title":   S("title",   fontName=FB, fontSize=20, leading=25, textColor=BLACK, spaceAfter=3*mm),
    "h2":      S("h2",      fontName=FB, fontSize=16, leading=21, textColor=BLACK, spaceBefore=4*mm, spaceAfter=2*mm),
    "sub":     S("sub",     fontName=FS, fontSize=11, leading=15, textColor=BLACK, spaceBefore=3*mm, spaceAfter=2*mm),
    "body":    S("body",    fontName=F,  fontSize=10, leading=15, textColor=BLACK, spaceAfter=2.5*mm),
    "body_g":  S("body_g",  fontName=F,  fontSize=10, leading=15, textColor=GRAY,  spaceAfter=2.5*mm),
    "body_b":  S("body_b",  fontName=FB, fontSize=10, leading=15, textColor=BLACK, spaceAfter=2.5*mm),
    "bullet":  S("bullet",  fontName=F,  fontSize=10, leading=15, textColor=BLACK, leftIndent=7*mm, spaceAfter=1.5*mm),
    "num":     S("num",     fontName=F,  fontSize=10, leading=15, textColor=BLACK, leftIndent=7*mm, spaceAfter=1.5*mm),
    "sect":    S("sect",    fontName=FS, fontSize=9,  leading=12, textColor=BLUE,  spaceAfter=1*mm),
    "quote":   S("quote",   fontName=FI, fontSize=10, leading=15, textColor=GRAY,  leftIndent=6*mm, spaceAfter=2*mm),
    "mn":      S("mn",      fontName=FB, fontSize=22, leading=26, textColor=BLUE),
    "ml":      S("ml",      fontName=F,  fontSize=8,  leading=11, textColor=GRAY),
    "th":      S("th",      fontName=FS, fontSize=8,  leading=11, textColor=GRAY),
    "tc":      S("tc",      fontName=F,  fontSize=9.5,leading=14, textColor=BLACK),
    "tcb":     S("tcb",     fontName=FS, fontSize=9.5,leading=14, textColor=BLACK),
    "pain":    S("pain",    fontName=FI, fontSize=10, leading=15, textColor=GRAY),
    "aha_l":   S("aha_l",   fontName=FS, fontSize=8,  leading=11, textColor=AMBER, spaceAfter=1*mm),
    "aha_s":   S("aha_s",   fontName=F,  fontSize=9.5,leading=14, textColor=GRAY,  spaceAfter=2*mm),
    "aha_q":   S("aha_q",   fontName=FI, fontSize=12, leading=17, textColor=BLACK, spaceAfter=2*mm),
    "aha_r":   S("aha_r",   fontName=FS, fontSize=11, leading=15, textColor=BLUE,  spaceAfter=1*mm),
    "aha_a":   S("aha_a",   fontName=F,  fontSize=8,  leading=11, textColor=GLIGHT),
    "brand":   S("brand",   fontName=FS, fontSize=11, leading=14, textColor=GLIGHT),
    "close_h": S("close_h", fontName=FB, fontSize=34, leading=40, textColor=BLACK, spaceAfter=4*mm),
    "close_l": S("close_l", fontName=FL, fontSize=34, leading=40, textColor=GRAY,  spaceAfter=8*mm),
    "url":     S("url",     fontName=FS, fontSize=13, leading=18, textColor=BLUE),
}
SP = Spacer


# ─── Inline MD → XML ───────────────────────────────────
def mi(text):
    text = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    text = re.sub(r'\*\*\*(.+?)\*\*\*', r'<b><i>\1</i></b>', text)
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)', r'<i>\1</i>', text)
    text = re.sub(r'`(.+?)`', r'<font face="Courier" size="9">\1</font>', text)
    text = re.sub(r'\[([^\]]+)\]\([^\)]+\)', r'\1', text)
    return text


# ─── Component builders ────────────────────────────────

def thin_rule():
    return HRFlowable(width="100%", thickness=0.25, color=DIVIDER, spaceBefore=2*mm, spaceAfter=2*mm)

def apple_table(headers, rows, ratios=None):
    n = len(headers)
    ratios = ratios or [1.0/n]*n
    cw = [W*r for r in ratios]
    hrow = [Paragraph(h, st["th"]) for h in headers]
    drows = [[Paragraph(mi(c), st["tcb"] if j==0 else st["tc"]) for j,c in enumerate(row)] for row in rows]
    t = Table([hrow]+drows, colWidths=cw)
    cmds = [
        ("VALIGN",(0,0),(-1,-1),"TOP"),
        ("TOPPADDING",(0,0),(-1,-1),5),("BOTTOMPADDING",(0,0),(-1,-1),5),
        ("LEFTPADDING",(0,0),(-1,-1),0),("RIGHTPADDING",(0,0),(-1,-1),4),
        ("LINEBELOW",(0,0),(-1,0),0.5,DIVIDER),
    ]
    for i in range(1,len(drows)):
        cmds.append(("LINEBELOW",(0,i),(-1,i),0.25,HexColor("#e8e8ed")))
    t.setStyle(TableStyle(cmds))
    return t

def pain_block(text):
    c = [[Paragraph(mi(text), st["pain"])]]
    t = Table(c, colWidths=[W-4*mm])
    t.setStyle(TableStyle([
        ("LEFTPADDING",(0,0),(-1,-1),10),
        ("TOPPADDING",(0,0),(-1,-1),4),("BOTTOMPADDING",(0,0),(-1,-1),4),
        ("LINEBEFOREWIDTH",(0,0),(0,-1),2),("LINEBEFORECOLOR",(0,0),(0,-1),DANGER),
    ]))
    return t

def aha_block(lines_raw):
    """Parse raw aha lines into a compact amber-bordered block."""
    # Combine all lines into one narrative
    full_text = " ".join(l.strip() for l in lines_raw if l.strip())
    # Try to find a quoted sentence (something in quotes within the text)
    quote_match = re.search(r'["\u201c]([^"\u201d]{15,})["\u201d]', full_text)

    # Find monetary results
    result_match = re.search(r'(\d[\d.,]+\s*(?:TL|EUR|\u20ac|\$|USD)[^.]*\.?)', full_text)
    # Also check for percentage results
    if not result_match:
        result_match = re.search(r'(%\d+[^.]*\.?)', full_text)

    parts = []
    parts.append([Paragraph("VAY BE ANI", st["aha_l"])])

    # Scene: use full text but abbreviated if long
    scene = full_text
    if len(scene) > 400:
        scene = scene[:397] + "..."
    parts.append([Paragraph(mi(scene), st["aha_s"])])

    # Extract best quote if found
    if quote_match:
        parts.append([Paragraph(f"\u201c{mi(quote_match.group(1))}\u201d", st["aha_q"])])

    # Result
    if result_match:
        parts.append([Paragraph(mi(result_match.group(1)), st["aha_r"])])

    t = Table(parts, colWidths=[W - 14*mm])
    cmds = [
        ("LEFTPADDING",(0,0),(-1,-1),10),("RIGHTPADDING",(0,0),(-1,-1),4),
        ("TOPPADDING",(0,0),(0,0),8),("BOTTOMPADDING",(0,-1),(-1,-1),8),
        ("LINEBEFOREWIDTH",(0,0),(0,-1),2.5),("LINEBEFORECOLOR",(0,0),(0,-1),AMBER),
        ("BACKGROUND",(0,0),(-1,-1),AHA_BG),
    ]
    for i in range(len(parts)):
        if i > 0: cmds.append(("TOPPADDING",(0,i),(-1,i),1))
        if i < len(parts)-1: cmds.append(("BOTTOMPADDING",(0,i),(-1,i),1))
    t.setStyle(TableStyle(cmds))
    return t

def metrics_row(items):
    n = len(items)
    cw = [W/n]*n
    cells = []
    for num, label in items:
        inner = Table([
            [Paragraph(num, st["mn"])],
            [Paragraph(label, st["ml"])],
        ], colWidths=[cw[0]-6*mm])
        inner.setStyle(TableStyle([
            ("VALIGN",(0,0),(-1,-1),"TOP"),
            ("LEFTPADDING",(0,0),(-1,-1),0),
            ("TOPPADDING",(0,0),(-1,-1),0),("BOTTOMPADDING",(0,0),(-1,-1),0),
        ]))
        cells.append(inner)
    t = Table([cells], colWidths=cw)
    t.setStyle(TableStyle([
        ("VALIGN",(0,0),(-1,-1),"TOP"),
        ("LEFTPADDING",(0,0),(-1,-1),0),("RIGHTPADDING",(0,0),(-1,-1),8),
    ]))
    return t


# ─── Table parsing ──────────────────────────────────────
def is_sep(line):
    return bool(re.match(r'^\|[\s\-:|]+\|$', line.strip()))

def parse_md_table(tlines):
    def split(l):
        l = l.strip().strip("|")
        return [c.strip() for c in l.split("|")]
    if len(tlines) < 3: return None, None
    h = split(tlines[0])
    rows = [split(l) for l in tlines[2:] if l.strip()]
    return h, rows

def is_kv_table(headers):
    return len(headers) == 2 and all(h.strip() == "" for h in headers)


# ─── Main converter ────────────────────────────────────

def convert(md_path, pdf_path):
    with open(md_path, "r", encoding="utf-8") as f:
        lines = f.read().split("\n")

    story = []
    i = 0
    page_title = ""
    first_h3_seen = False
    h2_count = 0
    in_code_block = False

    # Extract title
    if lines and lines[0].startswith("# "):
        page_title = lines[0][2:].strip()
        # Clean markdown from title
        page_title_clean = re.sub(r'\*\*(.+?)\*\*', r'\1', page_title)
        page_title_clean = re.sub(r'\*(.+?)\*', r'\1', page_title_clean)

    # ── Cover page ────────────────────────────────────
    story.append(SP(1, 35*mm))
    story.append(GradientBar(W, 3))
    story.append(SP(1, 10*mm))

    # Split title at " — " for two-line treatment
    if " — " in page_title or " - " in page_title:
        parts = re.split(r'\s*[\u2014\u2013—-]+\s*', page_title, 1)
        story.append(Paragraph(mi(parts[0]) + ".", st["hero"]))
        if len(parts) > 1:
            story.append(Paragraph(mi(parts[1]), st["hero_s"]))
    else:
        story.append(Paragraph(mi(page_title) + ".", st["hero"]))

    story.append(SP(1, 30*mm))
    story.append(Paragraph("invekto", st["brand"]))
    story.append(PageBreak())

    # Skip title line
    if lines and lines[0].startswith("# "):
        i = 1

    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        # ── Code block ────────────────────────────
        if stripped.startswith("```"):
            in_code_block = not in_code_block
            i += 1
            continue
        if in_code_block:
            story.append(Paragraph(
                stripped.replace(" ", "&nbsp;"),
                S("cd", fontName="Courier", fontSize=9, leading=13, textColor=GRAY, leftIndent=6*mm, spaceAfter=1*mm)
            ))
            i += 1
            continue

        # ── Empty / HR ────────────────────────────
        if not stripped:
            i += 1
            continue
        if re.match(r'^-{3,}$', stripped) or re.match(r'^\*{3,}$', stripped):
            # Don't add a rule for every --- , just skip. Scenarios get page breaks.
            i += 1
            continue

        # ── Aha moment ────────────────────────────
        if stripped.startswith("> \U0001f4a1") or stripped.startswith(">\U0001f4a1") or \
           (stripped.startswith(">") and "\U0001f4a1" in stripped and "Vay Be" in stripped):
            aha_raw = []
            first = re.sub(r'^>\s*', '', stripped)
            first = re.sub(r'\U0001f4a1\s*\*\*Vay Be An[ıi]:\*\*\s*', '', first).strip()
            if first: aha_raw.append(first)
            i += 1
            while i < len(lines):
                l = lines[i].strip()
                if l.startswith(">"):
                    c = re.sub(r'^>\s*', '', l).strip()
                    if c.startswith(">"):
                        c = re.sub(r'^>\s*', '', c).strip()
                    if c: aha_raw.append(c)
                    i += 1
                elif l == "":
                    i += 1
                else:
                    break
            story.append(SP(1, 3*mm))
            story.append(aha_block(aha_raw))
            story.append(SP(1, 2*mm))
            continue

        # ── Regular blockquote (header metadata or quote) ──
        if stripped.startswith("> ") and "\U0001f4a1" not in stripped:
            q_lines = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                ql = re.sub(r'^>\s*', '', lines[i].strip())
                if ql: q_lines.append(ql)
                i += 1
            # Header metadata?
            is_meta = all(re.match(r'\*\*[^*]+\*\*', ql) for ql in q_lines)
            if is_meta:
                for ql in q_lines:
                    story.append(Paragraph(mi(ql), st["body_g"]))
            else:
                for ql in q_lines:
                    story.append(Paragraph(mi(ql), st["quote"]))
            continue

        # ── Table ─────────────────────────────────
        if stripped.startswith("|") and i+1 < len(lines) and is_sep(lines[i+1]):
            tl = [stripped]
            i += 1
            while i < len(lines) and lines[i].strip().startswith("|"):
                tl.append(lines[i].strip())
                i += 1
            h, rows = parse_md_table(tl)
            if h and rows:
                if is_kv_table(h):
                    # Key-value: render as simple list
                    for row in rows:
                        label = re.sub(r'\*\*([^*]+)\*\*', r'\1', row[0])
                        val = row[1] if len(row) > 1 else ""
                        story.append(Paragraph(f"<b>{mi(label)}</b>\u2003{mi(val)}", st["body"]))
                else:
                    clean_h = [re.sub(r'\*\*([^*]+)\*\*', r'\1', hh) for hh in h]
                    story.append(apple_table(clean_h, rows))
                story.append(SP(1, 2*mm))
            continue

        # ── H1 (skip, already used for cover) ────
        if stripped.startswith("# ") and not stripped.startswith("## "):
            i += 1
            continue

        # ── H2 ────────────────────────────────────
        if stripped.startswith("## ") and not stripped.startswith("### "):
            h2_text = stripped[3:].strip()
            h2_count += 1
            # Section label
            story.append(SP(1, 4*mm))
            story.append(Paragraph(mi(h2_text).upper(), st["sect"]))
            story.append(thin_rule())
            i += 1
            continue

        # ── H3 (scenario start → PageBreak) ──────
        if stripped.startswith("### ") and not stripped.startswith("#### "):
            h3_text = stripped[4:].strip()

            # PageBreak before each scenario (except very first)
            if first_h3_seen:
                story.append(PageBreak())
            first_h3_seen = True

            story.append(SP(1, 2*mm))

            # Try to split "CODE — Title" pattern
            m = re.match(r'^(S\d+|[A-Z]{1,3}[\-\s]?\d+|Senaryo\s+\d+)\s*[\u2014\u2013—\-]+\s*(.+)', h3_text)
            if m:
                story.append(Paragraph(mi(m.group(1)), st["code"]))
                story.append(Paragraph(mi(m.group(2)), st["title"]))
            else:
                story.append(Paragraph(mi(h3_text), st["title"]))
            i += 1
            continue

        # ── H4 ────────────────────────────────────
        if stripped.startswith("#### "):
            h4_text = stripped[5:].strip()
            story.append(Paragraph(mi(h4_text), st["sub"]))
            i += 1
            continue

        # ── Bullet ────────────────────────────────
        if re.match(r'^[-*]\s', stripped):
            bt = re.sub(r'^[-*]\s+', '', stripped)
            story.append(Paragraph(f"\u2022\u2003{mi(bt)}", st["bullet"]))
            i += 1
            continue

        # ── Numbered list ─────────────────────────
        nm = re.match(r'^(\d+)\.\s+(.+)', stripped)
        if nm:
            num, text = nm.group(1), nm.group(2)
            i += 1
            # Collect continuation
            while i < len(lines) and lines[i].strip() and \
                  not re.match(r'^\d+\.', lines[i].strip()) and \
                  not lines[i].strip().startswith("#") and \
                  not lines[i].strip().startswith("|") and \
                  not lines[i].strip().startswith(">") and \
                  not lines[i].strip().startswith("-") and \
                  not lines[i].strip().startswith("```"):
                if lines[i].startswith("   ") or lines[i].startswith("\t"):
                    text += " " + lines[i].strip()
                    i += 1
                else:
                    break
            # Render as numbered step with blue number
            row = Table([
                [Paragraph(num, S("sn", fontName=FS, fontSize=9, leading=13, textColor=BLUE, alignment=TA_CENTER)),
                 Paragraph(mi(text), S("st2", fontName=F, fontSize=9.5, leading=14, textColor=BLACK))]
            ], colWidths=[8*mm, W-14*mm])
            row.setStyle(TableStyle([
                ("VALIGN",(0,0),(-1,-1),"TOP"),
                ("LEFTPADDING",(0,0),(0,0),0),
                ("TOPPADDING",(0,0),(-1,-1),2),("BOTTOMPADDING",(0,0),(-1,-1),2),
            ]))
            story.append(row)
            continue

        # ── Bold label: **Acı:** etc ──────────────
        bm = re.match(r'^\*\*([^*]+)\*\*:?\s*(.*)', stripped)
        if bm:
            label, rest = bm.group(1), bm.group(2).strip()
            pain_labels = {"Acı", "Sorun", "Problem"}
            result_labels = {"Sonuç", "Sonuc", "Etki", "Getiri"}

            if label in pain_labels:
                if rest:
                    story.append(pain_block(rest))
                i += 1
                # Next line might be pain description
                if not rest and i < len(lines) and lines[i].strip() and \
                   not lines[i].strip().startswith("#"):
                    story.append(pain_block(lines[i].strip()))
                    i += 1
                continue

            if label in result_labels and rest:
                # Green result text
                story.append(Paragraph(mi(f"<b>{label}:</b> {rest}"),
                    S("res", fontName=F, fontSize=10, leading=15, textColor=HexColor("#059669"), spaceAfter=2*mm)))
                i += 1
                continue

            # Known section labels → subhead
            section_labels = {"Nasıl Çalışır", "Nasıl Çalışıyor", "Invekto ile",
                              "Bu Olmadan Çalışmayan Senaryolar",
                              "Çözdüğü Sorun", "Kime Lazım", "Kim İçin",
                              "Bu Neden Var", "Psikolojik Etki",
                              "Maliyet ve Getiri", "Invekto Servisleri", "Bonus"}
            if label in section_labels:
                story.append(Paragraph(mi(label), st["sub"]))
                if rest:
                    story.append(Paragraph(mi(rest), st["body"]))
                i += 1
                continue

            # Generic bold label
            if rest:
                story.append(Paragraph(f"<b>{mi(label)}:</b> {mi(rest)}", st["body"]))
            else:
                story.append(Paragraph(f"<b>{mi(label)}</b>", st["body_b"]))
            i += 1
            continue

        # ── Regular paragraph ─────────────────────
        # Check if it's a pain-style italic line
        if stripped.startswith('*"') or stripped.startswith("*'") or stripped.startswith('*\u201c'):
            story.append(Paragraph(mi(stripped), st["quote"]))
        else:
            story.append(Paragraph(mi(stripped), st["body"]))
        i += 1

    # ── Closing page ──────────────────────────────────
    story.append(PageBreak())
    story.append(SP(1, 45*mm))
    story.append(GradientBar(W, 3))
    story.append(SP(1, 10*mm))

    # Dynamic closing text
    closings = {
        "e-ticaret":   ("Mesajdan\nsatışa.", "Satıştan\nsadakate."),
        "saglik":      ("Mesajdan\nrandevuya.", "Randevudan\nsadakate."),
        "otel":        ("Mesajdan\nkonaklamaya.", "Konaklamadan\nsadakate."),
        "guzellik":    ("Mesajdan\nrandevuya.", "Randevudan\nsadakate."),
        "egitim":      ("Mesajdan\nkayıta.", "Kayıttan\nmezuniyete."),
        "evrensel":    ("Altyapıdan\ndeğere.", "Değerden\nbüyümeye."),
        "ai":          ("Bugünden\ngeleceğe.", "Gelecekten\nkazanca."),
    }
    fname = os.path.basename(md_path).lower()
    c1, c2 = ("Mesajdan\ndeğere.", "Değerden\nbüyümeye.")
    for key, (t1, t2) in closings.items():
        if key in fname:
            c1, c2 = t1, t2
            break

    story.append(Paragraph(c1, st["close_h"]))
    story.append(Paragraph(c2, st["close_l"]))
    story.append(Paragraph("invekto.com", st["url"]))

    # ── Build ─────────────────────────────────────────
    def footer(canvas, doc):
        canvas.saveState()
        canvas.setFont(F, 8)
        canvas.setFillColor(GLIGHT)
        canvas.drawCentredString(PW/2, 12*mm, str(canvas.getPageNumber()))
        canvas.setStrokeColor(DIVIDER)
        canvas.setLineWidth(0.25)
        canvas.line(ML, 17*mm, PW-MR, 17*mm)
        canvas.restoreState()

    doc = SimpleDocTemplate(pdf_path, pagesize=A4,
        leftMargin=ML, rightMargin=MR, topMargin=MT, bottomMargin=MB)
    doc.build(story, onFirstPage=footer, onLaterPages=footer)

    from pypdf import PdfReader
    return len(PdfReader(pdf_path).pages)


# ─── Main ──────────────────────────────────────────────
if __name__ == "__main__":
    docs = r"c:\CRMs\InvektoServices\ideas\docs"
    files = [
        "e-ticaret-senaryolari.md",
        "saglik-senaryolari.md",
        "otel-turizm-senaryolari.md",
        "guzellik-salonu-senaryolari.md",
        "egitim-senaryolari.md",
        "evrensel-senaryolar.md",
        "ai-inovasyonlar.md",
    ]
    if len(sys.argv) > 1:
        files = [sys.argv[1]]

    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8') if hasattr(sys.stdout, 'buffer') else sys.stdout

    total = 0
    for fname in files:
        md = os.path.join(docs, fname)
        pdf = os.path.join(docs, fname.replace(".md", ".pdf"))
        if not os.path.exists(md):
            print(f"SKIP: {fname}")
            continue
        try:
            pages = convert(md, pdf)
            total += pages
            print(f"OK: {fname} -> {pages} pages")
        except Exception as e:
            print(f"ERR: {fname} -> {e}")
            import traceback; traceback.print_exc()

    print(f"\nTotal: {len(files)} files, {total} pages")
