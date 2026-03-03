/**
 * Scenario Test Engine — Q's SE Test Dashboard Core
 *
 * Maps scenario JSON steps to implied API calls, calculates readiness,
 * generates gap reports, and provides interactive simulation support.
 */

// ─── Capability → Service → Endpoint Mapping ──────────────────────────
export const CAPABILITY_MAP = {
    C1:  { service: 'Backend',      color: 'blue',   label: 'Unified Inbox',     endpoints: ['POST /api/v1/messages', 'GET /api/v1/channels'] },
    C2:  { service: 'Backend',      color: 'blue',   label: 'Routing',           endpoints: ['POST /api/v1/routing', 'POST /api/v1/assignments'] },
    C3:  { service: 'Knowledge',    color: 'purple',  label: 'Templates',         endpoints: ['GET /api/v1/templates', 'POST /api/v1/templates/render'] },
    C4:  { service: 'Backend',      color: 'blue',   label: 'CRM / Leads',       endpoints: ['GET /api/v1/leads/{id}', 'POST /api/v1/leads'] },
    C5:  { service: 'Backend',      color: 'rose',   label: 'KVKK / Privacy',    endpoints: ['POST /api/v1/privacy/delete', 'GET /api/v1/privacy/export'] },
    C6:  { service: 'Backend',      color: 'rose',   label: 'Consent',           endpoints: ['POST /api/v1/consent', 'GET /api/v1/consent/{phone}'] },
    C7:  { service: 'Knowledge',    color: 'purple',  label: 'Knowledge Base',    endpoints: ['POST /search', 'GET /api/v1/faqs', 'GET /api/v1/documents'] },
    C8:  { service: 'AgentAI',      color: 'amber',  label: 'AI Agent Assist',   endpoints: ['POST /api/v1/suggest', 'POST /api/v1/feedback'] },
    C9:  { service: 'Automation',   color: 'green',  label: 'Workflows',         endpoints: ['POST /api/v1/workflows', 'POST /api/v1/triggers'] },
    C10: { service: 'Backend',      color: 'blue',   label: 'Payments',          endpoints: ['POST /api/v1/payments/link'] },
    C11: { service: 'Integrations', color: 'indigo', label: 'External APIs',     endpoints: ['GET /api/v1/orders', 'GET /api/v1/shipment/track'] },
    C12: { service: 'Backend',      color: 'blue',   label: 'Attribution',       endpoints: ['POST /api/v1/attribution'] },
    C13: { service: 'ChatAnalysis', color: 'gray',   label: 'Analytics',         endpoints: ['POST /api/v1/analytics/anomaly'] },
};

