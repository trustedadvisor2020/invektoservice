import { useState } from 'react';
import {
  BookOpen,
  Target,
  Users,
  CheckSquare,
  MessageCircle,
  Layers,
  ArrowRight,
  AlertTriangle,
  Lightbulb,
  Clock,
  ChevronRight,
  Building2,
  Utensils,
  Stethoscope,
  ShoppingCart,
  Scissors,
  Plane,
  Square,
  CheckSquare as CheckSquareFilled,
  Mail,
  Copy,
  Check,
  TrendingUp,
  DollarSign,
  Heart,
  Shield,
  Zap,
  BarChart3,
  Repeat,
  UserPlus,
  Handshake,
  Activity,
  ExternalLink,
} from 'lucide-react';
import { cn } from '../lib/utils';
import { Card } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';

/* ─── Types ─────────────────────────────────────────────────── */

type GuideTab = 'overview' | 'onboarding' | 'features' | 'sectors' | 'communication' | 'saas' | 'actions';

interface TabDef {
  id: GuideTab;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

const TABS: TabDef[] = [
  { id: 'overview', label: 'Genel Bakış', icon: BookOpen },
  { id: 'onboarding', label: 'Onboarding Adımları', icon: Target },
  { id: 'features', label: 'Özellik Rehberi', icon: Layers },
  { id: 'sectors', label: 'Sektör Senaryoları', icon: Building2 },
  { id: 'communication', label: 'Müşteri İletişimi', icon: MessageCircle },
  { id: 'saas', label: 'SaaS Stratejisi', icon: TrendingUp },
  { id: 'actions', label: 'Aksiyon Listesi', icon: CheckSquare },
];

/* ─── Helpers ───────────────────────────────────────────────── */

/* ─── Tab navigation context ────────────────────────────────── */

let _setActiveTabGlobal: ((tab: GuideTab) => void) | null = null;

function TabLink({ to, children }: { to: GuideTab; children: React.ReactNode }) {
  const tabLabels: Record<GuideTab, string> = {
    overview: 'Genel Bakış',
    onboarding: 'Onboarding Adımları',
    features: 'Özellik Rehberi',
    sectors: 'Sektör Senaryoları',
    communication: 'Müşteri İletişimi',
    saas: 'SaaS Stratejisi',
    actions: 'Aksiyon Listesi',
  };
  return (
    <button
      onClick={() => _setActiveTabGlobal?.(to)}
      className="inline-flex items-center gap-1 text-brand-600 hover:text-brand-700 font-medium underline underline-offset-2 decoration-brand-200 hover:decoration-brand-400 transition-colors"
      title={tabLabels[to]}
    >
      {children}
      <ExternalLink className="w-3 h-3" />
    </button>
  );
}

function SectionTitle({ icon: Icon, children }: { icon: React.ComponentType<{ className?: string }>; children: React.ReactNode }) {
  return (
    <h3 className="text-base font-semibold text-navy-900 flex items-center gap-2 mb-3">
      <Icon className="w-4 h-4 text-brand-500 flex-shrink-0" />
      {children}
    </h3>
  );
}

function Tip({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex gap-2.5 p-3 bg-amber-50 border border-amber-100 rounded-lg text-sm text-amber-800">
      <Lightbulb className="w-4 h-4 text-amber-500 flex-shrink-0 mt-0.5" />
      <div>{children}</div>
    </div>
  );
}

function Warning({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex gap-2.5 p-3 bg-red-50 border border-red-100 rounded-lg text-sm text-red-700">
      <AlertTriangle className="w-4 h-4 text-red-500 flex-shrink-0 mt-0.5" />
      <div>{children}</div>
    </div>
  );
}

function StepCard({ step, title, duration, children }: { step: number; title: string; duration?: string; children: React.ReactNode }) {
  return (
    <div className="relative pl-10 pb-6 last:pb-0">
      {/* Vertical line */}
      <div className="absolute left-[15px] top-8 bottom-0 w-px bg-navy-100 last:hidden" />
      {/* Step number */}
      <div className="absolute left-0 top-0 w-8 h-8 rounded-full bg-brand-500 text-white text-sm font-bold flex items-center justify-center shadow-soft">
        {step}
      </div>
      <div className="pt-0.5">
        <div className="flex items-center gap-2 mb-1.5">
          <h4 className="text-sm font-semibold text-navy-900">{title}</h4>
          {duration && (
            <span className="inline-flex items-center gap-1 text-xs text-navy-400">
              <Clock className="w-3 h-3" />
              {duration}
            </span>
          )}
        </div>
        <div className="text-sm text-navy-600 space-y-2">
          {children}
        </div>
      </div>
    </div>
  );
}

function FeatureBlock({ title, problem, solution, howTo, when }: {
  title: string;
  problem: string;
  solution: string;
  howTo: string;
  when: string;
}) {
  return (
    <Card className="p-4">
      <h4 className="text-sm font-semibold text-navy-900 mb-2">{title}</h4>
      <div className="space-y-1.5 text-sm">
        <div className="flex gap-2">
          <span className="text-navy-400 w-20 flex-shrink-0 font-medium">Sorun:</span>
          <span className="text-navy-600">{problem}</span>
        </div>
        <div className="flex gap-2">
          <span className="text-navy-400 w-20 flex-shrink-0 font-medium">Çözüm:</span>
          <span className="text-navy-700 font-medium">{solution}</span>
        </div>
        <div className="flex gap-2">
          <span className="text-navy-400 w-20 flex-shrink-0 font-medium">Nasıl:</span>
          <span className="text-navy-600">{howTo}</span>
        </div>
        <div className="flex gap-2">
          <span className="text-navy-400 w-20 flex-shrink-0 font-medium">Ne zaman:</span>
          <Badge variant="info">{when}</Badge>
        </div>
      </div>
    </Card>
  );
}

function SectorCard({ icon: Icon, title, primary, secondary, firstFlow }: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  primary: string[];
  secondary: string[];
  firstFlow: string;
}) {
  return (
    <Card className="p-4">
      <div className="flex items-center gap-2 mb-3">
        <div className="w-8 h-8 rounded-lg bg-brand-50 flex items-center justify-center">
          <Icon className="w-4 h-4 text-brand-500" />
        </div>
        <h4 className="text-sm font-semibold text-navy-900">{title}</h4>
      </div>
      <div className="space-y-2.5 text-sm">
        <div>
          <span className="text-xs font-medium text-navy-400 uppercase tracking-wide">Önce Aç</span>
          <div className="flex flex-wrap gap-1.5 mt-1">
            {primary.map(f => <Badge key={f} variant="success">{f}</Badge>)}
          </div>
        </div>
        <div>
          <span className="text-xs font-medium text-navy-400 uppercase tracking-wide">Sonra Ekle</span>
          <div className="flex flex-wrap gap-1.5 mt-1">
            {secondary.map(f => <Badge key={f} variant="default">{f}</Badge>)}
          </div>
        </div>
        <div className="pt-1 border-t border-navy-50">
          <span className="text-xs text-navy-400">İlk akış örneği:</span>
          <p className="text-xs text-navy-700 font-medium mt-0.5">{firstFlow}</p>
        </div>
      </div>
    </Card>
  );
}

/* ─── Action List with local toggle ─────────────────────────── */

interface ActionItem {
  id: string;
  category: string;
  text: string;
  detail: string;
  priority: 'high' | 'medium' | 'low';
}

