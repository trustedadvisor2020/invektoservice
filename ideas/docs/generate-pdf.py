"""
Sağlık Senaryoları — Formatlı PDF Üretici
Invekto Sağlık Sektörü Senaryo Dokümanı
"""

from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm, cm
from reportlab.lib.colors import HexColor, white, black
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER, TA_JUSTIFY
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, KeepTogether, HRFlowable
)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
import os

# --- Colors ---
PRIMARY = HexColor("#1a56db")       # Deep blue
PRIMARY_LIGHT = HexColor("#e8f0fe") # Light blue bg
ACCENT = HexColor("#f59e0b")        # Amber/gold for aha
ACCENT_LIGHT = HexColor("#fef3c7")  # Light amber bg
ACCENT_DARK = HexColor("#b45309")   # Dark amber text
RED = HexColor("#dc2626")           # Pain/problem
RED_LIGHT = HexColor("#fef2f2")     # Light red bg
GREEN = HexColor("#059669")         # Success/result
GREEN_LIGHT = HexColor("#ecfdf5")   # Light green bg
GRAY_50 = HexColor("#f9fafb")
GRAY_100 = HexColor("#f3f4f6")
GRAY_200 = HexColor("#e5e7eb")
GRAY_500 = HexColor("#6b7280")
GRAY_700 = HexColor("#374151")
GRAY_900 = HexColor("#111827")

# --- Font Registration ---
# Try to use system fonts that support Turkish characters
font_paths = [
    ("C:/Windows/Fonts/segoeui.ttf", "Segoe"),
    ("C:/Windows/Fonts/segoeuib.ttf", "Segoe-Bold"),
    ("C:/Windows/Fonts/segoeuii.ttf", "Segoe-Italic"),
    ("C:/Windows/Fonts/segoeuiz.ttf", "Segoe-BoldItalic"),
]

use_custom_font = True
for path, name in font_paths:
    if os.path.exists(path):
        pdfmetrics.registerFont(TTFont(name, path))
    else:
        use_custom_font = False
        break

if use_custom_font:
    FONT = "Segoe"
    FONT_BOLD = "Segoe-Bold"
    FONT_ITALIC = "Segoe-Italic"
    FONT_BI = "Segoe-BoldItalic"
else:
    FONT = "Helvetica"
    FONT_BOLD = "Helvetica-Bold"
    FONT_ITALIC = "Helvetica-Oblique"
    FONT_BI = "Helvetica-BoldOblique"

# --- Styles ---
def make_styles():
    s = {}

    s["title"] = ParagraphStyle(
        "title", fontName=FONT_BOLD, fontSize=22, leading=28,
        textColor=PRIMARY, spaceAfter=4*mm,
    )
    s["subtitle"] = ParagraphStyle(
        "subtitle", fontName=FONT, fontSize=11, leading=15,
        textColor=GRAY_500, spaceAfter=6*mm,
    )
    s["h2"] = ParagraphStyle(
        "h2", fontName=FONT_BOLD, fontSize=16, leading=22,
        textColor=GRAY_900, spaceBefore=8*mm, spaceAfter=4*mm,
    )
    s["h3"] = ParagraphStyle(
        "h3", fontName=FONT_BOLD, fontSize=13, leading=18,
        textColor=PRIMARY, spaceBefore=6*mm, spaceAfter=3*mm,
    )
    s["h4"] = ParagraphStyle(
        "h4", fontName=FONT_BOLD, fontSize=11, leading=15,
        textColor=GRAY_700, spaceBefore=4*mm, spaceAfter=2*mm,
    )
    s["body"] = ParagraphStyle(
        "body", fontName=FONT, fontSize=10, leading=15,
        textColor=GRAY_700, spaceAfter=3*mm, alignment=TA_JUSTIFY,
    )
    s["body_bold"] = ParagraphStyle(
        "body_bold", fontName=FONT_BOLD, fontSize=10, leading=15,
        textColor=GRAY_900, spaceAfter=3*mm,
    )
    s["bullet"] = ParagraphStyle(
        "bullet", fontName=FONT, fontSize=10, leading=15,
        textColor=GRAY_700, leftIndent=12*mm, bulletIndent=6*mm,
        spaceAfter=2*mm,
    )
    s["pain_text"] = ParagraphStyle(
        "pain_text", fontName=FONT_ITALIC, fontSize=10, leading=15,
        textColor=RED, spaceAfter=3*mm, leftIndent=4*mm,
    )
    s["aha_title"] = ParagraphStyle(
        "aha_title", fontName=FONT_BOLD, fontSize=11, leading=16,
        textColor=ACCENT_DARK, spaceAfter=2*mm,
    )
    s["aha_text"] = ParagraphStyle(
        "aha_text", fontName=FONT, fontSize=10, leading=15,
        textColor=GRAY_700, spaceAfter=2*mm,
    )
    s["aha_bold"] = ParagraphStyle(
        "aha_bold", fontName=FONT_BOLD, fontSize=10, leading=15,
        textColor=GRAY_900, spaceAfter=2*mm,
    )
    s["aha_quote"] = ParagraphStyle(
        "aha_quote", fontName=FONT_ITALIC, fontSize=10, leading=15,
        textColor=ACCENT_DARK, leftIndent=6*mm, spaceAfter=2*mm,
    )
    s["aha_result"] = ParagraphStyle(
        "aha_result", fontName=FONT_BOLD, fontSize=11, leading=16,
        textColor=GREEN, spaceAfter=2*mm,
    )
    s["flow_step"] = ParagraphStyle(
        "flow_step", fontName=FONT, fontSize=9.5, leading=14,
        textColor=GRAY_700, alignment=TA_CENTER,
    )
    s["table_header"] = ParagraphStyle(
        "th", fontName=FONT_BOLD, fontSize=9, leading=13,
        textColor=white,
    )
    s["table_cell"] = ParagraphStyle(
        "tc", fontName=FONT, fontSize=9, leading=13,
        textColor=GRAY_700,
    )
    s["table_cell_bold"] = ParagraphStyle(
        "tcb", fontName=FONT_BOLD, fontSize=9, leading=13,
        textColor=GRAY_900,
    )
    s["meta_label"] = ParagraphStyle(
        "ml", fontName=FONT_BOLD, fontSize=9, leading=13,
        textColor=PRIMARY,
    )
    s["meta_value"] = ParagraphStyle(
        "mv", fontName=FONT, fontSize=9, leading=13,
        textColor=GRAY_700,
    )
    s["footer"] = ParagraphStyle(
        "footer", fontName=FONT, fontSize=8, leading=10,
        textColor=GRAY_500, alignment=TA_CENTER,
    )
    return s

