import React, { useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { Home, ShoppingCart, Activity, Globe, ChevronDown, ChevronRight, LayoutGrid, Building2, Sparkles, GraduationCap, Smartphone, Shield, HeartPulse } from 'lucide-react';
import { sidebarGroups } from '../data';

const iconMap = { ShoppingCart, Activity, Globe, Building2, Sparkles, GraduationCap, Smartphone, Shield };

// S1 → S01, E01 → E01 (consistent 3-char format)
const formatCode = (id) => {
    const upper = id.toUpperCase();
    const match = upper.match(/^([A-Z]+)(\d+)$/);
    if (!match) return upper;
    return `${match[1]}${match[2].padStart(2, '0')}`;
};

const Sidebar = () => {
    const location = useLocation();

    return (
        <aside className="w-72 bg-surface border-r border-gray-200 h-screen sticky top-0 flex flex-col p-6 shadow-sm overflow-y-auto">
            {/* Logo Area */}
            <NavLink to="/" className="flex items-center gap-4 mb-10 no-underline">
                <div className="w-12 h-12 bg-gradient-to-br from-brand-500 to-brand-700 rounded-xl flex items-center justify-center shadow-md">
                    <Home color="white" size={24} />
                </div>
                <h2 className="text-2xl font-bold text-t-primary tracking-tight">Invekto</h2>
            </NavLink>

            {/* Health Check Link */}
            <NavLink
                to="/health-check"
                className={({ isActive }) =>
                    `flex items-center gap-3 px-4 py-3 rounded-lg text-base font-medium transition-all mb-2 ${
                        isActive
                            ? 'bg-rose-50 text-rose-700 font-semibold shadow-sm ring-1 ring-rose-200'
                            : 'text-t-secondary hover:bg-gray-50 hover:text-t-primary'
                    }`
                }
            >
                <HeartPulse size={20} />
                Saglik Kontrolu
            </NavLink>

            {/* Landing Link */}
            <NavLink
                to="/"
                end
                className={({ isActive }) =>
                    `flex items-center gap-3 px-4 py-3 rounded-lg text-base font-medium transition-all mb-4 ${
                        isActive
                            ? 'bg-brand-50 text-brand-700 font-semibold shadow-sm ring-1 ring-brand-200'
                            : 'text-t-secondary hover:bg-gray-50 hover:text-t-primary'
                    }`
                }
            >
                <LayoutGrid size={20} />
                Tum Senaryolar
            </NavLink>

            {/* Dynamic Navigation Groups */}
            {sidebarGroups.map(group => (
                <SidebarGroup
                    key={group.key}
                    group={group}
                    currentPath={location.pathname}
                />
            ))}
        </aside>
    );
};

const SidebarGroup = ({ group, currentPath }) => {
    const hasActive = group.scenarios.some(s => currentPath === `/scenarios/${s.id}`);
    const [expanded, setExpanded] = useState(hasActive);
    const Icon = iconMap[group.icon] || Globe;

    return (
        <nav className="mb-6">
            <button
                onClick={() => setExpanded(prev => !prev)}
                className="flex items-center justify-between w-full text-base uppercase text-t-muted font-bold tracking-wider mb-3 px-3 hover:text-t-primary transition-colors"
            >
                <span className="flex items-center gap-2">
                    <Icon size={16} />
                    {group.label}
                </span>
                {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
            </button>

            {expanded && (
                <>
                    {group.scenarios.map(scenario => (
                        <SidebarLink
                            key={scenario.id}
                            to={`/scenarios/${scenario.id}`}
                            code={formatCode(scenario.id)}
                            label={scenario.title}
                        />
                    ))}
                </>
            )}
        </nav>
    );
};

const SidebarLink = ({ to, code, label }) => (
    <NavLink
        to={to}
        className={({ isActive }) =>
            `flex items-center gap-3 px-4 py-1.5 rounded-lg text-base font-medium transition-all ${
                isActive
                    ? 'bg-brand-50 text-brand-700 font-semibold shadow-sm ring-1 ring-brand-200'
                    : 'text-t-secondary hover:bg-gray-50 hover:text-t-primary'
            }`
        }
    >
        <span className="font-mono text-xs text-brand-500 w-8">{code}</span>
        <span className="truncate">{label}</span>
    </NavLink>
);

export default Sidebar;
