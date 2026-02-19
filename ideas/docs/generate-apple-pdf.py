"""
Apple-style PDF v2 — Compact: 1 scenario per page (overflow to next, but each starts fresh)
"""

import os
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
BLACK       = HexColor("#1d1d1f")
GRAY        = HexColor("#86868b")
GRAY_LIGHT  = HexColor("#a1a1a6")
BLUE        = HexColor("#0071e3")
GREEN       = HexColor("#30d158")
AMBER       = HexColor("#ff9f0a")
SURFACE     = HexColor("#f5f5f7")
DIVIDER     = HexColor("#d2d2d7")
DANGER      = HexColor("#ff3b30")

# ─── Fonts ──────────────────────────────────────────────
for p, n in [
    ("C:/Windows/Fonts/segoeui.ttf",   "SF"),
    ("C:/Windows/Fonts/segoeuib.ttf",  "SF-Bold"),
    ("C:/Windows/Fonts/segoeuii.ttf",  "SF-Italic"),
    ("C:/Windows/Fonts/segoeuiz.ttf",  "SF-BoldItalic"),
    ("C:/Windows/Fonts/segoeuil.ttf",  "SF-Light"),
    ("C:/Windows/Fonts/seguisb.ttf",   "SF-Semi"),
]:
    if os.path.exists(p):
        pdfmetrics.registerFont(TTFont(n, p))

F  = "SF"
FB = "SF-Bold"
FI = "SF-Italic"
FL = "SF-Light" if os.path.exists("C:/Windows/Fonts/segoeuil.ttf") else F
FS = "SF-Semi"  if os.path.exists("C:/Windows/Fonts/seguisb.ttf")  else FB

PAGE_W, PAGE_H = A4
ML, MR, MT, MB = 28*mm, 28*mm, 22*mm, 20*mm
W = PAGE_W - ML - MR


# ─── Custom Flowables ──────────────────────────────────

class GradientBar(Flowable):
    def __init__(self, width, height=2.5, c1=BLUE, c2=GREEN):
        Flowable.__init__(self)
        self.width = width
        self.height = height
        self.c1, self.c2 = c1, c2
    def wrap(self, aw, ah): return (self.width, self.height)
    def draw(self):
        for i in range(50):
            r = self.c1.red   + (self.c2.red   - self.c1.red)   * i/50
            g = self.c1.green + (self.c2.green - self.c1.green) * i/50
            b = self.c1.blue  + (self.c2.blue  - self.c1.blue)  * i/50
            sw = self.width/50
            self.canv.setFillColor(Color(r, g, b))
            self.canv.rect(i*sw, 0, sw+0.5, self.height, fill=1, stroke=0)


class AccentDot(Flowable):
    """Tiny amber dot before aha section."""
    def __init__(self, color=AMBER, r=3):
        Flowable.__init__(self)
        self.color, self.r = color, r
    def wrap(self, aw, ah): return (self.r*2, self.r*2 + 1*mm)
    def draw(self):
        self.canv.setFillColor(self.color)
        self.canv.circle(self.r, self.r + 0.5*mm, self.r, fill=1, stroke=0)


# ─── Styles (compact) ─────────────────────────────────

# Cover
st_hero      = ParagraphStyle("hero",    fontName=FB, fontSize=34, leading=40, textColor=BLACK, spaceAfter=3*mm)
st_hero_sub  = ParagraphStyle("hsub",    fontName=FL, fontSize=14, leading=20, textColor=GRAY,  spaceAfter=6*mm)

# Scenario page
st_code      = ParagraphStyle("code",    fontName=FS, fontSize=9,  leading=12, textColor=BLUE,  spaceAfter=1*mm)
st_title     = ParagraphStyle("title",   fontName=FB, fontSize=20, leading=25, textColor=BLACK, spaceAfter=3*mm)
st_sub       = ParagraphStyle("sub",     fontName=FS, fontSize=11, leading=15, textColor=BLACK, spaceBefore=4*mm, spaceAfter=2*mm)
st_body      = ParagraphStyle("body",    fontName=F,  fontSize=10, leading=15, textColor=BLACK, spaceAfter=2.5*mm)
st_body_g    = ParagraphStyle("bodyg",   fontName=F,  fontSize=10, leading=15, textColor=GRAY,  spaceAfter=2.5*mm)
st_bullet    = ParagraphStyle("bullet",  fontName=F,  fontSize=10, leading=15, textColor=BLACK, leftIndent=7*mm, spaceAfter=1.5*mm)
st_section   = ParagraphStyle("sect",    fontName=FS, fontSize=9,  leading=12, textColor=BLUE,  spaceAfter=1*mm)

