import { useState, useEffect, useCallback, useMemo } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { api, ApiClientError, TemplateCatalogItem, AvailableTemplate } from '../lib/api';
import { useAuth } from '../hooks/useAuth';
import {
  RefreshCw, Plus, Search, Eye, Trash2, Check, CheckCircle2,
  LayoutTemplate, FileQuestion, MessageCircle, Lightbulb, GitBranch, Layers,
  ChevronDown, Package,
} from 'lucide-react';
import { cn } from '../lib/utils';

/* ── Shared constants ──────────────────────────────────────── */

const TYPE_ICONS: Record<string, React.ComponentType<{ className?: string; style?: React.CSSProperties }>> = {
  faq: FileQuestion,
  message: MessageCircle,
  intent: Lightbulb,
  flow: GitBranch,
  scenario: Layers,
};

const TYPE_COLORS: Record<string, string> = {
  faq: 'bg-blue-50 text-blue-700',
  message: 'bg-green-50 text-green-700',
  intent: 'bg-amber-50 text-amber-700',
  flow: 'bg-purple-50 text-purple-700',
  scenario: 'bg-pink-50 text-pink-700',
};

const SCOPE_COLORS: Record<string, string> = {
  platform: 'bg-indigo-50 text-indigo-700',
  sector: 'bg-teal-50 text-teal-700',
  tenant: 'bg-orange-50 text-orange-700',
};

const TEMPLATE_TYPES = ['', 'faq', 'message', 'intent', 'flow', 'scenario'];
const SCOPES = ['', 'platform', 'sector', 'tenant'];

/* ── Category metadata ─────────────────────────────────────── */

interface CategoryInfo {
  label: string;
  description: string;
  icon: React.ComponentType<{ className?: string; style?: React.CSSProperties }>;
  accentHex: string;
}

const CATEGORY_INFO: Record<string, CategoryInfo> = {
  intent: {
    label: 'Niyet Tanımlama',
    description: 'Müşteri mesajlarını anlayabilmek için hazır niyet kalıpları.',
    icon: Lightbulb,
    accentHex: '#f59e0b',
  },
  faq: {
    label: 'Sık Sorulan Sorular',
    description: 'Müşterilerinize otomatik yanıt verilecek soru-cevap çiftleri.',
    icon: FileQuestion,
    accentHex: '#3b82f6',
  },
  message: {
    label: 'Mesaj Şablonları',
    description: 'Hazır mesaj metinleri ve yanıt kalıpları.',
    icon: MessageCircle,
    accentHex: '#10b981',
  },
  flow: {
    label: 'Akış Şablonları',
    description: 'Hazır konuşma senaryoları ve akış diyagramları.',
    icon: GitBranch,
    accentHex: '#8b5cf6',
  },
  scenario: {
    label: 'Senaryo Şablonları',
    description: 'Karmaşık müşteri etkileşimleri için hazır senaryolar.',
    icon: Layers,
    accentHex: '#ec4899',
  },
};

const CATEGORY_ORDER = ['intent', 'faq', 'message', 'flow', 'scenario'];

/* ── Intent Turkish labels + descriptions + examples ────────── */

interface IntentInfo { label: string; desc: string; examples: string }

