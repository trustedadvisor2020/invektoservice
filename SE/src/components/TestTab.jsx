import React, { useState, useMemo } from 'react';
import { Activity, Server, AlertTriangle, Play, ChevronRight, Gauge, Radio, Shield, CheckCircle, Clock, FileText } from 'lucide-react';
import { getTestSummary, getServiceSummary, CAPABILITY_MAP, calculateReadinessScore } from '../lib/scenarioTestEngine';
import Badge from './Badge';
import FlatCard from './FlatCard';
import TestApiCard from './TestApiCard';
import TestFlowTimeline from './TestFlowTimeline';
import TestGapAnalysis from './TestGapAnalysis';
import TestDataRunner from './TestDataRunner';
import testResultsData from '../data/test-results.json';

const scoreColor = (score) => {
    if (score >= 80) return 'bg-emerald-100 text-emerald-800 border-emerald-300';
    if (score >= 50) return 'bg-amber-100 text-amber-800 border-amber-300';
    return 'bg-red-100 text-red-800 border-red-300';
};

const statusConfig = {
    pass:    { label: 'GECTI',  color: 'bg-emerald-500', icon: CheckCircle },
    warning: { label: 'UYARI', color: 'bg-amber-500',   icon: AlertTriangle },
    error:   { label: 'HATA',  color: 'bg-red-500',     icon: AlertTriangle },
};