# Metrics
st_metric_n  = ParagraphStyle("mn", fontName=FB, fontSize=22, leading=26, textColor=BLUE)
st_metric_l  = ParagraphStyle("ml", fontName=F,  fontSize=8,  leading=11, textColor=GRAY)

# Aha (compact pull-quote)
st_aha_label = ParagraphStyle("al", fontName=FS, fontSize=8,  leading=11, textColor=AMBER, spaceAfter=1*mm)
st_aha_scene = ParagraphStyle("as", fontName=F,  fontSize=9.5,leading=14, textColor=GRAY,  spaceAfter=2*mm)
st_aha_quote = ParagraphStyle("aq", fontName=FI, fontSize=12, leading=17, textColor=BLACK, spaceAfter=2*mm)
st_aha_result= ParagraphStyle("ar", fontName=FS, fontSize=11, leading=15, textColor=BLUE,  spaceAfter=1*mm)
st_aha_attr  = ParagraphStyle("aa", fontName=F,  fontSize=8,  leading=11, textColor=GRAY_LIGHT)

# Tables
st_th = ParagraphStyle("th", fontName=FS, fontSize=8,  leading=11, textColor=GRAY)
st_tc = ParagraphStyle("tc", fontName=F,  fontSize=9.5,leading=14, textColor=BLACK)
st_tcb= ParagraphStyle("tcb",fontName=FS, fontSize=9.5,leading=14, textColor=BLACK)

# Pain
st_pain = ParagraphStyle("pain", fontName=FI, fontSize=10, leading=15, textColor=GRAY)


# ─── Builders ─────────────────────────────────────────

def thin_rule():
    return HRFlowable(width="100%", thickness=0.25, color=DIVIDER, spaceBefore=3*mm, spaceAfter=3*mm)

def metrics(items):
    """Horizontal metric row — compact."""
    n = len(items)
    cw = [W/n]*n
    cells = []
    for num, label in items:
        inner = Table([
            [Paragraph(num, st_metric_n)],
            [Paragraph(label, st_metric_l)],
        ], colWidths=[cw[0]-6*mm])
        inner.setStyle(TableStyle([
            ("VALIGN",(0,0),(-1,-1),"TOP"),
            ("LEFTPADDING",(0,0),(-1,-1),0),
            ("TOPPADDING",(0,0),(-1,-1),0),
            ("BOTTOMPADDING",(0,0),(-1,-1),0),
        ]))
        cells.append(inner)
    t = Table([cells], colWidths=cw)
    t.setStyle(TableStyle([
        ("VALIGN",(0,0),(-1,-1),"TOP"),
        ("LEFTPADDING",(0,0),(-1,-1),0),
        ("RIGHTPADDING",(0,0),(-1,-1),8),
    ]))
    return t

def apple_table(headers, rows, ratios=None):
    n = len(headers)
    ratios = ratios or [1.0/n]*n
    cw = [W*r for r in ratios]
    hrow = [Paragraph(h, st_th) for h in headers]
    drows = [[Paragraph(c, st_tcb if j==0 else st_tc) for j,c in enumerate(row)] for row in rows]
    t = Table([hrow]+drows, colWidths=cw)
    cmds = [
        ("VALIGN",(0,0),(-1,-1),"TOP"),
        ("TOPPADDING",(0,0),(-1,-1),5),
        ("BOTTOMPADDING",(0,0),(-1,-1),5),
        ("LEFTPADDING",(0,0),(-1,-1),0),
        ("RIGHTPADDING",(0,0),(-1,-1),4),
        ("LINEBELOW",(0,0),(-1,0),0.5,DIVIDER),
    ]
    for i in range(1,len(drows)):
        cmds.append(("LINEBELOW",(0,i),(-1,i),0.25,HexColor("#e8e8ed")))
    t.setStyle(TableStyle(cmds))
    return t

