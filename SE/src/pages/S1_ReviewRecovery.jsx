import React, { useState } from 'react';
import { ArrowRight, AlertTriangle, CheckCircle, Smartphone, Database, Server, User, BookOpen, Info, MessageCircle, Cpu, Zap, ChevronRight, Check } from 'lucide-react';

// --- Local Components Matching Style Guide ---

// 1. Callout (Info/Warning Box)
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

// 2. Badge
const Badge = ({ children, color = 'gray' }) => {
    const colors = {
        gray: 'bg-gray-100 text-t-secondary',
        blue: 'bg-brand-100 text-brand-700',
        green: 'bg-emerald-100 text-emerald-700',
        amber: 'bg-amber-100 text-amber-700',
        indigo: 'bg-brand-100 text-brand-700',
        purple: 'bg-purple-100 text-purple-700',
    };
    // Increased to text-sm
    return (
        <span className={`inline-flex items-center px-3 py-1 rounded-md text-sm font-medium font-mono ${colors[color] || colors.gray}`}>
            {children}
        </span>
    );
};

// 3. Step (Numbered Steps)
const Step = ({ number, title, goal, children }) => {
    return (
        <div className="flex gap-5 relative pb-10 last:pb-0 group">
            {/* Connector Line */}
            <div className="absolute left-[18px] top-10 bottom-0 w-0.5 bg-gray-200 group-last:hidden"></div>

            {/* Number Circle */}
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

// 4. FlatCard (Feature/Option Card)
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


const S1_ReviewRecovery = () => {
    const [activeTab, setActiveTab] = useState('overview');
    const [activeFlow, setActiveFlow] = useState(0);

    // Interactive ROI Calculator
    const [dailyOrders, setDailyOrders] = useState(200);
    const [errorRate, setErrorRate] = useState(0.5);
    const [avgBasket, setAvgBasket] = useState(1200);
    const affectedPerReview = 4;
    const dailyNegReviews = Math.max(1, Math.round(dailyOrders * (errorRate / 100)));
    const monthlyImpact = dailyNegReviews * affectedPerReview * avgBasket * 30;
    const recoveredMonthly = Math.round(monthlyImpact * 0.3);
    const roi = Math.round((recoveredMonthly / 5000) * 10) / 10;

    const flows = [
        {
            id: 'flow-1',
            title: 'Geç Teslimat / Hasarlı Ürün',
            description: 'Müşteri kargodan şikayetçi. Ürün kırık geldi.',
            requirements: {
                client: ['Yedek ürün stoğu', 'Hızlı kargo anlaşması (MNG/Yurtiçi VIP)'],
                provider: ['Hasar tanıma promptu (Image Analysis opsiyonel)']
            },
            steps: [
                { role: 'system', content: 'Trendyol API: 1 Yıldız Yorum Tespit Edildi ("Ürün kırık geldi, rezalet")' },
                { role: 'ai', content: 'Analiz: Negatif - Kargo Hasarı. Aksiyon: Telafi Modu.' },
                { role: 'bot', content: 'Merhaba [Ad], siparişinizin hasarlı ulaştığını üzülerek öğrendik. 😔 Hemen telafi etmek istiyoruz. Kırık ürünün fotoğrafını iletirseniz anında yenisini gönderelim.' },
                { role: 'user', content: '[Fotoğraf gönderir]' },
                { role: 'bot', content: 'Teşekkürler. Yeni ürününüzü Yurt içi Kargo [Kod] ile kargoladık. Bu aksaklık için özür dileriz.' },
                { role: 'system', content: 'T+3 Gün: Teslimat Başarılı.' },
                { role: 'bot', content: 'Merhaba, yeni ürününüz sağlam ulaştı mı? Memnun kaldıysanız yorumunuzu güncelleyerek bize destek olabilirsiniz. 🙏' }
            ]
        },
        {
            id: 'flow-2',
            title: 'Yanlış Beden / Ürün',
            description: 'Müşteri yanlış ürün geldiğini belirtmiş.',
            requirements: {
                client: ['Beden Tablosu Linki', 'Esnek İade Politikası Onayı'],
                provider: ['Beden eşleştirme mantığı (L -> XL dönüşümü)']
            },
            steps: [
                { role: 'system', content: 'Trendyol API: 2 Yıldız Yorum ("XL istedim L geldi")' },
                { role: 'ai', content: 'Analiz: Operasyon Hatası. Aksiyon: Değişim.' },
                { role: 'bot', content: 'Selamlar [Ad], bir karışıklık olmuş, çok üzgünüz! Yanlış ürünü geri göndermenize gerek yok, size hediye kalsın. Doğru bedeni hemen kargoluyoruz. 🎁' },
                { role: 'user', content: 'Gerçekten mi? Çok teşekkürler.' },
                { role: 'bot', content: 'Rica ederiz, asıl biz mahcubuz. Memnuniyetiniz bizim için önemli. (Yorum revize ricası)' }
            ]
        },
        {
            id: 'flow-3',
            title: 'Ürün Orijinallik Şüphesi',
            description: 'Müşteri ürünün sahte olduğunu iddia ediyor.',
            requirements: {
                client: ['Yetkili Satıcı Belgesi (PDF)', 'Fatura Silsilesi', 'ÜTS Sorgu Linki'],
                provider: ['Belge gönderme servisi (Media Message)']
            },
            steps: [
                { role: 'system', content: 'Trendyol API: 1 Yıldız ("Ürün sahte, barkodu okunmuyor")' },
                { role: 'ai', content: 'Analiz: Güven Sorunu (Kritik). Aksiyon: Kanıt Sunma.' },
                { role: 'bot', content: 'Merhaba [Ad], endişenizi anlıyoruz. Ürünlerimiz %100 orijinaldir ve distribütör garantilidir. İşte yetkili satıcı belgemiz ve ürünün fatura silsilesi: [Belge.pdf]. Barkodu ÜTS sisteminden şu linkten sorgulayabilirsiniz.' },
                { role: 'user', content: 'Tamamdır kontrol ettim, sorun yokmuş.' },
                { role: 'bot', content: 'İçiniz rahat olsun. Yanlış anlaşılmayı düzeltmek adına yorumunuzu güncellerseniz çok seviniriz.' }
            ]
        },
        {
            id: 'flow-4',
            title: 'Memnuniyetsizlik',
            description: 'Müşteri ürünü kullanamadı, bozuk sandı.',
            requirements: {
                client: ['Kullanım Kılavuzu Videosu (Youtube/Drive)', 'SSS Dokümanı'],
                provider: ['Video linki veritabanı']
            },
            steps: [
                { role: 'system', content: 'Trendyol API: 2 Yıldız ("Çalışmıyor, bozuk")' },
                { role: 'ai', content: 'Analiz: Kullanım Hatası İhtimali. Aksiyon: Eğitim.' },
                { role: 'bot', content: 'Merhaba, ürünle ilgili sorun yaşadığınızı gördük. Genellikle [Düğme]ye basılı tutulmadığında bu durum yaşanabiliyor. Şu kısa videomuzu izleyip dener misiniz? [Video Link]' },
                { role: 'user', content: 'Ah evet, şimdi çalıştı. Teşekkürler.' },
                { role: 'bot', content: 'Harika! Keyifli kullanımlar dileriz. Yıldız sayımızı yükseltirseniz bizi çok mutlu edersiniz. ⭐' }
            ]
        },
        {
            id: 'flow-5',
            title: 'Rakip / Spam',
            description: 'Gerçek olmayan, tutarsız sipariş/yorum.',
            requirements: {
                client: ['Satıcı Destek Ekibi İrtibatı'],
                provider: ['Anomali tespit algoritması (İsim eşleşmeme kontrolü)']
            },
            steps: [
                { role: 'system', content: 'Trendyol API: 1 Yıldız ("Berbat sakın almayın")' },
                { role: 'ai', content: 'Analiz: Sipariş eşleşmedi / Anomali. Aksiyon: İnsan Kontrolü.' },
                { role: 'system', content: 'ALERT: Eşleşmeyen telefondan/kişiden yorum. Agent incelemesi gerekli.' },
                { role: 'agent', content: 'Manuel Kontrol: Sipariş no yok. Trendyol Satıcı Desteğe "Haksız Rekabet" bildirimi açıldı.' }
            ]
        }
    ];

    return (
        <div className="max-w-[1700px] mx-auto p-10 font-sans bg-gray-50/50 min-h-screen">
            {/* Header Section */}
            <div className="mb-12">
                <div className="flex items-center gap-3 mb-5">
                    <Badge color="blue">PHASE 3</Badge>
                    <Badge color="green">E-TICARET OTOMASYONU</Badge>
                </div>

                <h1 className="text-5xl font-extrabold text-t-primary mb-6 tracking-tight">
                    S1: Negatif Yorum Kurtarma
                </h1>

                <p className="text-2xl text-t-secondary max-w-5xl font-light leading-relaxed">
                    Pazaryerlerinde (Trendyol, Hepsiburada) düşük puanlı yorumları anında tespit edip,
                    müşteriyle WhatsApp üzerinden iletişime geçerek sorunu çözen otonom sistem.
                </p>
            </div>

            {/* Main Tabs */}
            <div className="flex gap-2 mb-10 border-b border-gray-200">
                <button
                    className={`px-8 py-4 font-bold text-base transition-colors border-b-4 ${activeTab === 'overview' ? 'border-brand-600 text-brand-700' : 'border-transparent text-t-muted hover:text-t-primary'}`}
                    onClick={() => setActiveTab('overview')}
                >
                    GENEL BAKIŞ
                </button>
                <button
                    className={`px-8 py-4 font-bold text-base transition-colors border-b-4 ${activeTab === 'scenarios' ? 'border-brand-600 text-brand-700' : 'border-transparent text-t-muted hover:text-t-primary'}`}
                    onClick={() => setActiveTab('scenarios')}
                >
                    SENARYO AKIŞLARI ({flows.length})
                </button>
                <button
                    className={`px-8 py-4 font-bold text-base transition-colors border-b-4 ${activeTab === 'tech' ? 'border-brand-600 text-brand-700' : 'border-transparent text-t-muted hover:text-t-primary'}`}
                    onClick={() => setActiveTab('tech')}
                >
                    TEKNİK DETAYLAR
                </button>
            </div>

            {activeTab === 'overview' && (
                <div className="space-y-10 animate-fade-in">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-10">
                        <FlatCard title="Hedef Kitle & Sektör" icon={User}>
                            <ul className="space-y-4">
                                <li className="flex items-start gap-4 text-t-secondary text-lg">
                                    <CheckCircle size={24} className="text-emerald-500 mt-0.5" />
                                    <span><strong>Sektör:</strong> E-Ticaret (Pazaryeri Satıcıları)</span>
                                </li>
                                <li className="flex items-start gap-4 text-t-secondary text-lg">
                                    <CheckCircle size={24} className="text-emerald-500 mt-0.5" />
                                    <span><strong>Platformlar:</strong> Trendyol, Hepsiburada, Amazon TR</span>
                                </li>
                                <li className="flex items-start gap-4 text-t-secondary text-lg">
                                    <CheckCircle size={24} className="text-emerald-500 mt-0.5" />
                                    <span><strong>Hedef:</strong> Yüksek hacimli (günlük 50+ kargo) satıcılar</span>
                                </li>
                            </ul>
                        </FlatCard>

                        <FlatCard title="Entegre Servisler" icon={Database}>
                            <div className="flex flex-wrap gap-3">
                                <Badge color="amber">Trendyol API</Badge>
                                <Badge color="green">WhatsApp Business API</Badge>
                                <Badge color="blue">AI Sentiment Engine</Badge>
                                <Badge color="purple">CRM Database</Badge>
                            </div>
                            <div className="mt-6 text-base text-t-muted">
                                Bu servisler birbirleriyle asenkron olarak haberleşir ve tam otonom bir yapı oluşturur.
                            </div>
                        </FlatCard>
                    </div>

                    <FlatCard title="Sistem Çalışma Mantığı" icon={Zap} className="border-l-4 border-brand-500">
                        <div className="mt-4 space-y-4">
                            <Step number={1} title="Tespit (Detection)" goal="Negatif yorumu yakala">
                                <div className="flex gap-2 mb-2">
                                    <Badge color="purple">Sistem (Otomatik)</Badge>
                                    <Badge color="amber">Tenant (API Key)</Badge>
                                </div>
                                Trendyol API <code className="font-mono text-sm bg-gray-100 px-1 py-0.5 rounded">/products/reviews</code> endpoint'i her 15 dakikada bir taranır. 1-3 yıldızlı yorumlar filtrelenir.
                            </Step>
                            <Step number={2} title="Eşleştirme (Matching)" goal="Müşteri telefonunu bul">
                                <div className="flex gap-2 mb-2">
                                    <Badge color="purple">Sistem (Algoritma)</Badge>
                                    <Badge color="blue">Tenant (Veri Kaynağı)</Badge>
                                </div>
                                Yorum yapan kişinin ismi ve tarihi ile sipariş veritabanı taranır. Fuzzy matching ile doğru telefon numarası tespit edilir.
                            </Step>
                            <Step number={3} title="Analiz (Decision)" goal="Doğru aksiyonu planla">
                                <div className="flex gap-2 mb-2">
                                    <Badge color="purple">Sistem (AI Model)</Badge>
                                    <Badge color="amber">Tenant (Politika Dosyası)</Badge>
                                </div>
                                AI, yorumun içeriğini okur (Kargo mu? Ürün mü?) ve iade politikasına göre en uygun çözüm önerisini (İade/Değişim/Telafi) seçer.
                            </Step>
                            <Step number={4} title="Aksiyon (Execution)" goal="Müşteriyi geri kazan">
                                <div className="flex gap-2 mb-2">
                                    <Badge color="purple">Sistem (Otonom)</Badge>
                                    <Badge color="green">Tenant (WhatsApp Hattı)</Badge>
                                </div>
                                WhatsApp üzerinden müşteriye ulaşılır, sorun çözülür ve yorumun güncellenmesi rica edilir.
                            </Step>
                        </div>
                    </FlatCard>
                </div>
            )}

            {activeTab === 'scenarios' && (
                <div className="animate-fade-in">
                    {/* Sub-Navigation (Scenario Buttons) */}
                    <div className="flex flex-wrap gap-4 mb-10">
                        {flows.map((flow, idx) => (
                            <button
                                key={flow.id}
                                onClick={() => setActiveFlow(idx)}
                                className={`px-5 py-3 rounded-xl text-base font-bold transition-all shadow-sm border ${activeFlow === idx
                                    ? 'bg-brand-600 text-white border-brand-600 shadow-lg ring-4 ring-brand-100'
                                    : 'bg-surface text-t-secondary border-brand-100 hover:bg-brand-50 hover:border-brand-300'
                                    }`}
                            >
                                {idx + 1}. {flow.title}
                            </button>
                        ))}
                    </div>

                    {/* Scenario Details Panel */}
                    <div className="grid grid-cols-1 lg:grid-cols-3 gap-10">

                        {/* Left Column: Details & Requirements */}
                        <div className="lg:col-span-1 space-y-8">
                            <FlatCard title="Senaryo Detayı" icon={Smartphone} className="bg-brand-50/50 border-brand-100">
                                <h3 className="text-2xl font-bold text-t-primary mb-3">{flows[activeFlow].title}</h3>
                                <p className="text-t-secondary text-base leading-relaxed mb-6">{flows[activeFlow].description}</p>
                                <div className="flex gap-3">
                                    <Badge color="blue">Otomatik Yanıt</Badge>
                                    <Badge color="green">WhatsApp</Badge>
                                </div>
                            </FlatCard>

                            <FlatCard title="Gereksinimler" icon={Info}>
                                <div className="space-y-6">
                                    <div>
                                        <span className="text-sm font-bold text-t-muted uppercase tracking-wider block mb-3">Satıcı Tarafı</span>
                                        <ul className="space-y-3">
                                            {flows[activeFlow].requirements.client.map((req, i) => (
                                                <li key={i} className="text-base text-t-secondary flex items-start gap-3">
                                                    <div className="w-2 h-2 rounded-full bg-brand-400 mt-2 flex-shrink-0"></div>
                                                    {req}
                                                </li>
                                            ))}
                                        </ul>
                                    </div>
                                    <div className="pt-6 border-t border-brand-50">
                                        <span className="text-sm font-bold text-t-muted uppercase tracking-wider block mb-3">Sistem Tarafı</span>
                                        <ul className="space-y-3">
                                            {flows[activeFlow].requirements.provider.map((req, i) => (
                                                <li key={i} className="text-base text-t-secondary flex items-start gap-3">
                                                    <div className="w-2 h-2 rounded-full bg-emerald-400 mt-2 flex-shrink-0"></div>
                                                    {req}
                                                </li>
                                            ))}
                                        </ul>
                                    </div>
                                </div>
                            </FlatCard>
                        </div>

                        {/* Right Column: Chat/Flow Visualization */}
                        <div className="lg:col-span-2">
                            <div className="bg-gray-100 rounded-2xl overflow-hidden border border-brand-100 shadow-sm flex flex-col h-[800px]">
                                {/* Chat Header */}
                                <div className="bg-surface p-5 border-b border-brand-100 flex items-center justify-between shadow-sm z-10">
                                    <div className="flex items-center gap-4">
                                        <div className="w-12 h-12 rounded-full bg-emerald-500 overflow-hidden flex items-center justify-center text-white font-bold">
                                            <Smartphone size={24} />
                                        </div>
                                        <div>
                                            <h4 className="font-bold text-t-primary text-base">Destek Asistanı</h4>
                                            <span className="text-sm text-emerald-600 font-medium">Çevrimiçi</span>
                                        </div>
                                    </div>
                                    <Badge color="gray">Canlı Önizleme</Badge>
                                </div>

                                {/* Chat Content */}
                                <div className="flex-1 p-8 overflow-y-auto space-y-8 bg-gray-50">
                                    {flows[activeFlow].steps.map((step, idx) => {
                                        // Render System/AI messages as specialized dividers
                                        if (step.role === 'system' || step.role === 'ai') {
                                            return (
                                                <div key={idx} className="flex justify-center my-4">
                                                    <div className={`
                                max-w-[90%] px-4 py-2 rounded-lg text-sm font-bold text-center border shadow-sm flex items-center gap-2
                                ${step.role === 'ai' ? 'bg-brand-100 text-brand-800 border-brand-200' : 'bg-amber-50 text-amber-800 border-amber-200'}
                              `}>
                                                        {step.role === 'ai' ? <Cpu size={16} /> : <Server size={16} />}
                                                        {step.content}
                                                    </div>
                                                </div>
                                            );
                                        }
                                        // Render Chat Bubbles
                                        return (
                                            <div key={idx} className={`flex w-full ${step.role === 'user' ? 'justify-end' : 'justify-start'}`}>
                                                <div className={`
                              max-w-[80%] p-4 rounded-xl text-base shadow-sm relative leading-relaxed
                              ${step.role === 'user' ? 'bg-[#d9fdd3] text-gray-900 rounded-tr-none' : 'bg-white text-gray-900 rounded-tl-none'}
                            `}>
                                                    {step.content}
                                                    <span className="block text-xs text-gray-400 text-right mt-2">
                                                        14:{30 + idx} {step.role === 'user' && '✓✓'}
                                                    </span>

                                                    {/* Tail SVG */}
                                                    <svg className={`absolute top-0 w-4 h-4 ${step.role === 'user' ? '-right-3 fill-[#d9fdd3]' : '-left-3 fill-white'}`} viewBox="0 0 10 10">
                                                        <path d={step.role === 'user' ? "M0 0 L10 0 L0 10 Z" : "M0 0 L10 0 L10 10 Z"} />
                                                    </svg>
                                                </div>
                                            </div>
                                        );
                                    })}
                                </div>

                                {/* Chat Input Placeholder */}
                                <div className="p-4 bg-gray-50 border-t border-gray-200 flex items-center gap-3">
                                    <div className="p-2 text-gray-400 hover:text-gray-600 cursor-pointer"><Zap size={24} /></div>
                                    <div className="flex-1 bg-white border border-gray-200 rounded-full px-6 py-3 text-base text-gray-400 shadow-sm font-sans">
                                        Mesaj yazın...
                                    </div>
                                    <div className="p-2 text-gray-400 hover:text-gray-600 cursor-pointer"><Smartphone size={24} /></div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {activeTab === 'tech' && (
                <div className="animate-fade-in space-y-10">
                    <Callout type="info" title="Teknik Not">
                        Bu senaryo tamamen asenkron çalışır. Node.js backend üzerinde çalışan Cron Job modülleri ve Queue sistemi (Redis) ile yönetilir.
                    </Callout>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-10">
                        <FlatCard title="Backend Servisleri" icon={Server}>
                            <div className="space-y-6">
                                <Step number="A" title="Cron Job Service" goal="Zamanlı Görevler">
                                    Node.js servisi, her 15 dakikada bir çalışarak yeni verileri çeker.
                                </Step>
                                <Step number="B" title="Queue Manager" goal="Yük Dengeleme">
                                    Redis kuyruk sistemi ile anlık binlerce yorum gelse bile sistem çökmeden sırayla işler.
                                </Step>
                            </div>
                        </FlatCard>

                        <FlatCard title="API Entegrasyonları" icon={Database}>
                            <div className="space-y-6">
                                <Step number="A" title="Trendyol Seller API" goal="Veri Kaynağı">
                                    Oauth2 protokolü ile güvenli bağlantı.
                                </Step>
                                <Step number="B" title="OpenAI GPT-4" goal="Zeka & Karar">
                                    Sentiment analysis ve cevap üretimi için kullanılır.
                                </Step>
                            </div>
                        </FlatCard>
                    </div>
                </div>
            )}

            {/* Footer Impact Stats — Interactive ROI Calculator */}
            <div className="mt-16">
                <FlatCard className="bg-emerald-50/80 border-emerald-100">
                    <div className="mb-8">
                        <div className="flex items-center gap-3 mb-2">
                            <h2 className="text-3xl font-bold text-emerald-900">
                                Potansiyel Aylık Kayıp: ~{monthlyImpact.toLocaleString('tr-TR')} TL
                            </h2>
                            <span className="text-xs font-mono bg-emerald-200 text-emerald-800 px-2 py-1 rounded">CANLI HESAPLAMA</span>
                        </div>
                        <p className="text-emerald-700 text-lg">
                            Aşağıdaki değerleri <strong>kendi işletmenize göre düzenleyin</strong> — 1 negatif yorumun potansiyel 4 müşteriyi kaçırdığı varsayımıyla hesaplanır.
                        </p>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-8 pt-8 border-t border-emerald-200">
                        <div>
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">Günlük Sipariş</span>
                            <input
                                type="number"
                                value={dailyOrders}
                                onChange={(e) => setDailyOrders(Math.max(1, Number(e.target.value) || 1))}
                                className="block text-3xl font-bold text-emerald-900 bg-transparent border-b-2 border-dashed border-emerald-400 w-24 text-center focus:outline-none focus:border-emerald-600 transition-colors"
                            />
                            <span className="text-emerald-700/80 text-sm mt-1 block">Ortalama kargo sayısı</span>
                        </div>

                        <div>
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">Hata Oranı</span>
                            <div className="flex items-baseline gap-1">
                                <span className="text-3xl font-bold text-emerald-900">%</span>
                                <input
                                    type="number"
                                    value={errorRate}
                                    step="0.1"
                                    onChange={(e) => setErrorRate(Math.max(0.1, Number(e.target.value) || 0.1))}
                                    className="text-3xl font-bold text-emerald-900 bg-transparent border-b-2 border-dashed border-emerald-400 w-16 text-center focus:outline-none focus:border-emerald-600 transition-colors"
                                />
                            </div>
                            <span className="text-emerald-700/80 text-sm mt-1 block">→ {dailyNegReviews} yorum/gün</span>
                        </div>

                        <div>
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">Sepet Ortalaması</span>
                            <div className="flex items-baseline gap-1">
                                <input
                                    type="number"
                                    value={avgBasket}
                                    onChange={(e) => setAvgBasket(Math.max(0, Number(e.target.value) || 0))}
                                    className="text-3xl font-bold text-emerald-900 bg-transparent border-b-2 border-dashed border-emerald-400 w-24 text-center focus:outline-none focus:border-emerald-600 transition-colors"
                                />
                                <span className="text-3xl font-bold text-emerald-900">₺</span>
                            </div>
                            <span className="text-emerald-700/80 text-sm mt-1 block">× 4 etkilenen müşteri</span>
                        </div>

                        <div>
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">Aylık Kayıp</span>
                            <span className="block text-3xl font-bold text-rose-600">{(monthlyImpact / 1000).toFixed(0)}k ₺</span>
                            <span className="text-emerald-700/80 text-sm mt-1 block">Kurtarılmazsa kaybedilen</span>
                        </div>

                        <div className="bg-emerald-100/50 rounded-lg p-4 -m-2">
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">Kurtarılan (%30)</span>
                            <span className="block text-3xl font-bold text-emerald-900">{(recoveredMonthly / 1000).toFixed(1)}k ₺</span>
                            <span className="text-emerald-700/80 text-sm mt-1 block">{roi}x ROI (5.000₺ abonelik)</span>
                        </div>
                    </div>

                    <div className="mt-6 pt-4 border-t border-emerald-200">
                        <p className="text-emerald-600 text-sm font-mono">
                            Formül: {dailyNegReviews} yorum/gün × 4 etkilenen × {avgBasket.toLocaleString('tr-TR')}₺ sepet × 30 gün = <strong>{monthlyImpact.toLocaleString('tr-TR')} ₺ kayıp</strong> → %30 kurtarma = <strong>{recoveredMonthly.toLocaleString('tr-TR')} ₺/ay</strong>
                        </p>
                    </div>
                </FlatCard>
            </div>
        </div>
    );
};

export default S1_ReviewRecovery;