# --- Helper functions ---

def hr():
    return HRFlowable(width="100%", thickness=0.5, color=GRAY_200, spaceBefore=3*mm, spaceAfter=3*mm)

def thick_hr():
    return HRFlowable(width="100%", thickness=1.5, color=PRIMARY, spaceBefore=4*mm, spaceAfter=4*mm)

def meta_table(items, avail_width):
    """Create a metadata card table for a scenario"""
    rows = []
    for label, value in items:
        rows.append([
            Paragraph(label, styles["meta_label"]),
            Paragraph(value, styles["meta_value"]),
        ])

    t = Table(rows, colWidths=[avail_width * 0.28, avail_width * 0.72])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (0, -1), PRIMARY_LIGHT),
        ("BACKGROUND", (1, 0), (1, -1), GRAY_50),
        ("BOX", (0, 0), (-1, -1), 0.5, GRAY_200),
        ("INNERGRID", (0, 0), (-1, -1), 0.5, GRAY_200),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("ROUNDEDCORNERS", [3, 3, 3, 3]),
    ]))
    return t

def data_table(headers, rows_data, avail_width, col_ratios=None):
    """Create a styled data table"""
    if col_ratios is None:
        col_ratios = [1.0 / len(headers)] * len(headers)
    col_widths = [avail_width * r for r in col_ratios]

    header_row = [Paragraph(h, styles["table_header"]) for h in headers]
    data_rows = []
    for row in rows_data:
        data_rows.append([
            Paragraph(str(cell), styles["table_cell"]) if not str(cell).startswith("<b>")
            else Paragraph(str(cell), styles["table_cell_bold"])
            for cell in row
        ])

    t = Table([header_row] + data_rows, colWidths=col_widths)
    style_cmds = [
        ("BACKGROUND", (0, 0), (-1, 0), PRIMARY),
        ("TEXTCOLOR", (0, 0), (-1, 0), white),
        ("ALIGN", (0, 0), (-1, 0), "LEFT"),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("BOX", (0, 0), (-1, -1), 0.5, GRAY_200),
        ("INNERGRID", (0, 0), (-1, -1), 0.5, GRAY_200),
        ("ROUNDEDCORNERS", [3, 3, 3, 3]),
    ]
    # Alternating row colors
    for i in range(1, len(data_rows) + 1):
        if i % 2 == 0:
            style_cmds.append(("BACKGROUND", (0, i), (-1, i), GRAY_50))

    t.setStyle(TableStyle(style_cmds))
    return t

def aha_box(paragraphs, avail_width):
    """Create an aha moment highlighted box"""
    inner = []
    for p in paragraphs:
        inner.append(p)

    # Wrap in a table for background color
    inner_content = []
    for item in inner:
        inner_content.append([item])

    t = Table(inner_content, colWidths=[avail_width - 16*mm])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), ACCENT_LIGHT),
        ("BOX", (0, 0), (-1, -1), 1.5, ACCENT),
        ("LEFTPADDING", (0, 0), (-1, -1), 10),
        ("RIGHTPADDING", (0, 0), (-1, -1), 10),
        ("TOPPADDING", (0, 0), (0, 0), 10),
        ("BOTTOMPADDING", (0, -1), (-1, -1), 10),
        ("TOPPADDING", (0, 1), (-1, -1), 2),
        ("BOTTOMPADDING", (0, 0), (-1, -2), 2),
        ("ROUNDEDCORNERS", [4, 4, 4, 4]),
    ]))
    return t

def flow_diagram(steps, avail_width):
    """Create a vertical flow diagram"""
    rows = []
    for i, step in enumerate(steps):
        # Step box
        rows.append([Paragraph(step, styles["flow_step"])])
        if i < len(steps) - 1:
            rows.append([Paragraph("\u2193", ParagraphStyle(
                "arrow", fontName=FONT, fontSize=14, leading=16,
                textColor=PRIMARY, alignment=TA_CENTER,
            ))])

    t = Table(rows, colWidths=[avail_width * 0.7])
    style_cmds = [
        ("ALIGN", (0, 0), (-1, -1), "CENTER"),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("TOPPADDING", (0, 0), (-1, -1), 3),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
    ]
    # Style step boxes (even rows = steps, odd rows = arrows)
    for i in range(0, len(rows), 2):
        style_cmds.extend([
            ("BACKGROUND", (0, i), (0, i), PRIMARY_LIGHT),
            ("BOX", (0, i), (0, i), 0.5, PRIMARY),
            ("LEFTPADDING", (0, i), (0, i), 8),
            ("RIGHTPADDING", (0, i), (0, i), 8),
            ("ROUNDEDCORNERS", [3, 3, 3, 3]),
        ])

    t.setStyle(TableStyle(style_cmds))

    # Center the flow diagram
    wrapper = Table([[t]], colWidths=[avail_width])
    wrapper.setStyle(TableStyle([
        ("ALIGN", (0, 0), (0, 0), "CENTER"),
    ]))
    return wrapper

