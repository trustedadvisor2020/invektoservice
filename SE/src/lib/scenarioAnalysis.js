// ── Scenario Analysis Engine ─────────────────────────────────────
// Render-time analysis for all 170 scenarios:
// 1. Health check (same logic as HealthCheck page)
// 2. Auto-derived automation flow nodes from chat steps + requirements
// 3. Development suggestions based on findings

// ── Constants ────────────────────────────────────────────────────

const VALID_NICHES = ['ecommerce', 'health', 'dental', 'aesthetic', 'hotel', 'beauty', 'education', 'mobile', 'universal', 'crossSector'];
const VALID_ROLES = ['system', 'bot', 'user', 'ai', 'agent'];

// Real Chatinbox Automation v2 node types
const NODE_TYPES = {
    trigger_start:        { label: 'Tetikleyici',       color: 'amber',   icon: 'Zap' },
    message_text:         { label: 'Mesaj Gonder',      color: 'green',   icon: 'MessageSquare' },
    message_menu:         { label: 'Menu Goster',       color: 'green',   icon: 'List' },
    logic_condition:      { label: 'Kosul Kontrolu',    color: 'blue',    icon: 'GitBranch' },
    logic_switch:         { label: 'Coklu Dal',         color: 'blue',    icon: 'GitFork' },
    ai_intent:            { label: 'Niyet Tespit',      color: 'purple',  icon: 'Brain' },
    ai_faq:               { label: 'Bilgi Bankasi',     color: 'purple',  icon: 'BookOpen' },
    action_handoff:       { label: 'Agente Devret',     color: 'rose',    icon: 'UserCheck' },
    action_api_call:      { label: 'API Cagrisi',       color: 'cyan',    icon: 'Globe' },
    action_delay:         { label: 'Bekleme',           color: 'gray',    icon: 'Clock' },
    utility_set_variable: { label: 'Degisken Ata',      color: 'gray',    icon: 'Variable' },
    utility_note:         { label: 'Not',               color: 'gray',    icon: 'StickyNote' },
    // New node types (implemented in Automation v2)
    outbound_trigger:     { label: 'Outbound Tetik',    color: 'amber',   icon: 'Send' },
    schedule_trigger:     { label: 'Zamanli Tetik',     color: 'amber',   icon: 'Calendar' },
    ai_sentiment:         { label: 'Duygu Analizi',     color: 'purple',  icon: 'Heart' },
    webhook_trigger:      { label: 'Webhook Tetik',     color: 'amber',   icon: 'Webhook' },
};

// ── 1. Health Check (Analysis) ───────────────────────────────────