def pain(text):
    c = [[Paragraph(text, st_pain)]]
    t = Table(c, colWidths=[W-4*mm])
    t.setStyle(TableStyle([
        ("LEFTPADDING",(0,0),(-1,-1),10),
        ("TOPPADDING",(0,0),(-1,-1),4),
        ("BOTTOMPADDING",(0,0),(-1,-1),4),
        ("LINEBEFOREWIDTH",(0,0),(0,-1),2),
        ("LINEBEFORECOLOR",(0,0),(0,-1),DANGER),
    ]))
    return t

def aha(scene, quote, result, attr=None):
    """Compact aha block with amber left border."""
    inner_parts = []
    inner_parts.append([Paragraph("VAY BE ANI", st_aha_label)])
    inner_parts.append([Paragraph(scene, st_aha_scene)])
    inner_parts.append([Paragraph(f"\u201c{quote}\u201d", st_aha_quote)])
    inner_parts.append([Paragraph(result, st_aha_result)])
    if attr:
        inner_parts.append([Paragraph(f"\u2014 {attr}", st_aha_attr)])

    t = Table(inner_parts, colWidths=[W - 14*mm])
    t.setStyle(TableStyle([
        ("LEFTPADDING",(0,0),(-1,-1),10),
        ("RIGHTPADDING",(0,0),(-1,-1),4),
        ("TOPPADDING",(0,0),(0,0),8),
        ("BOTTOMPADDING",(0,-1),(-1,-1),8),
        ("TOPPADDING",(0,1),(-1,-1),1),
        ("BOTTOMPADDING",(0,0),(-1,-2),1),
        ("LINEBEFOREWIDTH",(0,0),(0,-1),2.5),
        ("LINEBEFORECOLOR",(0,0),(0,-1),AMBER),
        ("BACKGROUND",(0,0),(-1,-1),HexColor("#fffbf0")),
    ]))
    return t

def steps(items):
    """Numbered flow steps — compact."""
    els = []
    for i, step in enumerate(items):
        row = Table([
            [Paragraph(str(i+1), ParagraphStyle("sn", fontName=FS, fontSize=9, leading=13, textColor=BLUE, alignment=TA_CENTER)),
             Paragraph(step, ParagraphStyle("st", fontName=F, fontSize=9.5, leading=14, textColor=BLACK))]
        ], colWidths=[8*mm, W-14*mm])
        row.setStyle(TableStyle([
            ("VALIGN",(0,0),(-1,-1),"TOP"),
            ("LEFTPADDING",(0,0),(0,0),0),
            ("TOPPADDING",(0,0),(-1,-1),2),
            ("BOTTOMPADDING",(0,0),(-1,-1),2),
        ]))
        els.append(row)
    return els


# ─── Footer ──────────────────────────────────────────

def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont(F, 8)
    canvas.setFillColor(GRAY_LIGHT)
    canvas.drawCentredString(PAGE_W/2, 12*mm, str(canvas.getPageNumber()))
    canvas.setStrokeColor(DIVIDER)
    canvas.setLineWidth(0.25)
    canvas.line(ML, 17*mm, PAGE_W-MR, 17*mm)
    canvas.restoreState()


# ═══════════════════════════════════════════════════════
# BUILD
# ═══════════════════════════════════════════════════════

output = r"c:\CRMs\InvektoServices\ideas\docs\apple-style-ornek.pdf"
doc = SimpleDocTemplate(output, pagesize=A4,
    leftMargin=ML, rightMargin=MR, topMargin=MT, bottomMargin=MB)

story = []
SP = Spacer  # shorthand


# ── COVER ─────────────────────────────────────────────

story.append(SP(1, 35*mm))
story.append(GradientBar(W, 3))
story.append(SP(1, 10*mm))
story.append(Paragraph("Sa\u011fl\u0131k\nSenaryolar\u0131.", st_hero))
story.append(SP(1, 4*mm))
story.append(Paragraph(
    "Di\u015f klinikleri ve estetik merkezler i\u00e7in \u2014\n"
    "mesajdan randevuya, randevudan sadakate.",
    st_hero_sub))