def pain_box(text, avail_width):
    """Red-tinted pain/problem box"""
    content = [[Paragraph(text, styles["pain_text"])]]
    t = Table(content, colWidths=[avail_width - 12*mm])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), RED_LIGHT),
        ("BOX", (0, 0), (-1, -1), 0.5, RED),
        ("LEFTPADDING", (0, 0), (-1, -1), 8),
        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
        ("ROUNDEDCORNERS", [3, 3, 3, 3]),
    ]))
    return t

# --- Page template ---
def add_page_number(canvas, doc):
    canvas.saveState()
    canvas.setFont(FONT, 8)
    canvas.setFillColor(GRAY_500)
    page_num = canvas.getPageNumber()
    text = f"Invekto \u2014 Sa\u011fl\u0131k Senaryolar\u0131  |  Sayfa {page_num}"
    canvas.drawCentredString(A4[0] / 2, 12 * mm, text)
    canvas.restoreState()

# --- Build PDF ---
output_path = r"c:\CRMs\InvektoServices\ideas\docs\saglik-senaryolari-formatted.pdf"

doc = SimpleDocTemplate(
    output_path,
    pagesize=A4,
    leftMargin=20*mm,
    rightMargin=20*mm,
    topMargin=20*mm,
    bottomMargin=20*mm,
)

styles = make_styles()
story = []
W = A4[0] - 40*mm  # available width


# ============================================================
# COVER / TITLE
# ============================================================

story.append(Spacer(1, 20*mm))
story.append(Paragraph(
    "Sa\u011fl\u0131k Senaryolar\u0131",
    styles["title"]
))
story.append(Paragraph(
    "Di\u015f Klinikleri ve Estetik Klinikler i\u00e7in Invekto Kullan\u0131m Senaryolar\u0131",
    ParagraphStyle("big_sub", fontName=FONT, fontSize=13, leading=18, textColor=GRAY_500, spaceAfter=8*mm)
))
story.append(thick_hr())

# Sector info
story.append(Paragraph(
    "<b>Sekt\u00f6r:</b> Di\u015f klinikleri, estetik cerrahi klinikleri, medikal estetik merkezleri",
    styles["body"]
))
story.append(Paragraph(
    "<b>Hedef Kitle:</b> G\u00fcnde 20-100 mesaj alan, 2-15 ki\u015filik ekibiyle hasta ileti\u015fimini y\u00f6neten klinikler",
    styles["body"]
))
story.append(Spacer(1, 4*mm))

# Promises table
story.append(data_table(
    ["Segment", "Anahtar Vaat"],
    [
        ["<b>Di\u015f Klinikleri</b>", "Fiyat sorular\u0131n\u0131 randevuya \u00e7evir, no-show\u2019u %60 azalt"],
        ["<b>Estetik Klinikler</b>", "Lead\u2019leri hastaya d\u00f6n\u00fc\u015ft\u00fcr, medikal turizmi \u00f6l\u00e7ekle"],
    ],
    W, [0.25, 0.75]
))
story.append(Spacer(1, 6*mm))


# ============================================================
# WHY HEALTH?
# ============================================================

story.append(Paragraph("Neden Sa\u011fl\u0131k Sekt\u00f6r\u00fc \u0130\u00e7in Invekto?", styles["h2"]))

story.append(Paragraph(
    'Bir di\u015f klini\u011finin sabah mesajlar\u0131 aras\u0131nda en az 10 tane <i>"\u0130mplant ne kadar?"</i> sorusu vard\u0131r. Bu soruya verilen her ge\u00e7 cevap, ba\u015fka bir klini\u011fe giden bir hasta demektir.',
    styles["body"]
))
story.append(Paragraph(
    'Daha k\u00f6t\u00fcs\u00fc \u2014 randevu alan hastalar\u0131n <b>%15-20\u2019si gelmiyor.</b> No-show. Ve tedavi sonras\u0131 takip? \u00c7o\u011fu klinikte hi\u00e7 yap\u0131lm\u0131yor.',
    styles["body"]
))
story.append(Paragraph(
    'Estetik kliniklerde durum daha da kritik. Instagram\u2019dan gelen <i>"Botox fiyat\u0131 ne?"</i> DM\u2019leri, klinik kapat\u0131rken resepsiyonist mesaisi bitmi\u015fken birikiyor. Yabanc\u0131 hastalar \u0130ngilizce veya Arap\u00e7a yaz\u0131yor \u2014 cevap verecek kimse yok.',
    styles["body"]
))

story.append(pain_box(
    "Her ka\u00e7an lead = potansiyel 5.000 \u2013 50.000 TL kay\u0131p",
    W
))
story.append(Spacer(1, 4*mm))

story.append(Paragraph("Invekto bu d\u00f6ng\u00fcy\u00fc k\u0131rar:", styles["body_bold"]))
bullets = [
    "Fiyat sorusunu <b>randevuya</b> d\u00f6n\u00fc\u015ft\u00fcr\u00fcr",
    "Hat\u0131rlatma ile <b>no-show\u2019u</b> azalt\u0131r",
    "Tedavi sonras\u0131 <b>otomatik takip</b> g\u00f6nderir",
    "T\u00fcm bunlar\u0131 <b>KVKK\u2019ya uygun</b> yapar",
]
for b in bullets:
    story.append(Paragraph(f"\u2022  {b}", styles["bullet"]))

story.append(Spacer(1, 4*mm))


# ============================================================
# PERSONAS
# ============================================================

story.append(Paragraph("Sa\u011fl\u0131k Personalar\u0131", styles["h3"]))

story.append(data_table(
    ["Persona", "Kim", "G\u00fcnl\u00fck Ger\u00e7eklik"],
    [
        ["<b>Dr. Burak (D1)</b>", "Di\u015f klini\u011fi sahibi, 3 \u00fcnit", "Koltukta hasta varken telefonu kontrol edemiyor"],
        ["<b>Elif (D2)</b>", "\u00d6n b\u00fcro sorumlusu", "Telefon + WhatsApp + y\u00fcz y\u00fcze ayn\u0131 anda"],
        ["<b>Dr. Selin (A1)</b>", "Estetik klinik sahibi, 5 doktor", "Instagram leadlerinin %40\u2019\u0131 d\u00f6nm\u00fcyor"],
        ["<b>Zeynep (A2)</b>", "Operasyon sorumlusu", "3 kanaldan mesaj, doktor onay\u0131 bekliyor"],
    ],
    W, [0.22, 0.30, 0.48]
))

