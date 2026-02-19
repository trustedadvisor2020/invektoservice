import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { CheckCircle, Smartphone, Info, Zap, BookOpen, ArrowUpRight } from 'lucide-react';
import * as LucideIcons from 'lucide-react';
import Badge from './Badge';
import Step from './Step';
import FlatCard from './FlatCard';
import Callout from './Callout';
import ChatPreview from './ChatPreview';
import InteractiveROI from './InteractiveROI';

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

const ScenarioPage = ({ data }) => {
    const [activeTab, setActiveTab] = useState('overview');
    const [activeFlow, setActiveFlow] = useState(0);

    if (!data) return <div className="p-10 text-t-muted">Senaryo verisi bulunamadi.</div>;

    const { id, title, subtitle, phase, category, description, overview, flows = [], tech, impact } = data;

    const tabs = [
        { key: 'overview', label: 'GENEL BAKIS' },
        { key: 'scenarios', label: `SENARYO AKISLARI (${flows.length})` },
        { key: 'tech', label: 'TEKNIK DETAYLAR' },
    ];

    return (
        <div className="max-w-[1700px] mx-auto p-10 font-sans bg-gray-50/50 min-h-screen">
            {/* Header */}
            <div className="mb-12">
                <div className="flex items-center gap-3 mb-5">
                    {phase && <Badge color="blue">{phase.toUpperCase()}</Badge>}
                    {category && <Badge color="green">{category}</Badge>}
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
                    <div className="flex flex-wrap gap-4 mb-10">
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

            {/* Footer: Interactive ROI */}
            {impact && <InteractiveROI impact={impact} />}
        </div>
    );
};

export default ScenarioPage;
