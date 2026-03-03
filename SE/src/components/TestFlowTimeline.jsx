import React, { useMemo } from 'react';
import * as LucideIcons from 'lucide-react';
import { mapFlowToApiCalls, ROLE_META, CAPABILITY_MAP } from '../lib/scenarioTestEngine';
import Badge from './Badge';
import TestApiCard from './TestApiCard';

const RoleIcon = ({ role }) => {
    const meta = ROLE_META[role] || ROLE_META.user;
    const Icon = LucideIcons[meta.icon] || LucideIcons.User;
    return <Icon size={16} />;
};

const TestFlowTimeline = ({ flow, steps: overrideSteps }) => {
    const mapped = useMemo(() => {
        if (!flow) return [];
        // If overrideSteps provided (from simulation), create a patched flow
        const effectiveFlow = overrideSteps ? { ...flow, steps: overrideSteps } : flow;
        return mapFlowToApiCalls(effectiveFlow);
    }, [flow, overrideSteps]);

    if (mapped.length === 0) {
        return <div className="text-sm text-t-muted py-4">Bu akista adim bulunamadi.</div>;
    }

    return (
        <div className="relative">
            {/* Vertical connector line */}
            <div className="absolute left-6 top-8 bottom-8 w-px bg-gray-200" />

            <div className="space-y-1">
                {mapped.map((step, idx) => {
                    const meta = ROLE_META[step.role] || ROLE_META.user;
                    const isLast = idx === mapped.length - 1;

                    return (
                        <div key={idx} className="relative flex gap-4">
                            {/* Step indicator */}
                            <div className="flex-shrink-0 w-12 flex flex-col items-center z-10">
                                <div className={`w-10 h-10 rounded-full flex items-center justify-center border-2 ${meta.color}`}>
                                    <RoleIcon role={step.role} />
                                </div>
                                {!isLast && (
                                    <div className="flex-1 flex items-center justify-center py-1">
                                        <LucideIcons.ChevronDown size={14} className="text-gray-300" />
                                    </div>
                                )}
                            </div>

                            {/* Step content */}
                            <div className={`flex-1 rounded-xl border bg-white p-4 mb-3 ${
                                step.apiCalls.length > 0 ? 'border-gray-200' : 'border-gray-100'
                            }`}>
                                {/* Header */}
                                <div className="flex items-center gap-2 mb-2">
                                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border ${meta.color}`}>
                                        {meta.label}
                                    </span>
                                    <span className="text-[10px] font-mono text-t-muted">STEP {idx + 1}</span>
                                    {step.apiCalls.length > 0 && (
                                        <span className="ml-auto text-[10px] font-bold text-brand-500 bg-brand-50 px-2 py-0.5 rounded-full">
                                            {step.apiCalls.length} API
                                        </span>
                                    )}
                                </div>

                                {/* Message content */}
                                <div className="text-sm text-t-primary leading-relaxed mb-3 whitespace-pre-line">
                                    {step.content}
                                </div>

                                {/* API calls */}
                                {step.apiCalls.length > 0 && (
                                    <div className="border-t border-gray-100 pt-3 space-y-2">
                                        <div className="flex items-center gap-1.5 mb-1">
                                            <LucideIcons.Radio size={12} className="text-brand-400" />
                                            <span className="text-[10px] font-bold uppercase text-t-muted tracking-wider">Implied API Calls</span>
                                        </div>
                                        {step.apiCalls.map((call, ci) => (
                                            <TestApiCard key={ci} call={call} compact />
                                        ))}
                                    </div>
                                )}

                                {/* Capabilities */}
                                {step.apiCalls.length > 0 && (
                                    <div className="flex gap-1 mt-2 pt-2 border-t border-gray-50">
                                        {[...new Set(step.apiCalls.map(c => c.capability))].map(cap => (
                                            <Badge key={cap} color={CAPABILITY_MAP[cap]?.color || 'gray'}>
                                                {cap} {CAPABILITY_MAP[cap]?.label}
                                            </Badge>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};

export default TestFlowTimeline;
