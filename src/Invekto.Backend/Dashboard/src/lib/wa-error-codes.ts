// =============================================================================
// WhatsApp / Meta Cloud API error-code catalogue (Turkish, operator-facing).
//
// The Outbound report 'Hata' column carries cxapi's RAW English provider text,
// with the numeric Meta code embedded as "(NNNNN)" — see ProjectsService.cs
// CleanNotSentReason, which deliberately keeps the line bearing that code.
// This module turns that text into a rich Turkish explanation (why + what to do)
// WITHOUT any backend/DB change: extractWaErrorCode() pulls the code out of the
// free text, resolveWaError() maps it to an entry (or a category-based generic
// fallback so an unknown/new code is never a blank popup).
//
// Sources cross-checked (2026-06): Meta official error-codes doc, 360dialog,
// Heltar "All Meta error codes 2025", msg91. Codes are NOT invented.
// Scope: auth/permission, rate-limit/throttle, messaging (131xxx), template
// (132xxx), registration (133xxx), billing/policy. Pure migration/onboarding
// codes (2593xxx, 2494100, 2388091-103) are intentionally OMITTED — they never
// surface as a per-message send failure; the generic fallback still covers them.
// =============================================================================

/** Single Meta error-codes documentation entry point (no reliable per-code anchors). */
export const WA_DOC_URL =
  'https://developers.facebook.com/docs/whatsapp/cloud-api/support/error-codes/';

export type WaErrorSeverity = 'retryable' | 'terminal' | 'config' | 'transient';

export interface WaErrorInfo {
  /** Numeric Meta code. */
  code: number;
  /** Short Turkish title. */
  title: string;
  /** "Neden geldi?" — one sentence, Turkish. */
  neden: string;
  /** "Ne yapmalı?" — operator-facing action bullets, Turkish. */
  neYapmali: string[];
  /** Drives the badge label + colour and the retry semantics shown to the operator. */
  severity: WaErrorSeverity;
}

/** Badge label + Tailwind classes per severity (shared by popover and banners). */
export const WA_SEVERITY_META: Record<
  WaErrorSeverity,
  { label: string; pill: string; dot: string }
> = {
  retryable: {
    label: 'Tekrar denenebilir',
    pill: 'bg-amber-50 text-amber-700 border-amber-200',
    dot: 'bg-amber-500',
  },
  transient: {
    label: 'Geçici / sistem',
    pill: 'bg-slate-50 text-slate-600 border-slate-200',
    dot: 'bg-slate-400',
  },
  terminal: {
    label: 'Kalıcı — gönderilemez',
    pill: 'bg-red-50 text-red-700 border-red-200',
    dot: 'bg-red-500',
  },
  config: {
    label: 'Yapılandırma / şablon',
    pill: 'bg-indigo-50 text-indigo-700 border-indigo-200',
    dot: 'bg-indigo-500',
  },
};

