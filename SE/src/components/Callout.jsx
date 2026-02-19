import React from 'react';
import { AlertTriangle, Info } from 'lucide-react';

const Callout = ({ type = 'info', title, children }) => {
    const styles = {
        info: { border: 'border-l-4 border-brand-500', bg: 'bg-brand-50', text: 'text-brand-900', icon: 'text-brand-500' },
        warning: { border: 'border-l-4 border-amber-500', bg: 'bg-amber-50', text: 'text-amber-900', icon: 'text-amber-500' },
        success: { border: 'border-l-4 border-emerald-500', bg: 'bg-emerald-50', text: 'text-emerald-900', icon: 'text-emerald-500' },
        danger: { border: 'border-l-4 border-rose-500', bg: 'bg-rose-50', text: 'text-rose-900', icon: 'text-rose-500' },
    };
    const s = styles[type] || styles.info;

    return (
        <div className={`p-5 rounded-r-lg ${s.bg} ${s.border} mb-6 shadow-sm`}>
            {title && <div className={`font-bold ${s.text} text-lg mb-2 flex items-center gap-2`}>
                {type === 'warning' && <AlertTriangle size={20} />}
                {type === 'info' && <Info size={20} />}
                {title}
            </div>}
            <div className={`${s.text} text-base leading-relaxed`}>{children}</div>
        </div>
    );
};

export default Callout;