const INTENT_TR: Record<string, IntentInfo> = {
  greeting:           { label: 'Karşılama',        desc: 'Müşteri selamlaşma ve merhaba mesajları',         examples: '"Merhaba", "İyi günler", "Selam"' },
  price_inquiry:      { label: 'Fiyat Sorgusu',    desc: 'Ürün ve hizmet fiyatlarını soran mesajlar',       examples: '"Bu kaç TL?", "Fiyatı nedir?", "Ne kadar?"' },
  thank_you:          { label: 'Teşekkür',         desc: 'Müşterinin teşekkür ve memnuniyet bildirimi',     examples: '"Teşekkürler", "Sağ olun", "Çok teşekkür ederim"' },
  return_request:     { label: 'İade Talebi',      desc: 'Ürün iade ve değişim talepleri',                  examples: '"İade etmek istiyorum", "Ürünü geri göndermek istiyorum"' },
  order_confirmation: { label: 'Sipariş Onayı',    desc: 'Sipariş durumu ve onay sorguları',                examples: '"Siparişim ne durumda?", "Onaylandı mı?"' },
  shipping_inquiry:   { label: 'Kargo Sorgusu',    desc: 'Kargo takibi ve teslimat durumu soruları',        examples: '"Kargom nerede?", "Kaç günde gelir?", "Hangi kargo?"' },
  product_inquiry:    { label: 'Ürün Sorgusu',     desc: 'Ürün detayı, özellik ve bilgi talepleri',         examples: '"Bu ürün hakkında bilgi alabilir miyim?", "Özelliklerini yazar mısınız?"' },
  address_info:       { label: 'Adres Bilgisi',    desc: 'Adres sorgulama veya bildirme mesajları',         examples: '"Adresiniz nerede?", "Mağaza konumu?"' },
  stock_inquiry:      { label: 'Stok Sorgusu',     desc: 'Ürün stok ve beden mevcudiyet soruları',          examples: '"Stokta var mı?", "Bu ürün mevcut mu?"' },
  discount_inquiry:   { label: 'İndirim Sorgusu',  desc: 'İndirim, kampanya ve kupon soruları',             examples: '"İndirim var mı?", "Kampanya kodu?"' },
  size_inquiry:       { label: 'Beden Sorgusu',    desc: 'Beden, ölçü ve kalıp bilgisi soruları',           examples: '"Hangi bedenler var?", "XL var mı?", "Beden tablosu?"' },
  complaint:          { label: 'Şikâyet',          desc: 'Müşteri şikâyet ve memnuniyetsizlik bildirimleri', examples: '"Şikâyetim var", "Memnun kalmadım", "Sorun yaşıyorum"' },
  payment_inquiry:    { label: 'Ödeme Sorgusu',    desc: 'Ödeme yöntemleri ve işlem soruları',              examples: '"Nasıl ödeme yapabilirim?", "Kredi kartı geçiyor mu?"' },
  appointment:        { label: 'Randevu',          desc: 'Randevu alma, iptal ve değişiklik talepleri',     examples: '"Randevu almak istiyorum", "Müsait saatleriniz?"' },
  working_hours:      { label: 'Çalışma Saatleri', desc: 'İşletme çalışma saati ve ulaşım bilgisi',        examples: '"Saat kaça kadar açıksınız?", "Pazar açık mı?"' },
  cancel_order:       { label: 'Sipariş İptali',   desc: 'Sipariş iptal ve değişiklik talepleri',           examples: '"Siparişimi iptal edebilir miyim?", "Vazgeçtim"' },
  exchange_request:   { label: 'Değişim Talebi',   desc: 'Ürün değişim ve beden değişikliği',              examples: '"Beden değişimi yapabilir miyim?", "Başka ürünle değiştirmek istiyorum"' },
  warranty_inquiry:   { label: 'Garanti Sorgusu',  desc: 'Garanti süresi ve koşul soruları',               examples: '"Garanti süresi ne kadar?", "Garantiye girer mi?"' },
  feedback:           { label: 'Geri Bildirim',    desc: 'Müşteri görüş ve öneri mesajları',                examples: '"Bir önerim var", "Geri bildirim bırakmak istiyorum"' },
  goodbye:            { label: 'Vedalaşma',        desc: 'Konuşma sonlandırma ve veda mesajları',           examples: '"Hoşça kalın", "İyi günler", "Görüşürüz"' },
};

/* ── FAQ topic detection with examples ─────────────────────── */

interface FaqTopicMatch {
  test: RegExp;
  desc: string;
  examples: string;
}

