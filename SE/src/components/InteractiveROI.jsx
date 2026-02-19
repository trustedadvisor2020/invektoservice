import React, { useState, useMemo } from 'react';
import FlatCard from './FlatCard';

const InteractiveROI = ({ impact }) => {
    if (!impact) return null;

    const { title, description, inputs = [], formula, subscriptionCost = 5000 } = impact;

    // Initialize state from default values
    const [values, setValues] = useState(() => {
        const initial = {};
        inputs.forEach(input => {
            initial[input.key] = input.default;
        });
        return initial;
    });

    const updateValue = (key, val, min = 0) => {
        setValues(prev => ({ ...prev, [key]: Math.max(min, Number(val) || 0) }));
    };

    // Evaluate formula
    const result = useMemo(() => {
        if (!formula) return { mainResult: 0, roi: 0 };
        try {
            // Build evaluation context from values + constants
            const ctx = { ...values };
            inputs.forEach(input => {
                if (input.constant) ctx[input.key] = input.constant;
            });

            // Simple formula evaluation with named variables
            let expr = formula;
            Object.entries(ctx).forEach(([k, v]) => {
                expr = expr.replace(new RegExp(`\\b${k}\\b`, 'g'), String(v));
            });
            const mainResult = Math.round(eval(expr));
            const roi = subscriptionCost > 0 ? Math.round((mainResult / subscriptionCost) * 10) / 10 : 0;
            return { mainResult, roi };
        } catch {
            return { mainResult: 0, roi: 0 };
        }
    }, [values, formula, subscriptionCost, inputs]);

    return (
        <div className="mt-16">
            <FlatCard className="bg-emerald-50/80 border-emerald-100">
                <div className="mb-8">
                    <div className="flex items-center gap-3 mb-2">
                        <h2 className="text-3xl font-bold text-emerald-900">
                            {title}: ~{result.mainResult.toLocaleString('tr-TR')} TL
                        </h2>
                        <span className="text-xs font-mono bg-emerald-200 text-emerald-800 px-2 py-1 rounded">CANLI HESAPLAMA</span>
                    </div>
                    <p className="text-emerald-700 text-lg">
                        {description}
                    </p>
                </div>

                <div className={`grid grid-cols-1 md:grid-cols-2 lg:grid-cols-${Math.min(inputs.length + 1, 5)} gap-8 pt-8 border-t border-emerald-200`}>
                    {inputs.filter(i => !i.hidden).map(input => (
                        <div key={input.key}>
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">{input.label}</span>
                            <div className="flex items-baseline gap-1">
                                {input.prefix && <span className="text-3xl font-bold text-emerald-900">{input.prefix}</span>}
                                {input.editable !== false ? (
                                    <input
                                        type="number"
                                        value={values[input.key]}
                                        step={input.step || 1}
                                        onChange={(e) => updateValue(input.key, e.target.value, input.min ?? 0)}
                                        className="text-3xl font-bold text-emerald-900 bg-transparent border-b-2 border-dashed border-emerald-400 w-24 text-center focus:outline-none focus:border-emerald-600 transition-colors"
                                    />
                                ) : (
                                    <span className="text-3xl font-bold text-emerald-900">{values[input.key]}</span>
                                )}
                                {input.suffix && <span className="text-3xl font-bold text-emerald-900">{input.suffix}</span>}
                            </div>
                            {input.hint && <span className="text-emerald-700/80 text-sm mt-1 block">{input.hint}</span>}
                        </div>
                    ))}

                    <div className="bg-emerald-100/50 rounded-lg p-4 -m-2">
                        <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">
                            {impact.resultLabel || 'Aylik Etki'}
                        </span>
                        <span className="block text-3xl font-bold text-emerald-900">
                            {(result.mainResult / 1000).toFixed(1)}k TL
                        </span>
                        <span className="text-emerald-700/80 text-sm mt-1 block">
                            {result.roi}x ROI ({subscriptionCost.toLocaleString('tr-TR')} TL abonelik)
                        </span>
                    </div>
                </div>

                {impact.formulaDisplay && (
                    <div className="mt-6 pt-4 border-t border-emerald-200">
                        <p className="text-emerald-600 text-sm font-mono">
                            {typeof impact.formulaDisplay === 'function'
                                ? impact.formulaDisplay(values, result.mainResult)
                                : impact.formulaDisplay}
                        </p>
                    </div>
                )}
            </FlatCard>
        </div>
    );
};

export default InteractiveROI;