const ACTIONS: ActionItem[] = [
  // Onboarding Hazırlık
  { id: 'a1', category: 'Onboarding Hazırlık', text: 'Sektör bazlı hazır flow template\u0027leri oluştur', detail: 'En az 3 sektör: restoran, klinik, e-ticaret. Her biri çalışan, test edilmiş flow olmalı.', priority: 'high' },
  { id: 'a2', category: 'Onboarding Hazırlık', text: 'Her sektör için 1 sayfa "Chatinbox sizin için ne yapar" dokümanı yaz', detail: 'A4, Türkçe, teknik terim yok. Sorun-çözüm formatı. PDF veya görselle destekle.', priority: 'high' },
  { id: 'a3', category: 'Onboarding Hazırlık', text: '3 temel özellik için ekran kaydı videosu çek', detail: 'Flow oluşturma, FAQ ekleme, kampanya gönderme. Her biri max 60 saniye.', priority: 'high' },
  { id: 'a4', category: 'Onboarding Hazırlık', text: 'Yeni tenant için varsayılan feature flag setini belirle', detail: 'İlk hafta: FlowBuilder + Knowledge. 2. hafta: Analytics + Outbound. 3. hafta: Marketing + Integrations.', priority: 'medium' },

  // İlk Gün
  { id: 'b1', category: 'İlk Gün Checklist', text: 'Tenant oluştur ve temel ayarları yap', detail: 'Firma bilgileri, çalışma saatleri, saat dilimi, kapalı günler.', priority: 'high' },
  { id: 'b2', category: 'İlk Gün Checklist', text: 'WhatsApp hattını bağla ve test et', detail: 'WapCRM API key gir, hatları yenile, aktif hattı seç, test mesajı gönder.', priority: 'high' },
  { id: 'b3', category: 'İlk Gün Checklist', text: 'İlk flow\u0027u kur ve canlı test yap', detail: 'Basit bir "hoş geldin + menü" akışı. Müşterinin kendi telefonundan test etmesini sağla.', priority: 'high' },
  { id: 'b4', category: 'İlk Gün Checklist', text: 'Müşteriye dashboard erişimi ver', detail: 'Kullanıcı adı ve şifre oluştur, giriş URL\u0027ini paylaş.', priority: 'high' },

  // İlk Hafta
  { id: 'c1', category: 'İlk Hafta', text: 'FAQ içeriklerini gir veya müşteri ile birlikte oluştur', detail: 'En sık sorulan 10-15 soruyu belirle, bilgi bankasına ekle. Kısa ve net cevaplar.', priority: 'high' },
  { id: 'c2', category: 'İlk Hafta', text: 'Çalışma saatlerini yapılandır', detail: 'Mesai dışı otomatik cevap akışını oluştur ve test et.', priority: 'medium' },
  { id: 'c3', category: 'İlk Hafta', text: 'İlk hafta sonunda müşteri ile 15 dk görüşme yap', detail: 'Neler çalışıyor, neler zor, soru var mı? Not al, gerekirse flow\u0027u ayarla.', priority: 'high' },

  // İkinci Hafta
  { id: 'd1', category: 'İkinci Hafta', text: 'Analytics modülünü aç ve müşteriye göster', detail: 'İlk hafta verisi birikti. "Bak şu kadar mesaj geldi, şu kadar otomatik cevaplandı" göster.', priority: 'medium' },
  { id: 'd2', category: 'İkinci Hafta', text: 'Outbound/Kampanya modülünü aç', detail: 'İlk toplu mesajı birlikte gönderin. Opt-out yönetimini açıkla.', priority: 'medium' },
  { id: 'd3', category: 'İkinci Hafta', text: 'Condition/Switch node\u0027larını öğret', detail: 'Basit bir senaryo: "Randevu mu yoksa fiyat mı soruyor?" dallanması.', priority: 'medium' },

  // Üçüncü Hafta+
  { id: 'e1', category: 'Üçüncü Hafta+', text: 'Gelişmiş özellikleri kademeli aç', detail: 'Marketing (referral, review), Sentiment Analysis, API Call node\u0027u. Hepsini birden değil, ihtiyaca göre.', priority: 'low' },
  { id: 'e2', category: 'Üçüncü Hafta+', text: 'Randevu modülünü aç (sektör uygunsa)', detail: 'Klinik, kuaför, restoran gibi sektörlerde. Hatırlatma akışını birlikte kur.', priority: 'low' },
  { id: 'e3', category: 'Üçüncü Hafta+', text: 'Aylık performans raporu gönder', detail: 'Kaç mesaj geldi, kaçı otomatik cevaplandı, agent ihtiyacı ne kadar azaldı. Somut rakamlarla.', priority: 'low' },

  // Sürekli
  { id: 'f1', category: 'Sürekli', text: 'Yeni özellik çıkınca 30 sn video çek ve WhatsApp\u0027tan gönder', detail: 'Format: "Yeni özellik: [ne yapar] — nasıl açılır: [3 adım]". Kısa, görsel, net.', priority: 'medium' },
  { id: 'f2', category: 'Sürekli', text: 'Kullanılmayan modülleri takip et', detail: 'Açık ama kullanılmayan feature var mı? Müşteri ile konuşup ya öğret ya kapat.', priority: 'medium' },
  { id: 'f3', category: 'Sürekli', text: 'Müşteri memnuniyetini periyodik olarak sor', detail: 'Ayda 1 kere, kısa bir WhatsApp mesajı: "Nasılsınız, eksik var mı?" Basit ama etkili.', priority: 'low' },

  // SaaS Stratejisi — Fiyatlandırma
  { id: 'g1', category: 'SaaS: Fiyatlandırma', text: 'Başlangıç / Büyüme / Profesyonel paket yapısını oluştur', detail: 'Modül bazlı değil, değer bazlı paketleme. Her pakete somut bir sonuç eşle.', priority: 'high' },
  { id: 'g2', category: 'SaaS: Fiyatlandırma', text: 'Yıllık ödeme indirimi tanımla', detail: 'Aylık x12 yerine 10 aylık (2 ay bedava). Churn\u0027u %30-40 düşürür.', priority: 'high' },

  // SaaS Stratejisi — Health Score
  { id: 'h1', category: 'SaaS: Müşteri Sağlığı', text: 'Müşteri Health Score formülü oluştur', detail: 'Panel girişi, flow çalışma, otomasyon oranı, FAQ sayısı, destek talebi — ağırlıklı skor.', priority: 'high' },
  { id: 'h2', category: 'SaaS: Müşteri Sağlığı', text: 'Health Score < 60 için otomatik alarm kur', detail: 'Skor düşük müşterileri haftalık olarak kontrol et, proaktif iletişim kur.', priority: 'medium' },

  // SaaS Stratejisi — Ölçekleme
  { id: 'i1', category: 'SaaS: Ölçekleme', text: 'Sektör bazlı template flow\u0027ları + FAQ paketleri oluştur', detail: 'Yeni tenant açılırken "sektör seç" → hazır flow + FAQ otomatik yüklensin.', priority: 'high' },
  { id: 'i2', category: 'SaaS: Ölçekleme', text: 'Self-service kurulum wizard\u0027ı planla', detail: '10+ müşteride bizzat kurulum sürekli olmaz. Adım adım wizard ile müşterinin kendisi kursun.', priority: 'medium' },

  // SaaS Stratejisi — Büyüme
  { id: 'j1', category: 'SaaS: Büyüme', text: 'Referral programı kur', detail: 'Mevcut müşteri yeni getirir → 1 ay bedava. Gelen müşteri → %20 indirim. Health score 80+ olanlara öner.', priority: 'medium' },
  { id: 'j2', category: 'SaaS: Büyüme', text: 'Upsell tetikleyicilerini belirle ve takip et', detail: 'Mesaj hacmi 500+, 3+ dallanma, FAQ yoğunluğu → uygun modülü öner.', priority: 'medium' },

  // SaaS Stratejisi — Metrikler
  { id: 'k1', category: 'SaaS: Metrikler', text: 'MRR, churn rate, time-to-value takibini başlat', detail: 'Excel bile olur. Her ay kaydet: toplam gelir, ayrılan müşteri, ortalama kurulum süresi.', priority: 'high' },
  { id: 'k2', category: 'SaaS: Metrikler', text: 'Destek taleplerini kayıt altına al', detail: 'Hangi konuda en çok soru geldi? Tekrarlayan sorunları FAQ veya rehbere ekle.', priority: 'medium' },
];