export function analyzeScenario(s) {
    const findings = [];

    // Meta check
    const metaMissing = [];
    if (!s.title) metaMissing.push('title');
    if (!s.niche) metaMissing.push('niche');
    if (!s.description) metaMissing.push('description');
    if (!s.subtitle) metaMissing.push('subtitle');
    if (!s.phase) metaMissing.push('phase');
    if (!s.category) metaMissing.push('category');
    const metaCritical = !s.title || !s.niche || !s.description;
    findings.push({
        key: 'meta',
        type: metaCritical ? 'fail' : metaMissing.length ? 'warn' : 'pass',
        label: 'Meta Bilgiler',
        detail: metaMissing.length ? `Eksik: ${metaMissing.join(', ')}` : 'Tamam',
    });

    // Niche
    findings.push({
        key: 'niche',
        type: VALID_NICHES.includes(s.niche) ? 'pass' : 'fail',
        label: 'Niche',
        detail: VALID_NICHES.includes(s.niche) ? s.niche : `Gecersiz: "${s.niche}"`,
    });

    // Overview
    const ov = s.overview || {};
    const ovMissing = [];
    if (!ov.audience?.length) ovMissing.push('audience');
    if (!ov.services?.length) ovMissing.push('services');
    if (!ov.steps?.length) ovMissing.push('steps');
    const ovAll = ovMissing.length === 0;
    const stepsIncomplete = ov.steps?.filter(st => !st.title || !st.goal || !st.content).length || 0;
    findings.push({
        key: 'overview',
        type: !s.overview ? 'fail' : ovAll && !stepsIncomplete ? 'pass' : ovAll ? 'warn' : 'fail',
        label: 'Overview',
        detail: !s.overview ? 'Yok' : ovMissing.length ? `Eksik: ${ovMissing.join(', ')}` : stepsIncomplete ? `${stepsIncomplete} step eksik alan` : `${ov.audience?.length || 0} audience, ${ov.services?.length || 0} servis, ${ov.steps?.length || 0} step`,
    });

    // Flows
    const flows = s.flows || [];
    const flowCount = flows.length;
    const flowIssues = flows.filter(f => !f.id || !f.title || !f.steps?.length).length;
    findings.push({
        key: 'flows',
        type: !flowCount ? 'fail' : flowIssues ? 'warn' : flowCount >= 2 ? 'pass' : 'warn',
        label: 'Flow Sayisi',
        detail: !flowCount ? 'Hic flow yok' : `${flowCount} flow${flowIssues ? `, ${flowIssues} eksik` : ''}${flowCount < 2 ? ' (2+ onerili)' : ''}`,
    });

    // Chat depth
    const totalSteps = flows.reduce((sum, f) => sum + (f.steps?.length || 0), 0);
    const avgSteps = flowCount ? totalSteps / flowCount : 0;
    findings.push({
        key: 'chatDepth',
        type: !flowCount ? 'fail' : avgSteps >= 5 ? 'pass' : avgSteps >= 3 ? 'warn' : 'fail',
        label: 'Chat Derinligi',
        detail: !flowCount ? 'Flow yok' : `Toplam ${totalSteps} step, ort ${avgSteps.toFixed(1)}/flow`,
    });

    // Roles
    const allSteps = flows.flatMap(f => f.steps || []);
    const invalidRoles = allSteps.filter(st => !VALID_ROLES.includes(st.role));
    const hasBot = allSteps.some(st => st.role === 'bot');
    const hasUser = allSteps.some(st => st.role === 'user');
    findings.push({
        key: 'roles',
        type: invalidRoles.length ? 'fail' : hasBot && hasUser ? 'pass' : 'warn',
        label: 'Chat Rolleri',
        detail: invalidRoles.length
            ? `Gecersiz: ${invalidRoles.map(st => st.role).join(', ')}`
            : `Roller: ${[...new Set(allSteps.map(st => st.role))].join(', ')}`,
    });

    // Requirements
    const allHaveReqs = flows.every(f => f.requirements?.client?.length > 0 && f.requirements?.provider?.length > 0);
    const someHaveReqs = flows.some(f => f.requirements?.client?.length > 0 || f.requirements?.provider?.length > 0);
    findings.push({
        key: 'requirements',
        type: !flowCount ? 'fail' : allHaveReqs ? 'pass' : someHaveReqs ? 'warn' : 'fail',
        label: 'Requirements',
        detail: !flowCount ? 'Flow yok' : allHaveReqs ? 'Tum flowlarda tamam' : 'Bazi flowlarda eksik',
    });

    // Requirement enrichment
    const allReqs = flows.flatMap(f => [...(f.requirements?.client || []), ...(f.requirements?.provider || [])]);
    const enrichedReqs = allReqs.filter(r => typeof r === 'object' && r.service && r.status && r.priority && r.effort && r.capability);
    const enrichRatio = allReqs.length ? enrichedReqs.length / allReqs.length : 0;
    findings.push({
        key: 'reqEnrich',
        type: !allReqs.length ? 'fail' : enrichRatio >= 0.9 ? 'pass' : enrichRatio >= 0.5 ? 'warn' : 'fail',
        label: 'Req. Zenginlik',
        detail: `${enrichedReqs.length}/${allReqs.length} tam zenginlestirilmis`,
    });

    // Tech
    const tech = s.tech || {};
    const techMissing = [];
    if (!tech.note) techMissing.push('note');
    if (!tech.backend?.length) techMissing.push('backend');
    if (!tech.apis?.length) techMissing.push('apis');
    findings.push({
        key: 'tech',
        type: !s.tech ? 'fail' : techMissing.length === 0 ? 'pass' : techMissing.length <= 1 ? 'warn' : 'fail',
        label: 'Tech',
        detail: !s.tech ? 'Yok' : techMissing.length ? `Eksik: ${techMissing.join(', ')}` : 'Tamam',
    });

    // Impact
    const imp = s.impact || {};
    const impMissing = [];
    if (!imp.title) impMissing.push('title');
    if (!imp.formula) impMissing.push('formula');
    if (!imp.inputs?.length) impMissing.push('inputs');
    if (typeof imp.subscriptionCost !== 'number') impMissing.push('subscriptionCost');
    findings.push({
        key: 'impact',
        type: !s.impact ? 'fail' : impMissing.length === 0 ? 'pass' : 'fail',
        label: 'Impact/ROI',
        detail: !s.impact ? 'Yok' : impMissing.length ? `Eksik: ${impMissing.join(', ')}` : 'Tamam',
    });

    // Formula eval
    let formulaResult = null;
    let formulaError = null;
    if (imp.formula && imp.inputs?.length) {
        try {
            let expr = imp.formula;
            imp.inputs.forEach(input => {
                const val = input.constant ?? input.default;
                expr = expr.replace(new RegExp(`\\b${input.key}\\b`, 'g'), String(val));
            });
            formulaResult = Math.round(eval(expr));
            if (isNaN(formulaResult)) formulaError = 'NaN sonuc';
        } catch (e) {
            formulaError = e.message;
        }
    }
    findings.push({
        key: 'formula',
        type: formulaResult > 0 ? 'pass' : formulaResult === 0 ? 'warn' : 'fail',
        label: 'Formula Test',
        detail: formulaResult != null && !formulaError
            ? `${formulaResult.toLocaleString('tr-TR')} TL/ay`
            : formulaError || 'Formula yok',
    });

    // Score
    const pass = findings.filter(f => f.type === 'pass').length;
    const total = findings.length;
    const pct = Math.round((pass / total) * 100);
    const grade = pct >= 90 ? 'A' : pct >= 75 ? 'B' : pct >= 50 ? 'C' : 'D';

    return { findings, score: pct, grade, formulaResult };
}