const FAQ_TOPICS: FaqTopicMatch[] = [
  { test: /fiyat|ne kadar|kaç (tl|lira)/i,            desc: 'Fiyat soruları',       examples: '"Fiyatı nedir?", "Ne kadar?", "Kaç TL?"' },
  { test: /renk/i,                                     desc: 'Renk seçenekleri',     examples: '"Başka renk var mı?", "Hangi renkler mevcut?"' },
  { test: /beden|numara|ölçü/i,                        desc: 'Beden soruları',       examples: '"Hangi bedenler var?", "44 beden var mı?"' },
  { test: /kargo|teslimat|kaç gün/i,                   desc: 'Kargo ve teslimat',    examples: '"Kaç günde gelir?", "Kargo ücreti var mı?"' },
  { test: /iade|değişim/i,                             desc: 'İade ve değişim',      examples: '"İade yapabilir miyim?", "Değişim var mı?"' },
  { test: /ödeme|kapıda|kapida/i,                      desc: 'Ödeme yöntemleri',     examples: '"Kapıda ödeme var mı?", "Nasıl ödeme yaparım?"' },
];

function getFaqDisplay(cleanedName: string, apiDesc?: string): { desc: string; examples: string } {
  if (apiDesc) return { desc: apiDesc, examples: '' };
  for (const topic of FAQ_TOPICS) {
    if (topic.test.test(cleanedName)) return { desc: topic.desc, examples: topic.examples };
  }
  return { desc: 'Sık sorulan soru', examples: '' };
}

/* ── Template display helpers ──────────────────────────────── */

interface TemplateDisplay { label: string; desc: string; examples: string }

function extractSlug(name: string): string {
  const prefixMatch = name.match(/^(Intent|FAQ|Message|Flow|Scenario):\s*/i);
  const cleaned = prefixMatch ? name.slice(prefixMatch[0].length) : name;
  return cleaned.trim();
}

function getTemplateDisplay(tpl: AvailableTemplate): TemplateDisplay {
  const slug = extractSlug(tpl.name);

  if (tpl.template_type === 'intent') {
    const tr = INTENT_TR[slug.toLowerCase()];
    if (tr) return tr;
    const fallbackLabel = slug.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
    return { label: fallbackLabel, desc: tpl.description || 'Müşteri niyetini tanımlar', examples: '' };
  }

  if (tpl.template_type === 'faq') {
    const cleanedQ = slug.replace(/^\d+\s+/, '');
    const faq = getFaqDisplay(cleanedQ, tpl.description ?? undefined);
    return { label: cleanedQ, desc: faq.desc, examples: faq.examples };
  }

  const cleanedName = slug.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  return { label: cleanedName, desc: tpl.description || '', examples: '' };
}

/* ── Router ────────────────────────────────────────────────── */

export function TemplateLibraryPage() {
  const { session } = useAuth();
  const isTenant = session != null && session.tenantId > 0;

  if (isTenant) return <TenantTemplateView />;
  return <OpsTemplateView />;
}

/* ═══ Tenant View: Category-grouped ════════════════════════ */

