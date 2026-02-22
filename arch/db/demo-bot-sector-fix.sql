-- ============================================================
-- Demo Bot Sector-Specific Fix (2026-02-23)
-- Fixes: wrong example questions, empathetic fallback, sector FAQ
-- Target: flow_id=7 (Invekto Demo Bot), tenant_id=5050
-- ============================================================

-- ============================================================
-- PART 1: Flow Node Updates
-- ============================================================

DO $$
DECLARE
  v_config jsonb;
  v_nodes  jsonb;
  v_idx    int;
  v_node   jsonb;
BEGIN
  SELECT flow_config INTO v_config FROM chatbot_flows WHERE flow_id = 7;
  v_nodes := v_config->'nodes';

  FOR v_idx IN 0..jsonb_array_length(v_nodes)-1 LOOP
    v_node := v_nodes->v_idx;

    -- n3: Add intent_question so handler asks sector-specific question
    IF v_node->>'id' = 'n3' THEN
      v_nodes := jsonb_set(
        v_nodes,
        ARRAY[v_idx::text, 'data', 'intent_question'],
        '"hangi sektorde faaliyet gosteriyorsunuz?"'::jsonb
      );
    END IF;

    -- n5a: Restoran — fix example questions (was e-commerce)
    IF v_node->>'id' = 'n5a' THEN
      v_nodes := jsonb_set(
        v_nodes,
        ARRAY[v_idx::text, 'data', 'text'],
        to_jsonb(
          E'Harika {{customer_name}}! Restoran sektoru icin Invekto sunlari yapiyor:\n\n'
          || E'\u00b7 7/24 otomatik menu paylasimi\n'
          || E'\u00b7 Masa rezervasyonu alma\n'
          || E'\u00b7 Siparis durumu bildirimi\n'
          || E'\u00b7 Musteri sikayetlerini aninda yonlendirme\n\n'
          || E'Simdi yapay zekayi test edin! Bir musterinizmis gibi soru yazin \u2014 ornegin ''bugun menude ne var?'' veya ''aksam icin rezervasyon yapabilir miyim?'''
        )
      );
    END IF;

    -- n5b: Saglik/Klinik — fix example questions (was e-commerce)
    IF v_node->>'id' = 'n5b' THEN
      v_nodes := jsonb_set(
        v_nodes,
        ARRAY[v_idx::text, 'data', 'text'],
        to_jsonb(
          E'Mukemmel {{customer_name}}! Saglik sektoru icin Invekto sunlari yapiyor:\n\n'
          || E'\u00b7 7/24 randevu alma\n'
          || E'\u00b7 Tedavi bilgilendirme\n'
          || E'\u00b7 Hasta takip mesajlari\n'
          || E'\u00b7 Acil durumlari aninda temsilciye aktarma\n\n'
          || E'Yapay zekayi test edin! Bir soru yazin \u2014 ornegin ''dis beyazlatma fiyati ne kadar?'' veya ''yarin icin randevu alabilir miyim?'''
        )
      );
    END IF;

    -- n5d: Hizmet/Generic — fix example questions (was e-commerce)
    IF v_node->>'id' = 'n5d' THEN
      v_nodes := jsonb_set(
        v_nodes,
        ARRAY[v_idx::text, 'data', 'text'],
        to_jsonb(
          E'Tesekkurler {{customer_name}}! Invekto her sektore uyum saglar:\n\n'
          || E'\u00b7 Yapay zeka ile musteri niyeti anlama\n'
          || E'\u00b7 7/24 otomatik yanit\n'
          || E'\u00b7 Akilli temsilciye yonlendirme\n'
          || E'\u00b7 Cok dilli destek\n\n'
          || E'Yapay zekayi test edin! Bir soru yazin \u2014 ornegin ''calisma saatleriniz nedir?'' veya ''fiyat bilgisi alabilir miyim?'''
        )
      );
    END IF;

    -- n7b: FAQ not found — empathetic fallback
    IF v_node->>'id' = 'n7b' THEN
      v_nodes := jsonb_set(
        v_nodes,
        ARRAY[v_idx::text, 'data', 'text'],
        to_jsonb(
          E'Simdilik demo veritabaninda bu soruya ait bir cevap yok \u2014 ama merak etmeyin!\n\n'
          || E'Gercek kullanimda yapay zeka:\n'
          || E'\u00b7 Web sitenizden otomatik bilgi ceker\n'
          || E'\u00b7 WhatsApp gecmisinizden ogrenir\n'
          || E'\u00b7 Her gun daha akilli hale gelir\n'
          || E'\u00b7 Bulamadigi sorulari aninda temsilciye aktarir\n\n'
          || E'Yani sizin musteriniz bu soruyu sorsa, yaniti aninda alirdi.\n\n'
          || E'Bu deneyim hakkinda ne dusunuyorsunuz? Kisaca yazin'
        )
      );
    END IF;
  END LOOP;

  -- Write back
  v_config := jsonb_set(v_config, '{nodes}', v_nodes);
  UPDATE chatbot_flows
  SET flow_config = v_config, updated_at = now()
  WHERE flow_id = 7;

  RAISE NOTICE 'Flow nodes updated: n3 (intent_question), n5a, n5b, n5d, n7b';
