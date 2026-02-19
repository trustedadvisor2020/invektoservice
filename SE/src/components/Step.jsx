import React from 'react';

const Step = ({ number, title, goal, children }) => {
    return (
        <div className="flex gap-5 relative pb-10 last:pb-0 group">
            <div className="absolute left-[18px] top-10 bottom-0 w-0.5 bg-gray-200 group-last:hidden"></div>
            <div className="flex-shrink-0 w-10 h-10 rounded-full border-2 border-brand-200 bg-surface text-brand-600 flex items-center justify-center font-bold text-base relative z-10 shadow-sm font-mono">
                {number}
            </div>
            <div className="flex-1 pt-1.5">
                <div className="flex justify-between items-start mb-3">
                    <h4 className="text-xl font-bold text-t-primary">{title}</h4>
                    {goal && <span className="text-xs text-t-muted bg-gray-50 px-2 py-1 rounded border border-gray-100 font-mono">Hedef: {goal}</span>}
                </div>
                <div className="text-t-secondary leading-relaxed text-base">
                    {children}
                </div>
            </div>
        </div>
    );
};

export default Step;