story.append(SP(1, 10*mm))
story.append(metrics([
    ("~690K", "TL/ay potansiyel etki"),
    ("%60", "no-show azalma"),
    ("8sn", "ortalama cevap s\u00fcresi"),
]))
story.append(SP(1, 30*mm))
story.append(Paragraph("invekto", ParagraphStyle("b", fontName=FS, fontSize=11, textColor=GRAY_LIGHT)))

story.append(PageBreak())


# ── PERSONAS ──────────────────────────────────────────

story.append(SP(1, 4*mm))
story.append(Paragraph("PERSONALAR", st_section))
story.append(Paragraph("Sahne arkas\u0131ndaki ger\u00e7ek insanlar.", st_title))
story.append(SP(1, 3*mm))
story.append(apple_table(
    ["", "Rol", "Her g\u00fcn ya\u015fad\u0131\u011f\u0131"],
    [
        ["Dr. Burak", "Di\u015f klini\u011fi sahibi, 3 \u00fcnit",       "Koltukta hasta varken telefon kontrol edemiyor"],
        ["Elif",      "\u00d6n b\u00fcro sorumlusu",                     "Telefon + WhatsApp + y\u00fcz y\u00fcze ayn\u0131 anda"],
        ["Dr. Selin", "Estetik klinik sahibi, 5 doktor",                "Instagram lead\u2019lerinin %40\u2019\u0131 d\u00f6nm\u00fcyor"],
        ["Zeynep",    "Operasyon sorumlusu",                             "3 kanaldan mesaj, doktor onay\u0131 bekliyor"],
    ],
    [0.14, 0.28, 0.58]))

story.append(PageBreak())


# ── S6: Fiyat → Randevu ──────────────────────────────

story.append(SP(1, 2*mm))
story.append(Paragraph("S6", st_code))
story.append(Paragraph("Fiyat sorusunu randevuya \u00e7evir.", st_title))

story.append(metrics([
    ("~60K", "TL/ay potansiyel"),
    ("10\u00d7", "h\u0131zl\u0131 cevap = 10\u00d7 randevu"),
    ("5dk", "kritik pencere"),
]))
story.append(SP(1, 2*mm))
story.append(thin_rule())

story.append(pain(
    "\u201c\u0130mplant ne kadar?\u201d \u2014 5 dakikada cevap veren klinik kazan\u0131r. "
    "1 saat sonra cevap veren kaybeder."
))
story.append(SP(1, 3*mm))

story.append(Paragraph("Nas\u0131l \u00e7al\u0131\u015f\u0131r", st_sub))
for el in steps([
    "Hasta WhatsApp / Instagram\u2019dan fiyat sorusu sorar",
    'AI niyeti anlar \u2192 "fiyat + tedavi talebi"',
    "Fiyat <b>aral\u0131\u011f\u0131</b> verir \u2014 kesin fiyat de\u011fil, muayene gerekli",
    'Randevu teklifi: "Bu hafta m\u00fcsait saatlerimiz..."',
    "Hasta onaylarsa \u2192 hat\u0131rlatma zinciri ba\u015flar (R\u22121g\u00fcn, R\u22122saat)",
]):
    story.append(el)

story.append(SP(1, 4*mm))
story.append(aha(
    "Saat 22:30. Elif \u00f6n b\u00fcroyu kapat\u0131p eve gitmi\u015f. "
    "Bir hasta Instagram\u2019dan yazd\u0131: \u201c\u0130mplant ne kadar?\u201d "
    "Normalde sabah 09:00\u2019da g\u00f6r\u00fclecekti \u2014 hasta 4 klini\u011fe daha yazm\u0131\u015ft\u0131.",

    "Bu hasta biz uyurken gelmi\u015f!",

    "45.000 TL \u2014 8 saniye ile 11 saat aras\u0131ndaki fark.",
    "Elif"
))

story.append(PageBreak())


# ── S7: No-Show Önleme ───────────────────────────────

story.append(SP(1, 2*mm))
story.append(Paragraph("S7", st_code))
story.append(Paragraph("No-show\u2019u %60 azalt.", st_title))

