import React from 'react';

const FlatCard = ({ title, icon: Icon, children, className = '' }) => {
    return (
        <div className={`bg-surface rounded-xl shadow-sm border border-brand-100 p-8 ${className}`}>
            {title && (
                <h3 className="flex items-center gap-3 text-xl font-bold text-t-primary mb-5 pb-3 border-b border-gray-50">
                    {Icon && <Icon className="text-brand-500" size={24} />}
                    {title}
                </h3>
            )}
            {children}
        </div>
    );
};

export default FlatCard;
