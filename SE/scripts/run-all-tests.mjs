/**
 * Batch Scenario Test Runner
 * Runs scenarioTestEngine against all 170+ scenario JSON files.
 * Outputs: SE/src/data/test-results.json
 */
import { readFileSync, writeFileSync, readdirSync } from 'fs';
import { join, basename } from 'path';
import { fileURLToPath } from 'url';
import { dirname } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const dataDir = join(__dirname, '..', 'src', 'data');

// ─── Inline test engine (Node-compatible, no JSX deps) ──────────────

const CAPABILITY_MAP = {
    C1:  { service: 'Backend',      label: 'Unified Inbox',     endpoints: ['POST /api/v1/messages', 'GET /api/v1/channels'] },
    C2:  { service: 'Backend',      label: 'Routing',           endpoints: ['POST /api/v1/routing', 'POST /api/v1/assignments'] },
    C3:  { service: 'Knowledge',    label: 'Templates',         endpoints: ['GET /api/v1/templates', 'POST /api/v1/templates/render'] },
    C4:  { service: 'Backend',      label: 'CRM / Leads',       endpoints: ['GET /api/v1/leads/{id}', 'POST /api/v1/leads'] },
    C5:  { service: 'Backend',      label: 'KVKK / Privacy',    endpoints: ['POST /api/v1/privacy/delete', 'GET /api/v1/privacy/export'] },
    C6:  { service: 'Backend',      label: 'Consent',           endpoints: ['POST /api/v1/consent', 'GET /api/v1/consent/{phone}'] },
    C7:  { service: 'Knowledge',    label: 'Knowledge Base',    endpoints: ['POST /search', 'GET /api/v1/faqs', 'GET /api/v1/documents'] },
    C8:  { service: 'AgentAI',      label: 'AI Agent Assist',   endpoints: ['POST /api/v1/suggest', 'POST /api/v1/feedback'] },
    C9:  { service: 'Automation',   label: 'Workflows',         endpoints: ['POST /api/v1/workflows', 'POST /api/v1/triggers'] },
    C10: { service: 'Backend',      label: 'Payments',          endpoints: ['POST /api/v1/payments/link'] },
    C11: { service: 'Integrations', label: 'External APIs',     endpoints: ['GET /api/v1/orders', 'GET /api/v1/shipment/track'] },
    C12: { service: 'Backend',      label: 'Attribution',       endpoints: ['POST /api/v1/attribution'] },
    C13: { service: 'ChatAnalysis', label: 'Analytics',         endpoints: ['POST /api/v1/analytics/anomaly'] },
};

const CONTENT_PATTERNS = [
    { pattern: /intent[:\s]/i,          capability: 'C8',  method: 'POST', endpoint: '/api/v1/suggest',           desc: 'AI intent tespiti' },
    { pattern: /sentiment[:\s]/i,       capability: 'C13', method: 'POST', endpoint: '/api/v1/analytics/sentiment', desc: 'Duygu analizi' },
    { pattern: /lead\s*(yakalan|kayded|olustur)/i, capability: 'C4', method: 'POST', endpoint: '/api/v1/leads', desc: 'Lead olusturma' },
    { pattern: /crm|musteri\s*profil/i, capability: 'C4',  method: 'GET',  endpoint: '/api/v1/leads/{id}',       desc: 'CRM musteri sorgusu' },
    { pattern: /kargo|teslimat|gonderi/i, capability: 'C11', method: 'GET', endpoint: '/api/v1/shipment/track',  desc: 'Kargo takip' },
    { pattern: /siparis|order/i,        capability: 'C11', method: 'GET',  endpoint: '/api/v1/orders',           desc: 'Siparis sorgusu' },
    { pattern: /fiyat|ucret|tarife/i,   capability: 'C7',  method: 'POST', endpoint: '/search',                  desc: 'Knowledge fiyat aramasi' },
    { pattern: /faq|sik\s*sor/i,        capability: 'C7',  method: 'GET',  endpoint: '/api/v1/faqs',             desc: 'FAQ sorgusu' },
    { pattern: /bilgi\s*bank|dokuman/i, capability: 'C7',  method: 'GET',  endpoint: '/api/v1/documents',        desc: 'Dokuman aramasi' },
    { pattern: /odeme|payment|kapora/i, capability: 'C10', method: 'POST', endpoint: '/api/v1/payments/link',    desc: 'Odeme linki olusturma' },
    { pattern: /attribution|kaynak|utm/i, capability: 'C12', method: 'POST', endpoint: '/api/v1/attribution',   desc: 'Attribution kaydi' },
    { pattern: /eskal|yonlendir|ata/i,  capability: 'C2',  method: 'POST', endpoint: '/api/v1/routing',          desc: 'Yonlendirme / eskalasyon' },
    { pattern: /sablon|template/i,      capability: 'C3',  method: 'POST', endpoint: '/api/v1/templates/render', desc: 'Sablon render' },
    { pattern: /randevu|appointment/i,  capability: 'C4',  method: 'POST', endpoint: '/api/v1/appointments',     desc: 'Randevu olusturma' },
    { pattern: /kvkk|gdpr|consent|riza/i, capability: 'C6', method: 'POST', endpoint: '/api/v1/consent',        desc: 'Riza kaydi' },
    { pattern: /silme\s*taleb|veri\s*sil/i, capability: 'C5', method: 'POST', endpoint: '/api/v1/privacy/delete', desc: 'Veri silme talebi' },
    { pattern: /cron|zamanlan|polling|periyodik/i, capability: 'C9', method: 'POST', endpoint: '/api/v1/triggers', desc: 'Zamanlanmis tetikleme' },
    { pattern: /otomasyon|workflow|akis/i, capability: 'C9', method: 'POST', endpoint: '/api/v1/workflows',      desc: 'Otomasyon akisi' },
    { pattern: /anomali|fraud|sahte/i,  capability: 'C13', method: 'POST', endpoint: '/api/v1/analytics/anomaly', desc: 'Anomali tespiti' },
    { pattern: /whatsapp.*link|wa.*gecis/i, capability: 'C1', method: 'POST', endpoint: '/api/v1/messages',     desc: 'Kanal gecis mesaji' },
];

