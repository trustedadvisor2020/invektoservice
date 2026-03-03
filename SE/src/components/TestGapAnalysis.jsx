import React, { useMemo } from 'react';
import { AlertTriangle, CheckCircle, Info, ArrowUpRight } from 'lucide-react';
import { generateGapReport, CAPABILITY_MAP } from '../lib/scenarioTestEngine';
import Badge from './Badge';
import FlatCard from './FlatCard';

const priorityConfig = {
    required:    { label: 'Zorunlu',  color: 'rose',  bg: 'bg-red-50 border-red-200' },
    recommended: { label: 'Onerilen', color: 'amber', bg: 'bg-amber-50 border-amber-200' },
    optional:    { label: 'Opsiyonel', color: 'gray', bg: 'bg-gray-50 border-gray-200' },
};

const effortConfig = {
    easy:      { label: 'Kolay',   color: 'green' },
    medium:    { label: 'Orta',    color: 'amber' },
    technical: { label: 'Teknik',  color: 'purple' },
};

const GapItem = ({ item }) => {
    const pri = priorityConfig[item.priority] || priorityConfig.optional;
    const eff = effortConfig[item.effort] || effortConfig.medium;
    const cap = CAPABILITY_MAP[item.capability];
    const isGap = item.status !== 'ready';

    return (
        <div className={`rounded-lg border p-4 ${isGap ? pri.bg : 'bg-emerald-50 border-emerald-200'}`}>
            <div className="flex items-start gap-3">
                {isGap ? (
                    <AlertTriangle size={18} className={item.priority === 'required' ? 'text-red-500' : 'text-amber-500'} />
                ) : (
                    <CheckCircle size={18} className="text-emerald-500" />
                )}
                <div className="flex-1 min-w-0">
                    <div className="font-medium text-sm text-t-primary mb-1.5">{item.text}</div>
                    <div className="flex flex-wrap gap-1.5 mb-2">
                        <Badge color={pri.color}>{pri.label}</Badge>
                        <Badge color={eff.color}>{eff.label}</Badge>
                        {item.capability && <Badge color={cap?.color || 'gray'}>{item.capability}</Badge>}
                        {item.side === 'client' && <Badge color="blue">Musteri Tarafi</Badge>}
                        {item.side === 'provider' && <Badge color="green">Sistem Tarafi</Badge>}
                    </div>
                    {item.service && item.page && (
                        <div className="flex items-center gap-1 text-xs text-brand-500">
                            <ArrowUpRight size={12} />
                            <span className="font-medium">{item.service}</span>
                            <span className="text-t-muted">&middot;</span>
                            <span className="text-t-muted">{item.page}</span>
                        </div>
                    )}
                    {item.hint && (
                        <div className="flex items-start gap-1.5 mt-2 text-xs text-t-muted italic">
                            <Info size={12} className="flex-shrink-0 mt-0.5" />
                            {item.hint}
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

const TestGapAnalysis = ({ scenario }) => {
    const { gaps, readyItems } = useMemo(() => generateGapReport(scenario), [scenario]);

    const criticalGaps = gaps.filter(g => g.priority === 'required');
    const otherGaps = gaps.filter(g => g.priority !== 'required');

    if (gaps.length === 0 && readyItems.length === 0) {
        return (
            <FlatCard title="Gap Analizi" icon={Info}>
                <div className="text-sm text-t-muted py-4">
                    Bu senaryo icin yapilandirilmis gereksinim bulunamadi.
                </div>
            </FlatCard>
        );
    }

    return (
        <div className="space-y-6">
            {/* Summary banner */}
            <div className={`rounded-xl border-2 p-4 flex items-center gap-4 ${
                criticalGaps.length > 0
                    ? 'bg-red-50 border-red-200 text-red-800'
                    : gaps.length > 0
                    ? 'bg-amber-50 border-amber-200 text-amber-800'
                    : 'bg-emerald-50 border-emerald-200 text-emerald-800'
            }`}>
                {criticalGaps.length > 0 ? (
                    <AlertTriangle size={24} />
                ) : gaps.length > 0 ? (
                    <AlertTriangle size={24} />
                ) : (
                    <CheckCircle size={24} />
                )}
                <div className="flex-1">
                    <div className="font-bold text-sm">
                        {criticalGaps.length > 0
                            ? `${criticalGaps.length} zorunlu eksik var`
                            : gaps.length > 0
                            ? `${gaps.length} onerilen kurulum var`
                            : 'Tum gereksinimler hazir'}
                    </div>
                    <div className="text-xs opacity-75">
                        {readyItems.length} hazir &middot; {gaps.length} eksik/kurulum bekliyor
                    </div>
                </div>
                <div className="flex gap-2">
                    <span className="flex items-center gap-1 text-xs font-bold bg-white/50 rounded-full px-3 py-1">
                        <CheckCircle size={12} /> {readyItems.length}
                    </span>
                    {gaps.length > 0 && (
                        <span className="flex items-center gap-1 text-xs font-bold bg-white/50 rounded-full px-3 py-1">
                            <AlertTriangle size={12} /> {gaps.length}
                        </span>
                    )}
                </div>
            </div>

            {/* Critical gaps */}
            {criticalGaps.length > 0 && (
                <div>
                    <h3 className="text-xs font-bold uppercase tracking-wider text-red-600 mb-3">
                        Zorunlu Eksikler ({criticalGaps.length})
                    </h3>
                    <div className="space-y-2">
                        {criticalGaps.map((gap, i) => <GapItem key={`crit-${i}`} item={gap} />)}
                    </div>
                </div>
            )}

            {/* Other gaps */}
            {otherGaps.length > 0 && (
                <div>
                    <h3 className="text-xs font-bold uppercase tracking-wider text-amber-600 mb-3">
                        Onerilen Kurulumlar ({otherGaps.length})
                    </h3>
                    <div className="space-y-2">
                        {otherGaps.map((gap, i) => <GapItem key={`gap-${i}`} item={gap} />)}
                    </div>
                </div>
            )}

            {/* Ready items */}
            {readyItems.length > 0 && (
                <details>
                    <summary className="text-xs font-bold uppercase tracking-wider text-emerald-600 cursor-pointer hover:text-emerald-700 transition-colors select-none mb-3">
                        Hazir Olanlar ({readyItems.length})
                    </summary>
                    <div className="space-y-2 mt-2">
                        {readyItems.map((item, i) => <GapItem key={`ready-${i}`} item={item} />)}
                    </div>
                </details>
            )}
        </div>
    );
};

export default TestGapAnalysis;