END $$;

-- ============================================================
-- PART 2: Sector-Specific FAQ Entries
-- ============================================================

-- Health/Dental sector (sort_order 200-206)
INSERT INTO faq_entries (tenant_id, question, answer, keywords, sort_order) VALUES
(5050,
 'Dis beyazlatma fiyati ne kadar?',
 'Dis beyazlatma islemi 2.500-5.000 TL arasinda degismektedir. Ofis tipi beyazlatma tek seansta sonuc verir. Detayli bilgi ve size ozel fiyat icin randevu alabilirsiniz.',
 ARRAY['dis','beyazlatma','fiyat','ucret','ne kadar','bleaching'],
 200),

(5050,
 'Implant fiyati ne kadar?',
 'Dental implant fiyatlari 15.000-30.000 TL arasindadir. Fiyat, kullanilan marka ve ek islemlere gore degisir. Ucretsiz muayene ile size ozel fiyat belirlenebilir.',
 ARRAY['implant','fiyat','dis','ucret','ne kadar','dental'],
 201),

(5050,
 'Randevu alabilir miyim?',
 'Elbette! Size uygun tarih ve saati paylasin, hemen randevunuzu organize edelim. Online randevu icin web sitemizi de kullanabilirsiniz.',
 ARRAY['randevu','muayene','tarih','saat','almak','gelmek','gel'],
 202),

(5050,
 'Calisma saatleriniz nedir?',
 'Hafta ici 09:00-19:00, Cumartesi 09:00-14:00 arasinda hizmet veriyoruz. Pazar gunu klinik kapalidir. Acil durumlar icin 7/24 WhatsApp hattimizdan ulasabilirsiniz.',
 ARRAY['saat','calisma','acik','kapali','mesai','ne zaman','gun'],
 203),

(5050,
 'Dis agrisi icin ne yapmaliyim?',
 'Agri kesici (ibuprofen veya parasetamol) kullanabilirsiniz ancak en kisa surede muayene olmanizi oneriyoruz. Soguk kompres agriya iyi gelebilir. Acil randevu icin size yardimci olabiliriz.',
 ARRAY['agri','dis','acil','agrisi','zonkluyor','sivri','sancisi'],
 204),

(5050,
 'Tedavi suresi ne kadar?',
 'Tedavi sureleri isleme gore degisir: Dolgu 30-60 dakika, kanal tedavisi 1-2 seans, implant sureci 3-6 ay, ortodonti 12-24 ay. Detay icin muayene sonrasi net bilgi verebiliriz.',
 ARRAY['tedavi','sure','seans','kac','uzun','gun','ay','hafta'],
 205),

(5050,
 'Dis eti sisligi veya sislik',
 'Dis eti sisligi genellikle enfeksiyon veya iltihaplanma belirtisidir. Ilik tuzlu su ile garara yapabilirsiniz. En kisa surede dis hekimine gorunmenizi oneriyoruz. Randevu almak ister misiniz?',
 ARRAY['sislik','sis','dis eti','iltihaplanma','kanama','sisman','enfeksiyon','sisme'],
 206);