// ─── Content Pattern → API Call Rules ──────────────────────────────────
const CONTENT_PATTERNS = [
    // Intent detection keywords
    { pattern: /intent[:\s]/i,          capability: 'C8',  method: 'POST', endpoint: '/api/v1/suggest',           desc: 'AI intent tespiti' },
    { pattern: /sentiment[:\s]/i,       capability: 'C13', method: 'POST', endpoint: '/api/v1/analytics/sentiment', desc: 'Duygu analizi' },
    // Lead / CRM
    { pattern: /lead\s*(yakalan|kayded|olustur)/i, capability: 'C4', method: 'POST', endpoint: '/api/v1/leads', desc: 'Lead olusturma' },
    { pattern: /crm|musteri\s*profil/i, capability: 'C4',  method: 'GET',  endpoint: '/api/v1/leads/{id}',       desc: 'CRM musteri sorgusu' },
    // Shipping / Orders
    { pattern: /kargo|teslimat|gonderi/i, capability: 'C11', method: 'GET', endpoint: '/api/v1/shipment/track',  desc: 'Kargo takip' },
    { pattern: /siparis|order/i,        capability: 'C11', method: 'GET',  endpoint: '/api/v1/orders',           desc: 'Siparis sorgusu' },
    // Knowledge
    { pattern: /fiyat|ucret|tarife/i,   capability: 'C7',  method: 'POST', endpoint: '/search',                  desc: 'Knowledge fiyat aramasi' },
    { pattern: /faq|sik\s*sor/i,        capability: 'C7',  method: 'GET',  endpoint: '/api/v1/faqs',             desc: 'FAQ sorgusu' },
    { pattern: /bilgi\s*bank|dokuman/i, capability: 'C7',  method: 'GET',  endpoint: '/api/v1/documents',        desc: 'Dokuman aramasi' },
    // Payment
    { pattern: /odeme|payment|kapora/i, capability: 'C10', method: 'POST', endpoint: '/api/v1/payments/link',    desc: 'Odeme linki olusturma' },
    // Attribution
    { pattern: /attribution|kaynak|utm/i, capability: 'C12', method: 'POST', endpoint: '/api/v1/attribution',   desc: 'Attribution kaydi' },
    // Routing / Escalation
    { pattern: /eskal|yonlendir|ata/i,  capability: 'C2',  method: 'POST', endpoint: '/api/v1/routing',          desc: 'Yonlendirme / eskalasyon' },
    // Templates
    { pattern: /sablon|template/i,      capability: 'C3',  method: 'POST', endpoint: '/api/v1/templates/render', desc: 'Sablon render' },
    // Appointment
    { pattern: /randevu|appointment/i,  capability: 'C4',  method: 'POST', endpoint: '/api/v1/appointments',     desc: 'Randevu olusturma' },
    // Consent / KVKK
    { pattern: /kvkk|gdpr|consent|riza/i, capability: 'C6', method: 'POST', endpoint: '/api/v1/consent',        desc: 'Riza kaydi' },
    { pattern: /silme\s*taleb|veri\s*sil/i, capability: 'C5', method: 'POST', endpoint: '/api/v1/privacy/delete', desc: 'Veri silme talebi' },
    // Cron / Scheduled
    { pattern: /cron|zamanlan|polling|periyodik/i, capability: 'C9', method: 'POST', endpoint: '/api/v1/triggers', desc: 'Zamanlanmis tetikleme' },
    // Workflow
    { pattern: /otomasyon|workflow|akis/i, capability: 'C9', method: 'POST', endpoint: '/api/v1/workflows',      desc: 'Otomasyon akisi' },
    // Anomaly
    { pattern: /anomali|fraud|sahte/i,  capability: 'C13', method: 'POST', endpoint: '/api/v1/analytics/anomaly', desc: 'Anomali tespiti' },
    // WhatsApp link / channel switch
    { pattern: /whatsapp.*link|wa.*gecis/i, capability: 'C1', method: 'POST', endpoint: '/api/v1/messages',     desc: 'Kanal gecis mesaji' },
];

// ─── Role-based default API calls ──────────────────────────────────────
const ROLE_DEFAULTS = {
    user:   [{ capability: 'C1', method: 'POST', endpoint: '/api/v1/messages',        desc: 'Mesaj alimi (Unified Inbox)' }],
    ai:     [{ capability: 'C8', method: 'POST', endpoint: '/api/v1/suggest',          desc: 'AI oneri motoru' }],
    bot:    [{ capability: 'C3', method: 'POST', endpoint: '/api/v1/templates/render', desc: 'Sablon mesaj gonderimi' },
             { capability: 'C1', method: 'POST', endpoint: '/api/v1/messages',         desc: 'Mesaj gonderimi' }],
    system: [{ capability: 'C4', method: 'POST', endpoint: '/api/v1/leads',            desc: 'Sistem kaydi' }],
    agent:  [{ capability: 'C2', method: 'POST', endpoint: '/api/v1/routing',          desc: 'Agent yonlendirme' }],
};

// ─── Role metadata ─────────────────────────────────────────────────────
export const ROLE_META = {
    user:   { label: 'Musteri',  icon: 'User',          color: 'bg-blue-100 text-blue-800 border-blue-300' },
    ai:     { label: 'AI',       icon: 'Brain',         color: 'bg-purple-100 text-purple-800 border-purple-300' },
    bot:    { label: 'Bot',      icon: 'Bot',           color: 'bg-amber-100 text-amber-800 border-amber-300' },
    system: { label: 'Sistem',   icon: 'Server',        color: 'bg-gray-100 text-gray-800 border-gray-300' },
    agent:  { label: 'Agent',    icon: 'Headphones',    color: 'bg-emerald-100 text-emerald-800 border-emerald-300' },
};