// ── 2. Auto-derive Automation Flow Nodes ─────────────────────────

export function deriveAutomationFlow(flow, scenarioOverview) {
    if (!flow?.steps?.length) return { nodes: [], edges: [] };

    const nodes = [];
    const edges = [];
    let nodeId = 0;
    const nid = () => `n${++nodeId}`;

    // Detect trigger type from first system step
    const firstStep = flow.steps[0];
    const triggerType = detectTriggerType(firstStep, flow);
    const triggerId = nid();
    nodes.push({ id: triggerId, type: triggerType, label: extractTriggerLabel(firstStep, flow), service: null });

    let prevId = triggerId;
    let prevHandle = 'default';

    // Extract services from requirements
    const reqServices = extractRequiredServices(flow);

    // If there are provider API requirements, add API lookup nodes early
    const apiNodes = [];
    if (reqServices.apiLookups.length > 0) {
        reqServices.apiLookups.forEach(api => {
            const id = nid();
            nodes.push({ id, type: 'action_api_call', label: api.label, service: api.service });
            edges.push({ from: prevId, to: id, handle: prevHandle, label: null });
            apiNodes.push(id);
            prevId = id;
            prevHandle = 'success';
        });
    }

    // Process chat steps (skip first if it was used as trigger)
    const startIdx = firstStep.role === 'system' ? 1 : 0;
    let lastUserStep = false;

    for (let i = startIdx; i < flow.steps.length; i++) {
        const step = flow.steps[i];
        const isLast = i === flow.steps.length - 1;

        if (step.role === 'ai') {
            // AI analysis node
            const type = detectAiNodeType(step);
            const id = nid();
            nodes.push({ id, type, label: extractStepLabel(step, type), service: 'AgentAI' });
            edges.push({ from: prevId, to: id, handle: prevHandle });

            // AI nodes branch: add condition if there's decision content
            if (type === 'ai_intent' || type === 'ai_sentiment') {
                const condId = nid();
                nodes.push({ id: condId, type: 'logic_condition', label: extractDecisionLabel(step), service: null });
                edges.push({ from: id, to: condId, handle: 'high_confidence' });
                prevId = condId;
                prevHandle = 'true_handle';
            } else {
                prevId = id;
                prevHandle = 'default';
            }
        } else if (step.role === 'bot') {
            const id = nid();
            const hasMenu = step.content?.includes('1.') || step.content?.includes('1)') || step.content?.includes('secin');
            const type = hasMenu ? 'message_menu' : 'message_text';
            nodes.push({ id, type, label: truncate(step.content, 50), service: null });
            edges.push({ from: prevId, to: id, handle: prevHandle });
            prevId = id;
            prevHandle = 'default';
            lastUserStep = false;
        } else if (step.role === 'user') {
            // User input — don't create a node, but mark that next bot reply follows user input
            lastUserStep = true;
        } else if (step.role === 'agent') {
            const id = nid();
            nodes.push({ id, type: 'action_handoff', label: extractStepLabel(step, 'action_handoff'), service: null });
            edges.push({ from: prevId, to: id, handle: prevHandle });
            prevId = id;
            prevHandle = null; // terminal
        } else if (step.role === 'system' && !isLast) {
            // Mid-flow system event — set variable or delay
            const isDelay = /gun|saat|dakika|T\+/i.test(step.content);
            const type = isDelay ? 'action_delay' : 'utility_set_variable';
            const id = nid();
            nodes.push({ id, type, label: truncate(step.content, 50), service: null });
            edges.push({ from: prevId, to: id, handle: prevHandle });
            prevId = id;
            prevHandle = 'default';
        } else if (step.role === 'system' && isLast) {
            // Final system step — logging/completion
            const id = nid();
            nodes.push({ id, type: 'utility_set_variable', label: truncate(step.content, 50), service: null });
            edges.push({ from: prevId, to: id, handle: prevHandle });
        }
    }

    // Add Knowledge/FAQ node if requirements mention Knowledge service
    if (reqServices.hasKnowledge && !nodes.some(n => n.type === 'ai_faq')) {
        const kbIdx = nodes.findIndex(n => n.type === 'message_text');
        if (kbIdx > 0) {
            const id = nid();
            nodes.splice(kbIdx, 0, { id, type: 'ai_faq', label: 'Knowledge Base Sorgu', service: 'Knowledge' });
            // Rewire edges
            const edgeToRewrite = edges.find(e => e.to === nodes[kbIdx + 1]?.id);
            if (edgeToRewrite) {
                const originalTo = edgeToRewrite.to;
                edgeToRewrite.to = id;
                edges.push({ from: id, to: originalTo, handle: 'matched' });
            }
        }
    }

    return { nodes, edges };
}

