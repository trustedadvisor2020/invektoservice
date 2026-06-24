import React from 'react';
import { useParams, Navigate, Link } from 'react-router-dom';
import { MessageCircle, ArrowRight, ArrowLeft, Zap } from 'lucide-react';
import Badge from '../components/Badge';
import FlatCard from '../components/FlatCard';
import { fieldScenarioById } from '../data';

const capabilityLabels = {
    C1: 'Unified Inbox', C2: 'Routing', C3: 'Templates', C4: 'Reporting',
    C5: 'Security Baseline', C6: 'Enterprise Security', C7: 'Knowledge/RAG',
    C8: 'Agent Assist', C9: 'Auto-Resolution', C10: 'Revenue Agent',
    C11: 'E-com Integrations', C12: 'Ads Attribution', C13: 'QA Mining',
};

const capabilityColors = {
    C1: 'blue', C2: 'blue', C3: 'blue', C4: 'purple',
    C5: 'rose', C6: 'rose', C7: 'green', C8: 'amber',
    C9: 'gray', C10: 'purple', C11: 'amber', C12: 'purple', C13: 'gray',
};

const FieldScenarioDetail = () => {
    const { id } = useParams();
    const scenario = fieldScenarioById[id];

    if (!scenario) {
        return <Navigate to="/" replace />;
    }

    const { id: sid, name, description, customerMessage, solution, capabilities = [], phase } = scenario;

    return (
        <div className="max-w-[1200px] mx-auto p-10 font-sans min-h-screen">
            {/* Back */}
            <Link to="/" className="inline-flex items-center gap-2 text-brand-600 hover:text-brand-800 font-medium mb-8 transition-colors">
                <ArrowLeft size={18} />
                Tum Senaryolar
            </Link>

            {/* Header */}
            <div className="mb-10">
                <div className="flex items-center gap-3 mb-4">
                    <span className="text-sm font-mono font-bold text-brand-600 bg-brand-50 px-3 py-1.5 rounded-lg">{sid}</span>
                    {phase && <Badge color="blue">{phase}</Badge>}
                    <Badge color="gray">Saha Senaryosu</Badge>
                </div>
                <h1 className="text-4xl font-extrabold text-t-primary mb-4 tracking-tight">{name}</h1>
                <p className="text-xl text-t-secondary max-w-3xl font-light leading-relaxed">{description}</p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                {/* Customer Message */}
                {customerMessage && (
                    <FlatCard title="Musteri Mesaji" icon={MessageCircle}>
                        <div className="bg-[#d9fdd3] rounded-xl p-5 flex items-start gap-3">
                            <MessageCircle size={20} className="text-emerald-600 mt-0.5 flex-shrink-0" />
                            <span className="text-base text-gray-800 italic leading-relaxed">"{customerMessage}"</span>
                        </div>
                    </FlatCard>
                )}

                {/* Chatinbox Solution */}
                {solution && (
                    <FlatCard title="Chatinbox Cozumu" icon={Zap}>
                        <div className="bg-brand-50 rounded-xl p-5 flex items-start gap-3">
                            <ArrowRight size={20} className="text-brand-600 mt-0.5 flex-shrink-0" />
                            <span className="text-base text-brand-900 leading-relaxed">{solution}</span>
                        </div>
                    </FlatCard>
                )}
            </div>

            {/* Capabilities */}
            {capabilities.length > 0 && (
                <FlatCard title="Gereken Yetenekler (Capabilities)" icon={Zap} className="mt-8">
                    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
                        {capabilities.map((cap, i) => (
                            <div key={i} className="flex items-center gap-3 p-3 rounded-lg bg-gray-50 border border-gray-100">
                                <Badge color={capabilityColors[cap] || 'gray'}>{cap}</Badge>
                                <span className="text-sm text-t-secondary">{capabilityLabels[cap] || cap}</span>
                            </div>
                        ))}
                    </div>
                </FlatCard>
            )}
        </div>
    );
};

export default FieldScenarioDetail;