story.append(PageBreak())


# ============================================================
# S6 — Fiyat Sorusunu Randevuya Çevirme
# ============================================================

story.append(Paragraph("GEL\u0130R SENARYOLARI", ParagraphStyle(
    "section_label", fontName=FONT_BOLD, fontSize=10, leading=14,
    textColor=PRIMARY, spaceBefore=2*mm, spaceAfter=2*mm,
    backColor=PRIMARY_LIGHT,
)))
story.append(Spacer(1, 2*mm))

story.append(Paragraph("S6 \u2014 Fiyat Sorusunu Randevuya \u00c7evirme", styles["h2"]))

story.append(meta_table([
    ("Potansiyel Etki", "~60.000 TL / ay"),
    ("Tetikleyen", '\u201c\u0130mplant ne kadar?\u201d, \u201cBotox fiyat\u0131 nedir?\u201d'),
    ("Kritik S\u00fcre", "\u0130lk 5 dakika"),
], W))
story.append(Spacer(1, 4*mm))

# Pain
story.append(Paragraph("Ac\u0131", styles["h4"]))
story.append(pain_box(
    '\u201c\u0130mplant ne kadar?\u201d \u2014 bu soruya do\u011fru yakla\u015f\u0131m randevu, yanl\u0131\u015f yakla\u015f\u0131m kay\u0131p hasta. 5 dakika i\u00e7inde cevap veren klinik, 1 saat sonra cevap verene g\u00f6re <b>10 kat</b> daha fazla randevu al\u0131r.',
    W
))
story.append(Spacer(1, 3*mm))

# Flow
story.append(Paragraph("Nas\u0131l \u00c7al\u0131\u015f\u0131r", styles["h4"]))
story.append(flow_diagram([
    "Hasta sorusu (WhatsApp / Instagram)",
    'AI niyeti anlar \u2192 "fiyat + tedavi talebi"',
    "Fiyat ARALI\u011eI verir (kesin fiyat de\u011fil \u2014 muayene gerekli)",
    'Randevu teklifi: "Bu hafta m\u00fcsait saatlerimiz..."',
    "Hasta onaylarsa \u2192 hat\u0131rlatma zinciri ba\u015flar (R-1g\u00fcn, R-2saat)",
], W))
story.append(Spacer(1, 2*mm))

story.append(Paragraph(
    '<b>Perde Arkası:</b> AgentAI (7105) niyeti anlar \u2192 Knowledge (7104) fiyat aralıklarını çeker \u2192 doktor onaylı şablon ile yanıt.',
    styles["body"]
))
story.append(Spacer(1, 3*mm))

# Aha
story.append(aha_box([
    Paragraph("\U0001f4a1  VAY BE ANI", styles["aha_title"]),
    Paragraph(
        '<b>Saat 22:30.</b> Elif \u00f6n b\u00fcroyu kapat\u0131p eve gitmi\u015f.',
        styles["aha_bold"]
    ),
    Paragraph(
        'Tam o s\u0131rada bir hasta Instagram\u2019dan yazd\u0131: <i>"\u0130mplant ne kadar?"</i>',
        styles["aha_text"]
    ),
    Paragraph(
        'Normalde bu mesaj\u0131 Elif sabah 09:00\u2019da g\u00f6r\u00fcrd\u00fc \u2014 ama o zamana kadar hasta 4 klini\u011fe daha yazm\u0131\u015ft\u0131 ve en h\u0131zl\u0131 cevap verenden randevu alm\u0131\u015ft\u0131.',
        styles["aha_text"]
    ),
    Paragraph(
        '<b>Invekto ile?</b> Mesaj geldi, <b>8 saniyede</b> fiyat aral\u0131\u011f\u0131 ve randevu teklifi gitti. Hasta gece 22:31\u2019de randevusunu olu\u015fturdu.',
        styles["aha_text"]
    ),
    Paragraph(
        'Sabah Elif geldi\u011finde sistemde yeni randevu bildirimi vard\u0131:',
        styles["aha_text"]
    ),
    Paragraph(
        '\u201cBu hasta biz uyurken gelmi\u015f!\u201d',
        styles["aha_quote"]
    ),
    Paragraph(
        'O hastan\u0131n tedavi de\u011feri: <b>45.000 TL.</b>',
        styles["aha_result"]
    ),
    Paragraph(
        '8 saniye ile 11 saat aras\u0131ndaki fark buydu.',
        styles["aha_bold"]
    ),
], W))

story.append(PageBreak())


# ============================================================
# S7 — No-Show Önleme
# ============================================================

story.append(Paragraph("S7 \u2014 No-Show \u00d6nleme", styles["h2"]))

story.append(meta_table([
    ("Potansiyel Etki", "~135.000 TL / ay"),
    ("Problem", "Randevu alan hastalar\u0131n %15-20\u2019si gelmiyor"),
    ("Her bo\u015f koltuk", "1.000 \u2013 5.000 TL kay\u0131p"),
], W))
story.append(Spacer(1, 4*mm))

# How it works - timing table
story.append(Paragraph("Nas\u0131l \u00c7al\u0131\u015f\u0131r", styles["h4"]))
story.append(data_table(
    ["Zamanlama", "Mesaj / Aksiyon"],
    [
        ["<b>R \u2212 1 g\u00fcn</b>", '"Yar\u0131n saat 14:00 randevunuz var. Onayl\u0131yor musunuz?"'],
        ["<b>R \u2212 2 saat</b>", '"Randevunuz 2 saat sonra. Klinik adresi: ..."'],
        ["<b>\u0130ptal gelirse</b>", "Yeni tarih \u00f6nerilir + bekleme listesine haber verilir"],
        ["<b>Hasta geldi</b>", "Tedavi sonras\u0131 takip zinciri ba\u015flar"],
    ],
    W, [0.22, 0.78]
))
story.append(Spacer(1, 3*mm))