// -----------------------------------------------------------------------------
// Catalogue. Detail is richest for messaging + template codes (the ones that
// actually appear in broadcast reports); auth/registration get a concise entry.
// -----------------------------------------------------------------------------
export const WA_ERROR_CODES: Record<number, WaErrorInfo> = {
  // --- Auth / permission / request shape (your app/token/config) -------------
  0: {
    code: 0,
    title: 'Kimlik doğrulanamadı',
    neden: 'Meta erişim jetonu (access token) geçersiz veya kullanıcı doğrulanamadı.',
    neYapmali: ['Sağlayıcı bağlantısının (cxapi/WABA) jetonunu yenileyin.', 'Sorun sürerse Invekto destek ile entegrasyonu kontrol edin.'],
    severity: 'config',
  },
  1: {
    code: 1,
    title: 'Bilinmeyen API hatası',
    neden: 'Geçersiz istek ya da Meta tarafında geçici bir sunucu sorunu.',
    neYapmali: ['Bir süre sonra tekrar deneyin.', 'Tekrarlıyorsa Meta platform durumunu kontrol edin.'],
    severity: 'transient',
  },
  2: {
    code: 2,
    title: 'Meta servisi geçici kullanılamıyor',
    neden: 'Meta tarafı geçici kesinti veya aşırı yük yaşıyor.',
    neYapmali: ['Birkaç dakika sonra tekrar gönderin.', 'Meta platform durum sayfasını kontrol edin.'],
    severity: 'transient',
  },
  3: {
    code: 3,
    title: 'Yetki / yetenek sorunu',
    neden: 'İstek için gereken izin veya yetenek jetonda yok.',
    neYapmali: ['WABA izinlerini kontrol edin.', 'Sorun sürerse entegrasyonu yeniden yetkilendirin.'],
    severity: 'config',
  },
  4: {
    code: 4,
    title: 'API çağrı limiti aşıldı',
    neden: 'Kısa sürede çok fazla API çağrısı yapıldı (rate limit).',
    neYapmali: ['Gönderim hızını düşürün, bir süre sonra tekrar deneyin.'],
    severity: 'transient',
  },
  10: {
    code: 10,
    title: 'İzin reddedildi',
    neden: 'Gerekli izin verilmemiş ya da kaldırılmış.',
    neYapmali: ['Jeton izinlerini kontrol edin.', 'Numara izin listesinde (allowlist) mi doğrulayın.'],
    severity: 'config',
  },
  33: {
    code: 33,
    title: 'İş numarası geçersiz',
    neden: 'Gönderen iş telefon numarası silinmiş veya WABA’da bulunamadı.',
    neYapmali: ['WABA’daki gönderen numarayı doğrulayın.'],
    severity: 'config',
  },
  100: {
    code: 100,
    title: 'Geçersiz parametre',
    neden: 'İstekte desteklenmeyen veya hatalı yazılmış bir parametre var.',
    neYapmali: ['İstek alanlarını ve formatlarını kontrol edin.'],
    severity: 'config',
  },
  190: {
    code: 190,
    title: 'Erişim jetonu süresi doldu',
    neden: 'Meta access token süresi dolmuş.',
    neYapmali: ['Yeni sistem kullanıcı jetonu üretip entegrasyonu güncelleyin.'],
    severity: 'config',
  },
  200: {
    code: 200,
    title: 'API izni eksik',
    neden: 'İstek için gereken izin verilmemiş ya da kaldırılmış (200–299 izin grubu).',
    neYapmali: ['WABA / jeton izinlerini kontrol edip yeniden yetkilendirin.'],
    severity: 'config',
  },
  368: {
    code: 368,
    title: 'Politika ihlali — geçici blok',
    neden: 'Politika ihlalleri nedeniyle hesap geçici olarak bloke edildi.',
    neYapmali: ['Meta politika uygulama (Policy Enforcement) bildirimlerini inceleyin.', 'Blok kalkana kadar gönderim yapmayın.'],
    severity: 'config',
  },
  80007: {
    code: 80007,
    title: 'Hız limiti (rate limit)',
    neden: 'Kısa sürede çok fazla istek yapıldı.',
    neYapmali: ['Gönderim hızını düşürüp bir süre sonra tekrar deneyin.'],
    severity: 'transient',
  },

  // --- Integrity / account-level ---------------------------------------------
  130429: {
    code: 130429,
    title: 'Mesaj çıkış hacmi (throughput) limiti',
    neden: 'Cloud API mesaj gönderim hız tavanına ulaşıldı.',
    neYapmali: ['Gönderim hızını düşürün; biraz sonra tekrar deneyin.', 'Sürekliyse hacim yükseltme için sağlayıcıyla görüşün.'],
    severity: 'transient',
  },
  130472: {
    code: 130472,
    title: 'Kullanıcı Meta deney grubunda',
    neden: 'Alıcı numara Meta’nın pazarlama-mesajı deneyine (~%1 kullanıcı) dahil; mesaj teslim edilmedi. Ücretlendirilmez.',
    neYapmali: ['Tekrar denemeyin — aynı sonucu verir, kalıcıdır.', 'Müşteri size yazınca (24 saatlik pencere) ulaşabilirsiniz.', 'Gerekirse SMS/arama gibi başka kanaldan ulaşın.'],
    severity: 'terminal',
  },
  130497: {
    code: 130497,
    title: 'Ülke bazlı mesaj kısıtı',
    neden: 'İş hesabı bu ülkedeki kullanıcılara mesaj göndermekten kısıtlanmış.',
    neYapmali: ['Tanıtılan ürün/hizmetin Meta politikasına uygunluğunu gözden geçirin.'],
    severity: 'config',
  },
  131031: {
    code: 131031,
    title: 'Hesap kilitli / kısıtlı',
    neden: 'Politika ihlali nedeniyle WABA kısıtlanmış veya devre dışı bırakılmış.',
    neYapmali: ['İş Destek üzerinden ihlale itiraz edin.', 'Gerekiyorsa 2FA’yı sıfırlayın; Health Status API ile durumu kontrol edin.'],
    severity: 'config',
  },

  // --- Messaging (131xxx) ----------------------------------------------------
  131000: {
    code: 131000,
    title: 'Bir şeyler ters gitti',
    neden: 'Meta tarafında tanımlanamayan genel bir hata.',
    neYapmali: ['Bir süre sonra tekrar gönderin.', 'Tekrarlıyorsa kayıt detayını destekle paylaşın.'],
    severity: 'transient',
  },
  131005: {
    code: 131005,
    title: 'Erişim reddedildi',
    neden: 'Gerekli izin verilmemiş ya da kaldırılmış.',
    neYapmali: ['Jeton izinlerini kontrol edin / yeniden yetkilendirin.'],
    severity: 'config',
  },
  131008: {
    code: 131008,
    title: 'Zorunlu parametre eksik',
    neden: 'İstekte gerekli bir alan gönderilmemiş.',
    neYapmali: ['Şablon/istek alanlarının tümünü doldurun.'],
    severity: 'config',
  },
  131009: {
    code: 131009,
    title: 'Parametre değeri geçersiz',
    neden: 'Bir veya birden fazla parametre değeri hatalı (ör. numara WABA’ya eklenmemiş).',
    neYapmali: ['Parametre formatlarını ve numara kaydını doğrulayın.'],
    severity: 'config',
  },
  131016: {
    code: 131016,
    title: 'Servis geçici kullanılamıyor',
    neden: 'Meta tarafı geçici olarak hizmet veremiyor.',
    neYapmali: ['Bir süre sonra tekrar deneyin.'],
    severity: 'transient',
  },
  131021: {
    code: 131021,
    title: 'Gönderen ve alıcı aynı',
    neden: 'Gönderen ile alıcı numara aynı; WhatsApp kendine mesaj göndermeye izin vermez.',
    neYapmali: ['Farklı bir alıcı numarası kullanın.'],
    severity: 'terminal',
  },
  131026: {
    code: 131026,
    title: 'Mesaj iletilemedi (alıcı uygun değil)',
    neden: 'Alıcıda WhatsApp yok, numara hatalı ya da cihaz/sürüm mesajı alamıyor.',
    neYapmali: ['Numaranın WhatsApp kullandığını doğrulayın.', 'Numara formatını (ülke kodu) kontrol edin.', 'Doğrulanamıyorsa bu numarayı tekrar denemeyin.'],
    severity: 'terminal',
  },
  131037: {
    code: 131037,
    title: 'Görünen ad onaylı değil',
    neden: 'İş numarasının görünen adı (display name) onaylanmamış.',
    neYapmali: ['Görünen adı onaya gönderin / sağlayıcıyla onaylatın.'],
    severity: 'config',
  },
  131042: {
    code: 131042,
    title: 'Ödeme / faturalandırma sorunu',
    neden: 'WABA’nın ödeme yöntemi veya kredi limiti uygun değil.',
    neYapmali: ['Ödeme hesabını bağlayın, kredi limitini ve vergi bilgisini kontrol edin.'],
    severity: 'config',
  },
  131045: {
    code: 131045,
    title: 'Numara kaydı/sertifikası hatalı',
    neden: 'Telefon numarası gönderim için doğru sertifikayla kayıtlı değil.',
    neYapmali: ['Numarayı doğru sertifikayla yeniden kaydedin.'],
    severity: 'config',
  },
  131047: {
    code: 131047,
    title: 'Yeniden etkileşim gerekiyor (24 saat geçti)',
    neden: 'Müşterinin son yanıtından bu yana 24 saatten fazla geçti; serbest metin gönderilemez.',
    neYapmali: ['Onaylı bir şablon (template) mesajı ile yeniden başlatın.', 'Ya da müşteriden size yazmasını isteyin (pencere yeniden açılır).'],
    severity: 'terminal',
  },
  131048: {
    code: 131048,
    title: 'Spam hız limiti',
    neden: 'Çok sayıda mesaj spam olarak işaretlendiği için gönderim kısıtlandı.',
    neYapmali: ['Mesaj/şablon kalitesini artırın, alakasız kitleye toplu gönderimi azaltın.', 'Kalite durumunu (quality status) izleyin.'],
    severity: 'config',
  },
  131049: {
    code: 131049,
    title: 'Sağlıklı ekosistem — teslim edilmedi (frekans cap)',
    neden: 'Alıcı kısa sürede (tüm işletmelerden toplam) çok fazla pazarlama mesajı aldı; Meta bir süre teslimi engelliyor.',
    neYapmali: ['Bir süre (saatler/gün) sonra tekrar gönderin — geçicidir.', 'Aynı kişiye kısa sürede üst üste pazarlama mesajı atmayın.', 'İçerik işlemsel ise şablonu UTILITY kategorisine geçirin (cap’e takılmaz).'],
    severity: 'retryable',
  },
  131050: {
    code: 131050,
    title: 'Kullanıcı pazarlama mesajlarını durdurdu',
    neden: 'Alıcı bu işletmeden pazarlama mesajı almayı durdurdu (opt-out).',
    neYapmali: ['Tekrar göndermeyin — kullanıcı tercihine uyun (KVKK/izin).'],
    severity: 'terminal',
  },
  131051: {
    code: 131051,
    title: 'Desteklenmeyen mesaj tipi',
    neden: 'Gönderilen mesaj tipi desteklenmiyor.',
    neYapmali: ['Desteklenen mesaj tiplerini kullanın.'],
    severity: 'config',
  },
  131052: {
    code: 131052,
    title: 'Medya indirilemedi',
    neden: 'Kullanıcıdan gelen medya indirilemedi.',
    neYapmali: ['Medyayı tekrar isteyin / farklı yolla alın.'],
    severity: 'transient',
  },
  131053: {
    code: 131053,
    title: 'Medya yüklenemedi',
    neden: 'Mesajdaki medya yüklenemedi (format/boyut sorunu olabilir).',
    neYapmali: ['Medya tipi ve boyutunun desteklendiğini doğrulayın.'],
    severity: 'config',
  },
  131056: {
    code: 131056,
    title: 'Aynı alıcıya çok hızlı gönderim (pair limit)',
    neden: 'Aynı işletme–alıcı çiftine kısa sürede çok fazla mesaj gönderildi.',
    neYapmali: ['Bu alıcıya tekrar göndermeden önce bir süre bekleyin.'],
    severity: 'retryable',
  },
  131057: {
    code: 131057,
    title: 'Hesap bakım modunda',
    neden: 'WABA bakım/yükseltme modunda.',
    neYapmali: ['Bakım bitene kadar bekleyip tekrar deneyin.'],
    severity: 'transient',
  },
  131064: {
    code: 131064,
    title: 'Şablon ihlal limiti',
    neden: 'Şablon ihlalleri eşiğe ulaştı; gönderim kısıtlandı.',
    neYapmali: ['Şablon sınıflandırmalarını gözden geçirin, kısıt kalkana kadar bekleyin.'],
    severity: 'config',
  },

  // --- Template (132xxx) -----------------------------------------------------
  132000: {
    code: 132000,
    title: 'Şablon parametre sayısı uyuşmuyor',
    neden: 'Gönderilen değişken sayısı şablon tanımıyla eşleşmiyor.',
    neYapmali: ['Parametre sayısını şablon tanımına birebir eşitleyin.'],
    severity: 'config',
  },
  132001: {
    code: 132001,
    title: 'Şablon yok / onaylı değil',
    neden: 'Şablon adı/dili bulunamadı veya henüz onaylanmadı.',
    neYapmali: ['Şablon adını ve dil kodunu doğrulayın.', 'Onay bekliyorsa onay tamamlanana kadar bekleyin.'],
    severity: 'config',
  },
  132005: {
    code: 132005,
    title: 'Şablon metni çok uzun',
    neden: 'Değişkenler yerleştirilince mesaj metni izin verilen uzunluğu aştı.',
    neYapmali: ['Şablon metnini veya değişken içeriğini kısaltın.', 'WhatsApp Manager’da çeviri durumunu kontrol edin.'],
    severity: 'config',
  },
  132007: {
    code: 132007,
    title: 'Şablon politika ihlali',
    neden: 'Şablon biçimi/içeriği WhatsApp politikasını ihlal ediyor.',
    neYapmali: ['Reddedilme nedenini inceleyip şablon içeriğini düzeltin.'],
    severity: 'config',
  },
  132012: {
    code: 132012,
    title: 'Şablon parametre formatı hatalı',
    neden: 'Değişken formatı onaylı şablon biçimine uymuyor.',
    neYapmali: ['Parametreleri onaylı şablonun beklediği formata getirin.'],
    severity: 'config',
  },
  132015: {
    code: 132015,
    title: 'Şablon duraklatıldı (düşük kalite)',
    neden: 'Düşük kalite nedeniyle şablon geçici olarak duraklatıldı.',
    neYapmali: ['Mesaj kalitesini iyileştirin; gerekirse yeni şablon oluşturun.'],
    severity: 'config',
  },
  132016: {
    code: 132016,
    title: 'Şablon devre dışı',
    neden: 'Tekrarlı duraklatmalar sonrası şablon kalıcı olarak devre dışı bırakıldı.',
    neYapmali: ['İçeriği gözden geçirip yeni bir şablon oluşturun.'],
    severity: 'config',
  },
  132018: {
    code: 132018,
    title: 'Şablon doğrulama hatası',
    neden: 'Şablon parametreleri doğrulanamadı.',
    neYapmali: ['Şablon parametrelerini gözden geçirip düzeltin.'],
    severity: 'config',
  },
  132068: {
    code: 132068,
    title: 'Flow bloke durumda',
    neden: 'WhatsApp Flow yapılandırması bloke durumda.',
    neYapmali: ['Flow yapılandırmasındaki hata/eksik girdileri düzeltin.'],
    severity: 'config',
  },
  132069: {
    code: 132069,
    title: 'Flow kısıtlandı (throttle)',
    neden: 'Flow gönderimi hız sınırına takıldı (son 1 saatte çok mesaj).',
    neYapmali: ['Bir süre bekleyin; endpoint/navigasyon metriklerini iyileştirin.'],
    severity: 'transient',
  },

  // --- Registration / 2FA (133xxx) — concise --------------------------------
  133000: {
    code: 133000,
    title: 'Önceki kayıt-iptali tamamlanmadı',
    neden: 'Numaranın önceki deregistration işlemi tamamlanmadı.',
    neYapmali: ['Yeniden kaydetmeden önce kayıt-iptalini tamamlayın.'],
    severity: 'config',
  },
  133004: {
    code: 133004,
    title: 'Sunucu geçici kullanılamıyor',
    neden: 'Kayıt sırasında Meta sunucusu geçici olarak yanıt vermedi.',
    neYapmali: ['Bir süre sonra tekrar deneyin.'],
    severity: 'transient',
  },
  133005: {
    code: 133005,
    title: '2FA PIN hatalı',
    neden: 'İki adımlı doğrulama PIN’i yanlış.',
    neYapmali: ['Hesap PIN’ini doğrulayın; gerekirse sıfırlayın.'],
    severity: 'config',
  },
  133006: {
    code: 133006,
    title: 'Numara yeniden doğrulama gerektiriyor',
    neden: 'Numaranın kayıttan önce yeniden doğrulanması gerekiyor.',
    neYapmali: ['Numarayı doğrulayıp tekrar kaydedin.'],
    severity: 'config',
  },
  133008: {
    code: 133008,
    title: 'Çok fazla hatalı PIN denemesi',
    neden: 'PIN çok kez yanlış girildi; geçici kilit.',
    neYapmali: ['Kilit süresi dolana kadar bekleyin; gerekirse PIN sıfırlayın.'],
    severity: 'config',
  },
  133009: {
    code: 133009,
    title: 'PIN çok hızlı girildi',
    neden: 'PIN denemeleri arasında yeterli süre beklenmedi.',
    neYapmali: ['Belirtilen süreyi bekleyip tekrar deneyin.'],
    severity: 'transient',
  },
  133010: {
    code: 133010,
    title: 'Numara kayıtlı değil',
    neden: 'Telefon numarası platforma kayıtlı değil.',
    neYapmali: ['Numarayı WhatsApp Business platformuna kaydedin.'],
    severity: 'config',
  },
  133015: {
    code: 133015,
    title: 'Numara silindi, işlem sürüyor',
    neden: 'Yakında silinen numara hâlâ işleniyor.',
    neYapmali: ['Yeniden kaydetmeden önce 5+ dakika bekleyin.'],
    severity: 'transient',
  },
  133016: {
    code: 133016,
    title: 'Kayıt/iptal hız limiti',
    neden: '72 saatte çok fazla kayıt/iptal denemesi yapıldı.',
    neYapmali: ['72 saat bekleyip tekrar deneyin.'],
    severity: 'config',
  },

  // --- Billing / generic -----------------------------------------------------
  134011: {
    code: 134011,
    title: 'WhatsApp Payments şartları kabul edilmedi',
    neden: 'WhatsApp Payments hizmet şartları henüz kabul edilmemiş.',
    neYapmali: ['Sağlanan bağlantıdan Payments şartlarını kabul edin.'],
    severity: 'config',
  },
  135000: {
    code: 135000,
    title: 'Genel istek hatası',
    neden: 'İstek parametreleriyle ilgili tanımlanamayan bir hata.',
    neYapmali: ['Şablon/numara yapılandırmasını gözden geçirin, gerekirse yeniden oluşturun.'],
    severity: 'config',
  },
  2388019: {
    code: 2388019,
    title: 'Şablon limiti aşıldı',
    neden: 'Hesap başına şablon limiti (250) aşıldı.',
    neYapmali: ['Kullanılmayan eski şablonları silin.'],
    severity: 'config',
  },
};