function detectTriggerType(step, flow) {
    if (!step) return 'trigger_start';
    const c = (step.content || '').toLowerCase();
    if (/api|webhook|tespit|event/.test(c)) return 'webhook_trigger';
    if (/otomasyon|tetik|zamanl|cron|batch|gece/.test(c)) return 'schedule_trigger';
    if (/outbound|kampanya|bildirim|hatirlatma/.test(c)) return 'outbound_trigger';
    return 'trigger_start';
}

function extractTriggerLabel(step, flow) {
    if (!step) return flow?.title || 'Baslangic';
    return truncate(step.content, 60);
}

function detectAiNodeType(step) {
    const c = (step.content || '').toLowerCase();
    if (/sentiment|duygu|ofke|kizginlik|memnuniyet/.test(c)) return 'ai_sentiment';
    if (/analiz|tespit|sinifland|kategori|tur/.test(c)) return 'ai_intent';
    return 'ai_intent';
}

function extractDecisionLabel(step) {
    const c = step.content || '';
    // Try to extract the decision from content like "Analiz: Negatif - Kargo Hasari. Aksiyon: Telafi Modu."
    const match = c.match(/Aksiyon:\s*(.+?)[\.\,\n]/);
    if (match) return match[1].trim();
    const match2 = c.match(/Analiz:\s*(.+?)[\.\,\n]/);
    if (match2) return match2[1].trim();
    return truncate(c, 40);
}

function extractStepLabel(step, nodeType) {
    const c = step.content || '';
    if (nodeType === 'action_handoff') {
        const match = c.match(/Manuel Kontrol:\s*(.+)/);
        return match ? match[1].trim() : truncate(c, 50);
    }
    return truncate(c, 50);
}

function extractRequiredServices(flow) {
    const result = { apiLookups: [], hasKnowledge: false, hasOutbound: false };
    const providers = flow.requirements?.provider || [];
    const clients = flow.requirements?.client || [];
    const allReqs = [...providers, ...clients];

    const seenServices = new Set();
    allReqs.forEach(req => {
        if (typeof req !== 'object' || !req.service) return;
        const svc = req.service;
        if (svc === 'Knowledge') { result.hasKnowledge = true; return; }
        if (svc === 'Outbound') { result.hasOutbound = true; return; }
        if (['Integrations', 'Backend'].includes(svc) && !seenServices.has(svc)) {
            seenServices.add(svc);
            const label = req.text ? truncate(req.text, 40) : `${svc} API`;
            result.apiLookups.push({ service: svc, label });
        }
    });

    // Limit to max 2 API lookups to keep diagram clean
    result.apiLookups = result.apiLookups.slice(0, 2);
    return result;
}

function truncate(str, len) {
    if (!str) return '';
    return str.length > len ? str.substring(0, len) + '...' : str;
}

// ── 3. Generate Suggestions ─────────────────────────────────────