# Result box
result_content = [[Paragraph(
    '<b>Sonu\u00e7:</b> No-show oran\u0131 <b>%60 azal\u0131r.</b> Ortalama bir klinik i\u00e7in ayl\u0131k <b>135.000 TL</b> kay\u0131p \u00f6nlenir.',
    ParagraphStyle("res", fontName=FONT, fontSize=10, leading=15, textColor=GREEN)
)]]
rt = Table(result_content, colWidths=[W - 12*mm])
rt.setStyle(TableStyle([
    ("BACKGROUND", (0, 0), (-1, -1), GREEN_LIGHT),
    ("BOX", (0, 0), (-1, -1), 0.5, GREEN),
    ("LEFTPADDING", (0, 0), (-1, -1), 8),
    ("RIGHTPADDING", (0, 0), (-1, -1), 8),
    ("TOPPADDING", (0, 0), (-1, -1), 6),
    ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ("ROUNDEDCORNERS", [3, 3, 3, 3]),
]))
story.append(rt)
story.append(Spacer(1, 4*mm))

# Aha
story.append(aha_box([
    Paragraph("\U0001f4a1  VAY BE ANI", styles["aha_title"]),
    Paragraph(
        'Dr. Burak ay\u0131n sonunda raporlara bakt\u0131:',
        styles["aha_text"]
    ),
], W))
story.append(Spacer(1, 2*mm))

# Before/after comparison table
story.append(data_table(
    ["", "Ge\u00e7en Ay", "Bu Ay"],
    [
        ["<b>Ka\u00e7an randevu</b>", "47 hasta", "<b>19 hasta</b>"],
        ["<b>Fark</b>", "", "<b>+28 hasta geldi</b>"],
    ],
    W * 0.8, [0.35, 0.325, 0.325]
))
story.append(Spacer(1, 2*mm))

story.append(aha_box([
    Paragraph(
        '28 hasta x ort. 3.500 TL = <b>98.000 TL</b> \u2014 bir tek hat\u0131rlatma mesaj\u0131 y\u00fcz\u00fcnden.',
        styles["aha_text"]
    ),
    Paragraph(
        'Ama as\u0131l "vay be" an\u0131 \u015fuydu:',
        styles["aha_bold"]
    ),
    Paragraph(
        'Bekleme listesindeki Ay\u015fe Han\u0131m\u2019a "14:00\u2019te yer a\u00e7\u0131ld\u0131" mesaj\u0131 gitmi\u015f. Ay\u015fe Han\u0131m <b>3 dakikada</b> onaylam\u0131\u015f. O g\u00fcn <b>22.000 TL\u2019lik</b> implant tedavisine ba\u015flam\u0131\u015f.',
        styles["aha_text"]
    ),
    Paragraph(
        'Bo\u015f koltuk hem dolmu\u015f \u2014 hem de en de\u011ferli hastayla dolmu\u015f.',
        styles["aha_result"]
    ),
], W))

story.append(PageBreak())


# ============================================================
# Senaryo 27 — Instagram DM
# ============================================================

story.append(Paragraph("SAHA SENARYOLARI \u2014 D\u0130\u015e KL\u0130N\u0130KLER\u0130", ParagraphStyle(
    "section_label2", fontName=FONT_BOLD, fontSize=10, leading=14,
    textColor=PRIMARY, spaceBefore=2*mm, spaceAfter=2*mm,
    backColor=PRIMARY_LIGHT,
)))
story.append(Spacer(1, 2*mm))

story.append(Paragraph('Senaryo 27 \u2014 "Instagram DM: Foto atsam fiyat verir misiniz?"', styles["h2"]))

story.append(meta_table([
    ("Kanal", "Instagram DM"),
    ("Lead Tipi", "Foto\u011fraf + fiyat sorusu = en de\u011ferli lead"),
    ("Guardrail", "AI kesin fiyat vermez (sa\u011fl\u0131k riskli alan)"),
], W))
story.append(Spacer(1, 4*mm))

story.append(Paragraph("Ac\u0131", styles["h4"]))
story.append(pain_box(
    'Hasta Instagram\u2019dan di\u015f foto\u011fraf\u0131 g\u00f6nderiyor, "fiyat ne?" diyor. Bu sa\u011fl\u0131k sekt\u00f6r\u00fcn\u00fcn <b>en de\u011ferli lead\u2019idir</b> \u2014 hasta tedaviye haz\u0131r, sadece fiyat onay\u0131 bekliyor.',
    W
))
story.append(Spacer(1, 3*mm))

story.append(Paragraph("Invekto ile", styles["h4"]))
bullets2 = [
    "IG DM tek ekrandan y\u00f6netilir",
    'AI foto\u011fraf\u0131 inceler de\u011fil \u2014 ama <b>niyeti anlar:</b> "fiyat + tedavi talebi"',
    'Cevap \u00f6nerisi: <i>"Foto\u011fraf\u0131n\u0131z i\u00e7in te\u015fekk\u00fcrler! Kesin tedavi plan\u0131 ve fiyat muayenede belirlenir. Bu hafta m\u00fcsaitiz. Randevu olu\u015ftural\u0131m m\u0131?"</i>',
]
for b in bullets2:
    story.append(Paragraph(f"\u2022  {b}", styles["bullet"]))
story.append(Spacer(1, 3*mm))