-- Restaurant sector (sort_order 210-214)
INSERT INTO faq_entries (tenant_id, question, answer, keywords, sort_order) VALUES
(5050,
 'Bugun menude ne var?',
 'Gunun menusu: Corba + Ana yemek + Salata + Tatli dahil 250 TL. Her gun taze malzemelerle hazirlanan ozel menu! Detayli menu icin fotograf gonderebiliriz.',
 ARRAY['menu','bugun','gunun','yemek','ne var','liste'],
 210),

(5050,
 'Rezervasyon yapabilir miyim?',
 'Elbette! Kac kisilik ve hangi tarih/saat icin rezervasyon istersiniz? Hafta sonu yogunluk olabiliyor, erken rezervasyon oneriyoruz.',
 ARRAY['rezervasyon','masa','yer','ayirmak','booking','aksamyemegi'],
 211),

(5050,
 'Paket servis var mi?',
 'Evet, paket servis mevcuttur! 100 TL uzeri siparislerde ucretsiz kurye hizmeti sunuyoruz. Ortalama teslimat suresi 30-45 dakikadir.',
 ARRAY['paket','servis','kurye','gel-al','teslimat','eve','siparis'],
 212),

(5050,
 'Vejetaryen secenekler var mi?',
 'Evet, menumuzde vejetaryen ve vegan secenekler bulunmaktadir. Gluten-free alternatiflerimiz de mevcut. Detayli bilgi icin menuyemizi paylasmamizi ister misiniz?',
 ARRAY['vejetaryen','vegan','etsiz','sebze','gluten','alerji','diyet'],
 213),

(5050,
 'Cocuk menusu var mi?',
 'Evet! 0-12 yas icin ozel cocuk menusu mevcuttur. Kucuk porsiyonlar, saglikli secenekler ve eglenceli sunum. Cocuk sandalyesi de mevcuttur.',
 ARRAY['cocuk','menu','bebek','aile','sandalye','kucuk','porsiyon'],
 214);

-- Service/Generic sector (sort_order 220-224)
INSERT INTO faq_entries (tenant_id, question, answer, keywords, sort_order) VALUES
(5050,
 'Fiyatlariniz ne kadar?',
 'Hizmet fiyatlarimiz proje kapsamina gore degismektedir. Size ozel teklif hazirlayalim! Isim ve iletisim bilgilerinizi paylasin, en kisa surede donelim.',
 ARRAY['fiyat','ucret','ne kadar','maliyet','butce','teklif','para'],
 220),

(5050,
 'Nasil basvurabilirim?',
 'Basvuru icin isim, telefon ve ilgilendiginiz hizmeti paylasmaniz yeterli. Size en kisa surede donus yapacagiz. Online form icin web sitemizi de kullanabilirsiniz.',
 ARRAY['basvuru','nasil','form','kayit','uyelik','baslama','iletisim'],
 221),

(5050,
 'Referanslariniz var mi?',
 '500''den fazla mutlu musteriyle calistik! Sektorunuze ozel referans ve ornek calismalarimizi sizinle paylasmaktan mutluluk duyariz.',
 ARRAY['referans','ornek','musteri','portfolyo','calisma','gecmis'],
 222),

(5050,
 'Garanti veriyor musunuz?',
 'Evet, tum hizmetlerimiz garanti kapsamindadir. Memnun kalmazsaniz ucretsiz duzeltme yapiyoruz. Musterimemnuniyeti onceligimizdir.',
 ARRAY['garanti','memnuniyet','iade','para','duzeltme','sikayet'],
 223),

(5050,
 'Ne kadar surede tamamlanir?',
 'Proje sureleri kapsama gore degisir. Kucuk projeler 1-3 gun, orta olcekli projeler 1-2 hafta, buyuk projeler icin detayli zaman plani cikarilir. Ilk gorusme sonrasi net sure bilgisi verebiliriz.',
 ARRAY['sure','zaman','tamamlanma','teslim','kac gun','hafta','ay'],
 224);