// -----------------------------------------------------------------------------
// Extraction + resolution
// -----------------------------------------------------------------------------

// Auto-extraction is restricted to the MESSAGE-DELIVERY code space — the codes that
// actually surface in a send 'Hata' reason. Smaller API-level codes (0–999: auth /
// permission / param) are too ambiguous to pull out of free text — a "(4)" or
// "(100)" is far more likely a count/price than error code 4 — so they are NOT
// auto-extracted here (they still resolve via resolveWaError when a code is known by
// other means). This keeps real codes (80007, 130xxx–139xxx, 23xxxxx) while
// rejecting stray numbers, both in the parenthesised and the standalone path.
const EXTRACTABLE_CODE_RANGES: ReadonlyArray<readonly [number, number]> = [
  [80007, 80007], [130000, 139999], [2380000, 2600000],
];

function isExtractableMetaCode(n: number): boolean {
  return EXTRACTABLE_CODE_RANGES.some(([lo, hi]) => n >= lo && n <= hi);
}

/**
 * Pull the numeric Meta code out of cxapi's free-text reason.
 * Priority: the parenthesised "(NNNNN)" form CleanNotSentReason prefers — accepted
 * only when it is 4–7 digits AND inside the delivery-code ranges, so a stray
 * parenthesised number (a count, a price, a year) is never read as an error code.
 * Failing that, scan FULL digit runs (boundary-safe: a 10+ digit phone never
 * partially matches) and accept only a KNOWN catalogue code in those ranges.
 */