export function generateSuggestions(scenario, analysis) {
    const suggestions = [];
    const flows = scenario.flows || [];
    const flowCount = flows.length;

    // Flow count suggestions
    if (flowCount === 0) {
        suggestions.push({ priority: 'critical', category: 'icerik', text: 'Hic senaryo akisi (flow) yok. En az 2 flow eklenmeli.' });
    } else if (flowCount === 1) {
        suggestions.push({ priority: 'high', category: 'icerik', text: 'Sadece 1 flow var. Alternatif senaryolar ekleyin: basarisiz durum, edge case, veya farkli musteri profili.' });
    }

    // Chat depth
    const totalSteps = flows.reduce((sum, f) => sum + (f.steps?.length || 0), 0);
    const avgSteps = flowCount ? totalSteps / flowCount : 0;
    if (avgSteps < 4 && flowCount > 0) {
        suggestions.push({ priority: 'high', category: 'icerik', text: `Ort. ${avgSteps.toFixed(1)} chat stepi cok kisa. Her flow'a en az 5 step hedefleyin: karsilama, bilgi toplama, islem, onay, kapanıs.` });
    }

    // Overview steps
    if (scenario.overview?.steps?.length < 3) {
        suggestions.push({ priority: 'medium', category: 'icerik', text: 'Overview bolumunde 3+ sistem adimi olmali. Tetikleme, islem ve sonuc adimlari ekleyin.' });
    }

    // Overview services
    if (scenario.overview?.services?.length < 3) {
        suggestions.push({ priority: 'medium', category: 'icerik', text: 'Kullanilan servis sayisi az. Hangi Chatinbox servisleri (Backend, Knowledge, AgentAI, Outbound, vb.) devrede oldugunu belirtin.' });
    }

    // Tech config
    if (scenario.tech && !scenario.tech.config) {
        suggestions.push({ priority: 'low', category: 'teknik', text: 'Tech bolumune ornek JSON konfigurasyon ekleyin. Musteri teknik detaylari gormeyi sever.' });
    }

    // Missing roles in flows
    const allRoles = new Set(flows.flatMap(f => (f.steps || []).map(s => s.role)));
    if (!allRoles.has('ai') && !allRoles.has('agent')) {
        suggestions.push({ priority: 'medium', category: 'otomasyon', text: 'Hic AI analiz veya agent eskalasyon stepi yok. AI karar mekanizmasi ekleyin.' });
    }
    if (!allRoles.has('agent')) {
        suggestions.push({ priority: 'low', category: 'otomasyon', text: 'Agent (insan) eskalasyon senaryosu eksik. Sistemin cevaplayamadigi durumlar icin handoff flow\'u ekleyin.' });
    }

    // Requirement enrichment
    const allReqs = flows.flatMap(f => [...(f.requirements?.client || []), ...(f.requirements?.provider || [])]);
    const plainStringReqs = allReqs.filter(r => typeof r === 'string');
    if (plainStringReqs.length > 0) {
        suggestions.push({ priority: 'high', category: 'teknik', text: `${plainStringReqs.length} requirement duuz string — zenginlestirin: service, status, priority, effort, capability alanlari ekleyin.` });
    }

    // ROI realism check
    if (analysis.formulaResult != null) {
        const cost = scenario.impact?.subscriptionCost || 0;
        const roi = cost > 0 ? analysis.formulaResult / cost : 0;
        if (roi > 50) {
            suggestions.push({ priority: 'medium', category: 'roi', text: `ROI ${roi.toFixed(0)}x cok yuksek gorunuyor. Default degerleri ve formuluyu gozden gecirin — musteriye inandirici olmayabilir.` });
        }
        if (roi < 1 && cost > 0) {
            suggestions.push({ priority: 'high', category: 'roi', text: `ROI ${roi.toFixed(1)}x — abonelik maliyetinin altinda. Formula veya default degerleri guncelle.` });
        }
    }

    // Automation flow suggestions based on scenario type
    const desc = (scenario.description || '').toLowerCase();
    if (/proaktif|otomatik|trigger|tetik/.test(desc) && !allRoles.has('system')) {
        suggestions.push({ priority: 'medium', category: 'otomasyon', text: 'Proaktif senaryo ama system trigger stepi eksik. Tetikleme mekanizmasini acikca gosterin.' });
    }

    // Missing description on flows
    flows.forEach((f, i) => {
        if (!f.description) {
            suggestions.push({ priority: 'low', category: 'icerik', text: `Flow ${i + 1} (${f.title}) icin aciklama (description) eksik.` });
        }
    });

    // Sort by priority
    const pOrder = { critical: 0, high: 1, medium: 2, low: 3 };
    suggestions.sort((a, b) => pOrder[a.priority] - pOrder[b.priority]);

    return suggestions;
}

// ── Exports ──────────────────────────────────────────────────────

export { NODE_TYPES };
