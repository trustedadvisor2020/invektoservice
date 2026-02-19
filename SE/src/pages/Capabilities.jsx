import React, { useEffect } from 'react';
import { useLocation, Link } from 'react-router-dom';
import { ArrowLeft, Shield, MessageSquare, GitBranch, FileText, BarChart3, Lock, BookOpen, Bot, Zap, TrendingUp, ShoppingCart, Target, Search } from 'lucide-react';
import Badge from '../components/Badge';
import FlatCard from '../components/FlatCard';

const capabilities = [
    { id: 'C1', name: 'Unified Inbox', icon: MessageSquare, color: 'blue', description: 'Tum iletisim kanallarini (WhatsApp, Instagram, Web Chat, E-posta) tek bir panelden yonetin. Musteri gecmisi ve baglam her kanalda korunur.' },
    { id: 'C2', name: 'Routing', icon: GitBranch, color: 'blue', description: 'Gelen mesajlari departman, konu, oncelik veya AI analizine gore dogru kisiye/ekibe otomatik yonlendirin.' },
    { id: 'C3', name: 'Templates', icon: FileText, color: 'blue', description: 'Hazir mesaj sablonlari ve hizli yanitlar. WhatsApp onayili sablonlar, degisken destegi ve A/B test imkani.' },
    { id: 'C4', name: 'Reporting', icon: BarChart3, color: 'purple', description: 'Cevap suresi, cozum orani, musteri memnuniyeti, ajan performansi ve kanal bazli analitik raporlar.' },
    { id: 'C5', name: 'Security Baseline', icon: Shield, color: 'rose', description: 'Temel guvenlik katmani: uctan uca sifreleme, rol tabanli yetkilendirme, veri maskeleme ve KVKK uyumu.' },
    { id: 'C6', name: 'Enterprise Security', icon: Lock, color: 'rose', description: 'Kurumsal guvenlik: SSO/SAML entegrasyonu, detayli audit log, IP whitelist, 2FA ve veri izolasyonu.' },
    { id: 'C7', name: 'Knowledge / RAG', icon: BookOpen, color: 'green', description: 'Bilgi bankasi ve RAG (Retrieval-Augmented Generation). Dokumanlari, SSS ve urun bilgilerini yukleyin — AI otomatik olarak dogru cevabi bulur.' },
    { id: 'C8', name: 'Agent Assist', icon: Bot, color: 'amber', description: 'AI destekli ajan yardimcisi. Canli gorüsmelerde oneriler sunar, cevap taslagi hazirlar ve bilgi bankasinda arama yapar.' },
    { id: 'C9', name: 'Auto-Resolution', icon: Zap, color: 'gray', description: 'Tam otonom sorun cozme. Basit ve tekrarlayan talepleri insan mudahalesi olmadan AI ile cozumleyin.' },
    { id: 'C10', name: 'Revenue Agent', icon: TrendingUp, color: 'purple', description: 'Gelir odakli otonom ajan. Satis firsatlarini tespit eder, cross-sell/upsell onerileri sunar ve doviz/fiyat hesaplamalari yapar.' },
    { id: 'C11', name: 'E-com Integrations', icon: ShoppingCart, color: 'amber', description: 'E-ticaret platform entegrasyonlari: Trendyol, Hepsiburada, Amazon TR, Shopify. Siparis, stok ve kargo verilerini senkronize edin.' },
    { id: 'C12', name: 'Ads Attribution', icon: Target, color: 'purple', description: 'Reklam atribusyon ve ROI takibi. Hangi reklam kampanyasindan gelen musterinin ne kadar gelir getirdigini olcun.' },
    { id: 'C13', name: 'QA Mining', icon: Search, color: 'gray', description: 'Kalite guvence ve anomali tespiti. Gorusmelerdeki kalite sorunlarini, tutarsizliklari ve haksiz degerlendirmeleri otomatik tespit edin.' },
];

const capabilityColors = {
    blue: 'bg-brand-100 text-brand-700 border-brand-200',
    purple: 'bg-purple-100 text-purple-700 border-purple-200',
    rose: 'bg-rose-100 text-rose-700 border-rose-200',
    green: 'bg-emerald-100 text-emerald-700 border-emerald-200',
    amber: 'bg-amber-100 text-amber-700 border-amber-200',
    gray: 'bg-gray-100 text-t-secondary border-gray-200',
};

const Capabilities = () => {
    const location = useLocation();

    useEffect(() => {
        if (location.hash) {
            const el = document.getElementById(location.hash.slice(1));
            if (el) {
                el.scrollIntoView({ behavior: 'smooth', block: 'center' });
                el.classList.add('ring-2', 'ring-brand-400');
                setTimeout(() => el.classList.remove('ring-2', 'ring-brand-400'), 2000);
            }
        }
    }, [location.hash]);

    return (
        <div className="max-w-[1400px] mx-auto p-10 font-sans min-h-screen">
            <Link to="/" className="inline-flex items-center gap-2 text-brand-600 hover:text-brand-800 font-medium mb-8 transition-colors">
                <ArrowLeft size={18} />
                Tum Senaryolar
            </Link>

            <div className="mb-12">
                <div className="flex items-center gap-3 mb-4">
                    <Badge color="indigo">REFERANS</Badge>
                    <Badge color="green">{capabilities.length} Yetenek</Badge>
                </div>
                <h1 className="text-5xl font-extrabold text-t-primary mb-4 tracking-tight">
                    Platform Yetenekleri (Capabilities)
                </h1>
                <p className="text-2xl text-t-secondary max-w-4xl font-light leading-relaxed">
                    Invekto platformunun C1-C13 temel yetenekleri. Her senaryo bu yeteneklerin bir kombinasyonunu kullanir.
                </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
                {capabilities.map(cap => {
                    const Icon = cap.icon;
                    const colorClasses = capabilityColors[cap.color] || capabilityColors.gray;
                    return (
                        <div
                            key={cap.id}
                            id={cap.id}
                            className={`bg-surface rounded-xl shadow-sm border p-6 transition-all ${colorClasses.split(' ').pop()}`}
                        >
                            <div className="flex items-center gap-3 mb-4">
                                <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${colorClasses.split(' ').slice(0, 2).join(' ')}`}>
                                    <Icon size={20} />
                                </div>
                                <div>
                                    <span className="text-sm font-mono font-bold text-t-muted">{cap.id}</span>
                                    <h3 className="text-lg font-bold text-t-primary">{cap.name}</h3>
                                </div>
                            </div>
                            <p className="text-t-secondary text-base leading-relaxed">{cap.description}</p>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};

export default Capabilities;