export function extractWaErrorCode(raw: string | null | undefined): number | null {
  if (!raw) return null;
  const paren = raw.match(/\((\d{4,7})\)/);
  if (paren) {
    const n = Number(paren[1]);
    if (isExtractableMetaCode(n)) return n;
  }
  const runs = raw.match(/\d+/g);
  if (runs) {
    for (const run of runs) {
      if (run.length < 4 || run.length > 7) continue; // counts / phones — never a code
      const n = Number(run);
      if (WA_ERROR_CODES[n] && isExtractableMetaCode(n)) return n;
    }
  }
  return null;
}

/** Category-based generic when the code is real but not in the catalogue. */
function genericForUnknown(code: number): WaErrorInfo {
  if (code >= 132000 && code < 133000) {
    return {
      code,
      title: `Şablon hatası (${code})`,
      neden: 'Meta bu şablon mesajını reddetti (şablon, parametre veya onay kaynaklı).',
      neYapmali: ['WhatsApp Manager’da şablonun onay ve kalite durumunu kontrol edin.', 'Parametre sayısı ve formatı şablonla birebir eşleşsin.'],
      severity: 'config',
    };
  }
  if (code >= 133000 && code < 134000) {
    return {
      code,
      title: `Numara kayıt hatası (${code})`,
      neden: 'Numara kaydı / 2FA ile ilgili bir hata.',
      neYapmali: ['Numara kaydı ve 2FA PIN durumunu kontrol edin.'],
      severity: 'config',
    };
  }
  if (code >= 131000 && code < 132000) {
    return {
      code,
      title: `WhatsApp mesaj hatası (${code})`,
      neden: 'Meta bu mesajı iletmedi (mesajlaşma kaynaklı bir hata).',
      neYapmali: ['Alıcı numarayı ve gönderim koşullarını kontrol edin.', 'Tekrarlıyorsa Meta dokümanından kod açıklamasına bakın.'],
      severity: 'config',
    };
  }
  return {
    code,
    title: `WhatsApp hatası (${code})`,
    neden: 'Meta bu kodu döndürdü; ayrıntı için resmî dokümana bakın.',
    neYapmali: ['Meta hata-kodları dokümanından bu kodu kontrol edin.'],
    severity: 'config',
  };
}

export interface ResolvedWaError {
  /** Catalogue entry, or category-generic, or null when the text carries no code. */
  info: WaErrorInfo | null;
  /** Extracted numeric code, or null. */
  code: number | null;
  /** The original provider text (always preserved for the operator). */
  rawText: string | null;
  /** True when the code matched a curated catalogue entry (vs. generic fallback). */
  known: boolean;
}

/**
 * Resolve raw 'Hata' text into a rich explanation.
 * - No code in the text (e.g. our own Turkish "numara filtrelendi" reasons) →
 *   info=null, rawText kept (the popover then just shows the text).
 * - Known code → curated entry. Unknown numeric code → category generic.
 */
export function resolveWaError(raw: string | null | undefined): ResolvedWaError {
  const rawText = raw && raw.trim().length > 0 ? raw : null;
  if (!rawText) return { info: null, code: null, rawText: null, known: false };
  const code = extractWaErrorCode(rawText);
  if (code == null) return { info: null, code: null, rawText, known: false };
  const known = WA_ERROR_CODES[code];
  if (known) return { info: known, code, rawText, known: true };
  return { info: genericForUnknown(code), code, rawText, known: false };
}