// ─── API Call Mapping ──────────────────────────────────────────────────

/**
 * Maps a single step to its implied API calls based on role + content.
 */
function mapStepToApiCalls(step) {
    const calls = [];
    const seen = new Set();
    const content = step.content || '';
    const role = step.role || 'user';

    // 1. Content-based pattern matching (higher priority)
    for (const rule of CONTENT_PATTERNS) {
        if (rule.pattern.test(content)) {
            const key = `${rule.method}:${rule.endpoint}`;
            if (!seen.has(key)) {
                seen.add(key);
                calls.push({
                    capability: rule.capability,
                    service: CAPABILITY_MAP[rule.capability]?.service || 'Unknown',
                    method: rule.method,
                    endpoint: rule.endpoint,
                    desc: rule.desc,
                    source: 'content',
                });
            }
        }
    }

    // 2. Role-based defaults (only if no content-specific match for that capability)
    const defaults = ROLE_DEFAULTS[role] || [];
    for (const d of defaults) {
        const key = `${d.method}:${d.endpoint}`;
        if (!seen.has(key)) {
            seen.add(key);
            calls.push({
                capability: d.capability,
                service: CAPABILITY_MAP[d.capability]?.service || 'Unknown',
                method: d.method,
                endpoint: d.endpoint,
                desc: d.desc,
                source: 'role',
            });
        }
    }

    return calls;
}

/**
 * Maps all steps in a flow to API calls.
 * Returns: [{ stepIndex, role, content, apiCalls: [...] }]
 */
export function mapFlowToApiCalls(flow) {
    if (!flow?.steps) return [];
    return flow.steps.map((step, idx) => ({
        stepIndex: idx,
        role: step.role || 'user',
        content: step.content || '',
        apiCalls: mapStepToApiCalls(step),
    }));
}

/**
 * Aggregates all API calls across all flows, grouped by service.
 */
export function getServiceSummary(scenario) {
    const services = {};
    const flows = scenario?.flows || [];

    for (const flow of flows) {
        const mapped = mapFlowToApiCalls(flow);
        for (const step of mapped) {
            for (const call of step.apiCalls) {
                if (!services[call.service]) {
                    services[call.service] = { calls: [], capabilities: new Set() };
                }
                const key = `${call.method}:${call.endpoint}`;
                if (!services[call.service].calls.find(c => `${c.method}:${c.endpoint}` === key)) {
                    services[call.service].calls.push(call);
                }
                services[call.service].capabilities.add(call.capability);
            }
        }
    }

    // Convert sets to arrays
    return Object.fromEntries(
        Object.entries(services).map(([name, data]) => [
            name,
            { ...data, capabilities: [...data.capabilities] },
        ])
    );
}

// ─── Readiness Score ───────────────────────────────────────────────────

/**
 * Calculates readiness score (0-100) based on requirements status.
 */
export function calculateReadinessScore(scenario) {
    const items = [];
    const flows = scenario?.flows || [];

    for (const flow of flows) {
        const reqs = flow.requirements || {};
        for (const req of reqs.client || []) {
            if (typeof req === 'object') items.push(req);
        }
        for (const req of reqs.provider || []) {
            if (typeof req === 'object') items.push(req);
        }
    }

    if (items.length === 0) {
        // No structured requirements — check overview services presence
        const hasServices = (scenario.overview?.services?.length || 0) > 0;
        const hasTech = !!scenario.tech;
        const baseScore = hasServices ? 50 : 20;
        return { score: hasTech ? baseScore + 20 : baseScore, total: 0, ready: 0, setup: 0, missing: 0, items: [] };
    }

    let ready = 0;
    let setup = 0;
    let requiredSetup = 0;

    for (const item of items) {
        if (item.status === 'ready') {
            ready++;
        } else {
            setup++;
            if (item.priority === 'required') requiredSetup++;
        }
    }

    const total = items.length;
    const score = total > 0 ? Math.round(((ready + setup * 0.3) / total) * 100) : 50;

    return { score: Math.min(score, 100), total, ready, setup, missing: requiredSetup, items };
}

