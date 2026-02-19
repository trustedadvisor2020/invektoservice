import React from 'react';

const Badge = ({ children, color = 'gray' }) => {
    const colors = {
        gray: 'bg-gray-100 text-t-secondary',
        blue: 'bg-brand-100 text-brand-700',
        green: 'bg-emerald-100 text-emerald-700',
        amber: 'bg-amber-100 text-amber-700',
        indigo: 'bg-brand-100 text-brand-700',
        purple: 'bg-purple-100 text-purple-700',
        rose: 'bg-rose-100 text-rose-700',
    };
    return (
        <span className={`inline-flex items-center px-3 py-1 rounded-md text-sm font-medium font-mono ${colors[color] || colors.gray}`}>
            {children}
        </span>
    );
};

export default Badge;