story.append(metrics([
    ("~135K", "TL/ay potansiyel"),
    ("%15\u201320", "no-show oran\u0131 (bug\u00fcn)"),
    ("%60", "azalma (Invekto ile)"),
]))
story.append(SP(1, 2*mm))
story.append(thin_rule())

story.append(Paragraph("Hat\u0131rlatma zinciri", st_sub))
story.append(apple_table(
    ["Zamanlama", "Mesaj"],
    [
        ["R \u2212 1 g\u00fcn",   "Yar\u0131n saat 14:00 randevunuz var. Onayl\u0131yor musunuz?"],
        ["R \u2212 2 saat",       "Randevunuz 2 saat sonra. Klinik adresi: ..."],
        ["\u0130ptal gelirse",    "Yeni tarih \u00f6nerilir + bekleme listesinden doldurulur"],
        ["Hasta geldi",           "Tedavi sonras\u0131 takip zinciri ba\u015flar"],
    ],
    [0.20, 0.80]))

story.append(SP(1, 4*mm))
story.append(aha(
    "Dr. Burak ay sonu raporuna bakt\u0131: ge\u00e7en ay 47 ka\u00e7an randevu, "
    "bu ay 19. +28 hasta geldi. 28 \u00d7 3.500 TL = 98.000 TL. "
    "Bekleme listesindeki Ay\u015fe Han\u0131m\u2019a \u201cyer a\u00e7\u0131ld\u0131\u201d mesaj\u0131 gitmi\u015f, "
    "3 dakikada onaylam\u0131\u015f, 22.000 TL\u2019lik implant tedavisine ba\u015flam\u0131\u015f.",

    "Bo\u015f koltuk hem dolmu\u015f \u2014 hem de en de\u011ferli hastayla dolmu\u015f.",

    "98.000 TL \u2014 bir tek hat\u0131rlatma mesaj\u0131 y\u00fcz\u00fcnden.",
    "Dr. Burak"
))

story.append(PageBreak())


# ── S8: Tedavi Sonrası Takip ─────────────────────────

story.append(SP(1, 2*mm))
story.append(Paragraph("S8", st_code))
story.append(Paragraph("Tedavi bitti.\nBa\u011f kopmas\u0131n.", st_title))

story.append(metrics([
    ("~90K", "TL/ay potansiyel"),
    ("T+1\u2192T+30", "otomatik takip zinciri"),
    ("5\u2605", "Google yorum hedefi"),
]))
story.append(SP(1, 2*mm))
story.append(thin_rule())

story.append(Paragraph("Takip zinciri", st_sub))
story.append(apple_table(
    ["Zamanlama", "Mesaj"],
    [
        ["T + 1 g\u00fcn", "Tedaviniz nas\u0131l gidiyor? \u015ei\u015flik veya a\u011fr\u0131 varsa bilgi verebiliriz"],
        ["T + 3 g\u00fcn", "Herhangi bir \u015fikayetiniz var m\u0131?"],
        ["T + 7 g\u00fcn", "Kontrol\u00fcn\u00fcz yakla\u015f\u0131yor. Randevu olu\u015ftural\u0131m m\u0131?"],
        ["T + 30 g\u00fcn", "Memnun kald\u0131n\u0131z m\u0131? Bizi Google\u2019da de\u011ferlendirin"],
    ],
    [0.18, 0.82]))

story.append(SP(1, 4*mm))
story.append(aha(
    "Mehmet Bey implant yapt\u0131rd\u0131. T+1 mesaj\u0131 geldi, \u201cbiraz a\u011fr\u0131 var\u201d yazd\u0131. "
    "AI bilgi verdi: \u201c\u0130lk 48 saatte normal, so\u011fuk kompres uygulay\u0131n.\u201d "
    "T+7\u2019de kontrol randevusu ald\u0131. T+30\u2019da Google\u2019a 5 y\u0131ld\u0131z verdi.",

    "Tedaviden sonra bile benimle ilgilendiler, hi\u00e7bir klinikte b\u00f6yle bir \u015fey ya\u015famad\u0131m.",

    "120.000 TL \u2014 tek bir takip mesaj\u0131 zincirinin toplam de\u011feri.",
    "Mehmet Bey, e\u015fi 2 ay sonra tedaviye ba\u015flad\u0131"
))

