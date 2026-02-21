// ── Scenario Metadata: Tier, Acceptance Criteria, Business Value ──
// Derived from scenario data — no manual tagging needed.

// ── Tier Classification ──────────────────────────────────────────

const TIER1_PREFIXES = ['s', 'cs']; // Revenue + Cross-sector critical
const TIER1_KEYWORDS = [
    'randevu', 'appointment', 'rezervasyon', 'reservation', 'booking',
    'ödeme', 'odeme', 'payment', 'sipariş', 'siparis', 'order',
    'acil', 'emergency', 'iade', 'refund', 'iptal', 'cancel',
    'kayit', 'register', 'opt-in', 'onam', 'consent',
    'fatura', 'invoice', 'churn', 'kayıp', 'kayip',
];
const TIER3_KEYWORDS = [
    'analitik', 'analytics', 'rapor', 'report', 'ayar', 'setting',
    'istatistik', 'statistic', 'arsiv', 'archive', 'log', 'geçmiş', 'gecmis',
    'anket', 'survey', 'geri bildirim özeti', 'feedback summary',
];

export function getScenarioTier(scenario) {
    const id = (scenario.id || '').toLowerCase();
    const prefix = id.replace(/\d+$/, '');
    const text = `${scenario.title || ''} ${scenario.description || ''}`.toLowerCase();

    // S-series and CS-series are always Tier-1
    if (TIER1_PREFIXES.includes(prefix)) return 1;

    // Keyword match for Tier-1
    if (TIER1_KEYWORDS.some(kw => text.includes(kw))) return 1;

    // Keyword match for Tier-3
    if (TIER3_KEYWORDS.some(kw => text.includes(kw))) return 3;

    // Default: Tier-2
    return 2;
}

export const TIER_META = {
    1: { label: 'Tier-1', short: 'T1', color: 'text-red-700 bg-red-100 border-red-300', desc: 'Musteri akisini durduran / gelir etkileyen' },
    2: { label: 'Tier-2', short: 'T2', color: 'text-blue-700 bg-blue-100 border-blue-300', desc: 'Sik kullanilan, alternatifi olan akislar' },
    3: { label: 'Tier-3', short: 'T3', color: 'text-gray-600 bg-gray-100 border-gray-300', desc: 'Nadir / yardimci akislar' },
};

// ── Acceptance Criteria ("Tamam" tanımı) ─────────────────────────

export function getAcceptanceCriteria(scenario) {
    const tier = getScenarioTier(scenario);
    const flows = scenario.flows || [];
    const allSteps = flows.flatMap(f => f.steps || []);
    const hasBotResponse = allSteps.some(st => st.role === 'bot');
    const hasUserInput = allSteps.some(st => st.role === 'user');
    const techApis = scenario.tech?.apis?.length || 0;
    const techBackend = scenario.tech?.backend?.length || 0;

    return [
        {
            key: 'output',
            label: 'Dogru Cikti',
            desc: 'Her flow beklenen bot yanitini dondurur',
            met: hasBotResponse && hasUserInput,
        },
        {
            key: 'error',
            label: 'Hata Mesaji',
            desc: 'Edge case\'lerde kullaniciya anlasilir hata mesaji gosterilir',
            met: flows.length >= 2, // multiple flows = edge cases covered
        },
        {
            key: 'auth',
            label: 'Yetki Kontrolu',
            desc: 'Tenant izolasyonu ve kullanici yetkilendirmesi saglanir',
            met: techBackend > 0 || techApis > 0,
        },
        {
            key: 'perf',
            label: 'Performans Esigi',
            desc: tier === 1 ? 'Yanit suresi < 2sn' : tier === 2 ? 'Yanit suresi < 5sn' : 'Yanit suresi < 10sn',
            met: null, // requires runtime test
        },
        {
            key: 'log',
            label: 'Loglanabilirlik',
            desc: 'Tum aksiyonlar audit log\'a yazilir',
            met: tier <= 2, // Tier-1/2 must log
        },
    ];
}

// ── Exit Criteria ────────────────────────────────────────────────

export function getExitCriteria(scenario) {
    const tier = getScenarioTier(scenario);
    if (tier === 1) return { blocker: 0, critical: 0, high: 0, label: 'Blocker=0, Critical=0, High=0' };
    if (tier === 2) return { blocker: 0, critical: 0, high: 2, label: 'Blocker=0, Critical=0, High≤2' };
    return { blocker: 0, critical: 1, high: 5, label: 'Blocker=0, Critical≤1, High≤5' };
}

// ── Business Metadata ────────────────────────────────────────────

const VALUE_MAP = { 1: 'Kritik', 2: 'Yuksek', 3: 'Orta' };
const FREQ_MAP = { 1: 'Cok Yuksek', 2: 'Yuksek', 3: 'Dusuk' };
const RISK_MAP = { high: 'Yuksek', medium: 'Orta', low: 'Dusuk' };

const NICHE_OWNER = {
    ecommerce: 'E-Ticaret Ekibi',
    health: 'Saglik Ekibi',
    dental: 'Saglik Ekibi',
    aesthetic: 'Saglik Ekibi',
    hotel: 'Otel Ekibi',
    beauty: 'Guzellik Ekibi',
    education: 'Egitim Ekibi',
    mobile: 'Mobil Ekibi',
    universal: 'Platform Ekibi',
    crossSector: 'Platform Ekibi',
};