# Aha
story.append(aha_box([
    Paragraph("\U0001f4a1  VAY BE ANI", styles["aha_title"]),
    Paragraph(
        '<b>Pazar ak\u015fam\u0131, saat 21:00.</b> Elif evde, tatilini ya\u015f\u0131yor.',
        styles["aha_bold"]
    ),
    Paragraph(
        'Bir hasta Instagram DM\u2019den di\u015f foto\u011fraf\u0131 att\u0131: <i>"Bu di\u015f kurtar\u0131labilir mi? Fiyat ne olur?"</i>',
        styles["aha_text"]
    ),
    Paragraph(
        'Invekto <b>12 saniyede</b> cevap verdi: <i>"Foto\u011fraf\u0131n\u0131z i\u00e7in te\u015fekk\u00fcrler! Kesin tedavi plan\u0131 muayenede belirlenir. Yar\u0131n Pazartesi 10:00 veya 14:30 m\u00fcsaitiz. Hangisi size uyar?"</i>',
        styles["aha_text"]
    ),
    Paragraph(
        'Hasta 10:00\u2019\u0131 se\u00e7ti. Pazartesi geldi. <b>35.000 TL\u2019lik</b> implant tedavisine ba\u015flad\u0131.',
        styles["aha_text"]
    ),
    Paragraph(
        '\u201c5 klini\u011fe yazd\u0131m, sadece siz cevap verdiniz. Di\u011ferleri Pazartesi\u2019ye kadar d\u00f6nmedi bile.\u201d',
        styles["aha_quote"]
    ),
    Paragraph(
        'Bir Pazar gecesi. 12 saniye. <b>35.000 TL.</b>',
        styles["aha_result"]
    ),
], W))

story.append(PageBreak())


# ============================================================
# Senaryo 30 — Acil Ağrı
# ============================================================

story.append(Paragraph("Senaryo 30 \u2014 Acil A\u011fr\u0131: Gece Mesaj\u0131", styles["h2"]))

story.append(meta_table([
    ("Zaman", "Gece 02:00+"),
    ("Aciliyet", "Y\u00fcksek \u2014 triage gerekiyor"),
    ("Guardrail", "AI ila\u00e7 dozaj\u0131 veya tedavi \u00f6nerisi YAPMAZ"),
], W))
story.append(Spacer(1, 4*mm))

story.append(Paragraph("Ac\u0131", styles["h4"]))
story.append(pain_box(
    'Gece 2\u2019de hasta yaz\u0131yor: "Di\u015fim \u00e7ok a\u011fr\u0131yor, dayanam\u0131yorum!" Bu mesaj\u0131 sabaha erteleyemezsiniz.',
    W
))
story.append(Spacer(1, 3*mm))

story.append(Paragraph("Invekto ile", styles["h4"]))
bullets3 = [
    "AI <b>acil intent</b> tespit eder \u2192 y\u00fcksek \u00f6ncelik etiketi",
    "Otomatik cevap: genel bilgi + y\u00f6nlendirme",
    "Doktora <b>push bildirim</b> (mobil uygulama)",
    "Guardrail: sadece genel bilgi, kesin yorum yok",
]
for b in bullets3:
    story.append(Paragraph(f"\u2022  {b}", styles["bullet"]))
story.append(Spacer(1, 3*mm))

story.append(aha_box([
    Paragraph("\U0001f4a1  VAY BE ANI", styles["aha_title"]),
    Paragraph(
        '<b>Gece 02:15.</b> Ay\u015fe Han\u0131m uyand\u0131 \u2014 sol \u00e7enesinde dayan\u0131lmaz a\u011fr\u0131.',
        styles["aha_bold"]
    ),
    Paragraph(
        'Panikle Google\u2019a yazd\u0131: <i>"di\u015f a\u011fr\u0131s\u0131 \u00f6l\u00fcmc\u00fcl olabilir mi?"</i> Korkun\u00e7 sonu\u00e7lar \u00e7\u0131kt\u0131, uykusu ka\u00e7t\u0131.',
        styles["aha_text"]
    ),
    Paragraph(
        'Sonra akl\u0131na geldi \u2014 klini\u011fin WhatsApp\u2019\u0131n\u0131 denedi. <b>5 saniyede</b> cevap geldi:',
        styles["aha_text"]
    ),
    Paragraph(
        '\u201cAcil a\u011fr\u0131n\u0131z\u0131 anl\u0131yoruz. Ge\u00e7ici rahatlama i\u00e7in so\u011fuk kompres uygulayabilirsiniz. Sabah 09:00\u2019da size ilk randevuyu ay\u0131rd\u0131k. Ama: ate\u015f, y\u00fczde \u015fi\u015flik veya nefes darl\u0131\u011f\u0131 varsa hemen 112\u2019yi aray\u0131n.\u201d',
        styles["aha_quote"]
    ),
    Paragraph(
        'Ay\u015fe Han\u0131m derin bir nefes ald\u0131. <b>Biri vard\u0131, biri ilgileniyordu.</b>',
        styles["aha_text"]
    ),
    Paragraph(
        '\u00c7\u0131k\u0131\u015fta resepsiyona d\u00f6nd\u00fc:',
        styles["aha_text"]
    ),
    Paragraph(
        '\u201cGece 2\u2019de bile cevap vermeniz... ben ba\u015fka klini\u011fe gitmem art\u0131k.\u201d',
        styles["aha_quote"]
    ),
    Paragraph(
        '<b>6 ay sonra</b> t\u00fcm ailesini getirdi \u2014 e\u015fi, annesi, o\u011flu.',
        styles["aha_text"]
    ),
    Paragraph(
        'Toplam de\u011fer: <b>78.000 TL.</b> Hepsi gece 2\u2019deki o 5 saniyelik mesajla ba\u015flad\u0131.',
        styles["aha_result"]
    ),
], W))

story.append(PageBreak())


# ============================================================
# Senaryo 51 — Instagram DM Botox
# ============================================================

story.append(Paragraph("SAHA SENARYOLARI \u2014 ESTET\u0130K KL\u0130N\u0130KLER", ParagraphStyle(
    "section_label3", fontName=FONT_BOLD, fontSize=10, leading=14,
    textColor=PRIMARY, spaceBefore=2*mm, spaceAfter=2*mm,
    backColor=PRIMARY_LIGHT,
)))
story.append(Spacer(1, 2*mm))