story.append(PageBreak())


# ── Senaryo 27: Instagram DM Foto ────────────────────

story.append(SP(1, 2*mm))
story.append(Paragraph("SENARYO 27", st_code))
story.append(Paragraph("Foto atsam fiyat verir misiniz?", st_title))

story.append(pain(
    "Hasta Instagram\u2019dan di\u015f foto\u011fraf\u0131 g\u00f6nderiyor, \u201cfiyat ne?\u201d diyor. "
    "Sa\u011fl\u0131k sekt\u00f6r\u00fcn\u00fcn en de\u011ferli lead\u2019i \u2014 hasta tedaviye haz\u0131r."
))
story.append(SP(1, 3*mm))

story.append(Paragraph("Invekto ile", st_sub))
for b in [
    "IG DM tek ekrandan y\u00f6netilir",
    "AI foto\u011fraf\u0131 inceler de\u011fil \u2014 ama <b>niyeti anlar:</b> \u201cfiyat + tedavi talebi\u201d",
    'Cevap: <i>"Foto\u011fraf\u0131n\u0131z i\u00e7in te\u015fekk\u00fcrler! Tedavi plan\u0131 muayenede belirlenir. M\u00fcsaitiz, randevu olu\u015ftural\u0131m m\u0131?"</i>',
    "<b>Guardrail:</b> AI kesin fiyat vermez (sa\u011fl\u0131k riskli alan)",
]:
    story.append(Paragraph(f"\u2022\u2003{b}", st_bullet))

story.append(SP(1, 4*mm))
story.append(aha(
    "Pazar ak\u015fam\u0131 21:00. Elif evde. Bir hasta Instagram DM\u2019den di\u015f "
    "foto\u011fraf\u0131 att\u0131: \u201cBu di\u015f kurtar\u0131labilir mi?\u201d "
    "Invekto 12 saniyede cevap verdi. Hasta Pazartesi 10:00\u2019a geldi.",

    "5 klini\u011fe yazd\u0131m, sadece siz cevap verdiniz.",

    "35.000 TL \u2014 bir Pazar gecesi, 12 saniye.",
    "Hasta"
))

story.append(PageBreak())


# ── Senaryo 30: Gece Acil ────────────────────────────

story.append(SP(1, 2*mm))
story.append(Paragraph("SENARYO 30", st_code))
story.append(Paragraph("Gece 2\u2019de biri var.", st_title))

story.append(pain(
    "Gece 2\u2019de hasta yaz\u0131yor: \u201cDi\u015fim \u00e7ok a\u011fr\u0131yor, dayanam\u0131yorum!\u201d "
    "Bu mesaj\u0131 sabaha erteleyemezsiniz."
))
story.append(SP(1, 3*mm))

story.append(Paragraph("Invekto ile", st_sub))
for b in [
    "AI <b>acil intent</b> tespit eder \u2192 y\u00fcksek \u00f6ncelik",
    "Ge\u00e7ici rahatlama bilgisi: so\u011fuk kompres, pozisyon \u00f6nerisi",
    "Sabah ilk randevu otomatik ayr\u0131l\u0131r",
    "Tehlike belirtileri \u2192 112 y\u00f6nlendirmesi",
    "<b>Guardrail:</b> ila\u00e7 dozaj\u0131, tan\u0131 \u2014 asla",
]:
    story.append(Paragraph(f"\u2022\u2003{b}", st_bullet))

story.append(SP(1, 4*mm))
story.append(aha(
    "Gece 02:15. Ay\u015fe Han\u0131m uyand\u0131, sol \u00e7enesinde dayan\u0131lmaz a\u011fr\u0131. "
    "Google\u2019a yazd\u0131, korkun\u00e7 sonu\u00e7lar \u00e7\u0131kt\u0131. "
    "Klini\u011fin WhatsApp\u2019\u0131na yazd\u0131, 5 saniyede cevap geldi: "
    "bilgi + sabah randevusu + acil y\u00f6nlendirme.",

    "Gece 2\u2019de bile cevap vermeniz... ben ba\u015fka klini\u011fe gitmem art\u0131k.",

    "78.000 TL \u2014 6 ay sonra t\u00fcm ailesini getirdi.",
    "Ay\u015fe Han\u0131m"
))

