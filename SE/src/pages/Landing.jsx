import React, { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { Zap, TrendingUp, Filter } from 'lucide-react';
import Badge from '../components/Badge';
import { allScenarios, sidebarGroups, stats } from '../data';

const sectorFilters = [
    { key: 'all', label: 'Tumu' },
    { key: 'ecommerce', label: 'E-Ticaret', niches: ['ecommerce'] },
    { key: 'health', label: 'Saglik', niches: ['health', 'dental', 'aesthetic'] },
    { key: 'hotel', label: 'Otel', niches: ['hotel'] },
    { key: 'beauty', label: 'Guzellik', niches: ['beauty'] },
    { key: 'education', label: 'Egitim', niches: ['education'] },
    { key: 'mobile', label: 'Mobil', niches: ['mobile'] },
    { key: 'universal', label: 'Evrensel', niches: ['universal', 'crossSector'] },
];

const nicheColors = {
    ecommerce: 'blue', health: 'green', dental: 'green', aesthetic: 'green',
    universal: 'purple', crossSector: 'purple', hotel: 'amber',
    beauty: 'pink', education: 'indigo', mobile: 'gray',
};
const nicheLabels = {
    ecommerce: 'E-Ticaret', health: 'Saglik', dental: 'Dis Klinigi',
    aesthetic: 'Estetik', universal: 'Evrensel', crossSector: 'Cross-Sector',
    hotel: 'Otel', beauty: 'Guzellik', education: 'Egitim', mobile: 'Mobil',
};

const Landing = () => {
    const navigate = useNavigate();
    const [activeSector, setActiveSector] = useState('all');
    const [searchQuery, setSearchQuery] = useState('');

    const filtered = useMemo(() => {
        let items = allScenarios;
        if (activeSector !== 'all') {
            const filter = sectorFilters.find(f => f.key === activeSector);
            if (filter?.niches) {
                items = items.filter(s => filter.niches.includes(s.niche));
            }
        }
        if (searchQuery.trim()) {
            const q = searchQuery.toLowerCase();
            items = items.filter(s =>
                s.title.toLowerCase().includes(q) ||
                s.description.toLowerCase().includes(q) ||
                s.id.toLowerCase().includes(q) ||
                (s.subtitle && s.subtitle.toLowerCase().includes(q))
            );
        }
        return items;
    }, [activeSector, searchQuery]);

    // Calculate total potential TL from all scenarios
    const totalPotentialTL = useMemo(() => {
        return allScenarios.reduce((sum, s) => {
            if (!s.impact?.formula || !s.impact?.inputs) return sum;
            try {
                let expr = s.impact.formula;
                s.impact.inputs.forEach(input => {
                    expr = expr.replace(new RegExp(`\\b${input.key}\\b`, 'g'), String(input.default));
                });
                return sum + Math.round(eval(expr));
            } catch {
                return sum;
            }
        }, 0);
    }, []);

    return (
        <div className="max-w-[1700px] mx-auto p-10 font-sans min-h-screen">
            {/* Hero */}
            <div className="mb-12">
                <div className="flex items-center gap-3 mb-4">
                    <Badge color="indigo">SENARYO KUTUPHANESI</Badge>
                    <Badge color="green">{stats.totalScenarios} Senaryo</Badge>
                    <Badge color="purple">{stats.sectors} Sektor</Badge>
                </div>
                <h1 className="text-5xl font-extrabold text-t-primary mb-4 tracking-tight">
                    Invekto Senaryo Explorer
                </h1>
                <p className="text-2xl text-t-secondary max-w-4xl font-light leading-relaxed">
                    {stats.totalScenarios} senaryo, {stats.sectors} sektor.
                    AI destekli musteri iletisimi ile her sektor icin hazir cozumler.
                </p>
            </div>

            {/* Stats Bar */}
            <div className="grid grid-cols-2 md:grid-cols-3 gap-6 mb-12">
                <StatCard
                    icon={<Zap size={24} className="text-brand-600" />}
                    label="Toplam Senaryo"
                    value={stats.totalScenarios}
                />
                <StatCard
                    icon={<TrendingUp size={24} className="text-emerald-600" />}
                    label="Sektor"
                    value={stats.sectors}
                    sub="Farkli saha"
                />
                <StatCard
                    icon={<TrendingUp size={24} className="text-emerald-600" />}
                    label="Toplam Potansiyel"
                    value={`${(totalPotentialTL / 1000).toFixed(0)}k TL`}
                    sub="Aylik (varsayilan degerlerle)"
                />
            </div>

            {/* Filter Bar */}
            <div className="flex flex-wrap items-center gap-4 mb-10 pb-6 border-b border-gray-200">
                <Filter size={20} className="text-t-muted" />
                {sectorFilters.map(f => (
                    <button
                        key={f.key}
                        onClick={() => setActiveSector(f.key)}
                        className={`px-5 py-2.5 rounded-lg text-base font-bold transition-all border ${
                            activeSector === f.key
                                ? 'bg-brand-600 text-white border-brand-600 shadow-md'
                                : 'bg-surface text-t-secondary border-brand-100 hover:bg-brand-50 hover:border-brand-300'
                        }`}
                    >
                        {f.label}
                    </button>
                ))}
                <input
                    type="text"
                    placeholder="Senaryo ara..."
                    value={searchQuery}
                    onChange={e => setSearchQuery(e.target.value)}
                    className="ml-auto px-4 py-2.5 rounded-lg border border-gray-200 text-base bg-surface focus:outline-none focus:ring-2 focus:ring-brand-300 focus:border-brand-400 w-72"
                />
            </div>

            {/* Scenarios Grid */}
            {filtered.length > 0 && (
                <section>
                    <h2 className="text-2xl font-bold text-t-primary mb-6 flex items-center gap-3">
                        <TrendingUp size={24} className="text-brand-600" />
                        Senaryolar ({filtered.length})
                    </h2>
                    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
                        {filtered.map(s => (
                            <ScenarioCard
                                key={s.id}
                                scenario={s}
                                onClick={() => navigate(`/scenarios/${s.id}`)}
                            />
                        ))}
                    </div>
                </section>
            )}

            {/* Empty State */}
            {filtered.length === 0 && (
                <div className="text-center py-20 text-t-muted">
                    <p className="text-xl">Aramanizla eslesen senaryo bulunamadi.</p>
                    <button
                        onClick={() => { setSearchQuery(''); setActiveSector('all'); }}
                        className="mt-4 text-brand-600 font-bold hover:underline"
                    >
                        Filtreleri temizle
                    </button>
                </div>
            )}
        </div>
    );
};

// Scenario card (unified for all 170 scenarios)
const ScenarioCard = ({ scenario, onClick }) => {
    const { id, title, subtitle, phase, niche, description, impact } = scenario;

    let defaultResult = 0;
    if (impact?.formula && impact?.inputs) {
        try {
            let expr = impact.formula;
            impact.inputs.forEach(input => {
                expr = expr.replace(new RegExp(`\\b${input.key}\\b`, 'g'), String(input.default));
            });
            defaultResult = Math.round(eval(expr));
        } catch { /* skip */ }
    }

    return (
        <div
            className="bg-surface rounded-xl shadow-sm border border-brand-100 p-6 hover:shadow-lg hover:border-brand-300 transition-all cursor-pointer group"
            onClick={onClick}
        >
            <div className="flex items-center gap-2 mb-3">
                <span className="text-sm font-mono font-bold text-brand-600 bg-brand-50 px-2 py-1 rounded">
                    {id.toUpperCase()}
                </span>
                {phase && <Badge color="blue">{phase}</Badge>}
                {niche && <Badge color={nicheColors[niche] || 'gray'}>{nicheLabels[niche] || niche}</Badge>}
            </div>
            <h3 className="text-xl font-bold text-t-primary mb-1 group-hover:text-brand-700 transition-colors">
                {title}
            </h3>
            {subtitle && <p className="text-sm text-t-muted font-mono mb-3">{subtitle}</p>}
            <p className="text-t-secondary text-base mb-4 leading-relaxed line-clamp-2">{description}</p>
            {defaultResult > 0 && (
                <div className="flex items-center gap-2 pt-3 border-t border-gray-100">
                    <TrendingUp size={16} className="text-emerald-600" />
                    <span className="text-sm font-bold text-emerald-700">
                        ~{defaultResult >= 1000 ? `${(defaultResult / 1000).toFixed(1)}k` : defaultResult} TL/ay potansiyel
                    </span>
                </div>
            )}
        </div>
    );
};

// Stat card
const StatCard = ({ icon, label, value, sub }) => (
    <div className="bg-surface rounded-xl border border-brand-100 p-5 shadow-sm">
        <div className="flex items-center gap-3 mb-2">
            {icon}
            <span className="text-sm font-bold text-t-muted uppercase tracking-wider">{label}</span>
        </div>
        <span className="text-3xl font-bold text-t-primary">{value}</span>
        {sub && <p className="text-sm text-t-muted mt-1">{sub}</p>}
    </div>
);

export default Landing;