const ROLE_DEFAULTS = {
    user:   [{ capability: 'C1', method: 'POST', endpoint: '/api/v1/messages',        desc: 'Mesaj alimi (Unified Inbox)' }],
    ai:     [{ capability: 'C8', method: 'POST', endpoint: '/api/v1/suggest',          desc: 'AI oneri motoru' }],
    bot:    [{ capability: 'C3', method: 'POST', endpoint: '/api/v1/templates/render', desc: 'Sablon mesaj gonderimi' },
             { capability: 'C1', method: 'POST', endpoint: '/api/v1/messages',         desc: 'Mesaj gonderimi' }],
    system: [{ capability: 'C4', method: 'POST', endpoint: '/api/v1/leads',            desc: 'Sistem kaydi' }],
    agent:  [{ capability: 'C2', method: 'POST', endpoint: '/api/v1/routing',          desc: 'Agent yonlendirme' }],
};

function mapStepToApiCalls(step) {
    const calls = [];
    const seen = new Set();
    const content = step.content || '';
    const role = step.role || 'user';
    for (const rule of CONTENT_PATTERNS) {
        if (rule.pattern.test(content)) {
            const key = `${rule.method}:${rule.endpoint}`;
            if (!seen.has(key)) {
                seen.add(key);
                calls.push({ capability: rule.capability, service: CAPABILITY_MAP[rule.capability]?.service || 'Unknown', method: rule.method, endpoint: rule.endpoint, desc: rule.desc, source: 'content' });
            }
        }
    }
    const defaults = ROLE_DEFAULTS[role] || [];
    for (const d of defaults) {
        const key = `${d.method}:${d.endpoint}`;
        if (!seen.has(key)) {
            seen.add(key);
            calls.push({ capability: d.capability, service: CAPABILITY_MAP[d.capability]?.service || 'Unknown', method: d.method, endpoint: d.endpoint, desc: d.desc, source: 'role' });
        }
    }
    return calls;
}

function mapFlowToApiCalls(flow) {
    if (!flow?.steps) return [];
    return flow.steps.map((step, idx) => ({
        stepIndex: idx,
        role: step.role || 'user',
        content: step.content || '',
        apiCalls: mapStepToApiCalls(step),
    }));
}

function getServiceSummary(scenario) {
    const services = {};
    for (const flow of scenario?.flows || []) {
        const mapped = mapFlowToApiCalls(flow);
        for (const step of mapped) {
            for (const call of step.apiCalls) {
                if (!services[call.service]) services[call.service] = { calls: [], capabilities: new Set() };
                const key = `${call.method}:${call.endpoint}`;
                if (!services[call.service].calls.find(c => `${c.method}:${c.endpoint}` === key))
                    services[call.service].calls.push(call);
                services[call.service].capabilities.add(call.capability);
            }
        }
    }
    return Object.fromEntries(Object.entries(services).map(([n, d]) => [n, { ...d, capabilities: [...d.capabilities] }]));
}