story.append(PageBreak())


# ── Senaryo 51: Instagram DM Botox ───────────────────

story.append(SP(1, 2*mm))
story.append(Paragraph("SENARYO 51", st_code))
story.append(Paragraph("Her cevaps\u0131z DM, 6.000 TL kay\u0131p.", st_title))

story.append(pain(
    "Dr. Selin Instagram analiti\u011fine bakt\u0131: 127 DM, 68 cevaplanan, "
    "59 cevaps\u0131z. 59 \u00d7 6.000 = 354.000 TL havaya u\u00e7mu\u015f."
))
story.append(SP(1, 3*mm))

story.append(Paragraph("\u00d6NCES\u0130 / SONRASI", st_section))
story.append(SP(1, 2*mm))
story.append(apple_table(
    ["", "\u00d6ncesi", "Invekto ile"],
    [
        ["Cevaplanan",  "68 / 127",      "127 / 127"],
        ["Cevap s\u00fcresi","2\u20136 saat",    "15 saniye"],
        ["Randevu",     "18",             "31"],
        ["Tedavi geliri","108.000 TL",    "186.000 TL"],
    ],
    [0.25, 0.375, 0.375]))

story.append(SP(1, 4*mm))
story.append(aha(
    "Ayn\u0131 b\u00fct\u00e7e, ayn\u0131 ekip, ayn\u0131 klinik. "
    "Tek de\u011fi\u015fen: her DM\u2019e 15 saniyede cevap.",

    "Bu rakamlar\u0131 g\u00f6rene kadar ka\u00e7an hastalar\u0131 bilmiyordum. "
    "Bilmedi\u011fin \u015feyi \u00f6l\u00e7emezsin, \u00f6l\u00e7emedi\u011fin \u015feyi d\u00fczeltemezsin.",

    "+78.000 TL/ay \u2014 s\u0131f\u0131r ek maliyet.",
    "Dr. Selin"
))

story.append(PageBreak())


# ── Senaryo 56: Guardrail ────────────────────────────

story.append(SP(1, 2*mm))
story.append(Paragraph("SENARYO 56", st_code))
story.append(Paragraph("AI\u2019\u0131n en \u00f6nemli \u00f6zelli\u011fi:\nhay\u0131r demek.", st_title))

story.append(pain(
    "\u201cKan suland\u0131r\u0131c\u0131 kullan\u0131yorum, botox yapt\u0131rabilir miyim?\u201d \u2014 "
    "yanl\u0131\u015f cevap hayati tehlike yaratabilir."
))
story.append(SP(1, 3*mm))

story.append(Paragraph(
    "Invekto\u2019nun AI\u2019\u0131 t\u0131bbi tavsiye <b>vermez.</b> "
    "Kontrendikasyon sorusu geldi\u011finde hastay\u0131 doktora y\u00f6nlendirir.",
    st_body))

story.append(SP(1, 1*mm))
# AI message bubble
ai_msg = [[Paragraph(
    '\u201cBu \u00f6nemli bir sa\u011fl\u0131k sorusu ve kesinlikle doktorumuzun '
    'de\u011ferlendirmesi gerekiyor. Size \u00f6zel bir \u00f6n g\u00f6r\u00fc\u015fme '
    'randevusu olu\u015ftural\u0131m m\u0131?\u201d',
    ParagraphStyle("ai", fontName=FI, fontSize=10, leading=15, textColor=BLUE))]]
ai_t = Table(ai_msg, colWidths=[W-20*mm])
ai_t.setStyle(TableStyle([
    ("BACKGROUND",(0,0),(-1,-1),HexColor("#f0f7ff")),
    ("LEFTPADDING",(0,0),(-1,-1),12),("RIGHTPADDING",(0,0),(-1,-1),12),
    ("TOPPADDING",(0,0),(-1,-1),8),("BOTTOMPADDING",(0,0),(-1,-1),8),
    ("ROUNDEDCORNERS",[6,6,6,6]),
]))
story.append(ai_t)

