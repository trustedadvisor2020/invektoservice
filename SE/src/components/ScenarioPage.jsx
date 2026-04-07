import React, { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { CheckCircle, Smartphone, Info, Zap, BookOpen, ArrowUpRight, AlertTriangle, XCircle, Lightbulb, ShieldCheck, Copy, Check, Wand2 } from 'lucide-react';
import * as LucideIcons from 'lucide-react';
import Badge from './Badge';
import Step from './Step';
import FlatCard from './FlatCard';
import Callout from './Callout';
import ChatPreview from './ChatPreview';
import InteractiveROI from './InteractiveROI';
import FlowDiagram from './FlowDiagram';
import { analyzeScenario, deriveAutomationFlow, generateSuggestions } from '../lib/scenarioAnalysis';
import { getScenarioTier, TIER_META, getAcceptanceCriteria, getExitCriteria, getBusinessMeta } from '../lib/scenarioMeta';
import TestTab from './TestTab';

// Resolve icon name string to Lucide component
const getIcon = (name) => {
    if (!name) return null;
    if (typeof name === 'function') return name;
    return LucideIcons[name] || null;
};

// Requirement enrichment labels & colors
const statusMap = { ready: { label: 'Hazir', color: 'green' }, setup: { label: 'Kurulum', color: 'amber' }, optional: { label: 'Opsiyonel', color: 'gray' } };
const priorityMap = { required: { label: 'Zorunlu', color: 'rose' }, recommended: { label: 'Onerilen', color: 'blue' }, optional: { label: 'Opsiyonel', color: 'gray' } };
const effortMap = { easy: { label: 'Kolay', color: 'green' }, medium: { label: 'Orta', color: 'amber' }, technical: { label: 'Teknik', color: 'purple' } };

const gradeColors = {
    A: 'bg-emerald-100 text-emerald-800 border-emerald-300',
    B: 'bg-blue-100 text-blue-800 border-blue-300',
    C: 'bg-amber-100 text-amber-800 border-amber-300',
    D: 'bg-red-100 text-red-800 border-red-300',
};

const priorityColors = {
    critical: 'bg-red-50 border-red-200 text-red-800',
    high: 'bg-amber-50 border-amber-200 text-amber-800',
    medium: 'bg-blue-50 border-blue-200 text-blue-800',
    low: 'bg-gray-50 border-gray-200 text-gray-700',
};

const priorityLabels = { critical: 'Kritik', high: 'Yuksek', medium: 'Orta', low: 'Dusuk' };
const categoryLabels = { icerik: 'Icerik', teknik: 'Teknik', otomasyon: 'Otomasyon', roi: 'ROI' };

const FindingIcon = ({ type }) => {
    if (type === 'pass') return <CheckCircle size={14} className="text-emerald-500 flex-shrink-0" />;
    if (type === 'warn') return <AlertTriangle size={14} className="text-amber-500 flex-shrink-0" />;
    return <XCircle size={14} className="text-red-500 flex-shrink-0" />;
};

const RequirementItem = ({ req, bulletColor }) => {
    const isObj = typeof req === 'object' && req !== null;
    const text = isObj ? req.text : req;
    const source = isObj && req.service ? req : null;
    const status = isObj && req.status ? statusMap[req.status] : null;
    const priority = isObj && req.priority ? priorityMap[req.priority] : null;
    const effort = isObj && req.effort ? effortMap[req.effort] : null;
    const sourceColor = bulletColor === 'bg-brand-400' ? 'text-brand-500' : 'text-emerald-600';

    return (
        <li className="text-base text-t-secondary flex items-start gap-3">
            <div className={`w-2 h-2 rounded-full ${bulletColor} mt-2 flex-shrink-0`}></div>
            <div className="space-y-1.5">
                <span className="block font-medium text-t-primary">{text}</span>
                {(status || priority || effort || (isObj && req.capability)) && (
                    <div className="flex flex-wrap gap-1.5">
                        {status && <Badge color={status.color}>{status.label}</Badge>}
                        {priority && <Badge color={priority.color}>{priority.label}</Badge>}
                        {effort && <Badge color={effort.color}>{effort.label}</Badge>}
                        {isObj && req.capability && (
                            <Link to={`/capabilities#${req.capability}`} className="hover:opacity-80 transition-opacity">
                                <Badge color="purple">{req.capability}</Badge>
                            </Link>
                        )}
                    </div>
                )}
                {source && (
                    <span className={`flex items-center gap-1 text-sm ${sourceColor}`}>
                        <ArrowUpRight size={14} />
                        {source.service} &middot; {source.page}
                    </span>
                )}
                {isObj && req.hint && (
                    <span className="block text-sm text-t-muted italic">{req.hint}</span>
                )}
            </div>
        </li>
    );
};

const AiPromptTab = ({ flows, scenarioId }) => {
    const [activePrompt, setActivePrompt] = useState(0);
    const [copiedIdx, setCopiedIdx] = useState(null);

    const handleCopy = (text, idx) => {
        navigator.clipboard.writeText(text).then(() => {
            setCopiedIdx(idx);
            setTimeout(() => setCopiedIdx(null), 2000);
        });
    };

    const promptFlows = flows.filter(f => f.aiPrompt);

    return (
        <div className="animate-fade-in space-y-6">
            <div className="flex items-center gap-3 mb-2">
                <Wand2 size={24} className="text-brand-600" />
                <h2 className="text-2xl font-extrabold text-t-primary">Flow Designer Promptlari</h2>
                <span className="text-sm text-t-muted">Her akis icin AI flow designer'a verilecek talimat</span>
            </div>

            {/* Flow selector */}
            <div className="flex flex-wrap gap-3">
                {promptFlows.map((flow, idx) => (
                    <button
                        key={flow.id || idx}
                        onClick={() => setActivePrompt(idx)}
                        className={`px-5 py-3 rounded-xl text-base font-bold transition-all shadow-sm border ${activePrompt === idx
                            ? 'bg-brand-600 text-white border-brand-600 shadow-lg ring-4 ring-brand-100'
                            : 'bg-surface text-t-secondary border-brand-100 hover:bg-brand-50 hover:border-brand-300'}`}
                    >
                        {idx + 1}. {flow.title}
                    </button>
                ))}
            </div>

            {/* Prompt display */}
            {promptFlows[activePrompt] && (
                <div className="relative">
                    <div className="absolute top-4 right-4 z-10">
                        <button
                            onClick={() => handleCopy(promptFlows[activePrompt].aiPrompt, activePrompt)}
                            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-brand-600 text-white text-sm font-bold hover:bg-brand-700 transition-colors shadow-lg"
                        >
                            {copiedIdx === activePrompt ? (
                                <><Check size={16} /> Kopyalandi</>
                            ) : (
                                <><Copy size={16} /> Kopyala</>
                            )}
                        </button>
                    </div>
                    <div className="bg-gray-900 rounded-2xl border border-gray-700 overflow-hidden">
                        <div className="px-6 py-3 bg-gray-800 border-b border-gray-700 flex items-center gap-3">
                            <Wand2 size={16} className="text-brand-400" />
                            <span className="text-sm font-bold text-gray-300">
                                {scenarioId.toUpperCase()} / {promptFlows[activePrompt].title}
                            </span>
                        </div>
                        <pre className="p-6 pr-20 text-sm text-gray-200 font-mono whitespace-pre-wrap leading-relaxed overflow-x-auto max-h-[70vh] overflow-y-auto">
                            {promptFlows[activePrompt].aiPrompt}
                        </pre>
                    </div>
                </div>
            )}
        </div>
    );
};

const ScenarioPage = ({ data }) => {
    const [activeTab, setActiveTab] = useState('overview');
    const [activeFlow, setActiveFlow] = useState(0);
    const [showAnalysis, setShowAnalysis] = useState(true);

    // Run analysis at render time
    const analysis = useMemo(() => data ? analyzeScenario(data) : null, [data]);
    const suggestions = useMemo(() => data && analysis ? generateSuggestions(data, analysis) : [], [data, analysis]);
    const tier = useMemo(() => data ? getScenarioTier(data) : 2, [data]);
    const tierMeta = TIER_META[tier];
    const acceptance = useMemo(() => data ? getAcceptanceCriteria(data) : [], [data]);
    const exitCriteria = useMemo(() => data ? getExitCriteria(data) : null, [data]);
    const bizMeta = useMemo(() => data ? getBusinessMeta(data) : null, [data]);
    const flowDiagrams = useMemo(() => {
        if (!data?.flows) return [];
        return data.flows.map(flow => deriveAutomationFlow(flow, data.overview));
    }, [data]);

    if (!data) return <div className="p-10 text-t-muted">Senaryo verisi bulunamadi.</div>;

    const { id, title, subtitle, phase, category, description, overview, flows = [], tech, impact } = data;

    const hasAiPrompts = flows.some(f => f.aiPrompt);

    const tabs = [
        { key: 'overview', label: 'GENEL BAKIS' },
        { key: 'scenarios', label: `SENARYO AKISLARI (${flows.length})` },
        { key: 'tech', label: 'TEKNIK DETAYLAR' },
        { key: 'test', label: 'TEST' },
        ...(hasAiPrompts ? [{ key: 'aiPrompt', label: 'AI PROMPT' }] : []),
    ];

    return (
        <div className="max-w-[1700px] mx-auto p-10 font-sans bg-gray-50/50 min-h-screen">
            {/* Analysis Banner */}
            {analysis && showAnalysis && (
                <div className={`mb-6 rounded-xl border-2 p-4 flex items-center gap-4 ${gradeColors[analysis.grade]}`}>
                    <div className="flex items-center gap-3 flex-shrink-0">
                        <ShieldCheck size={24} />
                        <span className="text-3xl font-extrabold">{analysis.grade}</span>
                        <span className="text-sm font-bold opacity-75">%{analysis.score}</span>
                    </div>
                    <div className="h-8 w-px bg-current opacity-20 flex-shrink-0"></div>
                    <div className="flex flex-wrap gap-x-4 gap-y-1 flex-1 min-w-0">
                        {analysis.findings.map(f => (
                            <span key={f.key} className="flex items-center gap-1 text-xs font-medium whitespace-nowrap">
                                <FindingIcon type={f.type} />
                                {f.label}
                            </span>
                        ))}
                    </div>
                    {suggestions.length > 0 && (
                        <span className="flex items-center gap-1 text-xs font-bold flex-shrink-0 bg-white/50 rounded-full px-3 py-1">
                            <Lightbulb size={14} />
                            {suggestions.length} oneri
                        </span>
                    )}
                    <button onClick={() => setShowAnalysis(false)} className="text-current opacity-40 hover:opacity-100 transition-opacity flex-shrink-0">
                        <LucideIcons.X size={18} />
                    </button>
                </div>
            )}

            {/* Header */}
            <div className="mb-12">
                <div className="flex items-center gap-3 mb-5">
                    <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-extrabold border ${tierMeta.color}`} title={tierMeta.desc}>
                        {tierMeta.label}
                    </span>
                    {phase && <Badge color="blue">{phase.toUpperCase()}</Badge>}
                    {category && <Badge color="green">{category}</Badge>}
                    {bizMeta && (
                        <span className={`inline-flex items-center px-3 py-1 rounded-full text-xs font-bold ${bizMeta.statusMeta.color}`}>
                            {bizMeta.statusMeta.label}
                        </span>
                    )}
                </div>
                <h1 className="text-5xl font-extrabold text-t-primary mb-6 tracking-tight">
                    {id.toUpperCase()}: {title}
                </h1>
                {subtitle && (
                    <p className="text-sm text-t-muted font-mono mb-2">{subtitle}</p>
                )}
                <p className="text-2xl text-t-secondary max-w-5xl font-light leading-relaxed">
                    {description}
                </p>
            </div>

            {/* Tabs */}
            <div className="flex gap-2 mb-10 border-b border-gray-200">
                {tabs.map(tab => (
                    <button
                        key={tab.key}
                        className={`px-8 py-4 font-bold text-base transition-colors border-b-4 ${activeTab === tab.key
                            ? 'border-brand-600 text-brand-700'
                            : 'border-transparent text-t-muted hover:text-t-primary'}`}
                        onClick={() => setActiveTab(tab.key)}
                    >
                        {tab.label}
                    </button>
                ))}
            </div>

            {/* Tab: Overview */}
            {activeTab === 'overview' && overview && (
                <div className="space-y-10 animate-fade-in">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-10">
                        {/* Audience Card */}
                        {overview.audience && (
                            <FlatCard title="Hedef Kitle & Sektor" icon={LucideIcons.User}>
                                <ul className="space-y-4">
                                    {overview.audience.map((item, i) => (
                                        <li key={i} className="flex items-start gap-4 text-t-secondary text-lg">
                                            <CheckCircle size={24} className="text-emerald-500 mt-0.5" />
                                            <span><strong>{item.label}:</strong> {item.value}</span>
                                        </li>
                                    ))}
                                </ul>
                            </FlatCard>
                        )}

                        {/* Services Card */}
                        {overview.services && (
                            <FlatCard title="Entegre Servisler" icon={LucideIcons.Database}>
                                <div className="flex flex-wrap gap-3">
                                    {overview.services.map((svc, i) => (
                                        <Badge key={i} color={svc.color || 'blue'}>{svc.name}</Badge>
                                    ))}
                                </div>
                                {overview.servicesNote && (
                                    <div className="mt-6 text-base text-t-muted">{overview.servicesNote}</div>
                                )}
                            </FlatCard>
                        )}
                    </div>

                    {/* Steps */}
                    {overview.steps && (
                        <FlatCard title="Sistem Calisma Mantigi" icon={Zap} className="border-l-4 border-brand-500">
                            <div className="mt-4 space-y-4">
                                {overview.steps.map((step, i) => (
                                    <Step key={i} number={i + 1} title={step.title} goal={step.goal}>
                                        {step.badges && (
                                            <div className="flex gap-2 mb-2">
                                                {step.badges.map((b, j) => (
                                                    <Badge key={j} color={b.color || 'purple'}>{b.text}</Badge>
                                                ))}
                                            </div>
                                        )}
                                        {step.content}
                                    </Step>
                                ))}
                            </div>
                        </FlatCard>
                    )}
                </div>
            )}

            {/* Tab: Scenarios */}
            {activeTab === 'scenarios' && flows.length > 0 && (
                <div className="animate-fade-in">
                    {/* Flow Selector */}
                    <div className="flex flex-wrap gap-4 mb-6">
                        {flows.map((flow, idx) => (
                            <button
                                key={flow.id || idx}
                                onClick={() => setActiveFlow(idx)}
                                className={`px-5 py-3 rounded-xl text-base font-bold transition-all shadow-sm border ${activeFlow === idx
                                    ? 'bg-brand-600 text-white border-brand-600 shadow-lg ring-4 ring-brand-100'
                                    : 'bg-surface text-t-secondary border-brand-100 hover:bg-brand-50 hover:border-brand-300'}`}
                            >
                                {idx + 1}. {flow.title}
                            </button>
                        ))}
                    </div>

                    {/* Flow Automation Diagram */}
                    {flowDiagrams[activeFlow] && (
                        <FlowDiagram
                            nodes={flowDiagrams[activeFlow].nodes}
                            edges={flowDiagrams[activeFlow].edges}
                        />
                    )}

                    {/* Scenario Panel */}
                    <div className="grid grid-cols-1 lg:grid-cols-3 gap-10">
                        {/* Left: Details & Requirements */}
                        <div className="lg:col-span-1 space-y-8">
                            <FlatCard title="Senaryo Detayi" icon={Smartphone} className="bg-brand-50/50 border-brand-100">
                                <h3 className="text-2xl font-bold text-t-primary mb-3">{flows[activeFlow].title}</h3>
                                <p className="text-t-secondary text-base leading-relaxed mb-6">{flows[activeFlow].description}</p>
                                {flows[activeFlow].tags && (
                                    <div className="flex gap-3 flex-wrap">
                                        {flows[activeFlow].tags.map((tag, i) => (
                                            <Badge key={i} color={tag.color || 'blue'}>{tag.text}</Badge>
                                        ))}
                                    </div>
                                )}
                            </FlatCard>

                            {flows[activeFlow].requirements && (
                                <FlatCard title="Gereksinimler" icon={Info}>
                                    <div className="space-y-6">
                                        {flows[activeFlow].requirements.client && (
                                            <div>
                                                <span className="text-sm font-bold text-t-muted uppercase tracking-wider block mb-3">Satici Tarafi</span>
                                                <ul className="space-y-4">
                                                    {flows[activeFlow].requirements.client.map((req, i) => (
                                                        <RequirementItem key={i} req={req} bulletColor="bg-brand-400" />
                                                    ))}
                                                </ul>
                                            </div>
                                        )}
                                        {flows[activeFlow].requirements.provider && (
                                            <div className="pt-6 border-t border-brand-50">
                                                <span className="text-sm font-bold text-t-muted uppercase tracking-wider block mb-3">Sistem Tarafi</span>
                                                <ul className="space-y-4">
                                                    {flows[activeFlow].requirements.provider.map((req, i) => (
                                                        <RequirementItem key={i} req={req} bulletColor="bg-emerald-400" />
                                                    ))}
                                                </ul>
                                            </div>
                                        )}
                                    </div>
                                </FlatCard>
                            )}
                        </div>

                        {/* Right: Chat Preview */}
                        <div className="lg:col-span-2">
                            <ChatPreview
                                steps={flows[activeFlow].steps || []}
                                assistantName={flows[activeFlow].assistantName || 'Destek Asistani'}
                                assistantIcon={getIcon(flows[activeFlow].assistantIcon) || Smartphone}
                            />
                        </div>
                    </div>
                </div>
            )}

            {/* Tab: Tech */}
            {activeTab === 'tech' && tech && (
                <div className="animate-fade-in space-y-10">
                    {tech.note && (
                        <Callout type="info" title="Teknik Not">{tech.note}</Callout>
                    )}

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-10">
                        {tech.backend && (
                            <FlatCard title="Backend Servisleri" icon={LucideIcons.Server}>
                                <div className="space-y-6">
                                    {tech.backend.map((item, i) => (
                                        <Step key={i} number={String.fromCharCode(65 + i)} title={item.title} goal={item.goal}>
                                            {item.content}
                                        </Step>
                                    ))}
                                </div>
                            </FlatCard>
                        )}

                        {tech.apis && (
                            <FlatCard title="API Entegrasyonlari" icon={LucideIcons.Database}>
                                <div className="space-y-6">
                                    {tech.apis.map((item, i) => (
                                        <Step key={i} number={String.fromCharCode(65 + i)} title={item.title} goal={item.goal}>
                                            {item.content}
                                        </Step>
                                    ))}
                                </div>
                            </FlatCard>
                        )}
                    </div>

                    {tech.config && (
                        <FlatCard title="Ornek Konfigurasyon" icon={BookOpen}>
                            <pre className="bg-gray-50 border border-gray-200 rounded-lg p-6 text-sm font-mono overflow-x-auto text-t-primary">
                                {tech.config}
                            </pre>
                        </FlatCard>
                    )}
                </div>
            )}

            {/* Tab: Test */}
            {activeTab === 'test' && (
                <TestTab data={data} />
            )}

            {/* Tab: AI Prompt */}
            {activeTab === 'aiPrompt' && hasAiPrompts && (
                <AiPromptTab flows={flows} scenarioId={id} />
            )}

            {/* Acceptance Criteria & Business Metadata */}
            <div className="mt-12 grid grid-cols-1 lg:grid-cols-2 gap-8">
                {/* Left: Tamam Tanımı + Çıkış Kuralı */}
                <FlatCard title="Kabul Kriterleri (Tamam Tanimi)" icon={ShieldCheck} className="border-l-4 border-brand-500">
                    <table className="w-full text-sm mt-3">
                        <thead>
                            <tr className="border-b border-gray-200">
                                <th className="text-left py-2 px-2 text-xs font-bold text-t-muted uppercase">Kriter</th>
                                <th className="text-left py-2 px-2 text-xs font-bold text-t-muted uppercase">Aciklama</th>
                                <th className="text-center py-2 px-2 text-xs font-bold text-t-muted uppercase">Durum</th>
                            </tr>
                        </thead>
                        <tbody>
                            {acceptance.map(ac => (
                                <tr key={ac.key} className="border-b border-gray-100">
                                    <td className="py-2 px-2 font-medium text-t-primary">{ac.label}</td>
                                    <td className="py-2 px-2 text-t-secondary text-xs">{ac.desc}</td>
                                    <td className="py-2 px-2 text-center">
                                        {ac.met === true && <CheckCircle size={16} className="text-emerald-500 inline" />}
                                        {ac.met === false && <XCircle size={16} className="text-red-500 inline" />}
                                        {ac.met === null && <AlertTriangle size={16} className="text-amber-400 inline" />}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                    {exitCriteria && (
                        <div className="mt-4 flex items-center gap-2 bg-gray-50 rounded-lg px-4 py-2.5 border border-gray-200">
                            <span className="text-xs font-bold text-t-muted uppercase">Cikis Kurali:</span>
                            <code className="text-sm font-mono font-bold text-t-primary">{exitCriteria.label}</code>
                        </div>
                    )}
                </FlatCard>

                {/* Right: Business Metadata */}
                {bizMeta && (
                    <FlatCard title="Is Degeri & Operasyon" icon={LucideIcons.BarChart3} className="border-l-4 border-emerald-500">
                        <div className="space-y-3 mt-3">
                            {[
                                { label: 'Tier', value: tierMeta.label, sub: tierMeta.desc, badgeClass: tierMeta.color },
                                { label: 'Is Degeri', value: bizMeta.businessValue },
                                { label: 'Kullanim Sikligi', value: bizMeta.frequency },
                                { label: 'Bagimlilik', value: `${bizMeta.dependencyCount} servis/API` },
                                { label: 'Risk', value: bizMeta.risk, badgeClass: bizMeta.riskLevel === 'high' ? 'text-red-700 bg-red-100' : bizMeta.riskLevel === 'medium' ? 'text-amber-700 bg-amber-100' : 'text-emerald-700 bg-emerald-100' },
                                { label: 'Sorumlu', value: bizMeta.owner },
                                { label: 'Durum', value: bizMeta.statusMeta.label, badgeClass: bizMeta.statusMeta.color },
                            ].map(row => (
                                <div key={row.label} className="flex items-center justify-between py-2 border-b border-gray-100 last:border-0">
                                    <span className="text-sm font-bold text-t-muted">{row.label}</span>
                                    <div className="flex items-center gap-2">
                                        {row.badgeClass ? (
                                            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold ${row.badgeClass}`}>{row.value}</span>
                                        ) : (
                                            <span className="text-sm font-medium text-t-primary">{row.value}</span>
                                        )}
                                        {row.sub && <span className="text-xs text-t-muted hidden lg:inline">({row.sub})</span>}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </FlatCard>
                )}
            </div>

            {/* Suggestions Section */}
            {suggestions.length > 0 && (
                <div className="mt-12">
                    <FlatCard title="Gelistirme Onerileri" icon={Lightbulb} className="border-l-4 border-amber-400">
                        <div className="space-y-3 mt-2">
                            {suggestions.map((s, i) => (
                                <div key={i} className={`flex items-start gap-3 rounded-lg border px-4 py-3 ${priorityColors[s.priority]}`}>
                                    <span className="text-[10px] font-bold uppercase tracking-wider bg-white/60 rounded px-2 py-0.5 flex-shrink-0 mt-0.5">
                                        {priorityLabels[s.priority]}
                                    </span>
                                    <span className="text-sm font-medium flex-1">{s.text}</span>
                                    <span className="text-[10px] font-bold uppercase tracking-wider opacity-50 flex-shrink-0 mt-0.5">
                                        {categoryLabels[s.category] || s.category}
                                    </span>
                                </div>
                            ))}
                        </div>
                    </FlatCard>
                </div>
            )}

            {/* Detailed Findings (collapsible) */}
            {analysis && (
                <details className="mt-6">
                    <summary className="text-sm font-bold text-t-muted cursor-pointer hover:text-t-primary transition-colors select-none">
                        Detayli Saglik Raporu ({analysis.findings.length} kontrol)
                    </summary>
                    <div className="mt-3 grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
                        {analysis.findings.map(f => (
                            <div
                                key={f.key}
                                className={`rounded-lg border px-3 py-2 text-xs ${
                                    f.type === 'fail' ? 'bg-red-50 border-red-200 text-red-800' :
                                    f.type === 'warn' ? 'bg-amber-50 border-amber-200 text-amber-800' :
                                    'bg-emerald-50 border-emerald-200 text-emerald-800'
                                }`}
                            >
                                <div className="flex items-center gap-1 mb-0.5">
                                    <FindingIcon type={f.type} />
                                    <span className="font-bold">{f.label}</span>
                                </div>
                                <span className="text-[11px] opacity-80">{f.detail}</span>
                            </div>
                        ))}
                    </div>
                </details>
            )}

            {/* Footer: Interactive ROI */}
            {impact && <InteractiveROI impact={impact} />}
        </div>
    );
};

export default ScenarioPage;