function normalizeReq(req) {
    if (typeof req === 'string') return { text: req, status: 'setup', priority: 'optional', effort: 'medium' };
    return { text: req.text || req, service: req.service || '', page: req.page || '', status: req.status || 'setup', priority: req.priority || 'optional', effort: req.effort || 'medium', capability: req.capability || '', hint: req.hint || '', side: '' };
}

function generateGapReport(scenario) {
    const gaps = [];
    const readyItems = [];
    for (const flow of scenario?.flows || []) {
        const reqs = flow.requirements || {};
        const allReqs = [
            ...(reqs.client || []).map(r => ({ ...normalizeReq(r), side: 'client' })),
            ...(reqs.provider || []).map(r => ({ ...normalizeReq(r), side: 'provider' })),
        ];
        for (const req of allReqs) {
            if (req.status === 'ready') readyItems.push(req);
            else gaps.push(req);
        }
    }
    gaps.sort((a, b) => {
        const order = { required: 0, recommended: 1, optional: 2 };
        return (order[a.priority] ?? 2) - (order[b.priority] ?? 2);
    });
    return { gaps, readyItems };
}

function calculateReadinessScore(scenario) {
    const items = [];
    for (const flow of scenario?.flows || []) {
        const reqs = flow.requirements || {};
        for (const req of reqs.client || []) { if (typeof req === 'object') items.push(req); }
        for (const req of reqs.provider || []) { if (typeof req === 'object') items.push(req); }
    }
    if (items.length === 0) {
        const hasServices = (scenario.overview?.services?.length || 0) > 0;
        const hasTech = !!scenario.tech;
        const base = hasServices ? 50 : 20;
        return { score: hasTech ? base + 20 : base, total: 0, ready: 0, setup: 0, missing: 0 };
    }
    let ready = 0, setup = 0, requiredSetup = 0;
    for (const item of items) {
        if (item.status === 'ready') ready++;
        else { setup++; if (item.priority === 'required') requiredSetup++; }
    }
    const total = items.length;
    const score = total > 0 ? Math.round(((ready + setup * 0.3) / total) * 100) : 50;
    return { score: Math.min(score, 100), total, ready, setup, missing: requiredSetup };
}

// ─── Main ──────────────────────────────────────────────────────────────

const jsonFiles = readdirSync(dataDir).filter(f => f.endsWith('.json') && f !== 'field-scenarios.json' && f !== 'test-results.json' && f !== 'index.js');

console.log(`\n=== SE Scenario Test Runner ===`);
console.log(`Tarih: ${new Date().toISOString()}`);
console.log(`Bulunan senaryo dosyasi: ${jsonFiles.length}\n`);

const results = {};
const summary = { total: 0, passed: 0, warnings: 0, errors: 0, totalFlows: 0, totalSteps: 0, totalApiCalls: 0, totalGaps: 0, totalReady: 0, byCategory: {}, byService: {} };

