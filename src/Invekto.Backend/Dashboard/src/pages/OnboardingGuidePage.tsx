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
import { Card, CardContent } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';

/* ─── Types ─────────────────────────────────────────────────── */

type GuideTab = 'overview' | 'onboarding' | 'features' | 'sectors' | 'communication' | 'saas' | 'actions';

interface TabDef {
  id: GuideTab;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

const TABS: TabDef[] = [
  { id: 'overview', label: 'Genel Bakis', icon: BookOpen },
  { id: 'onboarding', label: 'Onboarding Adimlari', icon: Target },
  { id: 'features', label: 'Ozellik Rehberi', icon: Layers },
  { id: 'sectors', label: 'Sektor Senaryolari', icon: Building2 },
  { id: 'communication', label: 'Musteri Iletisimi', icon: MessageCircle },
  { id: 'saas', label: 'SaaS Stratejisi', icon: TrendingUp },
  { id: 'actions', label: 'Aksiyon Listesi', icon: CheckSquare },
];

/* ─── Helpers ───────────────────────────────────────────────── */

/* ─── Tab navigation context ────────────────────────────────── */

let _setActiveTabGlobal: ((tab: GuideTab) => void) | null = null;

function TabLink({ to, children }: { to: GuideTab; children: React.ReactNode }) {
  const tabLabels: Record<GuideTab, string> = {
    overview: 'Genel Bakis',
    onboarding: 'Onboarding Adimlari',
    features: 'Ozellik Rehberi',
    sectors: 'Sektor Senaryolari',
    communication: 'Musteri Iletisimi',
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
          <span className="text-navy-400 w-20 flex-shrink-0 font-medium">Cozum:</span>
          <span className="text-navy-700 font-medium">{solution}</span>
        </div>
        <div className="flex gap-2">
          <span className="text-navy-400 w-20 flex-shrink-0 font-medium">Nasil:</span>
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
          <span className="text-xs font-medium text-navy-400 uppercase tracking-wide">Once Ac</span>
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
          <span className="text-xs text-navy-400">Ilk akis ornegi:</span>
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
  { id: 'a1', category: 'Onboarding Hazirlik', text: 'Sektor bazli hazir flow template\u0027lari olustur', detail: 'En az 3 sektor: restoran, klinik, e-ticaret. Her biri calisan, test edilmis flow olmali.', priority: 'high' },
  { id: 'a2', category: 'Onboarding Hazirlik', text: 'Her sektor icin 1 sayfa "Invekto sizin icin ne yapar" dokumani yaz', detail: 'A4, Turkce, teknik terim yok. Sorun-cozum formati. PDF veya gorselle destekle.', priority: 'high' },
  { id: 'a3', category: 'Onboarding Hazirlik', text: '3 temel ozellik icin ekran kaydi videosu cek', detail: 'Flow olusturma, FAQ ekleme, kampanya gonderme. Her biri max 60 saniye.', priority: 'high' },
  { id: 'a4', category: 'Onboarding Hazirlik', text: 'Yeni tenant icin varsayilan feature flag setini belirle', detail: 'Ilk hafta: FlowBuilder + Knowledge. 2. hafta: Analytics + Outbound. 3. hafta: Marketing + Integrations.', priority: 'medium' },

  // İlk Gün
  { id: 'b1', category: 'Ilk Gun Checklist', text: 'Tenant olustur ve temel ayarlari yap', detail: 'Firma bilgileri, calisma saatleri, saat dilimi, kapali gunler.', priority: 'high' },
  { id: 'b2', category: 'Ilk Gun Checklist', text: 'WhatsApp hattini bagla ve test et', detail: 'WapCRM API key gir, hatlari yenile, aktif hatti sec, test mesaji gonder.', priority: 'high' },
  { id: 'b3', category: 'Ilk Gun Checklist', text: 'Ilk flow\u0027u kur ve canli test yap', detail: 'Basit bir "hos geldin + menu" akisi. Musterinin kendi telefonundan test etmesini sagla.', priority: 'high' },
  { id: 'b4', category: 'Ilk Gun Checklist', text: 'Musteriye dashboard erisimi ver', detail: 'Kullanici adi ve sifre olustur, giris URL\u0027ini paylas.', priority: 'high' },

  // İlk Hafta
  { id: 'c1', category: 'Ilk Hafta', text: 'FAQ iceriklerini gir veya musteri ile birlikte olustur', detail: 'En sik sorulan 10-15 soruyu belirle, bilgi bankasina ekle. Kisa ve net cevaplar.', priority: 'high' },
  { id: 'c2', category: 'Ilk Hafta', text: 'Calisma saatlerini yapilandir', detail: 'Mesai disi otomatik cevap akisini olustur ve test et.', priority: 'medium' },
  { id: 'c3', category: 'Ilk Hafta', text: 'Ilk hafta sonunda musteri ile 15 dk gorusme yap', detail: 'Neler calisiyor, neler zor, soru var mi? Not al, gerekirse flow\u0027u ayarla.', priority: 'high' },

  // İkinci Hafta
  { id: 'd1', category: 'Ikinci Hafta', text: 'Analytics modülünü ac ve musteriye goster', detail: 'Ilk hafta verisi birikti. "Bak su kadar mesaj geldi, su kadar otomatik cevaplandi" goster.', priority: 'medium' },
  { id: 'd2', category: 'Ikinci Hafta', text: 'Outbound/Kampanya modulunu ac', detail: 'Ilk toplu mesaji birlikte gonderin. Opt-out yonetimini acikla.', priority: 'medium' },
  { id: 'd3', category: 'Ikinci Hafta', text: 'Condition/Switch node\u0027larini ogret', detail: 'Basit bir senaryo: "Randevu mu yoksa fiyat mi soruyor?" dallanmasi.', priority: 'medium' },

  // Üçüncü Hafta+
  { id: 'e1', category: 'Ucuncu Hafta+', text: 'Gelismis ozellikleri kademeli ac', detail: 'Marketing (referral, review), Sentiment Analysis, API Call node\u0027u. Hepsini birden degil, ihtiyaca gore.', priority: 'low' },
  { id: 'e2', category: 'Ucuncu Hafta+', text: 'Randevu modulunu ac (sektor uygunsa)', detail: 'Klinik, kuafor, restoran gibi sektorlerde. Hatirlatma akisini birlikte kur.', priority: 'low' },
  { id: 'e3', category: 'Ucuncu Hafta+', text: 'Aylik performans raporu gonder', detail: 'Kac mesaj geldi, kaci otomatik cevaplandi, agent ihtiyaci ne kadar azaldi. Somut rakamlarla.', priority: 'low' },

  // Sürekli
  { id: 'f1', category: 'Surekli', text: 'Yeni ozellik cikinca 30 sn video cek ve WhatsApp\u0027tan gonder', detail: 'Format: "Yeni ozellik: [ne yapar] — nasil acilir: [3 adim]". Kisa, gorsel, net.', priority: 'medium' },
  { id: 'f2', category: 'Surekli', text: 'Kullanilmayan modulleri takip et', detail: 'Acik ama kullanilmayan feature var mi? Musteri ile konusup ya ogret ya kapat.', priority: 'medium' },
  { id: 'f3', category: 'Surekli', text: 'Musteri memnuniyetini periyodik olarak sor', detail: 'Ayda 1 kere, kisa bir WhatsApp mesaji: "Nasilsiniz, eksik var mi?" Basit ama etkili.', priority: 'low' },

  // SaaS Stratejisi — Fiyatlandırma
  { id: 'g1', category: 'SaaS: Fiyatlandirma', text: 'Baslangic / Buyume / Profesyonel paket yapisini olustur', detail: 'Modul bazli degil, deger bazli paketleme. Her pakete somut bir sonuc esle.', priority: 'high' },
  { id: 'g2', category: 'SaaS: Fiyatlandirma', text: 'Yillik odeme indirimi tanimla', detail: 'Aylik x12 yerine 10 aylik (2 ay bedava). Churn\u0027u %30-40 dusurur.', priority: 'high' },

  // SaaS Stratejisi — Health Score
  { id: 'h1', category: 'SaaS: Musteri Sagligi', text: 'Musteri Health Score formulu olustur', detail: 'Panel girisi, flow calisma, otomasyon orani, FAQ sayisi, destek talebi — agirlikli skor.', priority: 'high' },
  { id: 'h2', category: 'SaaS: Musteri Sagligi', text: 'Health Score < 60 icin otomatik alarm kur', detail: 'Skor dusuk musterileri haftalik olarak kontrol et, proaktif iletisim kur.', priority: 'medium' },

  // SaaS Stratejisi — Ölçekleme
  { id: 'i1', category: 'SaaS: Olcekleme', text: 'Sektor bazli template flow\u0027lari + FAQ paketleri olustur', detail: 'Yeni tenant acilirken "sektor sec" → hazir flow + FAQ otomatik yuklensin.', priority: 'high' },
  { id: 'i2', category: 'SaaS: Olcekleme', text: 'Self-service kurulum wizard\u0027i planla', detail: '10+ musteride bizzat kurulum surekli olmaz. Adim adim wizard ile musterinin kendisi kursun.', priority: 'medium' },

  // SaaS Stratejisi — Büyüme
  { id: 'j1', category: 'SaaS: Buyume', text: 'Referral programi kur', detail: 'Mevcut musteri yeni getirir → 1 ay bedava. Gelen musteri → %20 indirim. Health score 80+ olanlara oner.', priority: 'medium' },
  { id: 'j2', category: 'SaaS: Buyume', text: 'Upsell tetikleyicilerini belirle ve takip et', detail: 'Mesaj hacmi 500+, 3+ dallanma, FAQ yogunlugu → uygun modulu oner.', priority: 'medium' },

  // SaaS Stratejisi — Metrikler
  { id: 'k1', category: 'SaaS: Metrikler', text: 'MRR, churn rate, time-to-value takibini baslat', detail: 'Excel bile olur. Her ay kaydet: toplam gelir, ayrilan musteri, ortalama kurulum suresi.', priority: 'high' },
  { id: 'k2', category: 'SaaS: Metrikler', text: 'Destek taleplerini kayit altina al', detail: 'Hangi konuda en cok soru geldi? Tekrarlayan sorunlari FAQ veya rehbere ekle.', priority: 'medium' },
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
  const priorityLabels = { high: 'Yuksek', medium: 'Orta', low: 'Dusuk' };

  return (
    <div className="space-y-6">
      {/* Progress */}
      <Card className="p-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-sm font-medium text-navy-700">Ilerleme</span>
          <span className="text-sm text-navy-500">{done}/{total} tamamlandi</span>
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
        <SectionTitle icon={BookOpen}>Invekto Nedir — Tek Cumle</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-700 leading-relaxed">
            Invekto, isletmelerin WhatsApp uzerinden gelen musteri mesajlarini <strong>otomatik olarak yanitmasi</strong>,
            sik sorulan sorulari <strong>yapay zeka ile cevaplatmasi</strong> ve toplu mesaj gondermesini saglayan bir platformdur.
            Musteri hizmetini hizlandirir, insan ihtiyacini azaltir, hicbir mesaji kacirmaz.
          </p>
        </Card>
      </div>

      <div>
        <SectionTitle icon={Target}>Musteriye Ne Satiyorsun?</SectionTitle>
        <div className="space-y-3">
          <Card className="p-4">
            <h4 className="text-sm font-semibold text-navy-900 mb-2">Teknoloji Satma, Sonuc Sat</h4>
            <div className="space-y-2 text-sm">
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"17 node tipimiz var"</span>
                <ArrowRight className="w-3 h-3 text-navy-300 flex-shrink-0" />
                <span className="text-emerald-700 font-medium">"Musteri seni arar, otomatik cevap verir"</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"AI intent detection"</span>
                <ArrowRight className="w-3 h-3 text-navy-300 flex-shrink-0" />
                <span className="text-emerald-700 font-medium">"Musteri ne istedigini otomatik anlar"</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"pgvector embedding"</span>
                <ArrowRight className="w-3 h-3 text-navy-300 flex-shrink-0" />
                <span className="text-emerald-700 font-medium">"Soruya en yakin cevabi bulur"</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"Multi-tenant SaaS"</span>
                <ArrowRight className="w-3 h-3 text-navy-300 flex-shrink-0" />
                <span className="text-emerald-700 font-medium">"Her firma kendi panelini goruntuler"</span>
              </div>
            </div>
          </Card>

          <Tip>
            Musteri teknolojiyi degil, kendi hayatinin kolaylasmasini satin aliyor.
            Her ozelligi "bu sana ne kazandiriyor?" uzerinden anlat.
          </Tip>
        </div>
      </div>

      <div>
        <SectionTitle icon={AlertTriangle}>En Buyuk Risk: Bilgi Kirliligi</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">
            Platform cok genis. Musteriye hepsini gosterirsen bunalir ve hicbirini kullanmaz.
            <TabLink to="onboarding">Onboarding adimlari</TabLink> sekmesindeki kademeli acilim stratejisini uygula.
            <TabLink to="sectors">Sektor senaryolari</TabLink> sekmesinden sektore uygun modulleri belirle.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="p-3 bg-emerald-50 rounded-lg border border-emerald-100 text-center">
              <div className="text-lg font-bold text-emerald-700">Hafta 1</div>
              <div className="text-xs text-emerald-600 mt-1">Flow + FAQ</div>
              <div className="text-xs text-navy-400 mt-0.5">Temeli ogrensin</div>
            </div>
            <div className="p-3 bg-amber-50 rounded-lg border border-amber-100 text-center">
              <div className="text-lg font-bold text-amber-700">Hafta 2</div>
              <div className="text-xs text-amber-600 mt-1">Analytics + Kampanya</div>
              <div className="text-xs text-navy-400 mt-0.5">Degeri gorsun</div>
            </div>
            <div className="p-3 bg-brand-50 rounded-lg border border-brand-100 text-center">
              <div className="text-lg font-bold text-brand-700">Hafta 3+</div>
              <div className="text-xs text-brand-600 mt-1">Marketing + AI</div>
              <div className="text-xs text-navy-400 mt-0.5">Gucunu kesfetsin</div>
            </div>
          </div>
        </Card>
      </div>

      <div>
        <SectionTitle icon={Users}>Ilk 5 Dakika Kurali</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">
            Musteri platformu ilk actiginda <strong>5 dakika icinde somut bir deger gormeli</strong>.
            Aksi halde "sonra bakarim" der ve bir daha acmaz.
            Bunu <TabLink to="saas">SaaS Stratejisi</TabLink> sekmesinde "Aha Moment" olarak detaylandirdik.
          </p>
          <div className="space-y-2 text-sm">
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span className="text-navy-700"><strong>Hazir flow template:</strong> Tenant olusturulurken sektore uygun akis hazir gelsin</span>
            </div>
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span className="text-navy-700"><strong>"Merhaba Dunya" testi:</strong> Ilk 2 dakikada kendi telefonundan test mesaji gondersin</span>
            </div>
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span className="text-navy-700"><strong>Bos ekran yok:</strong> Ornek data veya demo verisiyle gelsin, bos dashboard moral bozar</span>
            </div>
          </div>
        </Card>
      </div>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">Ilgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="onboarding">Onboarding Adimlari</TabLink>
          <TabLink to="features">Ozellik Rehberi</TabLink>
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
        <SectionTitle icon={Target}>Musteri Onboarding Sureci</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Yeni bir musteri geldiginde asagidaki adimlari siraliyla takip et.
          Her adimi tamamlamadan bir sonrakine gecme.
        </p>
      </div>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">Satis Oncesi</h4>
        <StepCard step={1} title="Sektoru ve ihtiyaci anla" duration="15 dk">
          <p>Musterinin sektorunu belirle (restoran, klinik, e-ticaret, hizmet, turizm).</p>
          <p>Su sorulari sor:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>Gunluk kac mesaj aliyorsun?</li>
            <li>En cok hangi sorular soruluyor?</li>
            <li>Mesajlara kac dakikada donuyorsun?</li>
            <li>Randevu sistemi kullaniyormusun?</li>
            <li>Toplu mesaj/kampanya gonderiyor musun?</li>
          </ul>
          <Tip>
            Bu sorularin cevaplari hangi modulleri acik baslayacagini belirler.
            Hepsini acma, sadece ihtiyaca gore. <TabLink to="sectors">Sektor senaryolari</TabLink> sekmesinde
            sektore gore hangi modullerin acilacagi detayli anlatiyor.
          </Tip>
        </StepCard>

        <StepCard step={2} title="Demo goster (kendi test ortaminda)" duration="20 dk">
          <p>Musterinin sektorune uygun hazir flow ile canli demo yap.</p>
          <p>Gosterim sirasi:</p>
          <ol className="list-decimal pl-5 space-y-1 text-navy-600">
            <li>"Bir musteri mesaj yaziyor" — otomatik cevap donuyor</li>
            <li>"Sik sorulan soru soruyor" — AI cevapliyor</li>
            <li>"Yardim istiyor" — insana yonlendiriliyor</li>
          </ol>
          <Warning>
            Demo sirasinda arka plan teknolojisinden bahsetme (node, flow, API, embedding...).
            Sadece sonucu goster.
          </Warning>
        </StepCard>
      </Card>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">Ilk Gun (Kurulum)</h4>
        <StepCard step={3} title="Tenant olustur ve ayarlari yap" duration="10 dk">
          <p>Firmalar sayfasindan yeni tenant olustur. Asagidakileri doldur:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>Firma adi ve kodu</li>
            <li>Calisma saatleri ve kapali gunler</li>
            <li>Saat dilimi</li>
            <li>Kullanici adi ve sifre (musteri icin)</li>
          </ul>
        </StepCard>

        <StepCard step={4} title="WhatsApp hattini bagla" duration="10 dk">
          <p>Musterinin WapCRM hesabindaki API key'i al ve sisteme gir.</p>
          <p>Ayarlar &gt; Hatlar &gt; WapCRM'den Yenile butonuyla hatlari cek.</p>
          <p>Kullanilacak hatti aktif et, diger hatlari kapat.</p>
          <Tip>
            Bu adimda musteri yaninda olsun. Kendi telefonundan test mesaji
            gondererek "calisiyor!" momentini birlikte yasayin.
          </Tip>
        </StepCard>

        <StepCard step={5} title="Ilk akisi kur" duration="15 dk">
          <p>Sektore uygun hazir template'i yukle veya sifirdan basit bir akis olustur:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li><strong>Trigger Start:</strong> Musteri mesaj yazdiginda basla</li>
            <li><strong>Mesaj Menu:</strong> "Merhaba! Size nasil yardimci olabilirim?" + 3-4 secenek</li>
            <li><strong>FAQ Node:</strong> Sik sorulan sorulari otomatik cevapla</li>
            <li><strong>Handoff:</strong> "Detayli bilgi icin sizi yetkilimize yonlendiriyorum"</li>
          </ul>
          <Warning>
            Ilk akis basit olmali. Condition, Switch, AI Intent gibi gelismis node'lari EKLEME.
            Musteri once temel akisi ogrensin.
          </Warning>
        </StepCard>

        <StepCard step={6} title="Canli test yap" duration="5 dk">
          <p>Musterinin kendi telefonundan WhatsApp'a mesaj gondermesini sagla.</p>
          <p>Birlikte izleyin: mesaj geldi → akis basladi → cevap gitti.</p>
          <p>Bu "aha momenti" cok onemli. Musterinin gozleri parlayacak. (<TabLink to="saas">SaaS Stratejisi</TabLink> sekmesinde neden bu an kritik oldugunu oku.)</p>
        </StepCard>
      </Card>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">Ilk Hafta (Alisma)</h4>
        <StepCard step={7} title="FAQ iceriklerini doldur" duration="30 dk">
          <p>Musteri ile birlikte en sik sorulan 10-15 soruyu belirle.</p>
          <p>Bilgi Bankasi'na ekle. Kisa, net, konusma diliyle yaz.</p>
          <p>Yanlis: "Isletmemiz 2005 yilinda kurulmus olup..." — Dogru: "Pazartesi-Cuma 09:00-18:00 arasi acigiz."</p>
        </StepCard>

        <StepCard step={8} title="Mesai disi akisini kur" duration="10 dk">
          <p>Calisma saatleri disinda gelen mesajlara otomatik cevap:</p>
          <p className="italic text-navy-500">"Su anda mesai saatlerimiz disindayiz. En kisa surede donecegiz."</p>
        </StepCard>

        <StepCard step={9} title="Hafta sonu takip gorusmesi" duration="15 dk">
          <p>Ilk haftanin sonunda musteriye sor:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>Akis calisiyor mu, sorun var mi?</li>
            <li>Musterilerin tepkisi nasil?</li>
            <li>Eklemek istedigin bir soru/cevap var mi?</li>
            <li>Paneli acip bakiyor musun?</li>
          </ul>
          <Tip>
            Bu gorusme kritik. Musteri ilk haftada sikildiysa 2. haftada gelmez.
            Sorunlari hemen coz.
          </Tip>
        </StepCard>
      </Card>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">Ikinci Hafta (Derinlesme)</h4>
        <StepCard step={10} title="Analytics modulunu ac" duration="10 dk">
          <p>Ilk hafta verisi birikti. Musteriye somut rakamlar goster:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>"Bu hafta 47 mesaj geldi, 38'i otomatik cevaplandi"</li>
            <li>"En cok sorulan konu: fiyat bilgisi"</li>
            <li>"Ortalama cevap suresi: 3 saniye"</li>
          </ul>
          <p>Bu rakamlar musterinin "para ediyor mu?" sorusuna cevap verir.</p>
        </StepCard>

        <StepCard step={11} title="Kampanya modulunu ac (isteye bagli)" duration="15 dk">
          <p>Eger musteri toplu mesaj gondermek istiyorsa Outbound modulunu ac.</p>
          <p>Ilk kampanyayi birlikte gonderin. Opt-out (STOP) yonetimini acikla.</p>
          <Warning>
            Toplu mesaj hassas bir konu. Spam riski var. Musteriye su kurallari anlat:
            izinli kisi listesi, STOP yazanlari cikar, gunluk limit.
          </Warning>
        </StepCard>

        <StepCard step={12} title="Dallanma ogret (Condition/Switch)" duration="15 dk">
          <p>Basit bir senaryo olustur:</p>
          <p className="italic text-navy-500">"Musteri 'randevu' yazarsa → randevu akisina yonlendir. 'Fiyat' yazarsa → fiyat listesi gonder."</p>
          <p>Bu adimda musteri akis tasariminin gucunu anlamaya baslar.</p>
        </StepCard>
      </Card>

      <Card className="p-5">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-4">Ucuncu Hafta ve Sonrasi</h4>
        <StepCard step={13} title="Gelismis ozellikleri ihtiyaca gore ac" duration="Surekli">
          <p>Her yeni ozelligi acmadan once musteriye sor: "Buna ihtiyacin var mi?"</p>
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <Badge variant="default">Marketing</Badge>
              <span className="text-navy-500">→ Referral ve review isteme (hizmet sektoru icin)</span>
            </div>
            <div className="flex items-center gap-2">
              <Badge variant="default">Sentiment</Badge>
              <span className="text-navy-500">→ Musteri memnuniyeti takibi (yuksek hacim icin)</span>
            </div>
            <div className="flex items-center gap-2">
              <Badge variant="default">Appointments</Badge>
              <span className="text-navy-500">→ Randevu sistemi (klinik, kuafor, restoran)</span>
            </div>
            <div className="flex items-center gap-2">
              <Badge variant="default">API Call</Badge>
              <span className="text-navy-500">→ Dis sistem entegrasyonu (teknik musteri icin)</span>
            </div>
          </div>
        </StepCard>

        <StepCard step={14} title="Aylik performans raporu gonder" duration="Ayda 1">
          <p>Her ay musteriye kisa bir ozet gonder:</p>
          <ul className="list-disc pl-5 space-y-1 text-navy-600">
            <li>Toplam mesaj / otomatik cevaplanan oran</li>
            <li>En cok sorulan konular</li>
            <li>Kampanya sonuclari (varsa)</li>
            <li>Oneri: "Su FAQ'i eklesek %10 daha fazla otomatik cevap verebiliriz"</li>
          </ul>
          <Tip>
            Bu rapor musterinin "neden para oduyorum?" sorusunu onler.
            Somut rakam goster, genel laftan kacin. <TabLink to="communication">Musteri Iletisimi</TabLink> sekmesinde hazir rapor sablonlari var.
            <TabLink to="saas">SaaS Stratejisi</TabLink> sekmesinde hangi metrikleri takip etmen gerektigini oku.
          </Tip>
        </StepCard>
      </Card>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">Ilgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="sectors">Sektor Senaryolari</TabLink>
          <TabLink to="features">Ozellik Rehberi</TabLink>
          <TabLink to="communication">Iletisim Sablonlari</TabLink>
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
        <SectionTitle icon={Layers}>Ozellik Rehberi — Anlatim Sirasi</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Ozellikleri teknoloji sirasina gore degil, <strong>musteri acisi sirasina gore</strong> anlat.
          Asagidaki sira, musterinin "buna neden ihtiyacim var?" sorusunu en kolay anlayacagi siradir.
          Her musteri her seyi kullanmayacak — <TabLink to="sectors">Sektor Senaryolari</TabLink> sekmesinden sektore uygun modulleri belirle.
        </p>
      </div>

      <div className="space-y-3">
        <div className="flex items-center gap-2 mb-1">
          <Badge variant="success">1. Oncelik — Ilk Gun</Badge>
        </div>
        <FeatureBlock
          title="Flow Builder (Akis Tasarimi)"
          problem="WhatsApp'tan cok mesaj geliyor, yetisemiyorum"
          solution="Musteri yazinca otomatik cevap doner, menu gosterir, yonlendirir"
          howTo="Flow Builder > Yeni Akis > Sablondan sec veya sifirdan olustur"
          when="Ilk gun"
        />
        <FeatureBlock
          title="Bilgi Bankasi + AI FAQ"
          problem="Hep ayni sorulari soruyorlar, tek tek cevapliyorum"
          solution="Sik sorulan sorulari bir kere yaz, AI otomatik cevaplar"
          howTo="Bilgi Bankasi > Yeni Icerik > Soru-cevap ekle"
          when="Ilk hafta"
        />
      </div>

      <div className="space-y-3">
        <div className="flex items-center gap-2 mb-1">
          <Badge variant="warning">2. Oncelik — Ikinci Hafta</Badge>
        </div>
        <FeatureBlock
          title="Analizler"
          problem="Kimin ne sordugunu, kac mesaj geldigini takip edemiyorum"
          solution="Dashboard'dan mesaj trendlerini, otomatik cevap oranini gor"
          howTo="Analizler sayfasina git, tarih araligiyla filtrele"
          when="2. hafta (veri birikince)"
        />
        <FeatureBlock
          title="Kampanyalar (Outbound)"
          problem="Musterilere toplu mesaj gonderemiyorum, tek tek ugrasamam"
          solution="Kisi listesine toplu mesaj gonder, teslim durumunu takip et"
          howTo="Kampanyalar > Yeni Kampanya > Sablon sec > Gonder"
          when="2. hafta"
        />
        <FeatureBlock
          title="Dallanma (Condition/Switch)"
          problem="Her musteri farkli sey soruyor, tek cevap yetmiyor"
          solution="Musterinin ne istedigine gore farkli yollara yonlendir"
          howTo="Akis icerisine Condition veya Switch node'u ekle"
          when="2. hafta (temel akis ogrenildikten sonra)"
        />
      </div>

      <div className="space-y-3">
        <div className="flex items-center gap-2 mb-1">
          <Badge variant="info">3. Oncelik — Ucuncu Hafta+</Badge>
        </div>
        <FeatureBlock
          title="Randevular"
          problem="Randevu almak zahmetli, telefonla ugrasiyorum"
          solution="Musteri WhatsApp'tan randevu alir, otomatik hatirlatma gider"
          howTo="Ayarlar > Randevular modulu ac > Akisa randevu node'u ekle"
          when="Sektor uygunsa (klinik, kuafor, restoran)"
        />
        <FeatureBlock
          title="Marketing — Referral"
          problem="Musterilerim beni tavsiye etmiyor, organik buyume yok"
          solution="Referans linki olustur, paylasildiginda takip et"
          howTo="Pazarlama > Referanslar > Yeni link olustur"
          when="Aktif musteri tabani oldugunda"
        />
        <FeatureBlock
          title="Marketing — Review Isteme"
          problem="Google/sosyal medya yorumlarim az"
          solution="Mutlu musteri tespit et, otomatik yorum istegi gonder"
          howTo="Pazarlama > Yorumlar > Yorum iste"
          when="Hizmet sektoru icin"
        />
        <FeatureBlock
          title="AI Duygu Analizi (Sentiment)"
          problem="Musteri kizgin mi memnun mu anlamiyorum"
          solution="AI mesajin tonunu analiz eder, kizgin musterileri isaretle"
          howTo="Akis icinde Sentiment node'u ekle veya Analytics'ten gor"
          when="Yuksek mesaj hacmi oldugunda"
        />
        <FeatureBlock
          title="Entegrasyonlar (API Call)"
          problem="Dis sistemlerle baglantiyi manuel yapiyorum"
          solution="Akis icinden API cagirarak dis sisteme veri gonder/al"
          howTo="Akis icinde API Call node'u ekle, endpoint ve parametreleri ayarla"
          when="Teknik musteri ve ozel entegrasyon gerektiginde"
        />
      </div>

      <Tip>
        Her ozellik icin basit bir kural: Musteri "buna ihtiyacim var" demeden acma.
        Acilmamis ozellik kafa karistirmaz, acilip kullanilmayan ozellik karistirir.
        Mevcut musterilere yeni ozellik onerme zamanlama stratejisi icin <TabLink to="saas">SaaS Stratejisi: Upsell</TabLink> bolumune bak.
      </Tip>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">Ilgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="sectors">Sektor Senaryolari</TabLink>
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
        <SectionTitle icon={Building2}>Sektore Gore Ozellik Haritalamasi</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Her musteri her ozelligi kullanmayacak. Sektore gore oncelikli modulleri ac,
          gereksiz olanlari kapatik tut.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <SectorCard
          icon={Utensils}
          title="Restoran / Kafe"
          primary={['Flow Builder', 'Outbound', 'FAQ']}
          secondary={['Appointments', 'Marketing', 'Analytics']}
          firstFlow="'Merhaba! Menumuzu gormek ister misiniz?' + gunun ozel menusu + rezervasyon yonlendirme"
        />
        <SectorCard
          icon={Stethoscope}
          title="Klinik / Saglik"
          primary={['Flow Builder', 'Appointments', 'FAQ']}
          secondary={['Analytics', 'Outbound', 'Sentiment']}
          firstFlow="'Randevu almak icin 1, doktor bilgisi icin 2 yazin' + otomatik hatirlatma"
        />
        <SectorCard
          icon={ShoppingCart}
          title="E-Ticaret"
          primary={['Flow Builder', 'Outbound', 'Analytics']}
          secondary={['Marketing', 'Sentiment', 'Integrations']}
          firstFlow="'Siparisinizi takip etmek icin siparis numaranizi yazin' + kampanya bildirimi"
        />
        <SectorCard
          icon={Scissors}
          title="Hizmet (Kuafor, Berber, Oto Yikama)"
          primary={['Flow Builder', 'Appointments', 'Marketing']}
          secondary={['Outbound', 'Analytics', 'FAQ']}
          firstFlow="'Randevu icin gun ve saat secin' + hatirlatma + hizmet sonrasi yorum isteme"
        />
        <SectorCard
          icon={Plane}
          title="Turizm / Otel"
          primary={['Flow Builder', 'FAQ', 'Outbound']}
          secondary={['Marketing', 'Analytics', 'Appointments']}
          firstFlow="'Odalarimiz ve fiyatlarimiz...' + yardimci yonlendirme + kampanya bildirimi"
        />
        <SectorCard
          icon={Building2}
          title="Genel Hizmet / Kurumsal"
          primary={['Flow Builder', 'FAQ', 'Analytics']}
          secondary={['Outbound', 'Marketing', 'Sentiment']}
          firstFlow="'Size nasil yardimci olabiliriz?' + departmana yonlendirme + FAQ"
        />
      </div>

      <Warning>
        Sektor sablon akislari "ornek" olarak kullan, her musteriye birebir kopyalama.
        Her isletmenin kendine ozgu sorulari ve surecleri var. Template'i baslangic noktasi yap,
        musteri ile birlikte kisisellistir. <TabLink to="saas">SaaS Stratejisi</TabLink> sekmesinde olceklenebilir
        onboarding icin template flow + FAQ paketi sistemini detayli anlatiyoruz.
      </Warning>

      <div>
        <SectionTitle icon={Target}>Feature Flag Stratejisi</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">
            Tenant olustururken sektore gore asagidaki flag'leri ac:
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
                  <td className="text-center py-2 px-2"><Badge variant="warning">Ihtiyaca gore</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Appointments</td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="warning">Sektore gore</Badge></td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">Integrations</td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="default">Kapali</Badge></td>
                  <td className="text-center py-2 px-2"><Badge variant="warning">Teknik musteri</Badge></td>
                </tr>
              </tbody>
            </table>
          </div>
        </Card>
      </div>

      <Card className="p-4 bg-navy-50/50">
        <h4 className="text-xs font-semibold text-navy-400 uppercase tracking-wide mb-2">Ilgili Sekmeler</h4>
        <div className="flex flex-wrap gap-3 text-sm">
          <TabLink to="features">Ozellik Rehberi</TabLink>
          <TabLink to="onboarding">Onboarding Adimlari</TabLink>
          <TabLink to="saas">SaaS Stratejisi (Olcekleme)</TabLink>
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
          {copied ? 'Kopyalandi' : 'Kopyala'}
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
        <SectionTitle icon={MessageCircle}>Musteri Iletisim Rehberi</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Ozellikleri nasil anlatirsaniz o kadar etkili olur. Asagida her durum icin hazir sablonlar var.
          Kopyala butonuyla direkt alip kisisellestirebilirsin.
          Hangi musteriye ne zaman mesaj atacagini <TabLink to="saas">SaaS Stratejisi: Health Score</TabLink> ile belirle.
        </p>
      </div>

      <div>
        <h4 className="text-sm font-semibold text-navy-900 mb-3">Yeni Ozellik Duyurma Formati</h4>
        <Card className="p-4 space-y-4">
          <div>
            <div className="flex items-center gap-2 mb-2">
              <Badge variant="error">Yanlis</Badge>
            </div>
            <div className="p-3 bg-red-50 border border-red-100 rounded-lg text-sm text-navy-600 italic">
              "v2.3 cikti! 14 yeni ozellik, 47 bug fix, 8 improvement. Detaylar icin changelog'a bakin..."
            </div>
          </div>
          <div>
            <div className="flex items-center gap-2 mb-2">
              <Badge variant="success">Dogru</Badge>
            </div>
            <div className="p-3 bg-emerald-50 border border-emerald-100 rounded-lg text-sm text-navy-600 italic">
              "Artik musteriniz randevu aldiginda otomatik hatirlatma mesaji gidiyor.
              Acmak icin: Ayarlar &gt; Randevular &gt; Hatirlatma &gt; Ac. 3 adim, 1 dakika."
            </div>
          </div>
          <Tip>
            <strong>Format:</strong> Problem → Cozum → Nasil (max 3 adim).
            Teknik detay yok. Musteri ne kazaniyor, o kadar.
          </Tip>
        </Card>
      </div>

      {/* ─── WhatsApp Sablonları ───────────────────────────── */}

      <div>
        <div className="flex items-center gap-2 mb-3">
          <h4 className="text-sm font-semibold text-navy-900">WhatsApp Mesaj Sablonlari</h4>
          <Badge variant="success">8 Sablon</Badge>
        </div>
        <div className="space-y-3">
          <TemplateCard
            label="Ilk Gun — Hosgeldin"
            variant="info"
            channel="whatsapp"
            content={`Merhaba [Firma Adi]!

Invekto sisteminiz hazir. Ilk otomatik cevabiniz aktif.

Simdi yapmaniz gereken:
1. Telefonunuzdan [numara] numarasina "merhaba" yazin
2. Otomatik cevabi gorun
3. Degistirmek isterseniz beni arayin

Panel giris bilgileriniz:
Adres: [URL]
Kullanici: [user]
Sifre: [pass]

Herhangi bir sorunuz olursa yazin!`}
          />

          <TemplateCard
            label="3. Gun — Kontrol"
            variant="info"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

Sisteminiz 3 gundur aktif. Hersey yolunda mi?

Simdiye kadar:
- [X] mesaj geldi
- [Y] tanesi otomatik cevaplandi

Sorun veya soru varsa hemen yazin, birlikte cozelim.`}
          />

          <TemplateCard
            label="Hafta Sonu — Takip"
            variant="info"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

Ilk haftaniz nasil gecti? Hizli bir ozet:
- Bu hafta [X] mesaj geldi
- [Y] tanesi otomatik cevaplandi
- En cok sorulan konu: [konu]

Eklemek istediginiz soru/cevap var mi?
Degistirmek istediginiz bir sey varsa yazin, hemen ayarlayalim.`}
          />

          <TemplateCard
            label="Yeni Ozellik Duyurusu"
            variant="info"
            channel="whatsapp"
            content={`Merhaba [Firma Adi]!

Yeni ozellik: [tek cumle aciklama].

Ne ise yarar: [1 cumle — sorun ve cozum].
Nasil acilir: [max 3 adim].

Birlikte kuralim mi? Musait oldugunuz bir zaman yazin.`}
          />

          <TemplateCard
            label="Aylik Rapor"
            variant="info"
            channel="whatsapp"
            content={`[Firma Adi] — [Ay] Performans Ozeti

Toplam mesaj: [X]
Otomatik cevaplanan: [Y] (%[Z])
En cok sorulan: [konu1], [konu2], [konu3]
Kampanya gonderilen: [varsa]

Onerimiz: "[oneri — ornegin yeni FAQ eklemek]"
Bu ay [yeni ozellik] cikti, sizin icin uygun olabilir.

Detayli gormek icin panelinize girin: [URL]`}
          />

          <TemplateCard
            label="Odeme Hatirlatma"
            variant="warning"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

[Ay] donemi icin faturaniz olusturulmustur.

Tutar: [tutar] TL
Son odeme tarihi: [tarih]

Odeme bilgileri:
IBAN: [IBAN]
Aciklama: [firma kodu]

Sorunuz varsa yazabilirsiniz.
Iyi calismalar dileriz!`}
          />

          <TemplateCard
            label="Churn Riski — Yeniden Baglanti"
            variant="error"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

Bir suredir panelinize girmediginizi fark ettik. Umariz hersey yolundadir.

Son 30 gunde sisteminiz sizin icin calismaya devam etti:
- [X] mesaj otomatik cevaplandi
- [Y] musteri yonlendirildi

Kullanmakta zorluk yasiyorsaniz size ozel 15 dakikalik bir gorusme ayarlayalim.
Hicbir sey sormak zorunda degilsiniz — sadece "uygun" yazin, ben ararim.`}
          />

          <TemplateCard
            label="Sozlesme Yenileme"
            variant="default"
            channel="whatsapp"
            content={`Merhaba [Firma Adi],

Sozlesmenizin yenileme zamani yaklasti ([tarih]).

Son [donem] boyunca:
- Toplam [X] mesaj islendi
- %[Y] otomatik cevap orani
- Tahmini tasarruf: [Z] saat/ay

Yenileme icin herhangi bir islem yapmaniza gerek yok, otomatik devam eder.
Soru veya degisiklik istegi varsa yazin.`}
          />
        </div>
      </div>

      {/* ─── Email Sablonları ─────────────────────────────── */}

      <div>
        <div className="flex items-center gap-2 mb-3">
          <h4 className="text-sm font-semibold text-navy-900">Email Sablonlari</h4>
          <Badge variant="warning">6 Sablon</Badge>
        </div>
        <Tip>
          Email sablonlari daha resmi ve detaylidir. Kurumsal musteriler veya
          resmi yazisma gerektiren durumlarda kullan. WhatsApp kisa ve samimi,
          email profesyonel ve dokumante edici olmali.
        </Tip>
        <div className="space-y-3 mt-3">
          <TemplateCard
            label="Hosgeldin — Kurulum Tamam"
            variant="info"
            channel="email"
            content={`Konu: Invekto Sisteminiz Hazir — Giris Bilgileriniz

Merhaba [Yetkili Adi],

[Firma Adi] icin Invekto musteri iletisim sisteminiz basariyla kurulmustur.

Sistem Bilgileri:
- Panel Adresi: [URL]
- Kullanici Adi: [user]
- Sifre: [pass] (ilk giriste degistirmenizi oneririz)
- WhatsApp Hatti: [numara]

Ilk Adimlar:
1. Telefonunuzdan [numara] numarasina "merhaba" yazarak sistemi test edin
2. Panelden Bilgi Bankasi'na sik sorulan sorularinizi ekleyin
3. Ayarlar > Calisma Saatleri bolumunden mesai saatlerinizi belirleyin

Kurulum sirasinda olusturdigumuz otomatik cevap akisiniz aktiftir.
Herhangi bir degisiklik veya ekleme ihtiyaciniz olursa benimle iletisime gecin.

Iyi calismalar,
[Isim]
Invekto Destek Ekibi
[Telefon]`}
          />

          <TemplateCard
            label="Haftalik Performans Raporu"
            variant="info"
            channel="email"
            content={`Konu: [Firma Adi] — Haftalik Invekto Raporu ([tarih araligi])

Merhaba [Yetkili Adi],

Gectigimiz haftaya ait Invekto performans ozetiniz:

MESAJ ISTATISTIKLERI
- Gelen mesaj: [X]
- Otomatik cevaplanan: [Y] (%[oran])
- Insana yonlendirilen: [Z]
- Ortalama cevap suresi: [sure]

EN COK SORULAN KONULAR
1. [Konu 1] — [adet] kez
2. [Konu 2] — [adet] kez
3. [Konu 3] — [adet] kez

ONERIMIZ
[Konu 1] ile ilgili bilgi bankasina su cevabi eklersek otomatik karsilama orani %[X] artabilir:
"[onerilen cevap]"

Bu degisikligi yapmami ister misiniz? Tek kelimeyle "evet" yazmaniz yeterli.

Detayli istatistikler icin paneliniz: [URL]

Iyi calismalar,
[Isim]`}
          />

          <TemplateCard
            label="Aylik Performans Raporu"
            variant="info"
            channel="email"
            content={`Konu: [Firma Adi] — [Ay] Aylik Performans Raporu

Merhaba [Yetkili Adi],

[Ay] ayina ait Invekto performans raporunuz:

GENEL BAKIS
- Toplam mesaj: [X]
- Otomatik cevap orani: %[Y]
- Insana yonlendirme: [Z] mesaj
- Ortalama ilk cevap suresi: [sure]

ONCEKI AY KARSILASTIRMA
- Mesaj hacmi: [artis/azalis]% [yonu]
- Otomasyon orani: [onceki]% → [simdiki]%
- Yonlendirme: [onceki] → [simdiki]

KAMPANYA SONUCLARI (varsa)
- Gonderilen: [X]
- Teslim edilen: [Y]
- Cevap alan: [Z]

EN COK SORULAN 5 KONU
1. [Konu] — [adet] kez
2. [Konu] — [adet] kez
3. [Konu] — [adet] kez
4. [Konu] — [adet] kez
5. [Konu] — [adet] kez

TAHMINI TASARRUF
Otomatik cevaplanan [Y] mesaj, ortalama [A] dakika/mesaj hesabiyla ayda yaklasik [B] saat is gucunuz tasarruf edilmistir.

ONERILER
1. [Oneri 1 — ornegin eksik FAQ]
2. [Oneri 2 — ornegin yeni modul acilimi]

Detayli panele giris: [URL]
Bir sonraki gorusmemiz: [tarih/saat]

Iyi calismalar,
[Isim]
Invekto Destek Ekibi`}
          />

          <TemplateCard
            label="Yeni Ozellik Duyurusu"
            variant="info"
            channel="email"
            content={`Konu: Yeni Ozellik — [Ozellik Adi] Artik Kullanima Hazir

Merhaba [Yetkili Adi],

Invekto sisteminize yeni bir ozellik eklendi: [Ozellik Adi].

NE ISE YARAR?
[1-2 cumle: hangi sorunu cozer, ne kolaylastiriyor]

SIZIN ICIN NE DEGISIR?
[1-2 cumle: somut fayda, ornek senaryo]

NASIL ETKINLESTIRILIR?
1. [Adim 1]
2. [Adim 2]
3. [Adim 3]

Bu ozelligi sizin icin etkinlestirmemi ister misiniz?
Dilediginiz zaman donus yapabilirsiniz — birlikte ayarlayabiliriz.

Iyi calismalar,
[Isim]`}
          />

          <TemplateCard
            label="Odeme / Fatura"
            variant="warning"
            channel="email"
            content={`Konu: [Firma Adi] — [Ay] Donemi Fatura Bildirimi

Merhaba [Yetkili Adi],

[Ay] donemi icin Invekto hizmet faturaniz olusturulmustur.

FATURA DETAYLARI
- Donem: [baslangic] — [bitis]
- Tutar: [tutar] TL (KDV dahil)
- Son odeme tarihi: [tarih]
- Fatura no: [numara]

ODEME BILGILERI
Banka: [banka]
IBAN: [IBAN]
Hesap Adi: [hesap adi]
Aciklama: [firma kodu] — [ay]

Faturaniz ekte yer almaktadir.
Odeme sonrasi dekont gondermenize gerek yoktur — otomatik eslestirme yapilmaktadir.

Sorunuz varsa donus yapin.

Iyi calismalar,
[Isim]`}
          />

          <TemplateCard
            label="Sozlesme Yenileme"
            variant="default"
            channel="email"
            content={`Konu: [Firma Adi] — Sozlesme Yenileme Bildirimi

Merhaba [Yetkili Adi],

Invekto hizmet sozlesmenizin suresi [tarih] tarihinde dolmaktadir.

GECEN DONEM OZETI
- Toplam islenen mesaj: [X]
- Otomatik cevap orani: %[Y]
- Kullanilan moduller: [modul listesi]
- Tahmini tasarruf: [Z] saat/ay

YENILEME KOSULLARI
- Donem: [baslangic] — [bitis]
- Aylik ucret: [tutar] TL
- Degisiklik: [varsa belirt / "mevcut kosullar gecerlidir"]

Herhangi bir islem yapmaniza gerek yoktur — sozlesmeniz [tarih] itibariyle otomatik olarak yenilenir.

Modul ekleme/cikarma veya plan degisikligi icin [tarih]'e kadar bize bildirebilirsiniz.

Iyi calismalar,
[Isim]
Invekto Destek Ekibi`}
          />
        </div>
      </div>

      {/* ─── İletişim Takvimi ─────────────────────────────── */}

      <div>
        <h4 className="text-sm font-semibold text-navy-900 mb-3">Iletisim Takvimi</h4>
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
                  <td className="py-2 pr-4 font-medium">Ilk gun</td>
                  <td className="py-2 pr-4">Hosgeldin mesaji + giris bilgileri</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="success">WhatsApp</Badge>
                      <Badge variant="warning">Email</Badge>
                    </div>
                  </td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">3. gun</td>
                  <td className="py-2 pr-4">"Nasil gidiyor?" kontrolu</td>
                  <td className="py-2"><Badge variant="success">WhatsApp</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">7. gun</td>
                  <td className="py-2 pr-4">Haftalik rapor + takip gorusmesi</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="warning">Email</Badge>
                      <Badge variant="info">Telefon</Badge>
                    </div>
                  </td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">14. gun</td>
                  <td className="py-2 pr-4">Yeni modul acimlari + egitim</td>
                  <td className="py-2"><Badge variant="success">WhatsApp</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Her hafta</td>
                  <td className="py-2 pr-4">Haftalik performans raporu</td>
                  <td className="py-2"><Badge variant="warning">Email</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Her ay</td>
                  <td className="py-2 pr-4">Aylik performans raporu + fatura</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="warning">Email</Badge>
                      <Badge variant="success">WhatsApp</Badge>
                    </div>
                  </td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Yeni ozellik</td>
                  <td className="py-2 pr-4">Duyuru + nasil acilir</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="success">WhatsApp</Badge>
                      <Badge variant="warning">Email</Badge>
                    </div>
                  </td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Odeme zamani</td>
                  <td className="py-2 pr-4">Fatura + odeme bilgileri</td>
                  <td className="py-2">
                    <div className="flex gap-1">
                      <Badge variant="warning">Email</Badge>
                      <Badge variant="success">WhatsApp</Badge>
                    </div>
                  </td>
                </tr>
                <tr>
                  <td className="py-2 pr-4 font-medium">Sozlesme yenileme</td>
                  <td className="py-2 pr-4">Donem ozeti + yenileme bildirimi</td>
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
                <li>Hizli, kisa bilgilendirmeler</li>
                <li>Gunluk/haftalik takip mesajlari</li>
                <li>Yeni ozellik duyurulari (kisa versiyon)</li>
                <li>Odeme hatirlatmalari (samimi ton)</li>
                <li>Sorun/sikayet anlinda hizli donus</li>
                <li>Video ve gorsel icerik paylasimi</li>
              </ul>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <Mail className="w-4 h-4 text-amber-500" />
                <span className="text-sm font-semibold text-navy-900">Email Kullan</span>
              </div>
              <ul className="text-sm text-navy-600 space-y-1 pl-6 list-disc">
                <li>Resmi bildirimler (fatura, sozlesme)</li>
                <li>Detayli performans raporlari</li>
                <li>Giris bilgileri ve teknik dokumantasyon</li>
                <li>Yeni ozellik detayli aciklamalari</li>
                <li>Kayit altinda olmasi gereken yazismalar</li>
                <li>Ek dosya gondermek gerektiginde</li>
              </ul>
            </div>
          </div>
          <Tip>
            <strong>Altin kural:</strong> Ayni icerik icin ikisini birden gonderme.
            WhatsApp kisa ozeti, email detayli versiyonu olsun.
            Ornek: Odeme icin WhatsApp'tan hatirlatma, email'den fatura.
          </Tip>
        </Card>
      </div>

      {/* ─── Kriz Yönetimi ────────────────────────────────── */}

      <div>
        <h4 className="text-sm font-semibold text-navy-900 mb-3">Krizimi Donustur</h4>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">
            Musteri "kullanmiyorum, iptal edecem" dediginde:
          </p>
          <ol className="list-decimal pl-5 space-y-2 text-sm text-navy-700">
            <li><strong>Dinle, savunma:</strong> "Hangi konuda zorluk yasiyorsunuz?"</li>
            <li><strong>Veri goster:</strong> "Gecen ay [X] mesaj otomatik cevaplandi, bu [Y] saat tasarruf demek."</li>
            <li><strong>Basitlestir:</strong> Kullanmadigi modulleri kapat, sadece calisanlari birak.</li>
            <li><strong>Egitim teklif et:</strong> "15 dk'lik bir gorusme ile tekrar kuralim, ben yaninizdayim."</li>
            <li><strong>Takip:</strong> 3 gun sonra tekrar sor, iyilesti mi?</li>
          </ol>
          <Warning>
            Asla "ama cok iyi ozelliklerimiz var" deme.
            Musterinin sorununu coz, ozellik listesi sayma.
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
        <SectionTitle icon={TrendingUp}>SaaS Buyume Stratejisi</SectionTitle>
        <p className="text-sm text-navy-500 mb-4">
          Teknik olarak urunu yazmak isin yarisi. Diger yarisi: fiyatlandirma, musteri sagligi,
          churn onleme, upsell ve olcekleme. Asagidaki stratejiler gercek SaaS deneyiminden geliyor.
        </p>
      </div>

      {/* ─── 1. Fiyatlandırma ─────────────────────────────── */}

      <div>
        <SectionTitle icon={DollarSign}>1. Fiyatlandirma: Deger Bazli Paketleme</SectionTitle>
        <Card className="p-4 space-y-4">
          <div>
            <h4 className="text-sm font-semibold text-navy-900 mb-2">Modul Satma, Sonuc Sat</h4>
            <div className="space-y-2 text-sm">
              <div className="flex items-center gap-3">
                <span className="text-red-500 line-through flex-shrink-0">"Flow Builder 500, Analytics 200, Marketing 300..."</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-emerald-700 font-medium">"Baslangic: Mesajlara otomatik cevap / Buyume: Musteriyi geri getir / Pro: Tam CRM"</span>
              </div>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Paket</th>
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Icerik</th>
                  <th className="text-left py-2 font-medium text-navy-500">Hedef Musteri</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4"><Badge variant="default">Baslangic</Badge></td>
                  <td className="py-2 pr-4">Flow + FAQ + Calisma Saatleri</td>
                  <td className="py-2">"Mesajlara yetisemiyorum"</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4"><Badge variant="info">Buyume</Badge></td>
                  <td className="py-2 pr-4">+ Analytics + Outbound + Kampanya</td>
                  <td className="py-2">"Musterileri geri getirmek istiyorum"</td>
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
            <h5 className="text-sm font-semibold text-emerald-800 mb-1">Yillik Odeme Indirimi</h5>
            <p className="text-sm text-emerald-700">
              Aylik x12 yerine 10 aylik ucret al (2 ay bedava). Churn'u <strong>%30-40 dusurur</strong> cunku
              musteri 12 ay boyunca cikmaz ve o surede alisir. Bu mevcut <TabLink to="sectors">feature flag stratejisi</TabLink> ile uyumlu calisiyor.
            </p>
          </div>

          <Warning>
            Musteri "bu kadar para neden oduyorum?" diye sordiginda, <TabLink to="communication">aylik rapor sablonu</TabLink> ile
            somut tasarruf rakamlarini goster.
          </Warning>
        </Card>
      </div>

      {/* ─── 2. Müşteri Sağlık Skoru ─────────────────────── */}

      <div>
        <SectionTitle icon={Activity}>2. Musteri Saglik Skoru (Health Score)</SectionTitle>
        <Card className="p-4 space-y-4">
          <p className="text-sm text-navy-600">
            Bir musterinin "iyi" mi "kotu" mu oldugunu hislere birakma, veriyle ol. Basit bir formul:
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
                  <td className="py-2 pr-4">Son 7 gunde panel girisi var mi?</td>
                  <td className="text-center py-2 px-2">%25</td>
                  <td className="py-2">Evet=100, Hayir=0</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Bu hafta flow calisti mi?</td>
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
                  <td className="py-2 pr-4">Son 30 gunde destek talebi var mi?</td>
                  <td className="text-center py-2 px-2">%15</td>
                  <td className="py-2">Evet=50, Hayir=100</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="p-3 bg-emerald-50 rounded-lg border border-emerald-100 text-center">
              <div className="text-lg font-bold text-emerald-700">80-100</div>
              <div className="text-xs text-emerald-600">Saglikli</div>
              <div className="text-xs text-navy-400 mt-0.5">Upsell zamani</div>
            </div>
            <div className="p-3 bg-amber-50 rounded-lg border border-amber-100 text-center">
              <div className="text-lg font-bold text-amber-700">60-79</div>
              <div className="text-xs text-amber-600">Dikkat</div>
              <div className="text-xs text-navy-400 mt-0.5">Takip mesaji gonder</div>
            </div>
            <div className="p-3 bg-red-50 rounded-lg border border-red-100 text-center">
              <div className="text-lg font-bold text-red-700">0-59</div>
              <div className="text-xs text-red-600">Risk</div>
              <div className="text-xs text-navy-400 mt-0.5">Hemen ara, <TabLink to="communication">churn sablonunu</TabLink> kullan</div>
            </div>
          </div>

          <Tip>
            Bu veriler zaten DB'de var (login tarihi, flow execution, FAQ sayisi). Bir endpoint + dashboard widget'i yeter.
            <TabLink to="actions">Aksiyon listesinde</TabLink> bu madde mevcut.
          </Tip>
        </Card>
      </div>

      {/* ─── 3. Aha Moment ───────────────────────────────── */}

      <div>
        <SectionTitle icon={Zap}>3. "Aha Moment" Hizini Ol</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            SaaS'in en kritik metrigi: musteri ilk degeri ne kadar hizli gordu?
          </p>
          <div className="p-3 bg-brand-50 border border-brand-100 rounded-lg">
            <p className="text-sm text-brand-800 font-medium">
              Invekto icin "aha moment" = Musterinin telefonundan gonderdigi mesaja otomatik cevap donmesi.
            </p>
          </div>
          <div className="space-y-2 text-sm text-navy-700">
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span><strong>Ol:</strong> Tenant olusturma → ilk flow aktif olma suresi</span>
            </div>
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span><strong>Ol:</strong> Ilk flow aktif → ilk otomatik cevap suresi</span>
            </div>
            <div className="flex items-start gap-2">
              <ChevronRight className="w-4 h-4 text-brand-500 flex-shrink-0 mt-0.5" />
              <span><strong>Hedef:</strong> Ilk 1 saatte aha moment. 24 saatten uzunsa kayip riski cok yuksek.</span>
            </div>
          </div>
          <Tip>
            <TabLink to="onboarding">Onboarding adimlari</TabLink> bu hedefe gore tasarlandi:
            Adim 5 (ilk akis) + Adim 6 (canli test) = aha moment.
          </Tip>
        </Card>
      </div>

      {/* ─── 4. Ölçekleme ────────────────────────────────── */}

      <div>
        <SectionTitle icon={Users}>4. Onboarding'i Olceklendir</SectionTitle>
        <Card className="p-4 space-y-4">
          <p className="text-sm text-navy-600">
            Su anda her musteriye bizzat kurulum yapiyorsun. Bu 5 musteriye kadar calisir, 50'de coker.
          </p>

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Simdi (1-10)</th>
                  <th className="text-left py-2 pr-4 font-medium text-navy-500">Yakinda (10-50)</th>
                  <th className="text-left py-2 font-medium text-navy-500">Olcekte (50+)</th>
                </tr>
              </thead>
              <tbody className="text-navy-700">
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Her musteriye bizzat kurulum</td>
                  <td className="py-2 pr-4">Template flow + self-service setup</td>
                  <td className="py-2">Tamamen self-service + otomatik onboarding</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">WhatsApp'tan takip</td>
                  <td className="py-2 pr-4">Otomatik takip mesajlari (Outbound)</td>
                  <td className="py-2">In-app guided tour + email dizisi</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">Manuel FAQ girisi</td>
                  <td className="py-2 pr-4">Sektor bazli hazir FAQ paketleri</td>
                  <td className="py-2">AI ile web sitesinden FAQ cekme</td>
                </tr>
              </tbody>
            </table>
          </div>

          <Warning>
            Acil: <TabLink to="sectors">Sektor senaryolari</TabLink> sekmesindeki her sektor icin
            template flow + FAQ paketi olustur. Yeni tenant acilirken "sektor sec" → hazir set otomatik yuklensin.
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
                <h5 className="text-sm font-semibold text-red-800">Sessizlik: 2 haftadir panele girmemis</h5>
                <p className="text-xs text-red-700 mt-0.5">
                  Yapilacak: <TabLink to="communication">Churn riski sablonunu</TabLink> gonder. "Sisteminiz sizin icin [X] mesaj cevapladi" de, "Neden girmiyorsunuz?" deme.
                </p>
              </div>
            </div>
            <div className="flex items-start gap-3 p-3 bg-amber-50 border border-amber-100 rounded-lg">
              <div className="w-8 h-8 rounded-lg bg-amber-100 flex items-center justify-center flex-shrink-0">
                <span className="text-amber-700 font-bold text-sm">2</span>
              </div>
              <div>
                <h5 className="text-sm font-semibold text-amber-800">Sikayet artisi: "Calismiyor", "Anlamadim", "Zor"</h5>
                <p className="text-xs text-amber-700 mt-0.5">
                  Yapilacak: Hemen basitlestir. Modulleri kapa, akisi sadece temel <TabLink to="features">ozelliklerle</TabLink> birak.
                </p>
              </div>
            </div>
            <div className="flex items-start gap-3 p-3 bg-navy-50 border border-navy-100 rounded-lg">
              <div className="w-8 h-8 rounded-lg bg-navy-100 flex items-center justify-center flex-shrink-0">
                <span className="text-navy-700 font-bold text-sm">3</span>
              </div>
              <div>
                <h5 className="text-sm font-semibold text-navy-800">Odeme gecikmesi: Fatura 1 haftadir odenmemis</h5>
                <p className="text-xs text-navy-600 mt-0.5">
                  Yapilacak: Direkt fatura konusu acma. "Nasilsiniz, bir sorun var mi?" de, fatura kendilginden acilir.
                </p>
              </div>
            </div>
          </div>

          <Tip>Churn olustuktan sonra mudahale etmek cok gec. Sinyal gordugunde hareket et, bekleme.</Tip>
        </Card>
      </div>

      {/* ─── 6. Upsell ───────────────────────────────────── */}

      <div>
        <SectionTitle icon={Repeat}>6. Upsell: Mevcut Musteriden Daha Fazla Gelir</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            Yeni musteri kazanmak, mevcut musteriye satmaktan <strong>5-7 kat daha pahali</strong>.
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
                  <td className="py-2 pr-4">Aylik mesaj hacmi 500'u gecti</td>
                  <td className="py-2">"Hacminiz artti, Analytics ile trendleri gorebilirsiniz"</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Flow'da 3+ dallanma var</td>
                  <td className="py-2">"AI Intent ile otomatik dallandirma yapabiliriz"</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">FAQ'dan cok cevap donuyor</td>
                  <td className="py-2">"Knowledge Base genisletip Sentiment ile memnuniyeti olcelim"</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">"Toplu mesaj atabilir miyim?" diye sordu</td>
                  <td className="py-2">Outbound modulunu ac — tam zamani</td>
                </tr>
              </tbody>
            </table>
          </div>

          <Tip>
            <strong>Zamanlama:</strong> <TabLink to="communication">Aylik rapor</TabLink> gonderdikten sonra.
            "Bu ay 500 mesaj geldi, %60'i otomatik cevaplandi. Analytics ile bu orani %80'e cikarabiliriz." — veri + oneri + somut hedef.
          </Tip>
        </Card>
      </div>

      {/* ─── 7. Referral ─────────────────────────────────── */}

      <div>
        <SectionTitle icon={UserPlus}>7. Referral: Musteri Musteri Getirsin</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">En guclu satis kanali: memnun musterinin tavsiyesi.</p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div className="p-3 bg-brand-50 rounded-lg border border-brand-100">
              <h5 className="text-sm font-semibold text-brand-800">Getiren Musteri</h5>
              <p className="text-xs text-brand-700 mt-1">1 ay ucretsiz veya %20 indirim</p>
            </div>
            <div className="p-3 bg-emerald-50 rounded-lg border border-emerald-100">
              <h5 className="text-sm font-semibold text-emerald-800">Gelen Musteri</h5>
              <p className="text-xs text-emerald-700 mt-1">Ilk ay %20 indirim</p>
            </div>
          </div>
          <Tip>
            Ne zaman iste: Health Score 80+ olan musteriye <TabLink to="communication">aylik rapor</TabLink> sonrasinda.
            "Cevrenizdeki isletmelere de onerebilirsiniz, referans linkiniz: [link]"
          </Tip>
        </Card>
      </div>

      {/* ─── 8. Yapışkanlık ──────────────────────────────── */}

      <div>
        <SectionTitle icon={Heart}>8. "Yapiskan" Urun Yap</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            Musterinin ayrilma maliyetini "sozlesme cezasi" ile degil, <strong>biriken deger</strong> ile yukselt.
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
                  <td className="py-2">Baska yere tasimak zahmetli</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">6 aylik analiz verisi</td>
                  <td className="py-2">Gecmisi kaybeder</td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4">Ozellesmis 10+ flow</td>
                  <td className="py-2">Sifirdan kurmak 2 hafta surer</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4">Musteri aliskanligi</td>
                  <td className="py-2">"WhatsApp'a yaziyorum, cevap geliyor" refleksi</td>
                </tr>
              </tbody>
            </table>
          </div>
          <Tip>
            Ilk aydan FAQ doldurtmaya, flow ozellestirtmeye tesvik et. Ne kadar cok veri girerse, o kadar bagimli olur.
            <TabLink to="onboarding">Onboarding adim 7</TabLink> (FAQ doldurmak) bu yuzden kritik.
          </Tip>
        </Card>
      </div>

      {/* ─── 9. Rakip Sorusu ─────────────────────────────── */}

      <div>
        <SectionTitle icon={Shield}>9. Rakip Sorusuyla Basa Cik</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">Musteri "Neden sizi seceyim?" dediginde:</p>
          <div className="space-y-2 text-sm">
            <div className="flex items-center gap-3">
              <span className="text-red-500 line-through flex-shrink-0">Rakibi kotuleme, ozellik listesi karsilastirma</span>
            </div>
            <div className="flex items-center gap-3">
              <ArrowRight className="w-3 h-3 text-emerald-500 flex-shrink-0" />
              <span className="text-emerald-700 font-medium">Kendi farkini soyle:</span>
            </div>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 mt-2">
            <div className="p-2.5 bg-brand-50 rounded-lg border border-brand-100">
              <div className="text-xs font-semibold text-brand-700">Yerel Destek</div>
              <div className="text-xs text-brand-600 mt-0.5">Turkce konusuyorsun, arayinca aciyorsun</div>
            </div>
            <div className="p-2.5 bg-brand-50 rounded-lg border border-brand-100">
              <div className="text-xs font-semibold text-brand-700">Hizli Kurulum</div>
              <div className="text-xs text-brand-600 mt-0.5">1 saat icinde calisir durumda</div>
            </div>
            <div className="p-2.5 bg-brand-50 rounded-lg border border-brand-100">
              <div className="text-xs font-semibold text-brand-700">Kisisellestirme</div>
              <div className="text-xs text-brand-600 mt-0.5">Musteriye ozel flow yaparsın</div>
            </div>
            <div className="p-2.5 bg-brand-50 rounded-lg border border-brand-100">
              <div className="text-xs font-semibold text-brand-700">Fiyat</div>
              <div className="text-xs text-brand-600 mt-0.5">Dolar bazli degil, TL bazli</div>
            </div>
          </div>
          <div className="p-3 bg-navy-50 rounded-lg text-sm text-navy-700 italic">
            "Digerleri de yapar ama ben 1 saatte kurar, sorun olursa WhatsApp'tan yazarsiniz, 5 dakikada donerim."
          </div>
        </Card>
      </div>

      {/* ─── 10. Destek Süreci ───────────────────────────── */}

      <div>
        <SectionTitle icon={Handshake}>10. Destek Surecini Yapilandir</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            WhatsApp'tan destek vermek samimi ama olceklenmiyor ve kaybolur.
          </p>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <h5 className="text-sm font-semibold text-navy-900 mb-2">Kisa Vadede</h5>
              <ul className="text-sm text-navy-600 space-y-1 pl-5 list-disc">
                <li>Her destek talebini bir yere not et (Notion, Excel, hatta txt)</li>
                <li>En cok hangi konuda soru geldigini takip et</li>
                <li>Tekrarlayan sorular icin hazir cevap sablonlari olustur</li>
              </ul>
            </div>
            <div>
              <h5 className="text-sm font-semibold text-navy-900 mb-2">Orta Vadede</h5>
              <ul className="text-sm text-navy-600 space-y-1 pl-5 list-disc">
                <li>Dashboard'a "Destek Talebi" butonu ekle</li>
                <li>Musterinin panelinden sorun bildirmesini sagla</li>
                <li>Basit bir ticket sistemi (karmasik olmasin)</li>
              </ul>
            </div>
          </div>
          <Tip>
            Tekrarlayan destek konularini <TabLink to="features">FAQ ve Knowledge Base'e</TabLink> ekle.
            En iyi destek, musterinin destek istemeye ihtiyac duymamasidir.
          </Tip>
        </Card>
      </div>

      {/* ─── 11. Metrikler ───────────────────────────────── */}

      <div>
        <SectionTitle icon={BarChart3}>11. Temel Metrikleri Takip Et</SectionTitle>
        <Card className="p-4 space-y-3">
          <p className="text-sm text-navy-600">
            Baslangiçta olsan bile su 5 metrigi her ay bir kagida yaz. Olcmedigin seyi iyilestiremezsin.
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
                  <td className="py-2 pr-4">Aylik tekrarlayan gelir</td>
                  <td className="py-2"><Badge variant="success">Her ay artsin</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Churn Rate</td>
                  <td className="py-2 pr-4">Ayrilan musteri / toplam</td>
                  <td className="py-2"><Badge variant="warning">{'<'}%5/ay</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">Time-to-Value</td>
                  <td className="py-2 pr-4">Kurulumdan ilk aha moment'e sure</td>
                  <td className="py-2"><Badge variant="warning">{'<'}1 saat</Badge></td>
                </tr>
                <tr className="border-b border-navy-50">
                  <td className="py-2 pr-4 font-medium">NPS</td>
                  <td className="py-2 pr-4">"Bizi tavsiye eder misiniz?" 1-10</td>
                  <td className="py-2"><Badge variant="warning">{'>'}8</Badge></td>
                </tr>
                <tr>
                  <td className="py-2 pr-4 font-medium">DAU/MAU</td>
                  <td className="py-2 pr-4">Son 7 gunde giris yapan / toplam</td>
                  <td className="py-2"><Badge variant="warning">{'>'}%70</Badge></td>
                </tr>
              </tbody>
            </table>
          </div>
          <Tip>
            Bunu simdiye kadar Excel tablosuna bile yazsan yeter. Ama <strong>yaz</strong>.
            Tum bu metriklerin iyilesmesi icin <TabLink to="actions">aksiyon listesini</TabLink> takip et.
          </Tip>
        </Card>
      </div>

      {/* ─── Öncelik Sırası ──────────────────────────────── */}

      <div>
        <SectionTitle icon={Target}>Oncelik Sirasi</SectionTitle>
        <Card className="p-4">
          <p className="text-sm text-navy-600 mb-3">Bunlarin hepsini birden yapma. Sira:</p>
          <div className="space-y-2">
            <div className="flex items-center gap-3">
              <Badge variant="error">Hemen</Badge>
              <span className="text-sm text-navy-700">Sektor bazli template flow'lari olustur</span>
            </div>
            <div className="flex items-center gap-3">
              <Badge variant="warning">Bu Ay</Badge>
              <span className="text-sm text-navy-700">Fiyatlandirma paketlerini belirle (Baslangic/Buyume/Pro)</span>
            </div>
            <div className="flex items-center gap-3">
              <Badge variant="info">Bu Ceyrek</Badge>
              <span className="text-sm text-navy-700">Health Score widget'ini dashboard'a ekle</span>
            </div>
            <div className="flex items-center gap-3">
              <Badge variant="default">Surekli</Badge>
              <span className="text-sm text-navy-700">MRR ve churn takibi, aylik rapor disiplini</span>
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
          Asagidaki listeyi siraliyla takip et. Tikladigin maddeler isaretlenir (sadece bu oturum icin).
          Onceligi yuksek olanlari once yap. SaaS stratejisi maddeleri de dahil.
        </p>
        <div className="flex flex-wrap gap-2 text-sm">
          <span className="text-navy-400">Ilgili sekmeler:</span>
          <TabLink to="onboarding">Onboarding Adimlari</TabLink>
          <TabLink to="saas">SaaS Stratejisi</TabLink>
          <TabLink to="communication">Musteri Iletisimi</TabLink>
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
