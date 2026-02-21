import React from 'react';
import * as LucideIcons from 'lucide-react';
import { NODE_TYPES } from '../lib/scenarioAnalysis';
import Badge from './Badge';

const colorMap = {
    amber:  { bg: 'bg-amber-50',  border: 'border-amber-300',  text: 'text-amber-800',  dot: 'bg-amber-400',  line: 'bg-amber-300' },
    green:  { bg: 'bg-emerald-50', border: 'border-emerald-300', text: 'text-emerald-800', dot: 'bg-emerald-400', line: 'bg-emerald-300' },
    blue:   { bg: 'bg-blue-50',   border: 'border-blue-300',   text: 'text-blue-800',   dot: 'bg-blue-400',   line: 'bg-blue-300' },
    purple: { bg: 'bg-purple-50', border: 'border-purple-300', text: 'text-purple-800', dot: 'bg-purple-400', line: 'bg-purple-300' },
    rose:   { bg: 'bg-rose-50',   border: 'border-rose-300',   text: 'text-rose-800',   dot: 'bg-rose-400',   line: 'bg-rose-300' },
    cyan:   { bg: 'bg-cyan-50',   border: 'border-cyan-300',   text: 'text-cyan-800',   dot: 'bg-cyan-400',   line: 'bg-cyan-300' },
    gray:   { bg: 'bg-gray-50',   border: 'border-gray-300',   text: 'text-gray-700',   dot: 'bg-gray-400',   line: 'bg-gray-300' },
};

const FlowDiagram = ({ nodes = [], edges = [] }) => {
    if (!nodes.length) return null;

    return (
        <div className="bg-surface border border-gray-200 rounded-xl p-5 mb-6">
            <div className="flex items-center gap-2 mb-4">
                <LucideIcons.Workflow size={18} className="text-brand-500" />
                <h3 className="text-sm font-bold text-t-muted uppercase tracking-wider">Otomasyon Akis Diyagrami</h3>
            </div>

            {/* Horizontal flow */}
            <div className="flex items-start gap-0 overflow-x-auto pb-3">
                {nodes.map((node, idx) => {
                    const typeDef = NODE_TYPES[node.type] || NODE_TYPES.utility_note;
                    const colors = colorMap[typeDef.color] || colorMap.gray;
                    const Icon = LucideIcons[typeDef.icon] || LucideIcons.Circle;
                    const isProposed = typeDef.proposed;
                    const isLast = idx === nodes.length - 1;

                    return (
                        <React.Fragment key={node.id}>
                            <div className="flex flex-col items-center min-w-[140px] max-w-[160px] flex-shrink-0">
                                {/* Node card */}
                                <div className={`
                                    relative w-full rounded-lg border-2 px-3 py-2.5 shadow-sm transition-all hover:shadow-md
                                    ${colors.bg} ${colors.border}
                                    ${isProposed ? 'border-dashed' : ''}
                                `}>
                                    {/* Type badge */}
                                    <div className="flex items-center gap-1.5 mb-1.5">
                                        <Icon size={14} className={colors.text} />
                                        <span className={`text-[10px] font-bold uppercase tracking-wider ${colors.text}`}>
                                            {typeDef.label}
                                        </span>
                                    </div>

                                    {/* Label */}
                                    <p className="text-xs text-t-primary leading-snug font-medium line-clamp-3">
                                        {node.label}
                                    </p>

                                    {/* Service tag */}
                                    {node.service && (
                                        <div className="mt-1.5">
                                            <span className="inline-flex items-center text-[10px] font-bold bg-white/80 rounded px-1.5 py-0.5 text-t-muted border border-gray-200">
                                                {node.service}
                                            </span>
                                        </div>
                                    )}

                                    {/* Proposed badge */}
                                    {isProposed && (
                                        <div className="absolute -top-2 -right-2">
                                            <span className="text-[9px] font-bold bg-amber-400 text-amber-900 rounded-full px-1.5 py-0.5">ONERI</span>
                                        </div>
                                    )}
                                </div>

                                {/* Node index */}
                                <span className={`mt-1.5 w-5 h-5 rounded-full ${colors.dot} text-white text-[10px] font-bold flex items-center justify-center`}>
                                    {idx + 1}
                                </span>
                            </div>

                            {/* Connector arrow */}
                            {!isLast && (
                                <div className="flex items-center self-center mt-4 flex-shrink-0">
                                    <div className="w-6 h-0.5 bg-gray-300"></div>
                                    <LucideIcons.ChevronRight size={14} className="text-gray-400 -ml-1" />
                                </div>
                            )}
                        </React.Fragment>
                    );
                })}
            </div>

            {/* Legend */}
            <div className="flex flex-wrap gap-3 mt-4 pt-3 border-t border-gray-100">
                {getUsedTypes(nodes).map(typeKey => {
                    const typeDef = NODE_TYPES[typeKey];
                    if (!typeDef) return null;
                    const colors = colorMap[typeDef.color] || colorMap.gray;
                    const Icon = LucideIcons[typeDef.icon] || LucideIcons.Circle;
                    return (
                        <div key={typeKey} className="flex items-center gap-1.5 text-[11px] text-t-muted">
                            <div className={`w-3 h-3 rounded ${colors.dot}`}></div>
                            <Icon size={12} className={colors.text} />
                            <span className="font-medium">{typeDef.label}</span>
                            {typeDef.proposed && <span className="text-[9px] font-bold text-amber-600">(Oneri)</span>}
                        </div>
                    );
                })}
            </div>
        </div>
    );
};

function getUsedTypes(nodes) {
    const types = new Set();
    nodes.forEach(n => types.add(n.type));
    return [...types];
}

export default FlowDiagram;
