import React, { useState, useMemo, useCallback } from 'react';
import { Play, RotateCcw, Edit3 } from 'lucide-react';
import { extractTestVariables, simulateFlow, mapFlowToApiCalls } from '../lib/scenarioTestEngine';
import ChatPreview from './ChatPreview';
import FlatCard from './FlatCard';
import Badge from './Badge';

const TestDataRunner = ({ flow }) => {
    const baseVariables = useMemo(() => extractTestVariables(flow), [flow]);
    const [variables, setVariables] = useState(() =>
        baseVariables.map(v => ({ ...v, currentValue: v.defaultValue }))
    );
    const [simulated, setSimulated] = useState(null);
    const [isRunning, setIsRunning] = useState(false);

    const handleChange = useCallback((key, value) => {
        setVariables(prev => prev.map(v => v.key === key ? { ...v, currentValue: value } : v));
    }, []);

    const handleRun = useCallback(() => {
        setIsRunning(true);
        // Simulate a small delay for visual feedback
        setTimeout(() => {
            const newSteps = simulateFlow(flow, variables);
            setSimulated(newSteps);
            setIsRunning(false);
        }, 300);
    }, [flow, variables]);

    const handleReset = useCallback(() => {
        setVariables(baseVariables.map(v => ({ ...v, currentValue: v.defaultValue })));
        setSimulated(null);
    }, [baseVariables]);

    const simulatedApiCalls = useMemo(() => {
        if (!simulated) return null;
        return mapFlowToApiCalls({ ...flow, steps: simulated });
    }, [flow, simulated]);

    const totalCalls = simulatedApiCalls
        ? simulatedApiCalls.reduce((sum, s) => sum + s.apiCalls.length, 0)
        : 0;

    if (baseVariables.length === 0) {
        return (
            <FlatCard title="Interaktif Test" icon={Play}>
                <div className="text-sm text-t-muted py-4">
                    Bu akista test edilecek musteri mesaji bulunamadi.
                </div>
            </FlatCard>
        );
    }

    return (
        <div className="space-y-6">
            {/* Input form */}
            <FlatCard title="Test Verileri" icon={Edit3} className="border-l-4 border-brand-500">
                <div className="space-y-4 mt-2">
                    {variables.map(v => (
                        <div key={v.key}>
                            <label className="block text-xs font-bold text-t-muted uppercase tracking-wider mb-1.5">
                                {v.label}
                            </label>
                            {v.type === 'textarea' ? (
                                <textarea
                                    value={v.currentValue}
                                    onChange={e => handleChange(v.key, e.target.value)}
                                    className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm font-mono text-t-primary bg-white focus:border-brand-400 focus:ring-2 focus:ring-brand-100 transition-colors resize-none"
                                    rows={2}
                                />
                            ) : v.type === 'select' ? (
                                <select
                                    value={v.currentValue}
                                    onChange={e => handleChange(v.key, e.target.value)}
                                    className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm text-t-primary bg-white focus:border-brand-400 focus:ring-2 focus:ring-brand-100 transition-colors"
                                >
                                    {(v.options || []).map(opt => (
                                        <option key={opt} value={opt}>{opt}</option>
                                    ))}
                                </select>
                            ) : (
                                <input
                                    type="text"
                                    value={v.currentValue}
                                    onChange={e => handleChange(v.key, e.target.value)}
                                    className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm font-mono text-t-primary bg-white focus:border-brand-400 focus:ring-2 focus:ring-brand-100 transition-colors"
                                />
                            )}
                        </div>
                    ))}

                    {/* Action buttons */}
                    <div className="flex gap-3 pt-2">
                        <button
                            onClick={handleRun}
                            disabled={isRunning}
                            className={`flex items-center gap-2 px-5 py-2.5 rounded-lg font-bold text-sm transition-all ${
                                isRunning
                                    ? 'bg-gray-200 text-gray-500 cursor-wait'
                                    : 'bg-brand-600 text-white hover:bg-brand-700 shadow-lg shadow-brand-200 hover:shadow-xl'
                            }`}
                        >
                            <Play size={16} className={isRunning ? 'animate-spin' : ''} />
                            {isRunning ? 'Calistiriliyor...' : 'Simule Et'}
                        </button>
                        {simulated && (
                            <button
                                onClick={handleReset}
                                className="flex items-center gap-2 px-4 py-2.5 rounded-lg font-bold text-sm text-t-muted border border-gray-200 hover:bg-gray-50 transition-colors"
                            >
                                <RotateCcw size={14} />
                                Sifirla
                            </button>
                        )}
                    </div>
                </div>
            </FlatCard>

            {/* Simulation result */}
            {simulated && (
                <div className="animate-fade-in">
                    <div className="flex items-center gap-3 mb-4">
                        <h3 className="text-sm font-bold text-t-primary">Simulasyon Sonucu</h3>
                        <Badge color="green">{simulated.length} adim</Badge>
                        <Badge color="blue">{totalCalls} API call</Badge>
                    </div>
                    <ChatPreview
                        steps={simulated}
                        assistantName={flow.assistantName || 'Test Asistani'}
                    />
                </div>
            )}
        </div>
    );
};

export default TestDataRunner;
