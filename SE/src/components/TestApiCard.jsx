import React from 'react';
import { CAPABILITY_MAP } from '../lib/scenarioTestEngine';
import Badge from './Badge';

const methodColors = {
    GET:  'bg-emerald-50 text-emerald-700 border-emerald-200',
    POST: 'bg-blue-50 text-blue-700 border-blue-200',
    PUT:  'bg-amber-50 text-amber-700 border-amber-200',
    DELETE: 'bg-red-50 text-red-700 border-red-200',
};

const TestApiCard = ({ call, compact = false }) => {
    const cap = CAPABILITY_MAP[call.capability];
    const methodClass = methodColors[call.method] || methodColors.GET;

    if (compact) {
        return (
            <div className="flex items-center gap-2 text-xs">
                <span className={`inline-flex items-center px-1.5 py-0.5 rounded font-mono font-bold border ${methodClass}`}>
                    {call.method}
                </span>
                <code className="text-t-secondary font-mono truncate">{call.endpoint}</code>
                {call.capability && (
                    <Badge color={cap?.color || 'gray'}>{call.capability}</Badge>
                )}
            </div>
        );
    }

    return (
        <div className="rounded-lg border border-gray-200 bg-white p-3 hover:border-brand-300 transition-colors">
            <div className="flex items-center gap-2 mb-2">
                <span className={`inline-flex items-center px-2 py-0.5 rounded font-mono text-xs font-bold border ${methodClass}`}>
                    {call.method}
                </span>
                <code className="text-sm text-t-primary font-mono font-medium flex-1 truncate">{call.endpoint}</code>
                {call.source === 'content' && (
                    <span className="text-[10px] font-bold uppercase text-brand-500 bg-brand-50 px-1.5 py-0.5 rounded">icerik</span>
                )}
                {call.source === 'role' && (
                    <span className="text-[10px] font-bold uppercase text-gray-400 bg-gray-50 px-1.5 py-0.5 rounded">rol</span>
                )}
            </div>
            <div className="flex items-center gap-2">
                <span className="text-xs text-t-muted">{call.desc}</span>
                <div className="flex gap-1 ml-auto">
                    {call.capability && <Badge color={cap?.color || 'gray'}>{call.capability}</Badge>}
                    <Badge color={cap?.color || 'gray'}>{call.service}</Badge>
                </div>
            </div>
        </div>
    );
};

export default TestApiCard;
