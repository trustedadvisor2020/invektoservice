import React from 'react';
import { MessageCircle, ArrowRight } from 'lucide-react';
import Badge from './Badge';

const capabilityColors = {
    C1: 'blue', C2: 'blue', C3: 'blue', C4: 'purple',
    C5: 'rose', C6: 'rose', C7: 'green', C8: 'amber',
    C9: 'gray', C10: 'purple', C11: 'amber', C12: 'purple', C13: 'gray',
};

const ScenarioCard = ({ scenario, onClick }) => {
    const { id, name, description, customerMessage, solution, capabilities = [], phase } = scenario;

    return (
        <div
            className="bg-surface rounded-xl shadow-sm border border-brand-100 p-6 hover:shadow-md hover:border-brand-300 transition-all cursor-pointer group"
            onClick={onClick}
        >
            {/* Header */}
            <div className="flex items-start justify-between mb-4">
                <div className="flex items-center gap-3">
                    <span className="text-sm font-mono font-bold text-brand-600 bg-brand-50 px-2 py-1 rounded">{id}</span>
                    <h3 className="text-lg font-bold text-t-primary group-hover:text-brand-700 transition-colors">{name}</h3>
                </div>
                {phase && <Badge color="blue">{phase}</Badge>}
            </div>

            {/* Description */}
            <p className="text-t-secondary text-base mb-4 leading-relaxed">{description}</p>

            {/* Customer Message Example */}
            {customerMessage && (
                <div className="bg-[#d9fdd3] rounded-xl p-3 mb-4 flex items-start gap-2">
                    <MessageCircle size={16} className="text-emerald-600 mt-0.5 flex-shrink-0" />
                    <span className="text-sm text-gray-800 italic">"{customerMessage}"</span>
                </div>
            )}

            {/* Solution */}
            {solution && (
                <div className="bg-brand-50 rounded-xl p-3 mb-4 flex items-start gap-2">
                    <ArrowRight size={16} className="text-brand-600 mt-0.5 flex-shrink-0" />
                    <span className="text-sm text-brand-900">{solution}</span>
                </div>
            )}

            {/* Capabilities */}
            {capabilities.length > 0 && (
                <div className="flex flex-wrap gap-2 mt-4 pt-4 border-t border-gray-100">
                    {capabilities.map((cap, i) => (
                        <Badge key={i} color={capabilityColors[cap] || 'gray'}>{cap}</Badge>
                    ))}
                </div>
            )}
        </div>
    );
};

export default ScenarioCard;