story.append(Paragraph('Senaryo 51 \u2014 Instagram DM: "Botox fiyat\u0131 nedir?"', styles["h2"]))

story.append(meta_table([
    ("Kanal", "Instagram DM"),
    ("Sekt\u00f6r", "Estetik klinik"),
    ("Kritik Metrik", "Her cevaps\u0131z DM = ort. 6.000 TL kay\u0131p"),
], W))
story.append(Spacer(1, 4*mm))

story.append(Paragraph("Ac\u0131", styles["h4"]))
story.append(pain_box(
    'Estetik klini\u011fin Instagram\u2019\u0131 <b>vitrindir.</b> Her DM bir potansiyel hasta \u2014 ama cevap gecikmesi <b>%50 kay\u0131p</b> demek.',
    W
))
story.append(Spacer(1, 3*mm))

story.append(Paragraph("Invekto ile", styles["h4"]))
bullets4 = [
    "IG DM tek ekrandan y\u00f6netim",
    'AI: <i>"Botox fiyatlar\u0131m\u0131z b\u00f6lgeye g\u00f6re 3.000-8.000 TL aras\u0131nda de\u011fi\u015fiyor. Y\u00fcz y\u00fcze de\u011ferlendirme ile size \u00f6zel plan olu\u015fturuyoruz."</i>',
    "Lead takibi: DM \u2192 WhatsApp ge\u00e7i\u015fi \u2192 randevu \u2192 tedavi",
]
for b in bullets4:
    story.append(Paragraph(f"\u2022  {b}", styles["bullet"]))
story.append(Spacer(1, 3*mm))

# Aha with comparison tables
story.append(aha_box([
    Paragraph("\U0001f4a1  VAY BE ANI", styles["aha_title"]),
    Paragraph(
        'Dr. Selin Instagram analiti\u011fine bakt\u0131:',
        styles["aha_text"]
    ),
], W))
story.append(Spacer(1, 2*mm))

story.append(data_table(
    ["", "Ge\u00e7en Ay"],
    [
        ["Gelen DM", "127"],
        ["Cevaplanan", "68"],
        ["<b>Cevaps\u0131z</b>", "<b>59</b>"],
    ],
    W * 0.5, [0.5, 0.5]
))
story.append(Spacer(1, 2*mm))

story.append(pain_box(
    'Her DM = ortalama 6.000 TL potansiyel. <b>59 x 6.000 = 354.000 TL</b> havaya u\u00e7mu\u015f.',
    W
))
story.append(Spacer(1, 2*mm))

story.append(Paragraph("<b>Invekto sonras\u0131:</b>", styles["body_bold"]))
story.append(data_table(
    ["Metrik", "\u00d6ncesi", "Sonras\u0131"],
    [
        ["Cevaplanan", "68 / 127", "<b>127 / 127</b>"],
        ["Cevap s\u00fcresi", "2-6 saat", "<b>15 saniye</b>"],
        ["Randevu", "18", "<b>31</b>"],
        ["Tedavi geliri", "18 x 6K = 108K", "<b>31 x 6K = 186K TL</b>"],
    ],
    W * 0.85, [0.30, 0.35, 0.35]
))
story.append(Spacer(1, 2*mm))

story.append(aha_box([
    Paragraph(
        '\u201cBu rakamlar\u0131 g\u00f6rene kadar ka\u00e7an hastalar\u0131 bilmiyordum. Bilmedi\u011fin \u015feyi \u00f6l\u00e7emezsin, \u00f6l\u00e7emedi\u011fin \u015feyi d\u00fczeltemezsin.\u201d',
        styles["aha_quote"]
    ),
], W))

story.append(PageBreak())


# ============================================================
# Senaryo 56 — Kontrendikasyon
# ============================================================

story.append(Paragraph("Senaryo 56 \u2014 Uygunluk / Kontrendikasyon Sorular\u0131", styles["h2"]))

story.append(meta_table([
    ("Tipik Soru", "\u201cHamileyken botox yapt\u0131rabilir miyim?\u201d"),
    ("Risk", "Yanl\u0131\u015f cevap = ciddi sa\u011fl\u0131k riski"),
    ("Guardrail", "AI kesinlikle t\u0131bbi tavsiye vermez"),
], W))
story.append(Spacer(1, 4*mm))

story.append(Paragraph("Ac\u0131", styles["h4"]))
story.append(pain_box(
    '"Kan suland\u0131r\u0131c\u0131 kullan\u0131yorum, botox yapt\u0131rabilir miyim?" \u2014 bu soruya yanl\u0131\u015f cevap hayati tehlike yaratabilir.',
    W
))
story.append(Spacer(1, 3*mm))

story.append(Paragraph("Invekto ile", styles["h4"]))
bullets5 = [
    "<b>KR\u0130T\u0130K GUARDRAIL:</b> AI kesinlikle t\u0131bbi tavsiye vermez",
    'Cevap: <i>"Bu soruyu doktorumuzla de\u011ferlendirmemiz gerekiyor. Muayene randevusu olu\u015ftural\u0131m m\u0131?"</i>',
    "Genel g\u00fcvenlik bilgisi verilebilir ama kesin yorum yap\u0131lmaz",
    "Handoff: AI \u2192 doktora y\u00f6nlendirme",
]
for b in bullets5:
    story.append(Paragraph(f"\u2022  {b}", styles["bullet"]))
story.append(Spacer(1, 3*mm))