// ─── Gap Analysis ──────────────────────────────────────────────────────

/**
 * Generates a gap report from scenario requirements.
 * Returns sorted by priority (required first, then recommended, then optional).
 */
export function generateGapReport(scenario) {
    const gaps = [];
    const readyItems = [];
    const flows = scenario?.flows || [];

    for (const flow of flows) {
        const reqs = flow.requirements || {};
        const allReqs = [
            ...(reqs.client || []).map(r => ({ ...normalizeReq(r), side: 'client' })),
            ...(reqs.provider || []).map(r => ({ ...normalizeReq(r), side: 'provider' })),
        ];

        for (const req of allReqs) {
            if (req.status === 'ready') {
                readyItems.push(req);
            } else {
                gaps.push(req);
            }
        }
    }

    // Sort: required > recommended > optional
    const priorityOrder = { required: 0, recommended: 1, optional: 2 };
    gaps.sort((a, b) => (priorityOrder[a.priority] ?? 2) - (priorityOrder[b.priority] ?? 2));

    return { gaps, readyItems };
}

function normalizeReq(req) {
    if (typeof req === 'string') return { text: req, status: 'setup', priority: 'optional', effort: 'medium' };
    return {
        text: req.text || req,
        service: req.service || '',
        page: req.page || '',
        status: req.status || 'setup',
        priority: req.priority || 'optional',
        effort: req.effort || 'medium',
        capability: req.capability || '',
        hint: req.hint || '',
    };
}

// ─── Interactive Test Variables ────────────────────────────────────────

/**
 * Extracts editable variables from user/customer steps in a flow.
 */
export function extractTestVariables(flow) {
    if (!flow?.steps) return [];
    const variables = [];

    for (const step of flow.steps) {
        if (step.role !== 'user') continue;
        const content = step.content || '';

        // Extract number patterns (order IDs, phone numbers)
        const numbers = content.match(/\d{5,}/g);
        if (numbers) {
            for (const num of numbers) {
                variables.push({
                    key: `num_${variables.length}`,
                    label: num.length >= 10 ? 'Telefon No' : 'Referans No',
                    defaultValue: num,
                    type: 'text',
                });
            }
        }

        // The message itself is always editable
        variables.push({
            key: `msg_${variables.length}`,
            label: 'Musteri Mesaji',
            defaultValue: content,
            type: 'textarea',
        });
    }

    // Add channel selector
    variables.push({
        key: 'channel',
        label: 'Kanal',
        defaultValue: 'whatsapp',
        type: 'select',
        options: ['whatsapp', 'instagram', 'web', 'sms', 'email'],
    });

    return variables;
}

/**
 * Simulates a flow with modified variables — replaces user step content.
 */
export function simulateFlow(flow, variables) {
    if (!flow?.steps) return [];

    const userMsgVars = variables.filter(v => v.key.startsWith('msg_'));
    let userIdx = 0;

    return flow.steps.map(step => {
        if (step.role === 'user' && userMsgVars[userIdx]) {
            const newContent = userMsgVars[userIdx].currentValue ?? userMsgVars[userIdx].defaultValue;
            userIdx++;
            return { ...step, content: newContent };
        }
        return step;
    });
}

// ─── Summary Stats ─────────────────────────────────────────────────────

/**
 * Computes quick summary stats for the test banner.
 */
export function getTestSummary(scenario) {
    const services = getServiceSummary(scenario);
    const totalApiCalls = Object.values(services).reduce((sum, s) => sum + s.calls.length, 0);
    const totalCapabilities = new Set(Object.values(services).flatMap(s => s.capabilities)).size;
    const { gaps } = generateGapReport(scenario);
    const { score } = calculateReadinessScore(scenario);
    const totalFlows = scenario?.flows?.length || 0;
    const totalSteps = (scenario?.flows || []).reduce((sum, f) => sum + (f.steps?.length || 0), 0);

    return {
        score,
        totalApiCalls,
        totalCapabilities,
        totalGaps: gaps.length,
        criticalGaps: gaps.filter(g => g.priority === 'required').length,
        totalFlows,
        totalSteps,
        serviceCount: Object.keys(services).length,
    };
}
