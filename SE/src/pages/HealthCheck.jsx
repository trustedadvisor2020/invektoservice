import React, { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { CheckCircle, AlertTriangle, XCircle, Filter, ChevronDown, ChevronUp, ArrowUpDown, Eye, EyeOff } from 'lucide-react';
import { allScenarios, sidebarGroups } from '../data';
import { getScenarioTier, TIER_META } from '../lib/scenarioMeta';

// ── Validation checks ──────────────────────────────────────────────

const VALID_NICHES = ['ecommerce', 'health', 'dental', 'aesthetic', 'hotel', 'beauty', 'education', 'mobile', 'universal', 'crossSector'];
const VALID_ROLES = ['system', 'bot', 'user', 'ai', 'agent'];

const checks = [
    {
        key: 'id',
        label: 'ID',
        tip: 'id alani var ve bos degil',
        check: (s) => s.id && typeof s.id === 'string' && s.id.trim() ? 'pass' : 'fail',
        detail: (s) => !s.id ? 'id alani eksik' : '',
    },
    {
        key: 'meta',
        label: 'Meta',
        tip: 'title, subtitle, phase, category, niche',
        check: (s) => {
            const hasRequired = s.title && s.niche && s.description;
            if (!hasRequired) return 'fail';
            const hasOptional = s.subtitle && s.phase && s.category;
            return hasOptional ? 'pass' : 'warn';
        },
        detail: (s) => {
            const missing = [];
            if (!s.title) missing.push('title');
            if (!s.niche) missing.push('niche');
            if (!s.description) missing.push('description');
            if (!s.subtitle) missing.push('subtitle');
            if (!s.phase) missing.push('phase');
            if (!s.category) missing.push('category');
            return missing.length ? `Eksik: ${missing.join(', ')}` : '';
        },
    },
    {
        key: 'niche',
        label: 'Niche',
        tip: 'Gecerli niche degeri',
        check: (s) => VALID_NICHES.includes(s.niche) ? 'pass' : 'fail',
        detail: (s) => !VALID_NICHES.includes(s.niche) ? `Gecersiz niche: "${s.niche}"` : '',
    },
    {
        key: 'overview',
        label: 'Overview',
        tip: 'audience, services, steps mevcut ve dolu',
        check: (s) => {
            if (!s.overview) return 'fail';
            const { audience, services, steps } = s.overview;
            const all = audience?.length > 0 && services?.length > 0 && steps?.length > 0;
            if (all) {
                const stepsOk = steps.every(st => st.title && st.goal && st.content);
                return stepsOk ? 'pass' : 'warn';
            }
            return audience?.length > 0 || services?.length > 0 || steps?.length > 0 ? 'warn' : 'fail';
        },
        detail: (s) => {
            if (!s.overview) return 'overview objesi yok';
            const missing = [];
            if (!s.overview.audience?.length) missing.push('audience');
            if (!s.overview.services?.length) missing.push('services');
            if (!s.overview.steps?.length) missing.push('steps');
            if (s.overview.steps?.length) {
                const bad = s.overview.steps.filter(st => !st.title || !st.goal || !st.content);
                if (bad.length) missing.push(`${bad.length} step eksik alan`);
            }
            return missing.length ? `Eksik: ${missing.join(', ')}` : '';
        },
    },
    {
        key: 'flows',
        label: 'Flows',
        tip: 'En az 1 flow, her flow id+title+description+steps',
        check: (s) => {
            if (!s.flows?.length) return 'fail';
            const allOk = s.flows.every(f => f.id && f.title && f.steps?.length > 0);
            if (!allOk) return 'warn';
            return s.flows.length >= 2 ? 'pass' : 'warn';
        },
        detail: (s) => {
            if (!s.flows?.length) return 'flows dizisi bos veya yok';
            const issues = [];
            s.flows.forEach((f, i) => {
                const m = [];
                if (!f.id) m.push('id');
                if (!f.title) m.push('title');
                if (!f.steps?.length) m.push('steps');
                if (m.length) issues.push(`Flow ${i + 1}: ${m.join(', ')} eksik`);
            });
            if (s.flows.length < 2) issues.push(`Sadece ${s.flows.length} flow (2+ onerili)`);
            return issues.join('; ');
        },
    },
    {
        key: 'chatDepth',
        label: 'Chat Derinligi',
        tip: 'Toplam chat step sayisi (5+ iyi, 3-4 orta, <3 zayif)',
        check: (s) => {
            if (!s.flows?.length) return 'fail';
            const totalSteps = s.flows.reduce((sum, f) => sum + (f.steps?.length || 0), 0);
            const avgSteps = totalSteps / s.flows.length;
            if (avgSteps >= 5) return 'pass';
            if (avgSteps >= 3) return 'warn';
            return 'fail';
        },
        detail: (s) => {
            if (!s.flows?.length) return 'Flow yok';
            const counts = s.flows.map(f => f.steps?.length || 0);
            const total = counts.reduce((a, b) => a + b, 0);
            return `${s.flows.length} flow, toplam ${total} step (ort: ${(total / s.flows.length).toFixed(1)})`;
        },
    },
    {
        key: 'roles',
        label: 'Chat Rolleri',
        tip: 'Tum step rolleri gecerli (system/bot/user/ai/agent)',
        check: (s) => {
            if (!s.flows?.length) return 'fail';
            const allSteps = s.flows.flatMap(f => f.steps || []);
            if (!allSteps.length) return 'fail';
            const invalid = allSteps.filter(st => !VALID_ROLES.includes(st.role));
            if (invalid.length) return 'fail';
            const hasBot = allSteps.some(st => st.role === 'bot');
            const hasUser = allSteps.some(st => st.role === 'user');
            return hasBot && hasUser ? 'pass' : 'warn';
        },
        detail: (s) => {
            if (!s.flows?.length) return 'Flow yok';
            const allSteps = s.flows.flatMap(f => f.steps || []);
            const invalid = allSteps.filter(st => !VALID_ROLES.includes(st.role));
            if (invalid.length) return `Gecersiz roller: ${invalid.map(st => st.role).join(', ')}`;
            const roles = [...new Set(allSteps.map(st => st.role))];
            return `Roller: ${roles.join(', ')}`;
        },
    },
    {
        key: 'requirements',
        label: 'Requirements',
        tip: 'Her flow client+provider requirements icermeli',
        check: (s) => {
            if (!s.flows?.length) return 'fail';
            const allHave = s.flows.every(f =>
                f.requirements?.client?.length > 0 && f.requirements?.provider?.length > 0
            );
            if (allHave) return 'pass';
            const someHave = s.flows.some(f =>
                f.requirements?.client?.length > 0 || f.requirements?.provider?.length > 0
            );
            return someHave ? 'warn' : 'fail';
        },
        detail: (s) => {
            if (!s.flows?.length) return 'Flow yok';
            const issues = [];
            s.flows.forEach((f, i) => {
                const m = [];
                if (!f.requirements?.client?.length) m.push('client');
                if (!f.requirements?.provider?.length) m.push('provider');
                if (m.length) issues.push(`Flow ${i + 1}: ${m.join('+')}`);
            });
            return issues.length ? `Eksik: ${issues.join('; ')}` : '';
        },
    },
    {
        key: 'reqEnrichment',
        label: 'Req. Zenginlik',
        tip: 'Requirements service/status/priority/effort/capability alanlari',
        check: (s) => {
            if (!s.flows?.length) return 'fail';
            const allReqs = s.flows.flatMap(f => [
                ...(f.requirements?.client || []),
                ...(f.requirements?.provider || []),
            ]);
            if (!allReqs.length) return 'fail';
            const enriched = allReqs.filter(r =>
                typeof r === 'object' && r.service && r.status && r.priority && r.effort && r.capability
            );
            const ratio = enriched.length / allReqs.length;
            if (ratio >= 0.9) return 'pass';
            if (ratio >= 0.5) return 'warn';
            return 'fail';
        },
        detail: (s) => {
            if (!s.flows?.length) return 'Flow yok';
            const allReqs = s.flows.flatMap(f => [
                ...(f.requirements?.client || []),
                ...(f.requirements?.provider || []),
            ]);
            if (!allReqs.length) return 'Requirement yok';
            const enriched = allReqs.filter(r =>
                typeof r === 'object' && r.service && r.status && r.priority && r.effort && r.capability
            );
            return `${enriched.length}/${allReqs.length} tam zenginlestirilmis`;
        },
    },
    {
        key: 'tech',
        label: 'Tech',
        tip: 'tech.note + backend + apis mevcut',
        check: (s) => {
            if (!s.tech) return 'fail';
            const has = s.tech.note && (s.tech.backend?.length > 0 || s.tech.apis?.length > 0);
            if (has && s.tech.backend?.length > 0 && s.tech.apis?.length > 0) return 'pass';
            return has ? 'warn' : 'fail';
        },
        detail: (s) => {
            if (!s.tech) return 'tech objesi yok';
            const missing = [];
            if (!s.tech.note) missing.push('note');
            if (!s.tech.backend?.length) missing.push('backend');
            if (!s.tech.apis?.length) missing.push('apis');
            return missing.length ? `Eksik: ${missing.join(', ')}` : '';
        },
    },
    {
        key: 'impact',
        label: 'Impact/ROI',
        tip: 'impact.title + formula + inputs + subscriptionCost',
        check: (s) => {
            if (!s.impact) return 'fail';
            const { title, formula, inputs, subscriptionCost } = s.impact;
            if (!title || !formula || !inputs?.length) return 'fail';
            return typeof subscriptionCost === 'number' ? 'pass' : 'warn';
        },
        detail: (s) => {
            if (!s.impact) return 'impact objesi yok';
            const missing = [];
            if (!s.impact.title) missing.push('title');
            if (!s.impact.formula) missing.push('formula');
            if (!s.impact.inputs?.length) missing.push('inputs');
            if (typeof s.impact.subscriptionCost !== 'number') missing.push('subscriptionCost');
            return missing.length ? `Eksik: ${missing.join(', ')}` : '';
        },
    },
    {
        key: 'formulaEval',
        label: 'Formula Test',
        tip: 'Formula default degerlerle hatasiz calisir',
        check: (s) => {
            if (!s.impact?.formula || !s.impact?.inputs?.length) return 'fail';
            try {
                let expr = s.impact.formula;
                s.impact.inputs.forEach(input => {
                    const val = input.constant ?? input.default;
                    expr = expr.replace(new RegExp(`\\b${input.key}\\b`, 'g'), String(val));
                });
                const result = eval(expr);
                if (typeof result !== 'number' || isNaN(result)) return 'fail';
                return result > 0 ? 'pass' : 'warn';
            } catch {
                return 'fail';
            }
        },
        detail: (s) => {
            if (!s.impact?.formula) return 'Formula yok';
            try {
                let expr = s.impact.formula;
                (s.impact.inputs || []).forEach(input => {
                    const val = input.constant ?? input.default;
                    expr = expr.replace(new RegExp(`\\b${input.key}\\b`, 'g'), String(val));
                });
                const result = eval(expr);
                return `Sonuc: ${typeof result === 'number' ? Math.round(result).toLocaleString('tr-TR') : 'HATA'} TL`;
            } catch (e) {
                return `Eval hatasi: ${e.message}`;
            }
        },
    },
];

// ── Score calculation ───────────────────────────────────────────────

const calcScore = (scenario) => {
    let pass = 0, warn = 0, fail = 0;
    checks.forEach(c => {
        const r = c.check(scenario);
        if (r === 'pass') pass++;
        else if (r === 'warn') warn++;
        else fail++;
    });
    return { pass, warn, fail, total: checks.length, pct: Math.round((pass / checks.length) * 100) };
};

const gradeFromPct = (pct) => {
    if (pct >= 90) return { label: 'A', color: 'text-emerald-700 bg-emerald-100' };
    if (pct >= 75) return { label: 'B', color: 'text-blue-700 bg-blue-100' };
    if (pct >= 50) return { label: 'C', color: 'text-amber-700 bg-amber-100' };
    return { label: 'D', color: 'text-red-700 bg-red-100' };
};

// ── Status icons ────────────────────────────────────────────────────

const statusMeta = {
    pass: { icon: CheckCircle, color: 'text-emerald-500', label: 'Gecti' },
    warn: { icon: AlertTriangle, color: 'text-amber-500', label: 'Uyari' },
    fail: { icon: XCircle, color: 'text-red-500', label: 'Basarisiz' },
};

const StatusIcon = ({ status, checkLabel, tip, detail }) => {
    const meta = statusMeta[status] || statusMeta.fail;
    const Icon = meta.icon;
    const borderColor = status === 'pass' ? 'border-emerald-200' : status === 'warn' ? 'border-amber-200' : 'border-red-200';
    return (
        <span className="relative group inline-flex items-center justify-center">
            <Icon size={16} className={meta.color} />
            <span className={`pointer-events-none absolute top-full left-1/2 -translate-x-1/2 mt-2 z-50 hidden group-hover:flex flex-col w-60 bg-white text-t-primary text-xs rounded-lg px-3 py-2.5 shadow-xl border ${borderColor} leading-relaxed`}>
                <span className="absolute bottom-full left-1/2 -translate-x-1/2 border-[5px] border-transparent border-b-white drop-shadow-sm" />
                <span className={`font-bold ${meta.color} mb-1`}>[{meta.label}] {checkLabel}</span>
                {tip && <span className="text-t-muted mb-1">{tip}</span>}
                {detail && <span className="text-t-secondary font-medium border-t border-gray-100 pt-1 mt-0.5">{detail}</span>}
            </span>
        </span>
    );
};

// ── Niche label map ─────────────────────────────────────────────────

const nicheLabel = {
    ecommerce: 'E-Ticaret',
    health: 'Saglik',
    dental: 'Dis',
    aesthetic: 'Estetik',
    hotel: 'Otel',
    beauty: 'Guzellik',
    education: 'Egitim',
    mobile: 'Mobil',
    universal: 'Evrensel',
    crossSector: 'Cross-Sector',
};

// ── Main Page ───────────────────────────────────────────────────────

const HealthCheck = () => {
    const [filter, setFilter] = useState('all');       // all | pass | warn | fail | niche:xxx
    const [sortKey, setSortKey] = useState('score');    // score | id | niche
    const [sortDir, setSortDir] = useState('asc');
    const [expandedRow, setExpandedRow] = useState(null);
    const [showAllOk, setShowAllOk] = useState(false);

    // Run all validations
    const results = useMemo(() => {
        return allScenarios.map(s => ({
            scenario: s,
            checks: checks.map(c => ({ key: c.key, status: c.check(s), detail: c.detail(s) })),
            score: calcScore(s),
            tier: getScenarioTier(s),
        }));
    }, []);

    // Summary
    const summary = useMemo(() => {
        const grades = { A: 0, B: 0, C: 0, D: 0 };
        let totalPass = 0, totalWarn = 0, totalFail = 0;
        results.forEach(r => {
            const g = gradeFromPct(r.score.pct);
            grades[g.label]++;
            totalPass += r.score.pass;
            totalWarn += r.score.warn;
            totalFail += r.score.fail;
        });
        return { grades, totalPass, totalWarn, totalFail, totalChecks: results.length * checks.length };
    }, [results]);

    // Niche breakdown
    const nicheBreakdown = useMemo(() => {
        const map = {};
        results.forEach(r => {
            const n = r.scenario.niche || 'unknown';
            if (!map[n]) map[n] = { count: 0, totalPct: 0, fails: 0 };
            map[n].count++;
            map[n].totalPct += r.score.pct;
            if (r.score.fail > 0) map[n].fails++;
        });
        return Object.entries(map).map(([niche, d]) => ({
            niche,
            label: nicheLabel[niche] || niche,
            count: d.count,
            avgPct: Math.round(d.totalPct / d.count),
            fails: d.fails,
        })).sort((a, b) => b.count - a.count);
    }, [results]);

    // Tier breakdown
    const tierBreakdown = useMemo(() => {
        const map = { 1: 0, 2: 0, 3: 0 };
        results.forEach(r => { map[r.tier]++; });
        return map;
    }, [results]);

    // Filter
    const filtered = useMemo(() => {
        return results.filter(r => {
            if (filter === 'all') return true;
            if (filter === 'pass') return r.score.fail === 0 && r.score.warn === 0;
            if (filter === 'warn') return r.score.warn > 0 && r.score.fail === 0;
            if (filter === 'fail') return r.score.fail > 0;
            if (filter.startsWith('niche:')) return r.scenario.niche === filter.slice(6);
            if (filter.startsWith('tier:')) return r.tier === Number(filter.slice(5));
            return true;
        });
    }, [results, filter]);

    // Sort
    const sorted = useMemo(() => {
        const arr = [...filtered];
        arr.sort((a, b) => {
            let cmp = 0;
            if (sortKey === 'score') cmp = a.score.pct - b.score.pct;
            else if (sortKey === 'id') cmp = a.scenario.id.localeCompare(b.scenario.id);
            else if (sortKey === 'niche') cmp = (a.scenario.niche || '').localeCompare(b.scenario.niche || '');
            else if (sortKey === 'tier') cmp = a.tier - b.tier;
            return sortDir === 'asc' ? cmp : -cmp;
        });
        return arr;
    }, [filtered, sortKey, sortDir]);

    const toggleSort = (key) => {
        if (sortKey === key) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
        else { setSortKey(key); setSortDir('asc'); }
    };

    // Detect checks where ALL scenarios pass
    const allOkKeys = useMemo(() => {
        const keys = new Set();
        checks.forEach(c => {
            const allPass = results.every(r => r.checks.find(rc => rc.key === c.key)?.status === 'pass');
            if (allPass) keys.add(c.key);
        });
        return keys;
    }, [results]);

    const visibleChecks = useMemo(() => {
        if (showAllOk) return checks;
        return checks.filter(c => !allOkKeys.has(c.key));
    }, [showAllOk, allOkKeys]);

    // Per-check aggregate fail counts
    const checkFailCounts = useMemo(() => {
        const counts = {};
        checks.forEach(c => { counts[c.key] = { pass: 0, warn: 0, fail: 0 }; });
        results.forEach(r => {
            r.checks.forEach(rc => { counts[rc.key][rc.status]++; });
        });
        return counts;
    }, [results]);

    return (
        <div className="max-w-[1900px] mx-auto p-6 font-sans">
            {/* Header */}
            <div className="mb-8">
                <h1 className="text-4xl font-extrabold text-t-primary mb-2 tracking-tight">Senaryo Saglik Kontrolu</h1>
                <p className="text-lg text-t-secondary">
                    {allScenarios.length} senaryo &times; {checks.length} kontrol = {allScenarios.length * checks.length} dogrulama noktasi
                </p>
            </div>

            {/* Summary Cards */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
                <div className="bg-emerald-50 border border-emerald-200 rounded-xl p-5">
                    <div className="text-sm font-bold text-emerald-600 uppercase tracking-wide">A Grade (90%+)</div>
                    <div className="text-3xl font-extrabold text-emerald-800 mt-1">{summary.grades.A}</div>
                </div>
                <div className="bg-blue-50 border border-blue-200 rounded-xl p-5">
                    <div className="text-sm font-bold text-blue-600 uppercase tracking-wide">B Grade (75%+)</div>
                    <div className="text-3xl font-extrabold text-blue-800 mt-1">{summary.grades.B}</div>
                </div>
                <div className="bg-amber-50 border border-amber-200 rounded-xl p-5">
                    <div className="text-sm font-bold text-amber-600 uppercase tracking-wide">C Grade (50%+)</div>
                    <div className="text-3xl font-extrabold text-amber-800 mt-1">{summary.grades.C}</div>
                </div>
                <div className="bg-red-50 border border-red-200 rounded-xl p-5">
                    <div className="text-sm font-bold text-red-600 uppercase tracking-wide">D Grade (&lt;50%)</div>
                    <div className="text-3xl font-extrabold text-red-800 mt-1">{summary.grades.D}</div>
                </div>
            </div>

            {/* Check-level summary bar */}
            <div className="bg-surface border border-gray-200 rounded-xl p-5 mb-6 overflow-x-auto">
                <div className="text-sm font-bold text-t-muted uppercase tracking-wide mb-3">Kontrol Bazli Ozet</div>
                <div className="flex gap-3 min-w-max">
                    {checks.map(c => {
                        const fc = checkFailCounts[c.key];
                        const worstColor = fc.fail > 0 ? 'border-red-300 bg-red-50' : fc.warn > 0 ? 'border-amber-300 bg-amber-50' : 'border-emerald-300 bg-emerald-50';
                        return (
                            <div key={c.key} className={`border rounded-lg px-3 py-2 text-center min-w-[100px] ${worstColor}`} title={c.tip}>
                                <div className="text-xs font-bold text-t-secondary truncate">{c.label}</div>
                                <div className="flex items-center justify-center gap-1 mt-1 text-xs">
                                    <span className="text-emerald-600 font-bold">{fc.pass}</span>
                                    <span className="text-gray-300">/</span>
                                    <span className="text-amber-600 font-bold">{fc.warn}</span>
                                    <span className="text-gray-300">/</span>
                                    <span className="text-red-600 font-bold">{fc.fail}</span>
                                </div>
                            </div>
                        );
                    })}
                </div>
            </div>

            {/* Tier breakdown */}
            <div className="bg-surface border border-gray-200 rounded-xl p-5 mb-6">
                <div className="text-sm font-bold text-t-muted uppercase tracking-wide mb-3">Tier Dagilimi</div>
                <div className="flex gap-3">
                    {[1, 2, 3].map(t => {
                        const tm = TIER_META[t];
                        const isActive = filter === `tier:${t}`;
                        return (
                            <button
                                key={t}
                                onClick={() => setFilter(isActive ? 'all' : `tier:${t}`)}
                                className={`border rounded-lg px-5 py-3 text-left min-w-[180px] transition-all ${
                                    isActive ? 'ring-2 ring-brand-400 border-brand-400' : 'border-gray-200 hover:border-gray-400'
                                }`}
                            >
                                <div className="flex items-center gap-2">
                                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-extrabold border ${tm.color}`}>{tm.short}</span>
                                    <span className="text-sm font-bold text-t-primary">{tm.label}</span>
                                </div>
                                <div className="text-xs text-t-muted mt-1">{tm.desc}</div>
                                <div className="text-lg font-extrabold text-t-primary mt-1">{tierBreakdown[t]}</div>
                            </button>
                        );
                    })}
                </div>
            </div>

            {/* Niche breakdown */}
            <div className="bg-surface border border-gray-200 rounded-xl p-5 mb-6 overflow-x-auto">
                <div className="text-sm font-bold text-t-muted uppercase tracking-wide mb-3">Sektor Bazli</div>
                <div className="flex gap-3 min-w-max">
                    {nicheBreakdown.map(nb => (
                        <button
                            key={nb.niche}
                            onClick={() => setFilter(filter === `niche:${nb.niche}` ? 'all' : `niche:${nb.niche}`)}
                            className={`border rounded-lg px-4 py-2 text-left min-w-[130px] transition-all ${
                                filter === `niche:${nb.niche}` ? 'ring-2 ring-brand-400 border-brand-400' : 'border-gray-200 hover:border-gray-400'
                            }`}
                        >
                            <div className="text-sm font-bold text-t-primary">{nb.label}</div>
                            <div className="text-xs text-t-muted mt-1">
                                {nb.count} senaryo &middot; ort %{nb.avgPct}
                            </div>
                            {nb.fails > 0 && (
                                <div className="text-xs text-red-600 font-bold mt-0.5">{nb.fails} sorunlu</div>
                            )}
                        </button>
                    ))}
                </div>
            </div>

            {/* Filter tabs */}
            <div className="flex items-center gap-2 mb-4">
                <Filter size={16} className="text-t-muted" />
                {[
                    { key: 'all', label: `Tumu (${results.length})` },
                    { key: 'pass', label: `Temiz (${results.filter(r => r.score.fail === 0 && r.score.warn === 0).length})` },
                    { key: 'warn', label: `Uyari (${results.filter(r => r.score.warn > 0 && r.score.fail === 0).length})` },
                    { key: 'fail', label: `Hata (${results.filter(r => r.score.fail > 0).length})` },
                ].map(f => (
                    <button
                        key={f.key}
                        onClick={() => setFilter(f.key)}
                        className={`px-4 py-1.5 rounded-full text-sm font-bold transition-all ${
                            filter === f.key
                                ? 'bg-brand-600 text-white shadow-sm'
                                : 'bg-gray-100 text-t-secondary hover:bg-gray-200'
                        }`}
                    >
                        {f.label}
                    </button>
                ))}
                {filter.startsWith('niche:') && (
                    <button
                        onClick={() => setFilter('all')}
                        className="px-4 py-1.5 rounded-full text-sm font-bold bg-brand-600 text-white shadow-sm"
                    >
                        {nicheLabel[filter.slice(6)] || filter.slice(6)} &times;
                    </button>
                )}
                {filter.startsWith('tier:') && (
                    <button
                        onClick={() => setFilter('all')}
                        className="px-4 py-1.5 rounded-full text-sm font-bold bg-brand-600 text-white shadow-sm"
                    >
                        {TIER_META[Number(filter.slice(5))]?.label} &times;
                    </button>
                )}
                {allOkKeys.size > 0 && (
                    <button
                        onClick={() => setShowAllOk(v => !v)}
                        className={`ml-2 px-3 py-1.5 rounded-full text-sm font-bold transition-all inline-flex items-center gap-1.5 ${
                            showAllOk
                                ? 'bg-emerald-100 text-emerald-700 hover:bg-emerald-200'
                                : 'bg-gray-100 text-t-secondary hover:bg-gray-200'
                        }`}
                        title={showAllOk ? 'Tumu OK kolonlarini gizle' : `${allOkKeys.size} kolon tum senaryolarda OK — gostermek icin tikla`}
                    >
                        {showAllOk ? <EyeOff size={14} /> : <Eye size={14} />}
                        {showAllOk ? 'OK Gizle' : `${allOkKeys.size} OK Gizli`}
                    </button>
                )}
                <span className="ml-auto text-sm text-t-muted">{sorted.length} sonuc gosteriliyor</span>
            </div>

            {/* Main table */}
            <div className="bg-surface border border-gray-200 rounded-xl overflow-hidden shadow-sm">
                <div className="overflow-x-auto">
                    <table className="w-full text-sm">
                        <thead>
                            <tr className="bg-gray-50 border-b border-gray-200">
                                <th className="text-left px-4 py-3 font-bold text-t-muted uppercase tracking-wider text-xs cursor-pointer select-none" onClick={() => toggleSort('score')}>
                                    <span className="flex items-center gap-1">
                                        Skor <ArrowUpDown size={12} />
                                    </span>
                                </th>
                                <th className="text-left px-4 py-3 font-bold text-t-muted uppercase tracking-wider text-xs cursor-pointer select-none" onClick={() => toggleSort('id')}>
                                    <span className="flex items-center gap-1">
                                        ID <ArrowUpDown size={12} />
                                    </span>
                                </th>
                                <th className="text-left px-4 py-3 font-bold text-t-muted uppercase tracking-wider text-xs">Baslik</th>
                                <th className="text-left px-4 py-3 font-bold text-t-muted uppercase tracking-wider text-xs cursor-pointer select-none" onClick={() => toggleSort('niche')}>
                                    <span className="flex items-center gap-1">
                                        Sektor <ArrowUpDown size={12} />
                                    </span>
                                </th>
                                <th className="text-center px-3 py-3 font-bold text-t-muted uppercase tracking-wider text-xs cursor-pointer select-none" onClick={() => toggleSort('tier')}>
                                    <span className="flex items-center justify-center gap-1">
                                        Tier <ArrowUpDown size={12} />
                                    </span>
                                </th>
                                {visibleChecks.map(c => (
                                    <th key={c.key} className="text-center px-2 py-3 font-bold text-t-muted uppercase tracking-wider text-xs" title={c.tip}>
                                        {c.label}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {sorted.map((row) => {
                                const grade = gradeFromPct(row.score.pct);
                                const isExpanded = expandedRow === row.scenario.id;
                                return (
                                    <React.Fragment key={row.scenario.id}>
                                        <tr
                                            className={`border-b border-gray-100 cursor-pointer transition-colors ${
                                                isExpanded ? 'bg-brand-50/50' : 'hover:bg-gray-50/80'
                                            }`}
                                            onClick={() => setExpandedRow(isExpanded ? null : row.scenario.id)}
                                        >
                                            <td className="px-4 py-2.5">
                                                <span className={`inline-flex items-center justify-center w-9 h-7 rounded-md text-xs font-extrabold ${grade.color}`}>
                                                    {grade.label} {row.score.pct}
                                                </span>
                                            </td>
                                            <td className="px-4 py-2.5">
                                                <Link
                                                    to={`/scenarios/${row.scenario.id}`}
                                                    className="font-mono text-brand-600 font-bold hover:underline"
                                                    onClick={e => e.stopPropagation()}
                                                >
                                                    {row.scenario.id.toUpperCase()}
                                                </Link>
                                            </td>
                                            <td className="px-4 py-2.5 font-medium text-t-primary max-w-[200px] truncate">
                                                {row.scenario.title}
                                            </td>
                                            <td className="px-4 py-2.5 text-t-secondary text-xs">
                                                {nicheLabel[row.scenario.niche] || row.scenario.niche}
                                            </td>
                                            <td className="text-center px-3 py-2.5">
                                                {(() => {
                                                    const tm = TIER_META[row.tier];
                                                    return (
                                                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-extrabold border ${tm.color}`} title={tm.desc}>
                                                            {tm.short}
                                                        </span>
                                                    );
                                                })()}
                                            </td>
                                            {row.checks.filter(rc => showAllOk || !allOkKeys.has(rc.key)).map(rc => {
                                                const checkDef = checks.find(c => c.key === rc.key);
                                                return (
                                                    <td key={rc.key} className="text-center px-2 py-2.5">
                                                        <StatusIcon
                                                            status={rc.status}
                                                            checkLabel={checkDef?.label}
                                                            tip={checkDef?.tip}
                                                            detail={rc.detail}
                                                        />
                                                    </td>
                                                );
                                            })}
                                        </tr>
                                        {isExpanded && (
                                            <tr className="bg-gray-50/50">
                                                <td colSpan={5 + visibleChecks.length} className="px-6 py-4">
                                                    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
                                                        {row.checks.filter(rc => rc.detail).map(rc => {
                                                            const checkDef = checks.find(c => c.key === rc.key);
                                                            return (
                                                                <div
                                                                    key={rc.key}
                                                                    className={`rounded-lg px-3 py-2 text-xs border ${
                                                                        rc.status === 'fail' ? 'bg-red-50 border-red-200 text-red-800' :
                                                                        rc.status === 'warn' ? 'bg-amber-50 border-amber-200 text-amber-800' :
                                                                        'bg-emerald-50 border-emerald-200 text-emerald-800'
                                                                    }`}
                                                                >
                                                                    <span className="font-bold">{checkDef?.label}:</span> {rc.detail}
                                                                </div>
                                                            );
                                                        })}
                                                    </div>
                                                </td>
                                            </tr>
                                        )}
                                    </React.Fragment>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
};

export default HealthCheck;