function TenantTemplateView() {
  const [items, setItems] = useState<AvailableTemplate[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [adoptingId, setAdoptingId] = useState<number | null>(null);
  const [adoptedIds, setAdoptedIds] = useState<Set<number>>(new Set());
  const [expandedTypes, setExpandedTypes] = useState<Set<string>>(new Set());

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.getAvailableTemplates();
      setItems(res.items);
      setAdoptedIds(new Set(res.items.filter(t => t.is_adopted).map(t => t.id)));
    } catch (err) {
      const code = err instanceof ApiClientError ? err.errorCode : '';
      const msg = err instanceof Error ? err.message : String(err);
      setError(`Şablonlar yüklenemedi${code ? ` [${code}]` : ''}: ${msg}`);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchTemplates(); }, [fetchTemplates]);

  const handleAdopt = async (templateId: number) => {
    setAdoptingId(templateId);
    setError(null);
    try {
      await api.adoptTemplate(templateId);
      setAdoptedIds(prev => new Set([...prev, templateId]));
    } catch (err) {
      const status = err instanceof ApiClientError ? err.status : 0;
      const msg = err instanceof Error ? err.message : String(err);
      if (status === 409 || msg.includes('already')) {
        setAdoptedIds(prev => new Set([...prev, templateId]));
      } else {
        const code = err instanceof ApiClientError ? err.errorCode : '';
        setError(`Şablon eklenemedi${code ? ` [${code}]` : ''}: ${msg}`);
      }
    } finally {
      setAdoptingId(null);
    }
  };

  const toggleType = (type: string) => {
    setExpandedTypes(prev => {
      const next = new Set(prev);
      if (next.has(type)) next.delete(type);
      else next.add(type);
      return next;
    });
  };

  // Group by template_type
  const grouped = useMemo(() => {
    const groups: Record<string, AvailableTemplate[]> = {};
    for (const tpl of items) {
      if (!groups[tpl.template_type]) groups[tpl.template_type] = [];
      groups[tpl.template_type].push(tpl);
    }
    return groups;
  }, [items]);

  const sortedTypes = CATEGORY_ORDER.filter(t => (grouped[t]?.length ?? 0) > 0);

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between onboarding-fade-in">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-brand-50 flex items-center justify-center">
            <Package className="w-5 h-5 text-brand-600" />
          </div>
          <div>
            <h1 className="text-lg font-semibold text-navy-900">Şablon Kütüphanesi</h1>
            <p className="text-xs text-navy-400">Sektörünüze özel hazırlanmış şablonları chatbotunuza ekleyin.</p>
          </div>
        </div>
        <button onClick={fetchTemplates} className="p-2 rounded-lg hover:bg-navy-50 text-navy-400 hover:text-navy-600 transition-colors" disabled={loading}>
          <RefreshCw className={cn('w-4 h-4', loading && 'animate-spin')} />
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="flex items-center justify-between bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-400 hover:text-red-600 ml-2 text-lg leading-none">&times;</button>
        </div>
      )}

      {/* Loading */}
      {loading && !items.length && (
        <div className="space-y-4">
          {[1, 2].map(i => (
            <div key={i} className="bg-white rounded-xl border border-navy-100 p-5 animate-pulse">
              <div className="flex gap-4">
                <div className="w-10 h-10 rounded-xl bg-navy-100" />
                <div className="flex-1 space-y-2">
                  <div className="h-4 bg-navy-100 rounded w-40" />
                  <div className="h-3 bg-navy-50 rounded w-64" />
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Empty */}
      {!loading && items.length === 0 && !error && (
        <div className="py-16 text-center onboarding-fade-in">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-navy-50 flex items-center justify-center">
            <LayoutTemplate className="w-8 h-8 text-navy-200" />
          </div>
          <p className="text-sm font-medium text-navy-600 mb-1">Henüz şablon bulunamadı</p>
          <p className="text-xs text-navy-400">Önce sektörünüzü Ayarlar sayfasından seçin.</p>
        </div>
      )}

      {/* Category sections */}
      {sortedTypes.map((type, idx) => {
        const info = CATEGORY_INFO[type];
        if (!info) return null;
        const templates = grouped[type];

        return (
          <CategorySection
            key={type}
            type={type}
            info={info}
            templates={templates}
            adoptedIds={adoptedIds}
            isExpanded={expandedTypes.has(type)}
            onToggle={() => toggleType(type)}
            onAdopt={handleAdopt}
            adoptingId={adoptingId}
            animDelay={idx * 80 + 100}
          />
        );
      })}
    </div>
  );
}

/* ── Category Section ──────────────────────────────────────── */

function CategorySection({ info, templates, adoptedIds, isExpanded, onToggle, onAdopt, adoptingId, animDelay }: {
  type: string;
  info: CategoryInfo;
  templates: AvailableTemplate[];
  adoptedIds: Set<number>;
  isExpanded: boolean;
  onToggle: () => void;
  onAdopt: (id: number) => void;
  adoptingId: number | null;
  animDelay: number;
}) {
  const Icon = info.icon;
  const adoptedCount = templates.filter(t => adoptedIds.has(t.id)).length;

  return (
    <div
      className="rounded-xl border border-navy-100 bg-white overflow-hidden onboarding-fade-in"
      style={{ animationDelay: `${animDelay}ms` }}
    >
      {/* Accent top bar */}
      <div className="h-1" style={{ background: info.accentHex }} />

      <div className="p-5">
        {/* Header — clickable to toggle */}
        <button onClick={onToggle} className="w-full text-left flex items-start gap-4">
          <div
            className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
            style={{ backgroundColor: `${info.accentHex}12` }}
          >
            <Icon className="w-5 h-5" style={{ color: info.accentHex }} />
          </div>

          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 mb-0.5">
              <h3 className="text-sm font-semibold text-navy-900">{info.label}</h3>
              <span className="text-[10px] text-navy-400 font-medium">{templates.length} şablon</span>
              {adoptedCount > 0 && (
                <span className="text-[10px] font-medium text-emerald-600 bg-emerald-50 px-1.5 py-0.5 rounded-full">
                  {adoptedCount} eklendi
                </span>
              )}
            </div>
            <p className="text-xs text-navy-500">{info.description}</p>
          </div>

          <ChevronDown className={cn(
            'w-4 h-4 text-navy-300 shrink-0 mt-1 transition-transform duration-200',
            isExpanded && 'rotate-180',
          )} />
        </button>

        {/* Expanded: template list */}
        {isExpanded && (
          <div className="mt-4 pt-4 border-t border-navy-100 space-y-1 onboarding-step-expand">
            {templates.map(tpl => {
              const isAdopted = adoptedIds.has(tpl.id);
              const isAdopting = adoptingId === tpl.id;
              const display = getTemplateDisplay(tpl);

              return (
                <div
                  key={tpl.id}
                  className={cn(
                    'flex items-start gap-3 py-2.5 px-3 rounded-lg transition-colors',
                    isAdopted ? 'bg-emerald-50/40' : 'hover:bg-navy-50/50',
                  )}
                >
                  {/* Button or status — LEFT side */}
                  {isAdopted ? (
                    <span className="inline-flex items-center gap-1 text-[11px] font-medium text-emerald-600 bg-emerald-50 px-2 py-1 rounded-md shrink-0 mt-0.5">
                      <CheckCircle2 className="w-3 h-3" />
                      Eklendi
                    </span>
                  ) : (
                    <button
                      onClick={() => onAdopt(tpl.id)}
                      disabled={isAdopting}
                      className="inline-flex items-center gap-1 text-[11px] font-medium px-2.5 py-1 rounded-md shrink-0 mt-0.5 transition-colors disabled:opacity-50 hover:shadow-sm"
                      style={{
                        color: info.accentHex,
                        backgroundColor: `${info.accentHex}10`,
                      }}
                    >
                      <Plus className="w-3 h-3" />
                      {isAdopting ? '...' : 'Ekle'}
                    </button>
                  )}

                  {/* Name + description */}
                  <div className="flex-1 min-w-0">
                    <span className={cn(
                      'text-sm font-medium',
                      isAdopted ? 'text-navy-500' : 'text-navy-800',
                    )}>
                      {display.label}
                    </span>
                    {display.desc && (
                      <p className={cn(
                        'text-xs mt-0.5',
                        isAdopted ? 'text-navy-300' : 'text-navy-400',
                      )}>
                        {display.desc}
                      </p>
                    )}
                    {display.examples && (
                      <p className="text-[11px] mt-0.5 text-navy-300 italic">
                        Örn: {display.examples}
                      </p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

/* ═══ Ops View: admin table (unchanged) ════════════════════ */

function OpsTemplateView() {
  const navigate = useNavigate();
  const [items, setItems] = useState<TemplateCatalogItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);

  const [filterType, setFilterType] = useState('');
  const [filterScope, setFilterScope] = useState('');
  const [filterSearch, setFilterSearch] = useState('');

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.getTemplateCatalog({
        type: filterType || undefined,
        scope: filterScope || undefined,
        search: filterSearch || undefined,
        page,
        limit: 20,
      });
      setItems(result.items);
      setTotal(result.total);
    } catch (err) {
      console.error('Failed to fetch templates:', err);
      setError('Sablonlar yuklenirken hata olustu. Tekrar deneyin.');
    } finally {
      setLoading(false);
    }
  }, [filterType, filterScope, filterSearch, page]);

  useEffect(() => { fetchTemplates(); }, [fetchTemplates]);

  const handlePublish = async (id: number) => {
    setError(null);
    try {
      await api.publishTemplate(id);
      fetchTemplates();
    } catch (err) {
      console.error('Publish failed:', err);
      setError('Sablon yayinlanirken hata olustu.');
    }
  };

  const handleDelete = async (id: number) => {
    setError(null);
    try {
      await api.deleteTemplate(id);
      fetchTemplates();
    } catch (err) {
      console.error('Delete failed:', err);
      setError('Sablon silinirken hata olustu.');
    }
  };

  const totalPages = Math.ceil(total / 20);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold text-navy-900 flex items-center gap-2">
            <LayoutTemplate className="w-5 h-5" />
            Sablon Kutuphanesi
          </h1>
          <p className="text-xs text-navy-400">{total} sablon kayitli</p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={fetchTemplates} className="p-1.5 rounded hover:bg-navy-50">
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
          <Link
            to="/templates/new"
            className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700"
          >
            <Plus className="w-3.5 h-3.5" />
            Yeni Template
          </Link>
          <Link
            to="/templates/ingestion"
            className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded border border-navy-200 text-navy-700 hover:bg-navy-50"
          >
            <Plus className="w-3.5 h-3.5" />
            Veri Besleme
          </Link>
        </div>
      </div>

      {/* Filters */}
      <div className="flex items-center gap-3 bg-white rounded-lg border border-navy-100 p-3">
        <div className="flex items-center gap-1.5">
          <Search className="w-3.5 h-3.5 text-navy-400" />
          <input
            type="text"
            placeholder="Ara (slug, isim)..."
            value={filterSearch}
            onChange={e => { setFilterSearch(e.target.value); setPage(1); }}
            className="text-xs border-0 outline-none w-40 bg-transparent"
          />
        </div>
        <select
          value={filterType}
          onChange={e => { setFilterType(e.target.value); setPage(1); }}
          className="text-xs border border-navy-200 rounded px-2 py-1"
        >
          <option value="">Tum Tipler</option>
          {TEMPLATE_TYPES.filter(Boolean).map(t => (
            <option key={t} value={t}>{t.toUpperCase()}</option>
          ))}
        </select>
        <select
          value={filterScope}
          onChange={e => { setFilterScope(e.target.value); setPage(1); }}
          className="text-xs border border-navy-200 rounded px-2 py-1"
        >
          <option value="">Tum Scope</option>
          {SCOPES.filter(Boolean).map(s => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </div>

      {/* Error Banner */}
      {error && (
        <div className="flex items-center justify-between bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-400 hover:text-red-600 ml-2">&times;</button>
        </div>
      )}

      {/* Table */}
      <div className="bg-white rounded-lg border border-navy-100 overflow-hidden">
        <table className="w-full text-xs">
          <thead className="bg-navy-50 text-navy-500">
            <tr>
              <th className="text-left px-3 py-2 font-medium">Sablon</th>
              <th className="text-left px-3 py-2 font-medium">Tip</th>
              <th className="text-left px-3 py-2 font-medium">Scope</th>
              <th className="text-center px-3 py-2 font-medium">v</th>
              <th className="text-center px-3 py-2 font-medium">Guven</th>
              <th className="text-center px-3 py-2 font-medium">Kaynak</th>
              <th className="text-center px-3 py-2 font-medium">Durum</th>
              <th className="text-right px-3 py-2 font-medium">Islem</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-navy-50">
            {items.map(item => {
              const TypeIcon = TYPE_ICONS[item.template_type] || LayoutTemplate;
              return (
                <tr key={item.id} className="hover:bg-navy-25 cursor-pointer" onClick={() => navigate(`/templates/${item.id}`)}>
                  <td className="px-3 py-2">
                    <div className="font-medium text-navy-800">{item.name}</div>
                    <div className="text-navy-400 text-[10px]">{item.slug}</div>
                  </td>
                  <td className="px-3 py-2">
                    <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium ${TYPE_COLORS[item.template_type] || 'bg-gray-50 text-gray-700'}`}>
                      <TypeIcon className="w-3 h-3" />
                      {item.template_type}
                    </span>
                  </td>
                  <td className="px-3 py-2">
                    <span className={`inline-flex px-1.5 py-0.5 rounded text-[10px] font-medium ${SCOPE_COLORS[item.scope] || 'bg-gray-50'}`}>
                      {item.scope}
                      {item.sector && ` / ${item.sector}`}
                    </span>
                  </td>
                  <td className="px-3 py-2 text-center text-navy-500">v{item.version}</td>
                  <td className="px-3 py-2 text-center">
                    <div className="w-12 bg-navy-100 rounded-full h-1.5 mx-auto">
                      <div className="bg-emerald-500 h-1.5 rounded-full" style={{ width: `${item.confidence_score * 100}%` }} />
                    </div>
                    <div className="text-[10px] text-navy-400 mt-0.5">{(item.confidence_score * 100).toFixed(0)}%</div>
                  </td>
                  <td className="px-3 py-2 text-center text-navy-500">{item.source_count}</td>
                  <td className="px-3 py-2 text-center">
                    {item.is_published ? (
                      <span className="inline-flex items-center gap-0.5 text-emerald-600 text-[10px]">
                        <Check className="w-3 h-3" /> Yayinda
                      </span>
                    ) : (
                      <span className="text-amber-500 text-[10px]">Taslak</span>
                    )}
                  </td>
                  <td className="px-3 py-2 text-right" onClick={e => e.stopPropagation()}>
                    <div className="flex items-center justify-end gap-1">
                      <button onClick={() => navigate(`/templates/${item.id}`)} className="p-1 hover:bg-navy-100 rounded" title="Goruntule">
                        <Eye className="w-3.5 h-3.5 text-navy-500" />
                      </button>
                      {!item.is_published && (
                        <button onClick={() => handlePublish(item.id)} className="p-1 hover:bg-emerald-50 rounded" title="Yayinla">
                          <Check className="w-3.5 h-3.5 text-emerald-600" />
                        </button>
                      )}
                      <button onClick={() => handleDelete(item.id)} className="p-1 hover:bg-red-50 rounded" title="Sil">
                        <Trash2 className="w-3.5 h-3.5 text-red-400" />
                      </button>
                    </div>
                  </td>
                </tr>
              );
            })}
            {items.length === 0 && !loading && (
              <tr>
                <td colSpan={8} className="px-3 py-8 text-center text-navy-400">
                  Sablon bulunamadi
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 text-xs">
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="px-2 py-1 rounded border border-navy-200 disabled:opacity-40"
          >
            Onceki
          </button>
          <span className="text-navy-500">{page} / {totalPages}</span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
            className="px-2 py-1 rounded border border-navy-200 disabled:opacity-40"
          >
            Sonraki
          </button>
        </div>
      )}
    </div>
  );
}