function calcRisk(scenario) {
    const deps = new Set();
    (scenario.flows || []).forEach(f => {
        [...(f.requirements?.client || []), ...(f.requirements?.provider || [])].forEach(r => {
            if (typeof r === 'object' && r.service) deps.add(r.service);
            if (typeof r === 'object' && r.capability) deps.add(r.capability);
        });
    });
    (scenario.tech?.apis || []).forEach(a => deps.add(a.title));
    const count = deps.size;
    if (count >= 5) return 'high';
    if (count >= 2) return 'medium';
    return 'low';
}

function calcDependencyCount(scenario) {
    const deps = new Set();
    (scenario.overview?.services || []).forEach(s => deps.add(s.name));
    (scenario.tech?.apis || []).forEach(a => deps.add(a.title));
    (scenario.tech?.backend || []).forEach(b => deps.add(b.title));
    return deps.size;
}

function deriveStatus(scenario) {
    const flows = scenario.flows || [];
    if (!flows.length) return 'taslak';
    const allReqs = flows.flatMap(f => [
        ...(f.requirements?.client || []),
        ...(f.requirements?.provider || []),
    ]);
    const enriched = allReqs.filter(r => typeof r === 'object' && r.service && r.status);
    if (!allReqs.length) return 'taslak';
    const ratio = enriched.length / allReqs.length;
    if (ratio >= 0.9 && scenario.tech?.backend?.length && scenario.impact?.formula) return 'uretim-hazir';
    if (ratio >= 0.5) return 'gelistirme';
    return 'tasarim';
}

const STATUS_META = {
    'uretim-hazir': { label: 'Uretim Hazir', color: 'text-emerald-700 bg-emerald-100' },
    'gelistirme': { label: 'Gelistirme', color: 'text-blue-700 bg-blue-100' },
    'tasarim': { label: 'Tasarim', color: 'text-amber-700 bg-amber-100' },
    'taslak': { label: 'Taslak', color: 'text-gray-600 bg-gray-100' },
};

export { STATUS_META };

// ── Trigger Source Classification ────────────────────────────────

const OUTBOUND_KEYWORDS = [
    'kampanya', 'bildirim', 'hatirlatma', 'proaktif', 'outbound',
    'broadcast', 'toplu mesaj', 'segment', 'duyuru', 'early bird',
    'churn', 'retention', 'kayip', 'geri kazan', 'terk',
];
const AUTOMATIC_KEYWORDS = [
    'tespit', 'algilama', 'izleme', 'tarama', 'cron', 'zamanli',
    'otomasyon', 'otomatik', 'event', 'webhook', 'batch', 'gece',
    'analitik', 'rapor', 'skorlama', 'sinyal',
];

export function deriveTriggerSource(scenario) {
    const desc = (scenario.description || '').toLowerCase();
    const title = (scenario.title || '').toLowerCase();
    const text = `${title} ${desc}`;
    const flows = scenario.flows || [];

    // Outbound check: description-level keywords
    const outboundHits = OUTBOUND_KEYWORDS.filter(kw => text.includes(kw)).length;
    if (outboundHits >= 2) return 'outbound';

    // Check first step of all flows
    const firstStepRoles = flows
        .map(f => f.steps?.[0])
        .filter(Boolean);

    const systemStarts = firstStepRoles.filter(s => s.role === 'system').length;
    const userStarts = firstStepRoles.filter(s => s.role === 'user').length;

    // If majority of flows start with system role + automatic keywords
    if (systemStarts > userStarts) {
        const firstContents = firstStepRoles
            .filter(s => s.role === 'system')
            .map(s => (s.content || '').toLowerCase())
            .join(' ');
        const autoHits = AUTOMATIC_KEYWORDS.filter(kw => firstContents.includes(kw) || text.includes(kw)).length;
        const outHits = OUTBOUND_KEYWORDS.filter(kw => firstContents.includes(kw) || text.includes(kw)).length;
        if (outHits > autoHits) return 'outbound';
        return 'automatic';
    }

    // Single outbound keyword + system first step in any flow
    if (outboundHits >= 1 && systemStarts > 0) return 'outbound';

    return 'incoming';
}

export const TRIGGER_SOURCE_META = {
    incoming:  { label: 'Gelen Mesaj',  icon: 'MessageCircle', color: 'text-blue-700 bg-blue-100 border-blue-300' },
    automatic: { label: 'Otomatik',     icon: 'Zap',           color: 'text-purple-700 bg-purple-100 border-purple-300' },
    outbound:  { label: 'Giden Mesaj',  icon: 'Send',          color: 'text-amber-700 bg-amber-100 border-amber-300' },
};

export function getBusinessMeta(scenario) {
    const tier = getScenarioTier(scenario);
    const risk = calcRisk(scenario);
    const status = deriveStatus(scenario);

    return {
        businessValue: VALUE_MAP[tier],
        frequency: FREQ_MAP[tier],
        dependencyCount: calcDependencyCount(scenario),
        risk: RISK_MAP[risk],
        riskLevel: risk,
        owner: NICHE_OWNER[scenario.niche] || 'Platform Ekibi',
        status,
        statusMeta: STATUS_META[status],
    };
}