story.append(aha_box([
    Paragraph("\U0001f4a1  VAY BE ANI", styles["aha_title"]),
    Paragraph(
        'Bir hasta yazd\u0131: <i>"Kan suland\u0131r\u0131c\u0131 kullan\u0131yorum, botox yapt\u0131rabilir miyim?"</i>',
        styles["aha_text"]
    ),
    Paragraph(
        'Tehlikeli soru. Yanl\u0131\u015f cevap = ciddi sa\u011fl\u0131k riski. Invekto\u2019nun guardrail\u2019i devreye girdi:',
        styles["aha_text"]
    ),
    Paragraph(
        '\u201cBu \u00f6nemli bir sa\u011fl\u0131k sorusu ve kesinlikle doktorumuzun de\u011ferlendirmesi gerekiyor. Size \u00f6zel bir \u00f6n g\u00f6r\u00fc\u015fme randevusu olu\u015ftural\u0131m m\u0131?\u201d',
        styles["aha_quote"]
    ),
    Paragraph(
        'Hasta randevu ald\u0131. Dr. Selin muayenede:',
        styles["aha_text"]
    ),
    Paragraph(
        '\u201c\u0130yi ki do\u011frudan botox randevusu vermemi\u015fler. Bu hastada \u00f6nce kardiyologla kons\u00fcltasyon gerekiyordu.\u201d',
        styles["aha_quote"]
    ),
    Paragraph(
        'Hasta tedavi sonras\u0131:',
        styles["aha_text"]
    ),
    Paragraph(
        '\u201cBa\u015fka klinikte direkt yapt\u0131racaklard\u0131, burada \u00f6nce sa\u011fl\u0131\u011f\u0131m\u0131 d\u00fc\u015f\u00fcnd\u00fcler.\u201d',
        styles["aha_quote"]
    ),
    Paragraph(
        'Bu g\u00fcven, parayla sat\u0131n al\u0131namaz.',
        styles["aha_bold"]
    ),
    Paragraph(
        'O hasta <b>2 y\u0131lda 85.000 TL\u2019lik</b> i\u015flem yapt\u0131rd\u0131 \u2014 \u00e7\u00fcnk\u00fc "burada beni koruyorlar" hissetti.',
        styles["aha_result"]
    ),
], W))

story.append(PageBreak())


# ============================================================
# Senaryo 63 — Click-to-WhatsApp
# ============================================================

story.append(Paragraph("Senaryo 63 \u2014 Click-to-WhatsApp Reklam Lead\u2019i", styles["h2"]))

story.append(meta_table([
    ("Kanal", "Instagram Reklam \u2192 WhatsApp"),
    ("\u00d6nem", "Reklama para harcanm\u0131\u015f \u2014 her lead de\u011ferli"),
    ("Fark", "ROI \u00f6l\u00e7\u00fclebilir hale gelir"),
], W))
story.append(Spacer(1, 4*mm))

story.append(Paragraph("Ac\u0131", styles["h4"]))
story.append(pain_box(
    'Instagram reklam\u0131nda "\u015eimdi Yaz" butonu \u2192 WhatsApp\u2019a d\u00fc\u015f\u00fcyor. Bu lead\u2019in de\u011feri y\u00fcksek \u00e7\u00fcnk\u00fc <b>reklama para harcanm\u0131\u015f.</b> Ama "ka\u00e7 hasta geldi?" sorusuna kimse net cevap veremiyor.',
    W
))
story.append(Spacer(1, 3*mm))

story.append(Paragraph("Invekto ile", styles["h4"]))
bullets6 = [
    "<b>UTM tracking</b> ile reklam kayna\u011f\u0131 kaydedilir",
    "AI h\u0131zl\u0131 kar\u015f\u0131lama + niyete uygun cevap",
    'Dashboard: <i>"Bu kampanyadan 45 lead, 12 randevu, 8 tedavi"</i>',
]
for b in bullets6:
    story.append(Paragraph(f"\u2022  {b}", styles["bullet"]))
story.append(Spacer(1, 3*mm))

story.append(aha_box([
    Paragraph("\U0001f4a1  VAY BE ANI", styles["aha_title"]),
    Paragraph(
        'Dr. Selin ayda <b>25.000 TL</b> Instagram reklam\u0131 veriyordu. <i>"Ka\u00e7 hasta geldi?"</i> diye sordu\u011funda \u2014 kimse net cevap veremiyordu.',
        styles["aha_text"]
    ),
    Paragraph(
        '<b>Invekto ile</b> her Click-to-WhatsApp lead\u2019i UTM ile etiketlendi. Ay sonu raporu:',
        styles["aha_text"]
    ),
], W))
story.append(Spacer(1, 2*mm))

story.append(data_table(
    ["Kampanya", "Lead", "Randevu", "Tedavi", "Gelir"],
    [
        ["<b>A</b>", "45", "12", "8", "<b>64.000 TL</b>"],
        ["<b>B</b>", "62", "7", "3", "<b>18.000 TL</b>"],
    ],
    W * 0.9, [0.20, 0.15, 0.20, 0.15, 0.30]
))
story.append(Spacer(1, 2*mm))

story.append(aha_box([
    Paragraph(
        'Hemen g\u00f6r\u00fcn\u00fcyor: Kampanya A d\u00fc\u015f\u00fck maliyetli ve y\u00fcksek d\u00f6n\u00fc\u015f\u00fcml\u00fc. Kampanya B pahal\u0131 lead getiriyor ama d\u00f6nm\u00fcyor.',
        styles["aha_text"]
    ),
    Paragraph(
        'Dr. Selin b\u00fct\u00e7eyi yeniden da\u011f\u0131tt\u0131 \u2014 Kampanya A\u2019ya a\u011f\u0131rl\u0131k verdi.',
        styles["aha_text"]
    ),
    Paragraph(
        'Sonraki ay: ayn\u0131 25.000 TL b\u00fct\u00e7eyle <b>%40 daha fazla hasta.</b>',
        styles["aha_result"]
    ),
    Paragraph(
        '\u201cArt\u0131k karanl\u0131kta reklam vermiyorum, her kuru\u015fun nereye gitti\u011fini biliyorum.\u201d',
        styles["aha_quote"]
    ),
], W))


# ============================================================
# BUILD
# ============================================================

doc.build(story, onFirstPage=add_page_number, onLaterPages=add_page_number)
print(f"PDF created: {output_path}")