const TestTab = ({ data }) => {
    const [activeSection, setActiveSection] = useState('results');
    const [activeFlowIdx, setActiveFlowIdx] = useState(0);

    const summary = useMemo(() => getTestSummary(data), [data]);
    const services = useMemo(() => getServiceSummary(data), [data]);
    const flows = data?.flows || [];
    const activeFlow = flows[activeFlowIdx] || null;

    // Pre-computed test results
    const testResult = testResultsData?.results?.[data?.id] || null;
    const testedAt = testResult?.testedAt ? new Date(testResult.testedAt) : null;

    const sections = [
        { key: 'results',     label: 'Test Sonuclari',  icon: FileText },
        { key: 'timeline',    label: 'Akis Analizi',    icon: Activity },
        { key: 'apimap',      label: 'API Haritasi',    icon: Server },
        { key: 'gaps',        label: 'Gap Analizi',     icon: AlertTriangle },
        { key: 'interactive', label: 'Interaktif Test', icon: Play },
    ];

    return (
        <div className="animate-fade-in space-y-8">
            {/* ── Readiness Banner ────────────────────────────── */}
            <div className={`rounded-xl border-2 p-5 flex flex-wrap items-center gap-6 ${scoreColor(summary.score)}`}>
                <div className="flex items-center gap-3">
                    <Gauge size={28} />
                    <div>
                        <div className="text-4xl font-extrabold">{summary.score}</div>
                        <div className="text-xs font-bold uppercase opacity-70">Hazirlik Skoru</div>
                    </div>
                </div>
                <div className="h-10 w-px bg-current opacity-20 hidden sm:block" />
                <div className="flex flex-wrap gap-3">
                    <StatBadge icon={Activity} label="Akis" value={summary.totalFlows} />
                    <StatBadge icon={ChevronRight} label="Adim" value={summary.totalSteps} />
                    <StatBadge icon={Radio} label="API Call" value={summary.totalApiCalls} />
                    <StatBadge icon={Server} label="Servis" value={summary.serviceCount} />
                    <StatBadge icon={Shield} label="Capability" value={summary.totalCapabilities} />
                    {summary.totalGaps > 0 && (
                        <StatBadge icon={AlertTriangle} label="Eksik" value={summary.totalGaps} alert />
                    )}
                </div>
                {/* Tested stamp */}
                {testResult && (
                    <>
                        <div className="h-10 w-px bg-current opacity-20 hidden sm:block" />
                        <div className="flex items-center gap-2">
                            <div className={`w-2.5 h-2.5 rounded-full ${statusConfig[testResult.status]?.color || 'bg-gray-400'} animate-pulse`} />
                            <span className="text-xs font-bold uppercase">
                                {statusConfig[testResult.status]?.label || 'BILINMIYOR'}
                            </span>
                            {testedAt && (
                                <span className="flex items-center gap-1 text-[10px] opacity-60">
                                    <Clock size={10} />
                                    {testedAt.toLocaleDateString('tr-TR')} {testedAt.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}
                                </span>
                            )}
                        </div>
                    </>
                )}
            </div>

            {/* ── Section Tabs ──────────────────────────────── */}
            <div className="flex gap-2 border-b border-gray-200 overflow-x-auto">
                {sections.map(sec => {
                    const Icon = sec.icon;
                    return (
                        <button
                            key={sec.key}
                            onClick={() => setActiveSection(sec.key)}
                            className={`flex items-center gap-2 px-5 py-3 font-bold text-sm transition-colors border-b-3 whitespace-nowrap ${
                                activeSection === sec.key
                                    ? 'border-brand-600 text-brand-700'
                                    : 'border-transparent text-t-muted hover:text-t-primary'
                            }`}
                        >
                            <Icon size={16} />
                            {sec.label}
                        </button>
                    );
                })}
            </div>

            {/* ── Section: Test Results (Pre-computed) ──────── */}
            {activeSection === 'results' && testResult && (
                <div className="space-y-8">
                    {/* Overview cards */}
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                        <ResultCard label="Toplam Akis" value={testResult.totalFlows} sub="flow" color="blue" />
                        <ResultCard label="Toplam Adim" value={testResult.totalSteps} sub="step" color="purple" />
                        <ResultCard label="API Call" value={testResult.totalApiCalls} sub="endpoint" color="amber" />
                        <ResultCard label="Eksik" value={testResult.criticalGaps} sub={`/ ${testResult.gaps} gap`} color={testResult.criticalGaps > 0 ? 'rose' : 'green'} />
                    </div>

                    {/* Services used */}
                    <FlatCard title="Kullanilan Servisler" icon={Server}>
                        <div className="flex flex-wrap gap-2 mt-2">
                            {testResult.services.map(svc => (
                                <span key={svc} className="inline-flex items-center px-3 py-1.5 rounded-lg bg-brand-50 border border-brand-200 text-sm font-bold text-brand-700">
                                    {svc}
                                </span>
                            ))}
                        </div>
                        <div className="flex flex-wrap gap-1.5 mt-3">
                            {testResult.capabilities.map(cap => (
                                <Badge key={cap} color={CAPABILITY_MAP[cap]?.color || 'gray'}>
                                    {cap} {CAPABILITY_MAP[cap]?.label}
                                </Badge>
                            ))}
                        </div>
                    </FlatCard>

                    {/* Flow-by-flow results */}
                    <FlatCard title="Akis Bazli Sonuclar" icon={Activity}>
                        <div className="space-y-4 mt-2">
                            {testResult.flowResults.map((fr, fi) => (
                                <div key={fi} className="rounded-lg border border-gray-200 bg-white overflow-hidden">
                                    <div className="flex items-center gap-3 px-4 py-3 bg-gray-50 border-b border-gray-200">
                                        <span className="w-7 h-7 rounded-full bg-brand-600 text-white flex items-center justify-center text-xs font-bold">
                                            {fi + 1}
                                        </span>
                                        <span className="font-bold text-sm text-t-primary flex-1">{fr.title}</span>
                                        <Badge color="blue">{fr.steps} adim</Badge>
                                        <Badge color="amber">{fr.apiCalls} API</Badge>
                                    </div>
                                    <div className="p-4 space-y-3">
                                        {fr.stepDetails.map((sd, si) => (
                                            <div key={si} className="flex gap-3">
                                                <div className="flex-shrink-0 w-8 text-center">
                                                    <span className="inline-flex items-center justify-center w-6 h-6 rounded-full bg-gray-100 text-[10px] font-bold text-t-muted">
                                                        {sd.step}
                                                    </span>
                                                </div>
                                                <div className="flex-1 min-w-0">
                                                    <div className="flex items-center gap-2 mb-1">
                                                        <RoleBadge role={sd.role} />
                                                        <span className="text-xs text-t-secondary truncate">{sd.content}</span>
                                                    </div>
                                                    {sd.apis.length > 0 && (
                                                        <div className="flex flex-wrap gap-1">
                                                            {sd.apis.map((api, ai) => (
                                                                <code key={ai} className="text-[10px] font-mono bg-gray-50 border border-gray-200 rounded px-1.5 py-0.5 text-t-muted">
                                                                    {api}
                                                                </code>
                                                            ))}
                                                        </div>
                                                    )}
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </FlatCard>

                    {/* Gap details from pre-computed */}
                    {testResult.gapDetails.length > 0 && (
                        <FlatCard title={`Eksik Gereksinimler (${testResult.gapDetails.length})`} icon={AlertTriangle}>
                            <div className="space-y-2 mt-2">
                                {testResult.gapDetails.map((gap, gi) => (
                                    <div key={gi} className={`flex items-center gap-3 rounded-lg border px-4 py-3 ${
                                        gap.priority === 'required' ? 'bg-red-50 border-red-200' : 'bg-amber-50 border-amber-200'
                                    }`}>
                                        <AlertTriangle size={14} className={gap.priority === 'required' ? 'text-red-500' : 'text-amber-500'} />
                                        <span className="text-sm font-medium flex-1">{gap.text}</span>
                                        <div className="flex gap-1.5">
                                            {gap.capability && <Badge color="purple">{gap.capability}</Badge>}
                                            {gap.service && <Badge color="blue">{gap.service}</Badge>}
                                            <Badge color={gap.side === 'client' ? 'amber' : 'green'}>
                                                {gap.side === 'client' ? 'Musteri' : 'Sistem'}
                                            </Badge>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </FlatCard>
                    )}

                    {/* Ready items from pre-computed */}
                    {testResult.readyDetails.length > 0 && (
                        <details>
                            <summary className="text-xs font-bold uppercase tracking-wider text-emerald-600 cursor-pointer hover:text-emerald-700 transition-colors select-none">
                                Hazir Gereksinimler ({testResult.readyDetails.length})
                            </summary>
                            <div className="mt-3 grid grid-cols-1 md:grid-cols-2 gap-2">
                                {testResult.readyDetails.map((r, ri) => (
                                    <div key={ri} className="flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2">
                                        <CheckCircle size={14} className="text-emerald-500 flex-shrink-0" />
                                        <span className="text-xs font-medium text-emerald-800 flex-1">{r.text}</span>
                                        {r.capability && <Badge color="green">{r.capability}</Badge>}
                                    </div>
                                ))}
                            </div>
                        </details>
                    )}
                </div>
            )}

            {activeSection === 'results' && !testResult && (
                <div className="text-center py-12 text-t-muted">
                    <FileText size={48} className="mx-auto mb-4 opacity-30" />
                    <div className="text-sm font-medium">Bu senaryo icin onceden hesaplanmis test sonucu bulunamadi.</div>
                    <div className="text-xs mt-1">Diger sekmeleri kullanarak canli analiz yapabilirsiniz.</div>
                </div>
            )}

            {/* ── Flow Selector (shared across timeline + interactive) ─── */}
            {(activeSection === 'timeline' || activeSection === 'interactive') && flows.length > 1 && (
                <div className="flex flex-wrap gap-3">
                    {flows.map((flow, idx) => (
                        <button
                            key={flow.id || idx}
                            onClick={() => setActiveFlowIdx(idx)}
                            className={`px-4 py-2 rounded-lg text-sm font-bold transition-all border ${
                                activeFlowIdx === idx
                                    ? 'bg-brand-600 text-white border-brand-600 shadow-lg ring-4 ring-brand-100'
                                    : 'bg-surface text-t-secondary border-brand-100 hover:bg-brand-50 hover:border-brand-300'
                            }`}
                        >
                            {idx + 1}. {flow.title}
                        </button>
                    ))}
                </div>
            )}

            {/* ── Section: Timeline ───────────────────────────── */}
            {activeSection === 'timeline' && activeFlow && (
                <TestFlowTimeline flow={activeFlow} />
            )}

            {/* ── Section: API Map ────────────────────────────── */}
            {activeSection === 'apimap' && (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                    {Object.entries(services).map(([name, svc]) => (
                        <FlatCard key={name} title={name} icon={Server}>
                            <div className="flex gap-1.5 flex-wrap mb-4">
                                {svc.capabilities.map(cap => (
                                    <Badge key={cap} color={CAPABILITY_MAP[cap]?.color || 'gray'}>
                                        {cap} {CAPABILITY_MAP[cap]?.label}
                                    </Badge>
                                ))}
                            </div>
                            <div className="space-y-2">
                                {svc.calls.map((call, ci) => (
                                    <TestApiCard key={ci} call={call} />
                                ))}
                            </div>
                            <div className="mt-3 pt-3 border-t border-gray-100 text-xs text-t-muted">
                                {svc.calls.length} endpoint &middot; {svc.capabilities.length} capability
                            </div>
                        </FlatCard>
                    ))}

                    {Object.keys(services).length === 0 && (
                        <div className="col-span-full text-sm text-t-muted py-8 text-center">
                            Bu senaryo icin API call tespiti yapilamadi.
                        </div>
                    )}
                </div>
            )}

            {/* ── Section: Gap Analysis ──────────────────────── */}
            {activeSection === 'gaps' && (
                <TestGapAnalysis scenario={data} />
            )}

            {/* ── Section: Interactive Test ───────────────────── */}
            {activeSection === 'interactive' && activeFlow && (
                <TestDataRunner flow={activeFlow} />
            )}
        </div>
    );
};

const StatBadge = ({ icon: Icon, label, value, alert = false }) => (
    <div className={`flex items-center gap-1.5 text-xs font-bold px-3 py-1.5 rounded-full ${
        alert ? 'bg-white/60' : 'bg-white/40'
    }`}>
        <Icon size={14} />
        <span>{value}</span>
        <span className="opacity-60 font-medium">{label}</span>
    </div>
);

const ResultCard = ({ label, value, sub, color }) => {
    const colors = {
        blue: 'bg-blue-50 border-blue-200 text-blue-800',
        purple: 'bg-purple-50 border-purple-200 text-purple-800',
        amber: 'bg-amber-50 border-amber-200 text-amber-800',
        rose: 'bg-red-50 border-red-200 text-red-800',
        green: 'bg-emerald-50 border-emerald-200 text-emerald-800',
    };
    return (
        <div className={`rounded-xl border-2 p-4 text-center ${colors[color] || colors.blue}`}>
            <div className="text-3xl font-extrabold">{value}</div>
            <div className="text-xs font-bold uppercase mt-1">{label}</div>
            {sub && <div className="text-[10px] opacity-60 mt-0.5">{sub}</div>}
        </div>
    );
};

const roleColors = {
    user:   'bg-blue-100 text-blue-700',
    ai:     'bg-purple-100 text-purple-700',
    bot:    'bg-amber-100 text-amber-700',
    system: 'bg-gray-100 text-gray-700',
    agent:  'bg-emerald-100 text-emerald-700',
};

const roleLabels = { user: 'USR', ai: 'AI', bot: 'BOT', system: 'SYS', agent: 'AGT' };

const RoleBadge = ({ role }) => (
    <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-bold ${roleColors[role] || roleColors.user}`}>
        {roleLabels[role] || role?.toUpperCase()}
    </span>
);

export default TestTab;