function ActionList() {
  const [checked, setChecked] = useState<Set<string>>(new Set());

  const toggle = (id: string) => {
    setChecked(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const categories = [...new Set(ACTIONS.map(a => a.category))];
  const total = ACTIONS.length;
  const done = checked.size;

  const priorityColors = {
    high: 'bg-red-50 text-red-700 border-red-100',
    medium: 'bg-amber-50 text-amber-700 border-amber-100',
    low: 'bg-navy-50 text-navy-500 border-navy-100',
  };
  const priorityLabels = { high: 'Yüksek', medium: 'Orta', low: 'Düşük' };

  return (
    <div className="space-y-6">
      {/* Progress */}
      <Card className="p-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-sm font-medium text-navy-700">İlerleme</span>
          <span className="text-sm text-navy-500">{done}/{total} tamamlandı</span>
        </div>
        <div className="h-2 bg-navy-100 rounded-full overflow-hidden">
          <div
            className="h-full bg-brand-500 rounded-full transition-all duration-300"
            style={{ width: `${total > 0 ? (done / total) * 100 : 0}%` }}
          />
        </div>
      </Card>

      {categories.map(cat => {
        const items = ACTIONS.filter(a => a.category === cat);
        const catDone = items.filter(a => checked.has(a.id)).length;
        return (
          <div key={cat}>
            <div className="flex items-center gap-2 mb-2">
              <h4 className="text-sm font-semibold text-navy-900">{cat}</h4>
              <Badge variant={catDone === items.length ? 'success' : 'default'}>
                {catDone}/{items.length}
              </Badge>
            </div>
            <div className="space-y-1.5">
              {items.map(item => {
                const isDone = checked.has(item.id);
                return (
                  <button
                    key={item.id}
                    onClick={() => toggle(item.id)}
                    className={cn(
                      'w-full text-left flex items-start gap-3 p-3 rounded-lg border transition-colors',
                      isDone
                        ? 'bg-emerald-50/50 border-emerald-100'
                        : 'bg-white border-navy-100 hover:bg-navy-50/50'
                    )}
                  >
                    {isDone ? (
                      <CheckSquareFilled className="w-4 h-4 text-emerald-500 flex-shrink-0 mt-0.5" />
                    ) : (
                      <Square className="w-4 h-4 text-navy-300 flex-shrink-0 mt-0.5" />
                    )}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className={cn(
                          'text-sm font-medium',
                          isDone ? 'text-navy-400 line-through' : 'text-navy-800'
                        )}>
                          {item.text}
                        </span>
                        <span className={cn(
                          'inline-flex px-1.5 py-0.5 rounded text-xs font-medium border',
                          priorityColors[item.priority]
                        )}>
                          {priorityLabels[item.priority]}
                        </span>
                      </div>
                      <p className={cn(
                        'text-xs mt-0.5',
                        isDone ? 'text-navy-300' : 'text-navy-500'
                      )}>
                        {item.detail}
                      </p>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}

/* ─── Tab Content ───────────────────────────────────────────── */

function OverviewTab() {
  return (
    <div className="space-y-6">
      <div>
        <SectionTitle icon={BookOpen}>Chatinbox Nedir — Tek Cümle</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-700 leading-relaxed">
            Chatinbox, işletmelerin WhatsApp üzerinden gelen müşteri mesajlarını <strong>otomatik olarak yanıtması</strong>,
            sık sorulan soruları <strong>yapay zeka ile cevaplatması</strong> ve toplu mesaj göndermesini sağlayan bir platformdur.
            Müşteri hizmetini hızlandırır, insan ihtiyacını azaltır, hiçbir mesajı kaçırmaz.
          </p>
        </Card>
      </div>

      <div>
        <SectionTitle icon={Target}>Müşteriye Ne Satıyorsun?</SectionTitle>
        <div className="space-y-3">
          <Card className="p-4">
            <h4 className="text-sm font-semibold text-navy-900 mb-2">Teknoloji Satma, Sonuç Sat</h4>
            <div className="space-y-2 text-sm">
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"17 node tipimiz var"</span>
                <ArrowRight className="w-3 h-3 text-navy-300 flex-shrink-0" />
                <span className="text-emerald-700 font-medium">"Müşteri seni arar, otomatik cevap verir"</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"AI intent detection"</span>
                <ArrowRight className="w-3 h-3 text-navy-300 flex-shrink-0" />
                <span className="text-emerald-700 font-medium">"Müşteri ne istediğini otomatik anlar"</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"pgvector embedding"</span>
                <ArrowRight className="w-3 h-3 text-navy-300 flex-shrink-0" />
                <span className="text-emerald-700 font-medium">"Soruya en yakın cevabı bulur"</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"Multi-tenant SaaS"</span>
                <ArrowRight className="w-3 h-3 text-navy-300 flex-shrink-0" />
                <span className="text-emerald-700 font-medium">"Her firma kendi panelini görüntüler"</span>
              </div>
            </div>
          </Card>

          <Tip>
            Müşteri teknolojiyi değil, kendi hayatının kolaylaşmasını satın alıyor.
            Her özelliği "bu sana ne kazandırıyor?" üzerinden anlat.
          </Tip>
        </div>
      </div>

      <div>
        <SectionTitle icon={AlertTriangle}>En Büyük Risk: Bilgi Kirliliği</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">
            Platform çok geniş. Müşteriye hepsini gösterirsen bunalır ve hiçbirini kullanmaz.
            <TabLink to="onboarding">Onboarding adimlari</TabLink> sekmesindeki kademeli açılım stratejisini uygula.
            <TabLink to="sectors">Sektör Senaryoları</TabLink> sekmesinden sektöre uygun modülleri belirle.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="p-3 bg-emerald-50 rounded-lg border border-emerald-100 text-center">
              <div className="text-lg font-bold text-emerald-700">Hafta 1</div>
              <div className="text-xs text-emerald-600 mt-1">Flow + FAQ</div>
              <div className="text-xs text-navy-400 mt-0.5">Temeli öğrensin</div>
            </div>
            <div className="p-3 bg-amber-50 rounded-lg border border-amber-100 text-center">
              <div className="text-lg font-bold text-amber-700">Hafta 2</div>
              <div className="text-xs text-amber-600 mt-1">Analytics + Kampanya</div>
              <div className="text-xs text-navy-400 mt-0.5">Değeri görsün</div>
            </div>
            <div className="p-3 bg-brand-50 rounded-lg border border-brand-100 text-center">
              <div className="text-lg font-bold text-brand-700">Hafta 3+</div>
              <div className="text-xs text-brand-600 mt-1">Marketing + AI</div>
              <div className="text-xs text-navy-400 mt-0.5">Gücünü keşfetsin</div>
            </div>
          </div>
        </Card>
      </div>

      <div>
        <SectionTitle icon={Users}>İlk 5 Dakika Kuralı</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">
            Müşteri platformu ilk açtığında <strong>5 dakika içinde somut bir değer görmeli</strong>.
            Aksi halde "sonra bakarım" der ve bir daha açmaz.
            Bunu <TabLink to="saas">SaaS Stratejisi</TabLink> sekmesinde "Aha Moment" olarak detaylandırdık.
          </p>
          <div className="space-y-2 text-sm">
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span className="text-navy-700"><strong>Hazır flow template:</strong> Tenant oluşturulurken sektöre uygun akış hazır gelsin</span>
            </div>
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span className="text-navy-700"><strong>"Merhaba Dünya" testi:</strong> İlk 2 dakikada kendi telefonundan test mesajı göndersin</span>
            </div>
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span className="text-navy-700"><strong>Boş ekran yok:</strong> Örnek data veya demo verisiyle gelsin, boş dashboard moral bozar</span>
            </div>
          </div>
        </Card>
      </div>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">İlgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="onboarding">Onboarding Adımları</TabLink>
          <TabLink to="features">Özellik Rehberi</TabLink>
          <TabLink to="saas">SaaS Stratejisi</TabLink>
          <TabLink to="actions">Aksiyon Listesi</TabLink>
        </div>
      </Card>
    </div>
  );
}

function OnboardingTab() {
  return (
    <div className="space-y-6">
      <div>
        <SectionTitle icon={Target}>Müşteri Onboarding Süreci</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Yeni bir müşteri geldiğinde aşağıdaki adımları sırayla takip et.
          Her adımı tamamlamadan bir sonrakine geçme.
        </p>
      </div>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">Satış Öncesi</h4>
        <StepCard step={1} title="Sektörü ve ihtiyacı anla" duration="15 dk">
          <p>Müşterinin sektörünü belirle (restoran, klinik, e-ticaret, hizmet, turizm).</p>
          <p>Şu soruları sor:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>Günlük kaç mesaj alıyorsun?</li>
            <li>En çok hangi sorular soruluyor?</li>
            <li>Mesajlara kaç dakikada dönüyorsun?</li>
            <li>Randevu sistemi kullanıyor musun?</li>
            <li>Toplu mesaj/kampanya gönderiyor musun?</li>
          </ul>
          <Tip>
            Bu soruların cevapları hangi modülleri açık başlayacağını belirler.
            Hepsini açma, sadece ihtiyaca göre. <TabLink to="sectors">Sektör Senaryoları</TabLink> sekmesinde
            sektöre göre hangi modüllerin açılacağı detaylı anlatıyor.
          </Tip>
        </StepCard>

        <StepCard step={2} title="Demo göster (kendi test ortamında)" duration="20 dk">
          <p>Müşterinin sektörüne uygun hazır flow ile canlı demo yap.</p>
          <p>Gösterim sırası:</p>
          <ol className="list-decimal pl-5 space-y-1 text-navy-600">
            <li>"Bir müşteri mesaj yazıyor" — otomatik cevap dönüyor</li>
            <li>"Sık sorulan soru soruyor" — AI cevaplıyor</li>
            <li>"Yardım istiyor" — insana yönlendiriliyor</li>
          </ol>
          <Warning>
            Demo sırasında arka plan teknolojisinden bahsetme (node, flow, API, embedding...).
            Sadece sonucu göster.
          </Warning>
        </StepCard>
      </Card>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">İlk Gün (Kurulum)</h4>
        <StepCard step={3} title="Tenant oluştur ve ayarları yap" duration="10 dk">
          <p>Firmalar sayfasından yeni tenant oluştur. Aşağıdakileri doldur:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>Firma adi ve kodu</li>
            <li>Çalışma saatleri ve kapalı günler</li>
            <li>Saat dilimi</li>
            <li>Kullanıcı adı ve şifre (müşteri için)</li>
          </ul>
        </StepCard>

        <StepCard step={4} title="WhatsApp hattını bağla" duration="10 dk">
          <p>Müşterinin WapCRM hesabındaki API key'i al ve sisteme gir.</p>
          <p>Ayarlar &gt; Hatlar &gt; WapCRM'den Yenile butonuyla hatları çek.</p>
          <p>Kullanılacak hattı aktif et, diğer hatları kapat.</p>
          <Tip>
            Bu adımda müşteri yanında olsun. Kendi telefonundan test mesajı
            göndererek "çalışıyor!" momentini birlikte yaşayın.
          </Tip>
        </StepCard>

        <StepCard step={5} title="İlk akışı kur" duration="15 dk">
          <p>Sektöre uygun hazır template'i yükle veya sıfırdan basit bir akış oluştur:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li><strong>Trigger Start:</strong> Müşteri mesaj yazdığında başla</li>
            <li><strong>Mesaj Menü:</strong> "Merhaba! Size nasıl yardımcı olabilirim?" + 3-4 seçenek</li>
            <li><strong>FAQ Node:</strong> Sık sorulan soruları otomatik cevapla</li>
            <li><strong>Handoff:</strong> "Detaylı bilgi için sizi yetkilimize yönlendiriyorum"</li>
          </ul>
          <Warning>
            İlk akış basit olmalı. Condition, Switch, AI Intent gibi gelişmiş node'ları EKLEME.
            Müşteri önce temel akışı öğrensin.
          </Warning>
        </StepCard>

        <StepCard step={6} title="Canlı test yap" duration="5 dk">
          <p>Müşterinin kendi telefonundan WhatsApp'a mesaj göndermesini sağla.</p>
          <p>Birlikte izleyin: mesaj geldi → akış başladı → cevap gitti.</p>
          <p>Bu "aha momenti" çok önemli. Müşterinin gözleri parlayacak. (<TabLink to="saas">SaaS Stratejisi</TabLink> sekmesinde neden bu anın kritik olduğunu oku.)</p>
        </StepCard>
      </Card>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">İlk Hafta (Alışma)</h4>
        <StepCard step={7} title="FAQ içeriklerini doldur" duration="30 dk">
          <p>Müşteri ile birlikte en sık sorulan 10-15 soruyu belirle.</p>
          <p>Bilgi Bankası'na ekle. Kısa, net, konuşma diliyle yaz.</p>
          <p>Yanlış: "İşletmemiz 2005 yılında kurulmuş olup..." — Doğru: "Pazartesi-Cuma 09:00-18:00 arası açığız."</p>
        </StepCard>

        <StepCard step={8} title="Mesai dışı akışını kur" duration="10 dk">
          <p>Çalışma saatleri dışında gelen mesajlara otomatik cevap:</p>
          <p className="italic text-navy-500">"Şu anda mesai saatlerimiz dışındayız. En kısa sürede döneceğiz."</p>
        </StepCard>

        <StepCard step={9} title="Hafta sonu takip görüşmesi" duration="15 dk">
          <p>İlk haftanın sonunda müşteriye sor:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>Akış çalışıyor mu, sorun var mı?</li>
            <li>Müşterilerin tepkisi nasıl?</li>
            <li>Eklemek istediğin bir soru/cevap var mı?</li>
            <li>Paneli açıp bakıyor musun?</li>
          </ul>
          <Tip>
            Bu görüşme kritik. Müşteri ilk haftada sıkıldıysa 2. haftada gelmez.
            Sorunları hemen çöz.
          </Tip>
        </StepCard>
      </Card>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">İkinci Hafta (Derinleşme)</h4>
        <StepCard step={10} title="Analytics modülünü aç" duration="10 dk">
          <p>İlk hafta verisi birikti. Müşteriye somut rakamlar göster:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>"Bu hafta 47 mesaj geldi, 38'i otomatik cevaplandı"</li>
            <li>"En çok sorulan konu: fiyat bilgisi"</li>
            <li>"Ortalama cevap süresi: 3 saniye"</li>
          </ul>
          <p>Bu rakamlar müşterinin "para ediyor mu?" sorusuna cevap verir.</p>
        </StepCard>

        <StepCard step={11} title="Kampanya modülünü aç (isteğe bağlı)" duration="15 dk">
          <p>Eğer müşteri toplu mesaj göndermek istiyorsa Outbound modülünü aç.</p>
          <p>İlk kampanyayı birlikte gönderin. Opt-out (STOP) yönetimini açıkla.</p>
          <Warning>
            Toplu mesaj hassas bir konu. Spam riski var. Müşteriye şu kuralları anlat:
            izinli kişi listesi, STOP yazanları çıkar, günlük limit.
          </Warning>
        </StepCard>

        <StepCard step={12} title="Dallanma öğret (Condition/Switch)" duration="15 dk">
          <p>Basit bir senaryo oluştur:</p>
          <p className="italic text-navy-500">"Müşteri 'randevu' yazarsa → randevu akışına yönlendir. 'Fiyat' yazarsa → fiyat listesi gönder."</p>
          <p>Bu adımda müşteri akış tasarımının gücünü anlamaya başlar.</p>
        </StepCard>
      </Card>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">Üçüncü Hafta ve Sonrası</h4>
        <StepCard step={13} title="Gelişmiş özellikleri ihtiyaca göre aç" duration="Sürekli">
          <p>Her yeni özelliği açmadan önce müşteriye sor: "Buna ihtiyacın var mı?"</p>
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <Badge variant="default">Marketing</Badge>
              <span className="text-navy-500">→ Referral ve review isteme (hizmet sektörü için)</span>
            </div>
            <div className="flex items-center gap-2">
              <Badge variant="default">Sentiment</Badge>
              <span className="text-navy-500">→ Müşteri memnuniyeti takibi (yüksek hacim için)</span>
            </div>
            <div className="flex items-center gap-2">
              <Badge variant="default">Appointments</Badge>
              <span className="text-navy-500">→ Randevu sistemi (klinik, kuafor, restoran)</span>
            </div>
            <div className="flex items-center gap-2">
              <Badge variant="default">API Call</Badge>
              <span className="text-navy-500">→ Dış sistem entegrasyonu (teknik müşteri için)</span>
            </div>
          </div>
        </StepCard>

        <StepCard step={14} title="Aylık performans raporu gönder" duration="Ayda 1">
          <p>Her ay müşteriye kısa bir özet gönder:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>Toplam mesaj / otomatik cevaplanan oran</li>
            <li>En çok sorulan konular</li>
            <li>Kampanya sonuclari (varsa)</li>
            <li>Öneri: "Şu FAQ'i eklesek %10 daha fazla otomatik cevap verebiliriz"</li>
          </ul>
          <Tip>
            Bu rapor müşterinin "neden para ödüyorum?" sorusunu önler.
            Somut rakam göster, genel laftan kaçın. <TabLink to="communication">Müşteri İletişimi</TabLink> sekmesinde hazır rapor şablonları var.
            <TabLink to="saas">SaaS Stratejisi</TabLink> sekmesinde hangi metrikleri takip etmen gerektiğini oku.
          </Tip>
        </StepCard>
      </Card>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">Ilgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="sectors">Sektör Senaryoları</TabLink>
          <TabLink to="features">Özellik Rehberi</TabLink>
          <TabLink to="communication">İletişim Şablonları</TabLink>
          <TabLink to="saas">SaaS Stratejisi</TabLink>
          <TabLink to="actions">Aksiyon Listesi</TabLink>
        </div>
      </Card>
    </div>
  );
}

function FeaturesTab() {
  return (
    <div className="space-y-6">
      <div>
        <SectionTitle icon={Layers}>Özellik Rehberi — Anlatım Sırası</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Özellikleri teknoloji sırasına göre değil, <strong>müşteri açısı sırasına göre</strong> anlat.
          Aşağıdaki sıra, müşterinin "buna neden ihtiyacım var?" sorusunu en kolay anlayacağı sıradır.
          Her müşteri her şeyi kullanmayacak — <TabLink to="sectors">Sektör Senaryoları</TabLink> sekmesinden sektöre uygun modülleri belirle.
        </p>
      </div>

      <div className="space-y-3">
        <div className="flex items-center gap-2 mb-1">
          <Badge variant="success">1. Öncelik — İlk Gün</Badge>
        </div>
        <FeatureBlock
          title="Flow Builder (Akis Tasarimi)"
          problem="WhatsApp'tan çok mesaj geliyor, yetişemiyorum"
          solution="Müşteri yazınca otomatik cevap döner, menü gösterir, yönlendirir"
          howTo="Flow Builder > Yeni Akış > Şablondan seç veya sıfırdan oluştur"
          when="İlk gün"
        />
        <FeatureBlock
          title="Bilgi Bankasi + AI FAQ"
          problem="Hep aynı soruları soruyorlar, tek tek cevaplıyorum"
          solution="Sık sorulan soruları bir kere yaz, AI otomatik cevaplar"
          howTo="Bilgi Bankası > Yeni İçerik > Soru-cevap ekle"
          when="İlk hafta"
        />
      </div>

      <div className="space-y-3">
        <div className="flex items-center gap-2 mb-1">
          <Badge variant="warning">2. Öncelik — İkinci Hafta</Badge>
        </div>
        <FeatureBlock
          title="Analizler"
          problem="Kimin ne sorduğunu, kaç mesaj geldiğini takip edemiyorum"
          solution="Dashboard'dan mesaj trendlerini, otomatik cevap oranını gör"
          howTo="Analizler sayfasına git, tarih aralığıyla filtrele"
          when="2. hafta (veri birikince)"
        />
        <FeatureBlock
          title="Kampanyalar (Outbound)"
          problem="Müşterilere toplu mesaj gönderemiyorum, tek tek uğraşamam"
          solution="Kişi listesine toplu mesaj gönder, teslim durumunu takip et"
          howTo="Kampanyalar > Yeni Kampanya > Şablon seç > Gönder"
          when="2. hafta"
        />
        <FeatureBlock
          title="Dallanma (Condition/Switch)"
          problem="Her müşteri farklı şey soruyor, tek cevap yetmiyor"
          solution="Müşterinin ne istediğine göre farklı yollara yönlendir"
          howTo="Akış içerisine Condition veya Switch node'u ekle"
          when="2. hafta (temel akış öğrenildikten sonra)"
        />
      </div>

      <div className="space-y-3">
        <div className="flex items-center gap-2 mb-1">
          <Badge variant="info">3. Öncelik — Üçüncü Hafta+</Badge>
        </div>
        <FeatureBlock
          title="Randevular"
          problem="Randevu almak zahmetli, telefonla uğraşıyorum"
          solution="Müşteri WhatsApp'tan randevu alır, otomatik hatırlatma gider"
          howTo="Ayarlar > Randevular modülü aç > Akışa randevu node'u ekle"
          when="Sektör uygunsa (klinik, kuaför, restoran)"
        />
        <FeatureBlock
          title="Marketing — Referral"
          problem="Müşterilerim beni tavsiye etmiyor, organik büyüme yok"
          solution="Referans linki oluştur, paylaşıldığında takip et"
          howTo="Pazarlama > Referanslar > Yeni link oluştur"
          when="Aktif müşteri tabanı olduğunda"
        />
        <FeatureBlock
          title="Marketing — Review Isteme"
          problem="Google/sosyal medya yorumlarım az"
          solution="Mutlu müşteri tespit et, otomatik yorum isteği gönder"
          howTo="Pazarlama > Yorumlar > Yorum iste"
          when="Hizmet sektörü için"
        />
        <FeatureBlock
          title="AI Duygu Analizi (Sentiment)"
          problem="Müşteri kızgın mı memnun mu anlamıyorum"
          solution="AI mesajın tonunu analiz eder, kızgın müşterileri işaretle"
          howTo="Akış içinde Sentiment node'u ekle veya Analytics'ten gör"
          when="Yüksek mesaj hacmi olduğunda"
        />
        <FeatureBlock
          title="Entegrasyonlar (API Call)"
          problem="Dış sistemlerle bağlantıyı manuel yapıyorum"
          solution="Akış içinden API çağırarak dış sisteme veri gönder/al"
          howTo="Akış içinde API Call node'u ekle, endpoint ve parametreleri ayarla"
          when="Teknik müşteri ve özel entegrasyon gerektiğinde"
        />
      </div>

      <Tip>
        Her özellik için basit bir kural: Müşteri "buna ihtiyacım var" demeden açma.
        Açılmamış özellik kafa karıştırmaz, açılıp kullanılmayan özellik karıştırır.
        Mevcut müşterilere yeni özellik önerme zamanlama stratejisi için <TabLink to="saas">SaaS Stratejisi: Upsell</TabLink> bolumune bak.
      </Tip>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">Ilgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="sectors">Sektör Senaryoları</TabLink>
          <TabLink to="onboarding">Onboarding Adimlari</TabLink>
          <TabLink to="saas">SaaS Stratejisi</TabLink>
        </div>
      </Card>
    </div>
  );
}

function SectorsTab() {
  return (
    <div className="space-y-6">
      <div>
        <SectionTitle icon={Building2}>Sektöre Göre Özellik Haritalaması</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Her müşteri her özelliği kullanmayacak. Sektöre göre öncelikli modülleri aç,
          gereksiz olanları kapalı tut.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <SectorCard
          icon={Utensils}
          title="Restoran / Kafe"
          primary={['Flow Builder', 'Outbound', 'FAQ']}
          secondary={['Appointments', 'Marketing', 'Analytics']}
          firstFlow="'Merhaba! Menümüzü görmek ister misiniz?' + günün özel menüsü + rezervasyon yönlendirme"
        />
        <SectorCard
          icon={Stethoscope}
          title="Klinik / Saglik"
          primary={['Flow Builder', 'Appointments', 'FAQ']}
          secondary={['Analytics', 'Outbound', 'Sentiment']}
          firstFlow="'Randevu almak için 1, doktor bilgisi için 2 yazın' + otomatik hatırlatma"
        />
        <SectorCard
          icon={ShoppingCart}
          title="E-Ticaret"
          primary={['Flow Builder', 'Outbound', 'Analytics']}
          secondary={['Marketing', 'Sentiment', 'Integrations']}
          firstFlow="'Siparişinizi takip etmek için sipariş numaranızı yazın' + kampanya bildirimi"
        />
        <SectorCard
          icon={Scissors}
          title="Hizmet (Kuafor, Berber, Oto Yikama)"
          primary={['Flow Builder', 'Appointments', 'Marketing']}
          secondary={['Outbound', 'Analytics', 'FAQ']}
          firstFlow="'Randevu için gün ve saat seçin' + hatırlatma + hizmet sonrası yorum isteme"
        />
        <SectorCard
          icon={Plane}
          title="Turizm / Otel"
          primary={['Flow Builder', 'FAQ', 'Outbound']}
          secondary={['Marketing', 'Analytics', 'Appointments']}
          firstFlow="'Odalarımız ve fiyatlarımız...' + yardımcı yönlendirme + kampanya bildirimi"
        />
        <SectorCard
          icon={Building2}
          title="Genel Hizmet / Kurumsal"
          primary={['Flow Builder', 'FAQ', 'Analytics']}
          secondary={['Outbound', 'Marketing', 'Sentiment']}
          firstFlow="'Size nasıl yardımcı olabiliriz?' + departmana yönlendirme + FAQ"
        />
      </div>

      <Warning>
        Sektör şablon akışları "örnek" olarak kullan, her müşteriye birebir kopyalama.
        Her işletmenin kendine özgü soruları ve süreçleri var. Template'i başlangıç noktası yap,
        müşteri ile birlikte kişiselleştir. <TabLink to="saas">SaaS Stratejisi</TabLink> sekmesinde ölçeklenebilir
        onboarding için template flow + FAQ paketi sistemini detaylı anlatıyoruz.
      </Warning>

      <div>
        <SectionTitle icon={Target}>Feature Flag Stratejisi</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">
            Tenant oluştururken sektöre göre aşağıdaki flag'leri aç:
          </p>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Modul</th>
                  <th className="text-center py-2 px-2 font-medium text-navy-500">Hafta 1</th>
                  <th className="text-center py-2 px-2 font-medium text-navy-500">Hafta 2</th>
                  <th className="text-center py-2 px-2 font-medium text-navy-500">Hafta 3+</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">FlowBuilder</td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Knowledge</td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Analytics</td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Outbound</td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="success">Ac</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Marketing</td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="warning">İhtiyaca göre</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Appointments</td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="warning">Sektöre göre</Badge></td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">Integrations</td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="warning">Teknik müşteri</Badge></td>
                </tr>
              </tbody>
            </table>
          </div>
        </Card>
      </div>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">Ilgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="features">Özellik Rehberi</TabLink>
          <TabLink to="onboarding">Onboarding Adimlari</TabLink>
          <TabLink to="saas">SaaS Stratejisi (Ölçekleme)</TabLink>
        </div>
      </Card>
    </div>
  );
}

function TemplateCard({ label, variant, channel, content }: {
  label: string;
  variant: 'info' | 'success' | 'warning' | 'error' | 'default';
  channel: 'whatsapp' | 'email';
  content: string;
}) {
  const [copied, setCopied] = useState(false);
  const handleCopy = () => {
    navigator.clipboard.writeText(content);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };
  const isEmail = channel === 'email';
  return (
    <Card className="p-4">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          <Badge variant={variant}>{label}</Badge>
          <Badge variant={isEmail ? 'warning' : 'success'}>{isEmail ? 'Email' : 'WhatsApp'}</Badge>
        </div>
        <button
          onClick={handleCopy}
          className="inline-flex items-center gap-1 px-2 py-1 rounded-md text-xs text-navy-400 hover:bg-navy-50 hover:text-navy-600 transition-colors"
          title="Kopyala"
        >
          {copied ? <Check className="w-3 h-3 text-emerald-500" /> : <Copy className="w-3 h-3" />}
          {copied ? 'Kopyalandı' : 'Kopyala'}
        </button>
      </div>
      <div className={cn(
        'p-3 rounded-lg text-sm whitespace-pre-line',
        isEmail ? 'bg-amber-50/50 text-navy-700 border border-amber-100/50' : 'bg-navy-50 text-navy-700'
      )}>
        {content}
      </div>
    </Card>
  );
}

function CommunicationTab() {
  return (
    <div className="space-y-6">
      <div>
        <SectionTitle icon={MessageCircle}>Müşteri İletişim Rehberi</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Özellikleri nasıl anlatırsanız o kadar etkili olur. Aşağıda her durum için hazır şablonlar var.
          Kopyala butonuyla direkt alıp kişiselleştirebilirsin.
          Hangi müşteriye ne zaman mesaj atacağını <TabLink to="saas">SaaS Stratejisi: Health Score</TabLink> ile belirle.
        </p>
      </div>

      <div>
        <h4 className="text-sm font-semibold text-navy-900 mb-3">Yeni Özellik Duyurma Formatı</h4>
        <Card className="p-4 space-y-4">
          <div>
            <div className="flex items-center gap-2 mb-2">
              <Badge variant="error">Yanlis</Badge>
            </div>
            <div className="p-3 bg-red-50 border border-red-100 rounded-lg text-sm text-navy-600 italic">
              "v2.3 çıktı! 14 yeni özellik, 47 bug fix, 8 improvement. Detaylar için changelog'a bakın..."
            </div>
          </div>
          <div>
            <div className="flex items-center gap-2 mb-2">
              <Badge variant="success">Dogru</Badge>
            </div>
            <div className="p-3 bg-emerald-50 border border-emerald-100 rounded-lg text-sm text-navy-600 italic">
              "Artık müşteriniz randevu aldığında otomatik hatırlatma mesajı gidiyor.
              Açmak için: Ayarlar &gt; Randevular &gt; Hatırlatma &gt; Aç. 3 adım, 1 dakika."
            </div>
          </div>
          <Tip>
            <strong>Format:</strong> Problem → Çözüm → Nasıl (max 3 adım).
            Teknik detay yok. Müşteri ne kazanıyor, o kadar.
          </Tip>
        </Card>
      </div>

      {/* ─── WhatsApp Sablonları ───────────────────────────── */}

      <div>
        <div className="flex items-center gap-2 mb-3">
          <h4 className="text-sm font-semibold text-navy-900">WhatsApp Mesaj Sablonlari</h4>
          <Badge variant="success">8 Şablon</Badge>
        </div>
        <div className="space-y-3">
          <TemplateCard
            label="İlk Gün — Hoşgeldiniz"
            variant="info"
            channel="whatsapp"
            content={`Merhaba [Firma Adi]!

Chatinbox sisteminiz hazır. İlk otomatik cevabınız aktif.

Şimdi yapmanız gereken:
1. Telefonunuzdan [numara] numarasina "merhaba" yazin
2. Otomatik cevabı görün
3. Değiştirmek isterseniz beni arayın

Panel giriş bilgileriniz:
Adres: [URL]
Kullanici: [user]
Sifre: [pass]

Herhangi bir sorunuz olursa yazın!`}
          />

          <TemplateCard
            label="3. Gün — Kontrol"
            variant="info"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

Sisteminiz 3 gündür aktif. Her şey yolunda mı?

Simdiye kadar:
- [X] mesaj geldi
- [Y] tanesi otomatik cevaplandi

Sorun veya soru varsa hemen yazın, birlikte çözelim.`}
          />

          <TemplateCard
            label="Hafta Sonu — Takip"
            variant="info"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

İlk haftanız nasıl geçti? Hızlı bir özet:
- Bu hafta [X] mesaj geldi
- [Y] tanesi otomatik cevaplandi
- En çok sorulan konu: [konu]

Eklemek istediğiniz soru/cevap var mı?
Değiştirmek istediğiniz bir şey varsa yazın, hemen ayarlayalım.`}
          />

          <TemplateCard
            label="Yeni Özellik Duyurusu"
            variant="info"
            channel="whatsapp"
            content={`Merhaba [Firma Adi]!

Yeni özellik: [tek cümle açıklama].

Ne işe yarar: [1 cümle — sorun ve çözüm].
Nasıl açılır: [max 3 adım].

Birlikte kuralım mı? Müsait olduğunuz bir zaman yazın.`}
          />

          <TemplateCard
            label="Aylık Rapor"
            variant="info"
            channel="whatsapp"
            content={`[Firma Adı] — [Ay] Performans Özeti

Toplam mesaj: [X]
Otomatik cevaplanan: [Y] (%[Z])
En çok sorulan: [konu1], [konu2], [konu3]
Kampanya gönderilen: [varsa]

Önerimiz: "[öneri — örneğin yeni FAQ eklemek]"
Bu ay [yeni özellik] çıktı, sizin için uygun olabilir.

Detaylı görmek için panelinize girin: [URL]`}
          />

          <TemplateCard
            label="Ödeme Hatırlatma"
            variant="warning"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

[Ay] dönemi için faturanız oluşturulmuştur.

Tutar: [tutar] TL
Son ödeme tarihi: [tarih]

Ödeme bilgileri:
IBAN: [IBAN]
Açıklama: [firma kodu]

Sorunuz varsa yazabilirsiniz.
İyi çalışmalar dileriz!`}
          />

          <TemplateCard
            label="Churn Riski — Yeniden Bağlantı"
            variant="error"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

Bir süredir panelinize girmediğinizi fark ettik. Umarız her şey yolundadır.

Son 30 günde sisteminiz sizin için çalışmaya devam etti:
- [X] mesaj otomatik cevaplandı
- [Y] müşteri yönlendirildi

Kullanmakta zorluk yaşıyorsanız size özel 15 dakikalık bir görüşme ayarlayalım.
Hiçbir şey sormak zorunda değilsiniz — sadece "uygun" yazın, ben ararım.`}
          />

          <TemplateCard
            label="Sözleşme Yenileme"
            variant="default"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

Sözleşmenizin yenileme zamanı yaklaştı ([tarih]).

Son [dönem] boyunca:
- Toplam [X] mesaj işlendi
- %[Y] otomatik cevap orani
- Tahmini tasarruf: [Z] saat/ay

Yenileme için herhangi bir işlem yapmanıza gerek yok, otomatik devam eder.
Soru veya değişiklik isteği varsa yazın.`}
          />
        </div>
      </div>

      {/* ─── Email Sablonları ─────────────────────────────── */}

      <div>
        <div className="flex items-center gap-2 mb-3">
          <h4 className="text-sm font-semibold text-navy-900">Email Sablonlari</h4>
          <Badge variant="warning">6 Şablon</Badge>
        </div>
        <Tip>
          Email şablonları daha resmi ve detaylıdır. Kurumsal müşteriler veya
          resmi yazışma gerektiren durumlarda kullan. WhatsApp kısa ve samimi,
          email profesyonel ve dökümante edici olmalı.
        </Tip>
        <div className="space-y-3 mt-3">
          <TemplateCard
            label="Hoşgeldiniz — Kurulum Tamam"
            variant="info"
            channel="email"
            content={`Konu: Chatinbox Sisteminiz Hazır — Giriş Bilgileriniz

Merhaba [Yetkili Adi],

[Firma Adı] için Chatinbox müşteri iletişim sisteminiz başarıyla kurulmuştur.

Sistem Bilgileri:
- Panel Adresi: [URL]
- Kullanici Adi: [user]
- Şifre: [pass] (ilk girişte değiştirmenizi öneririz)
- WhatsApp Hatti: [numara]

İlk Adımlar:
1. Telefonunuzdan [numara] numarasına "merhaba" yazarak sistemi test edin
2. Panelden Bilgi Bankası'na sık sorulan sorularınızı ekleyin
3. Ayarlar > Çalışma Saatleri bölümünden mesai saatlerinizi belirleyin

Kurulum sırasında oluşturduğumuz otomatik cevap akışınız aktiftir.
Herhangi bir değişiklik veya ekleme ihtiyacınız olursa benimle iletişime geçin.

İyi çalışmalar,
[Isim]
Chatinbox Destek Ekibi
[Telefon]`}
          />

          <TemplateCard
            label="Haftalik Performans Raporu"
            variant="info"
            channel="email"
            content={`Konu: [Firma Adı] — Haftalık Chatinbox Raporu ([tarih aralığı])

Merhaba [Yetkili Adi],

Geçtiğimiz haftaya ait Chatinbox performans özetiniz:

MESAJ İSTATİSTİKLERİ
- Gelen mesaj: [X]
- Otomatik cevaplanan: [Y] (%[oran])
- İnsana yönlendirilen: [Z]
- Ortalama cevap süresi: [süre]

EN ÇOK SORULAN KONULAR
1. [Konu 1] — [adet] kez
2. [Konu 2] — [adet] kez
3. [Konu 3] — [adet] kez

ÖNERİMİZ
[Konu 1] ile ilgili bilgi bankasına şu cevabı eklersek otomatik karşılama oranı %[X] artabilir:
"[onerilen cevap]"

Bu değişikliği yapmamı ister misiniz? Tek kelimeyle "evet" yazmanız yeterli.

Detaylı istatistikler için paneliniz: [URL]

İyi çalışmalar,
[Isim]`}
          />

          <TemplateCard
            label="Aylık Performans Raporu"
            variant="info"
            channel="email"
            content={`Konu: [Firma Adı] — [Ay] Aylık Performans Raporu

Merhaba [Yetkili Adi],

[Ay] ayına ait Chatinbox performans raporunuz:

GENEL BAKIŞ
- Toplam mesaj: [X]
- Otomatik cevap oranı: %[Y]
- İnsana yönlendirme: [Z] mesaj
- Ortalama ilk cevap süresi: [süre]

ÖNCEKİ AY KARŞILAŞTIRMA
- Mesaj hacmi: [artış/azalış]% [yönü]
- Otomasyon oranı: [önceki]% → [şimdiki]%
- Yönlendirme: [önceki] → [şimdiki]

KAMPANYA SONUÇLARI (varsa)
- Gonderilen: [X]
- Teslim edilen: [Y]
- Cevap alan: [Z]

EN ÇOK SORULAN 5 KONU
1. [Konu] — [adet] kez
2. [Konu] — [adet] kez
3. [Konu] — [adet] kez
4. [Konu] — [adet] kez
5. [Konu] — [adet] kez

TAHMİNİ TASARRUF
Otomatik cevaplanan [Y] mesaj, ortalama [A] dakika/mesaj hesabıyla ayda yaklaşık [B] saat iş gücünüz tasarruf edilmiştir.

ÖNERİLER
1. [Öneri 1 — örneğin eksik FAQ]
2. [Öneri 2 — örneğin yeni modül açılımı]

Detaylı panele giriş: [URL]
Bir sonraki görüşmemiz: [tarih/saat]

İyi çalışmalar,
[Isim]
Chatinbox Destek Ekibi`}
          />

          <TemplateCard
            label="Yeni Özellik Duyurusu"
            variant="info"
            channel="email"
            content={`Konu: Yeni Özellik — [Özellik Adı] Artık Kullanıma Hazır

Merhaba [Yetkili Adi],

Chatinbox sisteminize yeni bir özellik eklendi: [Özellik Adı].

NE İŞE YARAR?
[1-2 cümle: hangi sorunu çözer, ne kolaylaştırıyor]

SİZİN İÇİN NE DEĞİŞİR?
[1-2 cümle: somut fayda, örnek senaryo]

NASIL ETKİNLEŞTİRİLİR?
1. [Adim 1]
2. [Adim 2]
3. [Adim 3]

Bu özelliği sizin için etkinleştirmemi ister misiniz?
Dilediğiniz zaman dönüş yapabilirsiniz — birlikte ayarlayabiliriz.

İyi çalışmalar,
[Isim]`}
          />

          <TemplateCard
            label="Ödeme / Fatura"
            variant="warning"
            channel="email"
            content={`Konu: [Firma Adı] — [Ay] Dönemi Fatura Bildirimi

Merhaba [Yetkili Adi],

[Ay] dönemi için Chatinbox hizmet faturanız oluşturulmuştur.

FATURA DETAYLARI
- Dönem: [başlangıç] — [bitiş]
- Tutar: [tutar] TL (KDV dahil)
- Son ödeme tarihi: [tarih]
- Fatura no: [numara]

ÖDEME BİLGİLERİ
Banka: [banka]
IBAN: [IBAN]
Hesap Adı: [hesap adı]
Açıklama: [firma kodu] — [ay]

Faturanız ekte yer almaktadır.
Ödeme sonrası dekont göndermenize gerek yoktur — otomatik eşleştirme yapılmaktadır.

Sorunuz varsa dönüş yapın.

İyi çalışmalar,
[Isim]`}
          />

          <TemplateCard
            label="Sözleşme Yenileme"
            variant="default"
            channel="email"
            content={`Konu: [Firma Adı] — Sözleşme Yenileme Bildirimi

Merhaba [Yetkili Adi],

Chatinbox hizmet sözleşmenizin süresi [tarih] tarihinde dolmaktadır.

GEÇEN DÖNEM ÖZETİ
- Toplam işlenen mesaj: [X]
- Otomatik cevap orani: %[Y]
- Kullanılan modüller: [modül listesi]
- Tahmini tasarruf: [Z] saat/ay

YENİLEME KOŞULLARI
- Dönem: [başlangıç] — [bitiş]
- Aylık ücret: [tutar] TL
- Değişiklik: [varsa belirt / "mevcut koşullar geçerlidir"]

Herhangi bir işlem yapmanıza gerek yoktur — sözleşmeniz [tarih] itibarıyla otomatik olarak yenilenir.

Modül ekleme/çıkarma veya plan değişikliği için [tarih]'e kadar bize bildirebilirsiniz.

İyi çalışmalar,
[Isim]
Chatinbox Destek Ekibi`}
          />
        </div>
      </div>

      {/* ─── İletişim Takvimi ─────────────────────────────── */}

      <div>
        <h4 className="text-sm font-semibold text-navy-900 mb-3">İletişim Takvimi</h4>
        <Card className="p-4">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Zaman</th>
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Ne Yap</th>
                  <th className="text-left py-2 font-medium text-navy-500">Kanal</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">İlk gün</td>
                  <td className="py-2 pr-4">Hoşgeldiniz mesajı + giriş bilgileri</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="success">WhatsApp</Badge>
                      <Badge variant="warning">Email</Badge>
                    </div>
                  </td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">3. gün</td>
                  <td className="py-2 pr-4">"Nasıl gidiyor?" kontrolü</td>
                  <td className="py-2"><Badge variant="success">WhatsApp</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">7. gün</td>
                  <td className="py-2 pr-4">Haftalık rapor + takip görüşmesi</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="warning">Email</Badge>
                      <Badge variant="info">Telefon</Badge>
                    </div>
                  </td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">14. gün</td>
                  <td className="py-2 pr-4">Yeni modül açılımları + eğitim</td>
                  <td className="py-2"><Badge variant="success">WhatsApp</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Her hafta</td>
                  <td className="py-2 pr-4">Haftalık performans raporu</td>
                  <td className="py-2"><Badge variant="warning">Email</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Her ay</td>
                  <td className="py-2 pr-4">Aylık performans raporu + fatura</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="warning">Email</Badge>
                      <Badge variant="success">WhatsApp</Badge>
                    </div>
                  </td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Yeni özellik</td>
                  <td className="py-2 pr-4">Duyuru + nasıl açılır</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="success">WhatsApp</Badge>
                      <Badge variant="warning">Email</Badge>
                    </div>
                  </td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Ödeme zamanı</td>
                  <td className="py-2 pr-4">Fatura + ödeme bilgileri</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="warning">Email</Badge>
                      <Badge variant="success">WhatsApp</Badge>
                    </div>
                  </td>
                </tr>
                <tr>
                  <td className="py-2 pr-4 font-medium">Sözleşme yenileme</td>
                  <td className="py-2 pr-4">Dönem özeti + yenileme bildirimi</td>
                  <td className="py-2"><Badge variant="warning">Email</Badge></td>
                </tr>
              </tbody>
            </table>
          </div>
        </Card>
      </div>

      {/* ─── Kanal Seçim Rehberi ──────────────────────────── */}

      <div>
        <h4 className="text-sm font-semibold text-navy-900 mb-3">Hangi Kanal Ne Zaman?</h4>
        <Card className="p-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <MessageCircle className="w-4 h-4 text-emerald-500" />
                <span className="text-sm font-semibold text-navy-900">WhatsApp Kullan</span>
              </div>
              <ul className="text-sm text-navy-600 space-y-1 pl-6 list-disc">
                <li>Hızlı, kısa bilgilendirmeler</li>
                <li>Günlük/haftalık takip mesajları</li>
                <li>Yeni özellik duyuruları (kısa versiyon)</li>
                <li>Ödeme hatırlatmaları (samimi ton)</li>
                <li>Sorun/şikayet anında hızlı dönüş</li>
                <li>Video ve görsel içerik paylaşımı</li>
              </ul>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <Mail className="w-4 h-4 text-amber-500" />
                <span className="text-sm font-semibold text-navy-900">Email Kullan</span>
              </div>
              <ul className="text-sm text-navy-600 space-y-1 pl-6 list-disc">
                <li>Resmi bildirimler (fatura, sözleşme)</li>
                <li>Detaylı performans raporları</li>
                <li>Giriş bilgileri ve teknik dokümantasyon</li>
                <li>Yeni özellik detaylı açıklamaları</li>
                <li>Kayıt altında olması gereken yazışmalar</li>
                <li>Ek dosya göndermek gerektiğinde</li>
              </ul>
            </div>
          </div>
          <Tip>
            <strong>Altın kural:</strong> Aynı içerik için ikisini birden gönderme.
            WhatsApp kısa özeti, email detaylı versiyonu olsun.
            Örnek: Ödeme için WhatsApp'tan hatırlatma, email'den fatura.
          </Tip>
        </Card>
      </div>

      {/* ─── Kriz Yönetimi ────────────────────────────────── */}

      <div>
        <h4 className="text-sm font-semibold text-navy-900 mb-3">Krizi Dönüştür</h4>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">
            Müşteri "kullanmıyorum, iptal edeceğim" dediğinde:
          </p>
          <ol className="list-decimal pl-5 space-y-2 text-sm text-navy-700">
            <li><strong>Dinle, savunma:</strong> "Hangi konuda zorluk yaşıyorsunuz?"</li>
            <li><strong>Veri göster:</strong> "Geçen ay [X] mesaj otomatik cevaplandı, bu [Y] saat tasarruf demek."</li>
            <li><strong>Basitleştir:</strong> Kullanmadığı modülleri kapat, sadece çalışanları bırak.</li>
            <li><strong>Eğitim teklif et:</strong> "15 dk'lik bir görüşme ile tekrar kuralım, ben yanınızdayım."</li>
            <li><strong>Takip:</strong> 3 gün sonra tekrar sor, iyileşti mi?</li>
          </ol>
          <Warning>
            Asla "ama çok iyi özelliklerimiz var" deme.
            Müşterinin sorununu çöz, özellik listesi sayma.
            Churn sinyallerini erken fark etmek icin <TabLink to="saas">SaaS Stratejisi: Churn Sinyalleri</TabLink> bolumunu oku.
          </Warning>
        </Card>
      </div>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">Ilgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="onboarding">Onboarding Adimlari</TabLink>
          <TabLink to="saas">SaaS Stratejisi</TabLink>
          <TabLink to="actions">Aksiyon Listesi</TabLink>
        </div>
      </Card>
    </div>
  );
}

function SaasTab() {
  return (
    <div className="space-y-6">
      <div>
        <SectionTitle icon={TrendingUp}>SaaS Büyüme Stratejisi</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Teknik olarak ürünü yazmak işin yarısı. Diğer yarısı: fiyatlandırma, müşteri sağlığı,
          churn önleme, upsell ve ölçekleme. Aşağıdaki stratejiler gerçek SaaS deneyiminden geliyor.
        </p>
      </div>

      {/* ─── 1. Fiyatlandırma ─────────────────────────────── */}

      <div>
        <SectionTitle icon={DollarSign}>1. Fiyatlandırma: Değer Bazlı Paketleme</SectionTitle>
        <Card className="p-4 space-y-4">
          <div>
            <h4 className="text-sm font-semibold text-navy-900 mb-2">Modül Satma, Sonuç Sat</h4>
            <div className="space-y-2 text-sm">
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"Flow Builder 500, Analytics 200, Marketing 300..."</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-emerald-700 font-medium">"Başlangıç: Mesajlara otomatik cevap / Büyüme: Müşteriyi geri getir / Pro: Tam CRM"</span>
              </div>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Paket</th>
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Icerik</th>
                  <th className="text-left py-2 font-medium text-navy-500">Hedef Müşteri</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4"><Badge variant="default">Başlangıç</Badge></td>
                  <td className="py-2 pr-4">Flow + FAQ + Çalışma Saatleri</td>
                  <td className="py-2">"Mesajlara yetişemiyorum"</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4"><Badge variant="info">Büyüme</Badge></td>
                  <td className="py-2 pr-4">+ Analytics + Outbound + Kampanya</td>
                  <td className="py-2">"Müşterileri geri getirmek istiyorum"</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4"><Badge variant="success">Profesyonel</Badge></td>
                  <td className="py-2 pr-4">+ Marketing + Appointments + Sentiment + Integrations</td>
                  <td className="py-2">"Tam CRM istiyorum"</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div className="p-3 bg-emerald-50 border border-emerald-100 rounded-lg">
            <h5 className="text-sm font-semibold text-emerald-800 mb-1">Yıllık Ödeme İndirimi</h5>
            <p className="text-sm text-emerald-700">
              Aylık x12 yerine 10 aylık ücret al (2 ay bedava). Churn'u <strong>%30-40 düşürür</strong> çünkü
              müşteri 12 ay boyunca çıkmaz ve o sürede alışır. Bu mevcut <TabLink to="sectors">feature flag stratejisi</TabLink> ile uyumlu çalışıyor.
            </p>
          </div>

          <Warning>
            Müşteri "bu kadar para neden ödüyorum?" diye sorduğunda, <TabLink to="communication">aylık rapor şablonu</TabLink> ile
            somut tasarruf rakamlarını göster.
          </Warning>
        </Card>
      </div>

      {/* ─── 2. Müşteri Sağlık Skoru ─────────────────────── */}

      <div>
        <SectionTitle icon={Activity}>2. Müşteri Sağlık Skoru (Health Score)</SectionTitle>
        <Card className="p-4 space-y-4">
          <p className="text-sm text-navy-600">
            Bir müşterinin "iyi" mi "kötü" mü olduğunu hislere bırakma, veriyle ölç. Basit bir formül:
          </p>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Sinyal</th>
                  <th className="text-center py-2 px-2 font-medium text-navy-500 w-20">Agirlik</th>
                  <th className="text-left py-2 font-medium text-navy-500">Olcum</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Son 7 günde panel girişi var mı?</td>
                  <td className="text-center py-2 px-2">%25</td>
                  <td className="py-2">Evet=100, Hayır=0</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Bu hafta flow çalıştı mı?</td>
                  <td className="text-center py-2 px-2">%25</td>
                  <td className="py-2">Evet=100, Hayir=0</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Otomatik cevap orani {'>'} %50 mi?</td>
                  <td className="text-center py-2 px-2">%20</td>
                  <td className="py-2">Evet=100, Hayir=0</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">FAQ sayisi {'>'} 5 mi?</td>
                  <td className="text-center py-2 px-2">%15</td>
                  <td className="py-2">Evet=100, Hayir=0</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">Son 30 günde destek talebi var mı?</td>
                  <td className="text-center py-2 px-2">%15</td>
                  <td className="py-2">Evet=50, Hayır=100</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="p-3 bg-emerald-50 rounded-lg border border-emerald-100 text-center">
              <div className="text-lg font-bold text-emerald-700">80-100</div>
              <div className="text-xs text-emerald-600">Sağlıklı</div>
              <div className="text-xs text-navy-400 mt-0.5">Upsell zamanı</div>
            </div>
            <div className="p-3 bg-amber-50 rounded-lg border border-amber-100 text-center">
              <div className="text-lg font-bold text-amber-700">60-79</div>
              <div className="text-xs text-amber-600">Dikkat</div>
              <div className="text-xs text-navy-400 mt-0.5">Takip mesajı gönder</div>
            </div>
            <div className="p-3 bg-red-50 rounded-lg border border-red-100 text-center">
              <div className="text-lg font-bold text-red-700">0-59</div>
              <div className="text-xs text-red-600">Risk</div>
              <div className="text-xs text-navy-400 mt-0.5">Hemen ara, <TabLink to="communication">churn şablonunu</TabLink> kullan</div>
            </div>
          </div>

          <Tip>
            Bu veriler zaten DB'de var (login tarihi, flow execution, FAQ sayısı). Bir endpoint + dashboard widget'ı yeter.
            <TabLink to="actions">Aksiyon listesinde</TabLink> bu madde mevcut.
          </Tip>
        </Card>
      </div>

      {/* ─── 3. Aha Moment ───────────────────────────────── */}

      <div>
        <SectionTitle icon={Zap}>3. "Aha Moment" Hızını Ölç</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            SaaS'in en kritik metriği: müşteri ilk değeri ne kadar hızlı gördü?
          </p>
          <div className="p-3 bg-brand-50 border border-brand-100 rounded-lg">
            <p className="text-sm text-brand-800 font-medium">
              Chatinbox için "aha moment" = Müşterinin telefonundan gönderdiği mesaja otomatik cevap dönmesi.
            </p>
          </div>
          <div className="space-y-2 text-sm text-navy-700">
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span><strong>Ölç:</strong> Tenant oluşturma → ilk flow aktif olma süresi</span>
            </div>
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span><strong>Ölç:</strong> İlk flow aktif → ilk otomatik cevap süresi</span>
            </div>
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span><strong>Hedef:</strong> İlk 1 saatte aha moment. 24 saatten uzunsa kayıp riski çok yüksek.</span>
            </div>
          </div>
          <Tip>
            <TabLink to="onboarding">Onboarding adimlari</TabLink> bu hedefe gore tasarlandi:
            Adım 5 (ilk akış) + Adım 6 (canlı test) = aha moment.
          </Tip>
        </Card>
      </div>

      {/* ─── 4. Ölçekleme ────────────────────────────────── */}

      <div>
        <SectionTitle icon={Users}>4. Onboarding'i Ölçeklendir</SectionTitle>
        <Card className="p-4 space-y-4">
          <p className="text-sm text-navy-600">
            Şu anda her müşteriye bizzat kurulum yapıyorsun. Bu 5 müşteriye kadar çalışır, 50'de çöker.
          </p>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Şimdi (1-10)</th>
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Yakında (10-50)</th>
                  <th className="text-left py-2 font-medium text-navy-500">Ölçekte (50+)</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Her müşteriye bizzat kurulum</td>
                  <td className="py-2 pr-4">Template flow + self-service setup</td>
                  <td className="py-2">Tamamen self-service + otomatik onboarding</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">WhatsApp'tan takip</td>
                  <td className="py-2 pr-4">Otomatik takip mesajlari (Outbound)</td>
                  <td className="py-2">In-app guided tour + email dizisi</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">Manuel FAQ girişi</td>
                  <td className="py-2 pr-4">Sektör bazlı hazır FAQ paketleri</td>
                  <td className="py-2">AI ile web sitesinden FAQ çekme</td>
                </tr>
              </tbody>
            </table>
          </div>

          <Warning>
            Acil: <TabLink to="sectors">Sektör Senaryoları</TabLink> sekmesindeki her sektör için
            template flow + FAQ paketi oluştur. Yeni tenant açılırken "sektör seç" → hazır set otomatik yüklensin.
          </Warning>
        </Card>
      </div>

      {/* ─── 5. Churn Sinyalleri ─────────────────────────── */}

      <div>
        <SectionTitle icon={AlertTriangle}>5. Churn'un 3 Sinyali</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">Veri olmadan bile anlayabilirsin:</p>

          <div className="space-y-3">
            <div className="flex items-start gap-3 p-3 bg-red-50 border border-red-100 rounded-lg">
              <div className="w-8 h-8 rounded-lg bg-red-100 flex items-center justify-center flex-shrink-0">
                <span className="text-red-700 font-bold text-sm">1</span>
              </div>
              <div>
                <h5 className="text-sm font-semibold text-red-800">Sessizlik: 2 haftadır panele girmemiş</h5>
                <p className="text-xs text-red-700 mt-0.5">
                  Yapılacak: <TabLink to="communication">Churn riski şablonunu</TabLink> gönder. "Sisteminiz sizin için [X] mesaj cevapladı" de, "Neden girmiyorsunuz?" deme.
                </p>
              </div>
            </div>
            <div className="flex items-start gap-3 p-3 bg-amber-50 border border-amber-100 rounded-lg">
              <div className="w-8 h-8 rounded-lg bg-amber-100 flex items-center justify-center flex-shrink-0">
                <span className="text-amber-700 font-bold text-sm">2</span>
              </div>
              <div>
                <h5 className="text-sm font-semibold text-amber-800">Şikayet artışı: "Çalışmıyor", "Anlamadım", "Zor"</h5>
                <p className="text-xs text-amber-700 mt-0.5">
                  Yapılacak: Hemen basitleştir. Modülleri kapa, akışı sadece temel <TabLink to="features">ozelliklerle</TabLink> bırak.
                </p>
              </div>
            </div>
            <div className="flex items-start gap-3 p-3 bg-navy-50 border border-navy-100 rounded-lg">
              <div className="w-8 h-8 rounded-lg bg-navy-100 flex items-center justify-center flex-shrink-0">
                <span className="text-navy-700 font-bold text-sm">3</span>
              </div>
              <div>
                <h5 className="text-sm font-semibold text-navy-800">Ödeme gecikmesi: Fatura 1 haftadır ödenmemiş</h5>
                <p className="text-xs text-navy-600 mt-0.5">
                  Yapılacak: Direkt fatura konusu açma. "Nasılsınız, bir sorun var mı?" de, fatura kendiliğinden açılır.
                </p>
              </div>
            </div>
          </div>

          <Tip>Churn oluştuktan sonra müdahale etmek çok geç. Sinyal gördüğünde hareket et, bekleme.</Tip>
        </Card>
      </div>

      {/* ─── 6. Upsell ───────────────────────────────────── */}

      <div>
        <SectionTitle icon={Repeat}>6. Upsell: Mevcut Müşteriden Daha Fazla Gelir</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            Yeni müşteri kazanmak, mevcut müşteriye satmaktan <strong>5-7 kat daha pahalı</strong>.
          </p>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Tetikleyici</th>
                  <th className="text-left py-2 font-medium text-navy-500">Upsell Firsati</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Aylık mesaj hacmi 500'u geçti</td>
                  <td className="py-2">"Hacminiz arttı, Analytics ile trendleri görebilirsiniz"</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Flow'da 3+ dallanma var</td>
                  <td className="py-2">"AI Intent ile otomatik dallandırma yapabiliriz"</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">FAQ'dan cok cevap donuyor</td>
                  <td className="py-2">"Knowledge Base genişletip Sentiment ile memnuniyeti ölçelim"</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">"Toplu mesaj atabilir miyim?" diye sordu</td>
                  <td className="py-2">Outbound modulunu ac — tam zamani</td>
                </tr>
              </tbody>
            </table>
          </div>

          <Tip>
            <strong>Zamanlama:</strong> <TabLink to="communication">Aylık rapor</TabLink> gönderdikten sonra.
            "Bu ay 500 mesaj geldi, %60'i otomatik cevaplandi. Analytics ile bu orani %80'e cikarabiliriz." — veri + öneri + somut hedef.
          </Tip>
        </Card>
      </div>

      {/* ─── 7. Referral ─────────────────────────────────── */}

      <div>
        <SectionTitle icon={UserPlus}>7. Referral: Müşteri Müşteri Getirsin</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">En güçlü satış kanalı: memnun müşterinin tavsiyesi.</p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="p-3 bg-brand-50 rounded-lg border border-brand-100">
              <h5 className="text-sm font-semibold text-brand-800">Getiren Müşteri</h5>
              <p className="text-xs text-brand-700 mt-1">1 ay ücretsiz veya %20 indirim</p>
            </div>
            <div className="p-3 bg-emerald-50 rounded-lg border border-emerald-100">
              <h5 className="text-sm font-semibold text-emerald-800">Gelen Müşteri</h5>
              <p className="text-xs text-emerald-700 mt-1">İlk ay %20 indirim</p>
            </div>
          </div>
          <Tip>
            Ne zaman iste: Health Score 80+ olan müşteriye <TabLink to="communication">aylık rapor</TabLink> sonrasında.
            "Çevrenizdeki işletmelere de önerebilirsiniz, referans linkiniz: [link]"
          </Tip>
        </Card>
      </div>

      {/* ─── 8. Yapışkanlık ──────────────────────────────── */}

      <div>
        <SectionTitle icon={Heart}>8. "Yapışkan" Ürün Yap</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            Müşterinin ayrılma maliyetini "sözleşme cezası" ile değil, <strong>biriken değer</strong> ile yükselt.
          </p>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Biriken Deger</th>
                  <th className="text-left py-2 font-medium text-navy-500">Neden Ayrilamaz</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">50+ FAQ girisi</td>
                  <td className="py-2">Başka yere taşımak zahmetli</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">6 aylık analiz verisi</td>
                  <td className="py-2">Gecmisi kaybeder</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Özelleşmiş 10+ flow</td>
                  <td className="py-2">Sıfırdan kurmak 2 hafta sürer</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">Müşteri alışkanlığı</td>
                  <td className="py-2">"WhatsApp'a yazıyorum, cevap geliyor" refleksi</td>
                </tr>
              </tbody>
            </table>
          </div>
          <Tip>
            İlk aydan FAQ doldurtmaya, flow özelleştirtmeye teşvik et. Ne kadar çok veri girerse, o kadar bağımlı olur.
            <TabLink to="onboarding">Onboarding adim 7</TabLink> (FAQ doldurmak) bu yuzden kritik.
          </Tip>
        </Card>
      </div>

      {/* ─── 9. Rakip Sorusu ─────────────────────────────── */}

      <div>
        <SectionTitle icon={Shield}>9. Rakip Sorusuyla Başa Çık</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">Müşteri "Neden sizi seçeyim?" dediğinde:</p>
          <div className="space-y-2 text-sm">
            <div className="flex items-center gap-3">
              <span className="text-red-500 line-through flex-shrink-0">Rakibi kötüleme, özellik listesi karşılaştırma</span>
            </div>
            <div className="flex items-center gap-3">
              <ArrowRight className="w-3 h-3 text-emerald-500 flex-shrink-0" />
              <span className="text-emerald-700 font-medium">Kendi farkını söyle:</span>
            </div>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 mt-2">
            <div className="p-2.5 bg-brand-50 rounded-lg border border-brand-100">
              <div className="text-xs font-semibold text-brand-700">Yerel Destek</div>
              <div className="text-xs text-brand-600 mt-0.5">Türkçe konuşuyorsun, arayınca açıyorsun</div>
            </div>
            <div className="p-2.5 bg-brand-50 rounded-lg border border-brand-100">
              <div className="text-xs font-semibold text-brand-700">Hizli Kurulum</div>
              <div className="text-xs text-brand-600 mt-0.5">1 saat içinde çalışır durumda</div>
            </div>
            <div className="p-2.5 bg-brand-50 rounded-lg border border-brand-100">
              <div className="text-xs font-semibold text-brand-700">Kisisellestirme</div>
              <div className="text-xs text-brand-600 mt-0.5">Müşteriye özel flow yaparsın</div>
            </div>
            <div className="p-2.5 bg-brand-50 rounded-lg border border-brand-100">
              <div className="text-xs font-semibold text-brand-700">Fiyat</div>
              <div className="text-xs text-brand-600 mt-0.5">Dolar bazlı değil, TL bazlı</div>
            </div>
          </div>
          <div className="p-3 bg-navy-50 rounded-lg text-sm text-navy-700 italic">
            "Diğerleri de yapar ama ben 1 saatte kurar, sorun olursa WhatsApp'tan yazarsınız, 5 dakikada dönerim."
          </div>
        </Card>
      </div>

      {/* ─── 10. Destek Süreci ───────────────────────────── */}

      <div>
        <SectionTitle icon={Handshake}>10. Destek Sürecini Yapılandır</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            WhatsApp'tan destek vermek samimi ama ölçeklenmiyor ve kaybolur.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <h5 className="text-sm font-semibold text-navy-900 mb-2">Kisa Vadede</h5>
              <ul className="text-sm text-navy-600 space-y-1 pl-5 list-disc">
                <li>Her destek talebini bir yere not et (Notion, Excel, hatta txt)</li>
                <li>En çok hangi konuda soru geldiğini takip et</li>
                <li>Tekrarlayan sorular için hazır cevap şablonları oluştur</li>
              </ul>
            </div>
            <div>
              <h5 className="text-sm font-semibold text-navy-900 mb-2">Orta Vadede</h5>
              <ul className="text-sm text-navy-600 space-y-1 pl-5 list-disc">
                <li>Dashboard'a "Destek Talebi" butonu ekle</li>
                <li>Müşterinin panelinden sorun bildirmesini sağla</li>
                <li>Basit bir ticket sistemi (karmaşık olmasın)</li>
              </ul>
            </div>
          </div>
          <Tip>
            Tekrarlayan destek konularını <TabLink to="features">FAQ ve Knowledge Base'e</TabLink> ekle.
            En iyi destek, müşterinin destek istemeye ihtiyaç duymamasıdır.
          </Tip>
        </Card>
      </div>

      {/* ─── 11. Metrikler ───────────────────────────────── */}

      <div>
        <SectionTitle icon={BarChart3}>11. Temel Metrikleri Takip Et</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            Başlangıçta olsan bile şu 5 metriği her ay bir kağıda yaz. Ölçmediğin şeyi iyileştiremezsin.
          </p>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Metrik</th>
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Aciklama</th>
                  <th className="text-left py-2 font-medium text-navy-500">Hedef</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">MRR</td>
                  <td className="py-2 pr-4">Aylık tekrarlayan gelir</td>
                  <td className="py-2"><Badge variant="success">Her ay artsin</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Churn Rate</td>
                  <td className="py-2 pr-4">Ayrılan müşteri / toplam</td>
                  <td className="py-2"><Badge variant="warning">{'<'}%5/ay</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Time-to-Value</td>
                  <td className="py-2 pr-4">Kurulumdan ilk aha moment'e süre</td>
                  <td className="py-2"><Badge variant="warning">{'<'}1 saat</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">NPS</td>
                  <td className="py-2 pr-4">"Bizi tavsiye eder misiniz?" 1-10</td>
                  <td className="py-2"><Badge variant="warning">{'>'}8</Badge></td>
                </tr>
                <tr>
                  <td className="py-2 pr-4 font-medium">DAU/MAU</td>
                  <td className="py-2 pr-4">Son 7 günde giriş yapan / toplam</td>
                  <td className="py-2"><Badge variant="warning">{'>'}%70</Badge></td>
                </tr>
              </tbody>
            </table>
          </div>
          <Tip>
            Bunu şimdiye kadar Excel tablosuna bile yazsan yeter. Ama <strong>yaz</strong>.
            Tüm bu metriklerin iyileşmesi için <TabLink to="actions">aksiyon listesini</TabLink> takip et.
          </Tip>
        </Card>
      </div>

      {/* ─── Öncelik Sırası ──────────────────────────────── */}

      <div>
        <SectionTitle icon={Target}>Öncelik Sırası</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">Bunların hepsini birden yapma. Sıra:</p>
          <div className="space-y-2">
            <div className="flex items-center gap-3">
              <Badge variant="error">Hemen</Badge>
              <span className="text-sm text-navy-700">Sektör bazlı template flow'ları oluştur</span>
            </div>
            <div className="flex items-center gap-3">
              <Badge variant="warning">Bu Ay</Badge>
              <span className="text-sm text-navy-700">Fiyatlandırma paketlerini belirle (Başlangıç/Büyüme/Pro)</span>
            </div>
            <div className="flex items-center gap-3">
              <Badge variant="info">Bu Ceyrek</Badge>
              <span className="text-sm text-navy-700">Health Score widget'ını dashboard'a ekle</span>
            </div>
            <div className="flex items-center gap-3">
              <Badge variant="default">Sürekli</Badge>
              <span className="text-sm text-navy-700">MRR ve churn takibi, aylık rapor disiplini</span>
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}

function ActionsTab() {
  return (
    <div className="space-y-6">
      <div>
        <SectionTitle icon={CheckSquare}>Aksiyon Listesi</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Aşağıdaki listeyi sırayla takip et. Tıkladığın maddeler işaretlenir (sadece bu oturum için).
          Önceliği yüksek olanları önce yap. SaaS stratejisi maddeleri de dahil.
        </p>
        <div className="flex flex-wrap gap-2 text-sm">
          <span className="text-navy-400">Ilgili sekmeler:</span>
          <TabLink to="onboarding">Onboarding Adimlari</TabLink>
          <TabLink to="saas">SaaS Stratejisi</TabLink>
          <TabLink to="communication">Müşteri İletişimi</TabLink>
        </div>
      </div>
      <ActionList />
    </div>
  );
}

/* ─── Main Page ─────────────────────────────────────────────── */

export function OnboardingGuidePage() {
  const [activeTab, setActiveTab] = useState<GuideTab>('overview');
  _setActiveTabGlobal = setActiveTab;

  const tabContent: Record<GuideTab, React.ReactNode> = {
    overview: <OverviewTab />,
    onboarding: <OnboardingTab />,
    features: <FeaturesTab />,
    sectors: <SectorsTab />,
    communication: <CommunicationTab />,
    saas: <SaasTab />,
    actions: <ActionsTab />,
  };

  return (
    <div>
      <h1 className="-mx-8 -mt-8 px-8 py-5 mb-6 text-xl font-semibold text-navy-900 flex items-center gap-2 border-b border-navy-100">
        <BookOpen className="w-5 h-5 text-navy-400" />
        Onboarding Rehberi
      </h1>

      <div className="flex items-start gap-6">
        {/* Vertical Tabs */}
        <nav className="w-52 flex-shrink-0">
          <div className="bg-white rounded-xl border border-navy-100 shadow-card p-2 space-y-1">
            {TABS.map(tab => {
              const Icon = tab.icon;
              const isActive = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={cn(
                    'w-full flex items-center gap-2.5 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors text-left',
                    isActive
                      ? 'bg-brand-50 text-brand-700 border border-brand-200'
                      : 'text-navy-500 hover:bg-navy-50 hover:text-navy-700 border border-transparent'
                  )}
                >
                  <Icon className={cn('w-4 h-4 flex-shrink-0', isActive ? 'text-brand-500' : 'text-navy-400')} />
                  {tab.label}
                </button>
              );
            })}
          </div>
        </nav>

        {/* Tab Content */}
        <div className="flex-1 min-w-0">
          {tabContent[activeTab]}
        </div>
      </div>
    </div>
  );
}
