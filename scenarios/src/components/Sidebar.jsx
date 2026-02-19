import React from 'react';
import { NavLink } from 'react-router-dom';
import { Home, ShoppingCart, Activity, Globe, ChevronRight } from 'lucide-react';

const Sidebar = () => {
    return (
        <aside className="w-72 bg-surface border-r border-gray-200 h-screen sticky top-0 flex flex-col p-6 shadow-sm overflow-y-auto">
            {/* Logo Area */}
            <div className="flex items-center gap-4 mb-10">
                <div className="w-12 h-12 bg-gradient-to-br from-brand-500 to-brand-700 rounded-xl flex items-center justify-center shadow-md">
                    <Home color="white" size={24} />
                </div>
                <h2 className="text-2xl font-bold text-t-primary tracking-tight">Invekto</h2>
            </div>

            {/* Navigation Group 1: E-Commerce */}
            <nav className="mb-8">
                <h3 className="text-sm uppercase text-t-muted font-bold tracking-wider mb-4 px-3">E-Ticaret (Niche 1)</h3>

                <SidebarLink to="/scenarios/s1" icon={ShoppingCart} active>
                    S1: Review Recovery
                </SidebarLink>

                <SidebarLink icon={ShoppingCart} disabled>
                    S2: Pre-Sale Q&A
                </SidebarLink>

                <SidebarLink icon={ShoppingCart} disabled>
                    S3: Return Deflection
                </SidebarLink>
            </nav>

            {/* Navigation Group 2: Health */}
            <nav className="mb-8">
                <h3 className="text-sm uppercase text-t-muted font-bold tracking-wider mb-4 px-3">Sağlık (Niche 2)</h3>
                <SidebarLink icon={Activity} disabled>
                    S6: Price Conversion
                </SidebarLink>
                <SidebarLink icon={Activity} disabled>
                    S7: No-Show Killer
                </SidebarLink>
            </nav>

            {/* Navigation Group 3: Universal */}
            <nav>
                <h3 className="text-sm uppercase text-t-muted font-bold tracking-wider mb-4 px-3">Evrensel</h3>
                <SidebarLink icon={Globe} disabled>
                    S11: Abonelik
                </SidebarLink>
            </nav>
        </aside>
    );
};

// Helper Component for consistent link styling
const SidebarLink = ({ to, icon: Icon, children, disabled = false, active = false }) => {
    // Bumped text size to text-base, adjusted padding
    const baseClasses = "flex items-center gap-3 px-4 py-3 rounded-lg text-base font-medium transition-all mb-1.5";

    // Disabled State
    if (disabled) {
        return (
            <div className={`${baseClasses} text-gray-400 cursor-not-allowed opacity-60`}>
                <Icon size={20} />
                {children}
            </div>
        );
    }

    // Active/Link State
    return (
        <NavLink
            to={to}
            className={({ isActive }) =>
                `${baseClasses} ${isActive
                    ? 'bg-brand-50 text-brand-700 font-semibold shadow-sm ring-1 ring-brand-200'
                    : 'text-t-secondary hover:bg-gray-50 hover:text-t-primary'
                }`
            }
        >
            <Icon size={20} />
            {children}
        </NavLink>
    );
};

export default Sidebar;
