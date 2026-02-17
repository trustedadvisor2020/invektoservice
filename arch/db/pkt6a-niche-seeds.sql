-- PKT-6A: Sector Intent Template Seeds
-- Idempotent: ON CONFLICT DO NOTHING
-- 3 sectors x 7-9 intents = 22 total seed records
-- NOTE: These are TEMPLATES. Tenants get copies via POST /api/v1/intents/{tenantId}/seed

-- ============================================================
-- E-TICARET (7 intents) — GR-3.1
-- ============================================================
INSERT INTO sector_intent_templates (sector, intent_name, keywords, description)
VALUES
    ('eticaret', 'return_request',
     ARRAY['iade', 'geri gonder', 'para iade', 'iade etmek', 'iade sureci'],
     'Urun iade talebi'),
    ('eticaret', 'order_cancel',
     ARRAY['iptal', 'siparisi iptal', 'vazgectim', 'iptal etmek', 'siparisi geri al'],
     'Siparis iptali'),
    ('eticaret', 'stock_inquiry',
     ARRAY['stok', 'mevcut mu', 'var mi', 'kaldi mi', 'ne zaman gelecek'],
     'Stok durumu sorgusu'),
    ('eticaret', 'b2b_inquiry',
     ARRAY['toptan', 'bayi', 'acente', 'kurumsal', 'toptan fiyat', '100 adet', 'bulk'],
     'B2B / toptan satis sorgusu'),
    ('eticaret', 'invoice_request',
     ARRAY['fatura', 'fatura kesme', 'kurumsal fatura', 'e-fatura', 'fatura istiyorum'],
     'Fatura talebi'),
    ('eticaret', 'product_exchange',
     ARRAY['degisim', 'degistirmek', 'degistirir misiniz', 'beden degisimi', 'renk degisimi'],
     'Urun degisim talebi'),
    ('eticaret', 'negative_feedback',
     ARRAY['sikayet', 'kotu', 'berbat', 'rezalet', 'memnun degil', 'hic begenmedim'],
     'Olumsuz geri bildirim')
ON CONFLICT (sector, intent_name) DO NOTHING;

-- ============================================================
-- DIS KLINIK (6 intents) — GR-3.9
-- ============================================================
INSERT INTO sector_intent_templates (sector, intent_name, keywords, description)
VALUES
    ('dis_klinik', 'appointment_change',
     ARRAY['randevu degistir', 'ertelemek', 'farkli saat', 'randevu iptal', 'degisiklik'],
     'Randevu degisikligi/iptali'),
    ('dis_klinik', 'treatment_info',
     ARRAY['tedavi', 'implant', 'kanal', 'dolgu', 'protez', 'ne kadar surer', 'agri olur mu'],
     'Tedavi bilgisi sorgusu'),
    ('dis_klinik', 'emergency',
     ARRAY['acil', 'agri var', 'cok acitiyor', 'sislik', 'kanama', 'kirildi'],
     'Dis acil durumu'),
    ('dis_klinik', 'insurance_query',
     ARRAY['sigorta', 'sgk', 'ozel sigorta', 'provizyon', 'karsilaniyor mu'],
     'Sigorta/SGK sorgusu'),
    ('dis_klinik', 'directions',
     ARRAY['adres', 'nerede', 'nasil gelinir', 'park', 'yol tarifi', 'konum'],
     'Klinik adres/yol tarifi'),
    ('dis_klinik', 'working_hours',
     ARRAY['saat kacta', 'acik mi', 'calisma saati', 'cumartesi', 'pazar', 'hafta sonu'],
     'Calisma saatleri sorgusu')
ON CONFLICT (sector, intent_name) DO NOTHING;

-- ============================================================
-- ESTETIK (9 intents) — GR-3.12
-- ============================================================
INSERT INTO sector_intent_templates (sector, intent_name, keywords, description)
VALUES
    ('estetik', 'before_after_photo',
     ARRAY['oncesi sonrasi', 'fotograf', 'gorsel', 'ornek', 'sonuc fotografi'],
     'Once/sonra fotograf talebi'),
    ('estetik', 'procedure_info',
     ARRAY['nasil yapilir', 'kac seans', 'ne kadar surer', 'islem detayi', 'islem nasil'],
     'Islem bilgisi sorgusu'),
    ('estetik', 'contraindication',
     ARRAY['sakincali mi', 'yapilabilir mi', 'uygun musunuz', 'risk', 'hamilelikte'],
     'Kontrendikasyon sorgusu'),
    ('estetik', 'recovery',
     ARRAY['iyilesme', 'ne zaman duzelir', 'kirmizi', 'sislik ne zaman', 'iyilesme suresi'],
     'Iyilesme sureci sorgusu'),
    ('estetik', 'package_inquiry',
     ARRAY['paket', 'kampanya', 'fiyat paketi', 'kombo', 'indirim', 'ozel fiyat'],
     'Paket/kampanya sorgusu'),
    ('estetik', 'foreign_patient',
     ARRAY['yurt disi', 'turkiye', 'geliyorum', 'transfer', 'otel', 'abroad', 'coming from'],
     'Yabanci hasta sorgusu'),
    ('estetik', 'referral',
     ARRAY['arkadas getirdim', 'tavsiye', 'referans', 'onerdiler', 'arkadasim geldi'],
     'Referans/tavsiye'),
    ('estetik', 'payment_installment',
     ARRAY['taksit', 'kredi', 'odeme plani', 'pesin', 'kac taksit', 'odeme yontemi'],
     'Taksit/odeme sorgusu'),
    ('estetik', 'price_consultation',
     ARRAY['fiyat', 'ne kadar', 'ucret', 'maliyet', 'fiyat listesi', 'kac lira'],
     'Fiyat danismanligi')
ON CONFLICT (sector, intent_name) DO NOTHING;