for (const file of jsonFiles) {
    const id = basename(file, '.json');
    try {
        const raw = readFileSync(join(dataDir, file), 'utf-8');
        const scenario = JSON.parse(raw);

        if (!scenario.id || !scenario.title) {
            // Not a scenario file (might be field-scenarios or other config)
            continue;
        }

        summary.total++;

        const flows = scenario.flows || [];
        const totalSteps = flows.reduce((s, f) => s + (f.steps?.length || 0), 0);

        // Run analysis
        const readiness = calculateReadinessScore(scenario);
        const { gaps, readyItems } = generateGapReport(scenario);
        const services = getServiceSummary(scenario);

        // Collect all API calls per flow
        const flowResults = flows.map((flow, fi) => {
            const mapped = mapFlowToApiCalls(flow);
            const totalCalls = mapped.reduce((s, m) => s + m.apiCalls.length, 0);
            return {
                flowId: flow.id || `flow-${fi + 1}`,
                title: flow.title || `Flow ${fi + 1}`,
                steps: mapped.length,
                apiCalls: totalCalls,
                stepDetails: mapped.map(m => ({
                    step: m.stepIndex + 1,
                    role: m.role,
                    content: m.content.substring(0, 120) + (m.content.length > 120 ? '...' : ''),
                    apis: m.apiCalls.map(a => `${a.method} ${a.endpoint} (${a.capability} ${a.service})`),
                })),
            };
        });

        const totalApiCalls = flowResults.reduce((s, f) => s + f.apiCalls, 0);
        const serviceNames = Object.keys(services);
        const capabilities = [...new Set(Object.values(services).flatMap(s => s.capabilities))];
        const criticalGaps = gaps.filter(g => g.priority === 'required');

        // Determine status
        let status = 'pass';
        if (criticalGaps.length > 0) status = 'warning';
        if (flows.length === 0 || totalSteps === 0) status = 'error';

        results[id] = {
            id: scenario.id,
            title: scenario.title,
            category: scenario.category || '',
            phase: scenario.phase || '',
            niche: scenario.niche || '',
            status,
            readinessScore: readiness.score,
            totalFlows: flows.length,
            totalSteps,
            totalApiCalls,
            services: serviceNames,
            capabilities,
            gaps: gaps.length,
            criticalGaps: criticalGaps.length,
            readyItems: readyItems.length,
            gapDetails: gaps.map(g => ({ text: g.text, priority: g.priority, service: g.service, capability: g.capability, side: g.side })),
            readyDetails: readyItems.map(r => ({ text: r.text, service: r.service, capability: r.capability })),
            flowResults,
            testedAt: new Date().toISOString(),
        };

        // Update summary
        if (status === 'pass') summary.passed++;
        else if (status === 'warning') summary.warnings++;
        else summary.errors++;
        summary.totalFlows += flows.length;
        summary.totalSteps += totalSteps;
        summary.totalApiCalls += totalApiCalls;
        summary.totalGaps += gaps.length;
        summary.totalReady += readyItems.length;

        const cat = scenario.category || 'Diger';
        if (!summary.byCategory[cat]) summary.byCategory[cat] = { count: 0, pass: 0, warn: 0, err: 0 };
        summary.byCategory[cat].count++;
        if (status === 'pass') summary.byCategory[cat].pass++;
        else if (status === 'warning') summary.byCategory[cat].warn++;
        else summary.byCategory[cat].err++;

        for (const svc of serviceNames) {
            summary.byService[svc] = (summary.byService[svc] || 0) + 1;
        }

        // Console output
        const icon = status === 'pass' ? '✓' : status === 'warning' ? '⚠' : '✗';
        console.log(`  ${icon} ${id.toUpperCase().padEnd(6)} | Score: ${String(readiness.score).padStart(3)} | ${flows.length}F ${totalSteps}S ${totalApiCalls}A | ${criticalGaps.length > 0 ? criticalGaps.length + ' gap' : 'OK'} | ${scenario.title}`);

    } catch (err) {
        console.log(`  ✗ ${id.toUpperCase().padEnd(6)} | PARSE ERROR: ${err.message}`);
        summary.errors++;
        summary.total++;
    }
}

// ─── Write results ─────────────────────────────────────────────────────

const output = {
    meta: {
        generatedAt: new Date().toISOString(),
        totalScenarios: summary.total,
        engine: 'scenarioTestEngine v1.0',
    },
    summary,
    results,
};

writeFileSync(join(dataDir, 'test-results.json'), JSON.stringify(output, null, 2), 'utf-8');

// ─── Print summary ─────────────────────────────────────────────────────

console.log(`\n${'='.repeat(60)}`);
console.log(`OZET RAPOR`);
console.log(`${'='.repeat(60)}`);
console.log(`Toplam Senaryo:    ${summary.total}`);
console.log(`  ✓ Gecti:         ${summary.passed}`);
console.log(`  ⚠ Uyari (gap):   ${summary.warnings}`);
console.log(`  ✗ Hata (bos):    ${summary.errors}`);
console.log(`Toplam Akis:       ${summary.totalFlows}`);
console.log(`Toplam Adim:       ${summary.totalSteps}`);
console.log(`Toplam API Call:   ${summary.totalApiCalls}`);
console.log(`Toplam Gap:        ${summary.totalGaps}`);
console.log(`Toplam Hazir:      ${summary.totalReady}`);
console.log();
console.log(`KATEGORI BAZLI:`);
for (const [cat, data] of Object.entries(summary.byCategory).sort((a, b) => b[1].count - a[1].count)) {
    console.log(`  ${cat.padEnd(30)} ${String(data.count).padStart(3)} senaryo | ✓${data.pass} ⚠${data.warn} ✗${data.err}`);
}
console.log();
console.log(`SERVIS KULLANIMI:`);
for (const [svc, count] of Object.entries(summary.byService).sort((a, b) => b[1] - a[1])) {
    console.log(`  ${svc.padEnd(20)} ${count} senaryoda kullaniliyor`);
}
console.log();
console.log(`Sonuclar: SE/src/data/test-results.json`);
console.log(`${'='.repeat(60)}\n`);