story.append(SP(1, 4*mm))
story.append(aha(
    "Hasta kan suland\u0131r\u0131c\u0131 kullan\u0131yordu. Guardrail devreye girdi, "
    "doktora y\u00f6nlendirildi. Dr. Selin: \u201c\u0130yi ki direkt botox randevusu vermemi\u015fler. "
    "\u00d6nce kardiyologla kons\u00fcltasyon gerekiyordu.\u201d",

    "Ba\u015fka klinikte direkt yapt\u0131racaklard\u0131, burada \u00f6nce sa\u011fl\u0131\u011f\u0131m\u0131 d\u00fc\u015f\u00fcnd\u00fcler.",

    "85.000 TL \u2014 2 y\u0131lda, \u00e7\u00fcnk\u00fc \u201cburada beni koruyorlar\u201d hissetti.",
    "Hasta"
))

story.append(PageBreak())


# ── Senaryo 63: Click-to-WhatsApp ────────────────────

story.append(SP(1, 2*mm))
story.append(Paragraph("SENARYO 63", st_code))
story.append(Paragraph("Her kuru\u015fun nereye gitti\u011fini bil.", st_title))

story.append(pain(
    "Dr. Selin ayda 25.000 TL Instagram reklam\u0131 veriyor. "
    "\u201cKa\u00e7 hasta geldi?\u201d diye sorunca \u2014 kimse cevap veremiyor."
))
story.append(SP(1, 3*mm))

story.append(Paragraph("Invekto ile", st_sub))
for b in [
    "<b>UTM tracking</b> ile her lead\u2019in reklam kayna\u011f\u0131 kaydedilir",
    "AI h\u0131zl\u0131 kar\u015f\u0131lama + niyete uygun cevap",
    'Dashboard: "Bu kampanyadan 45 lead, 12 randevu, 8 tedavi"',
]:
    story.append(Paragraph(f"\u2022\u2003{b}", st_bullet))

story.append(SP(1, 2*mm))

story.append(Paragraph("Kampanya kar\u015f\u0131la\u015ft\u0131rmas\u0131", st_sub))
story.append(apple_table(
    ["Kampanya", "Lead", "Randevu", "Tedavi", "Gelir"],
    [
        ["A", "45", "12", "8", "64.000 TL"],
        ["B", "62", "7",  "3", "18.000 TL"],
    ],
    [0.18, 0.15, 0.18, 0.15, 0.34]))

story.append(SP(1, 4*mm))
story.append(aha(
    "B\u00fct\u00e7e yeniden da\u011f\u0131t\u0131ld\u0131 \u2014 Kampanya A\u2019ya a\u011f\u0131rl\u0131k verildi. "
    "Sonraki ay: ayn\u0131 25.000 TL b\u00fct\u00e7eyle %40 daha fazla hasta.",

    "Art\u0131k karanl\u0131kta reklam vermiyorum, her kuru\u015fun nereye gitti\u011fini biliyorum.",

    "+%40 hasta \u2014 ayn\u0131 b\u00fct\u00e7e.",
    "Dr. Selin"
))

story.append(PageBreak())


# ── CLOSING ──────────────────────────────────────────

story.append(SP(1, 45*mm))
story.append(GradientBar(W, 3))
story.append(SP(1, 10*mm))
story.append(Paragraph("Mesajdan\nrandevuya.", ParagraphStyle("c1",
    fontName=FB, fontSize=34, leading=40, textColor=BLACK, spaceAfter=4*mm)))
story.append(Paragraph("Randevudan\nsadakate.", ParagraphStyle("c2",
    fontName=FL, fontSize=34, leading=40, textColor=GRAY, spaceAfter=8*mm)))
story.append(Paragraph("invekto.com", ParagraphStyle("url",
    fontName=FS, fontSize=13, leading=18, textColor=BLUE)))


# ── BUILD ────────────────────────────────────────────

doc.build(story, onFirstPage=footer, onLaterPages=footer)

from pypdf import PdfReader
r = PdfReader(output)
print(f"Apple-style PDF: {len(r.pages)} pages -> {output}")
