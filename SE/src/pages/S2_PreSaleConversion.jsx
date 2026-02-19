import React, { useState } from 'react';
import { ArrowRight, AlertTriangle, CheckCircle, Smartphone, Database, Server, User, BookOpen, Info, MessageCircle, Cpu, Zap, ChevronRight, Check, Search, ShoppingCart, Package, HelpCircle } from 'lucide-react';

// --- Local Components Matching Style Guide ---

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

const Badge = ({ children, color = 'gray' }) => {
    const colors = {
        gray: 'bg-gray-100 text-t-secondary',
        blue: 'bg-brand-100 text-brand-700',
        green: 'bg-emerald-100 text-emerald-700',
        amber: 'bg-amber-100 text-amber-700',
        indigo: 'bg-brand-100 text-brand-700',
        purple: 'bg-purple-100 text-purple-700',
    };
    return (
        <span className={`inline-flex items-center px-3 py-1 rounded-md text-sm font-medium font-mono ${colors[color] || colors.gray}`}>
            {children}
        </span>
    );
};

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


const S2_PreSaleConversion = () => {
    const [activeTab, setActiveTab] = useState('overview');
    const [activeFlow, setActiveFlow] = useState(0);

    // Interactive ROI Calculator
    const [dailyQuestions, setDailyQuestions] = useState(50);
    const [lateAnswered, setLateAnswered] = useState(30);
    const [avgBasket, setAvgBasket] = useState(300);
    const conversionDiff = 0.35;
    const monthlyRevenue = Math.round(lateAnswered * conversionDiff * avgBasket * 30);
    const roi = Math.round((monthlyRevenue / 5000) * 10) / 10;

    const flows = [
        {
            id: 'flow-1',
            title: 'Beden / Ölçü Sorusu',
            description: 'Müşteri ürünün bedeninin kendisine uyup uymayacağını soruyor.',
            requirements: {
                client: ['Ürün kataloğu (beden tablosu dahil)', 'Knowledge base\'e yüklenmiş ürün verileri (CSV/PDF)'],
                provider: ['Beden eşleştirme algoritması', 'Knowledge RAG altyapısı (C7)']
            },
            steps: [
                { role: 'user', content: 'Merhaba, bu ceket XL bedene uyar mı? Boy 1.82, kilo 90.' },
                { role: 'ai', content: 'Intent: Beden Sorusu. Knowledge Base sorgulanıyor → Ürün #4521 Beden Tablosu.' },
                { role: 'bot', content: 'Merhaba! Bu ceket XL bedene uygundur. Ölçüler: Göğüs: 112cm, Boy: 74cm, Kol: 65cm. 1.82 boy ve 90 kilo için XL tam olacaktır. 👍' },
                { role: 'user', content: 'Kumaşı nasıl, kaliteli mi?' },
                { role: 'ai', content: 'Intent: Ürün Özellik. Knowledge Base → Ürün kartı detayları.' },
                { role: 'bot', content: '%100 polyester, su itici kaplama. Rüzgar geçirmez. Müşterilerimizden 4.6/5 puan almış. Sipariş vermek ister misiniz? Bugün kargolayabiliriz! 🚀' },
                { role: 'user', content: 'Tamam, sipariş veriyorum.' },
                { role: 'system', content: 'Dönüşüm Başarılı — Yanıt Süresi: 28 saniye' }
            ]
        },
        {
            id: 'flow-2',
            title: 'Renk / Varyant Sorusu',
            description: 'Müşteri aynı üründen farklı renk veya model olup olmadığını soruyor.',
            requirements: {
                client: ['Güncel stok/varyant bilgisi', 'Ürün görselleri (renk bazlı)'],
                provider: ['Stok sorgulama entegrasyonu (C11)', 'Görsel eşleştirme']
            },
            steps: [
                { role: 'user', content: 'Bu çantanın siyah rengi var mı?' },
                { role: 'ai', content: 'Intent: Varyant Sorusu. Knowledge Base → Ürün #7823 Renk Seçenekleri.' },
                { role: 'bot', content: 'Evet! Bu çantanın Siyah, Lacivert ve Haki renk seçenekleri mevcut. Üçü de şu an stokta. Hangi rengi tercih edersiniz?' },
                { role: 'user', content: 'Siyah olsun. Peki lacivert ile siyah arasında ton farkı nasıl?' },
                { role: 'bot', content: 'Siyah mat bir görünüme sahipken, lacivert daha koyu bir mavi tonundadır. Size her iki rengin fotoğrafını göndereyim mi?' },
                { role: 'user', content: 'Evet lütfen.' },
                { role: 'bot', content: '[Siyah görsel] [Lacivert görsel] İşte karşılaştırmalı görseller. Hangisini beğendiyseniz hemen sipariş oluşturabilirim!' }
            ]
        },
        {
            id: 'flow-3',
            title: 'Stok / Teslimat Sorusu',
            description: 'Müşteri ürünün stokta olup olmadığını ve ne zaman ulaşacağını soruyor.',
            requirements: {
                client: ['Gerçek zamanlı stok verisi', 'Kargo SLA bilgisi (ortalama teslimat süreleri)'],
                provider: ['Stok API entegrasyonu (C11)', 'Kargo tahmini modülü']
            },
            steps: [
                { role: 'user', content: 'Bu ürün stokta var mı? Ne zaman gelir?' },
                { role: 'ai', content: 'Intent: Stok Sorusu. Stok API sorgulanıyor → Ürün #3312.' },
                { role: 'bot', content: 'Evet, stokta mevcut! 🎉 Bugün sipariş verirseniz tahmini teslimat: 2-3 iş günü (İstanbul için). Hangi adrese göndermemi istersiniz?' },
                { role: 'user', content: 'Ankara\'ya gönderilecek.' },
                { role: 'bot', content: 'Ankara için teslimat süresi 3-4 iş günü. Hemen sipariş oluşturmak ister misiniz?' },
                { role: 'user', content: 'Olur, oluştur.' },
                { role: 'system', content: 'Dönüşüm Başarılı — Stok sorusu → Satış. Yanıt Süresi: 45 saniye' }
            ]
        },
        {
            id: 'flow-4',
            title: 'Uyumluluk Sorusu',
            description: 'Müşteri bir ürünün başka bir ürünle uyumlu olup olmadığını soruyor.',
            requirements: {
                client: ['Ürün uyumluluk matrisi', 'Teknik spesifikasyon dökümanları'],
                provider: ['Çapraz ürün eşleştirme mantığı', 'Knowledge RAG (C7)']
            },
            steps: [
                { role: 'user', content: 'Bu kılıf iPhone 15 Pro Max\'e uyar mı?' },
                { role: 'ai', content: 'Intent: Uyumluluk Sorusu. Knowledge Base → Ürün #9102 Uyumluluk Listesi.' },
                { role: 'bot', content: 'Evet, bu kılıf iPhone 15 Pro Max ile %100 uyumludur. Kamera çıkıntısı ve tüm tuş yerleşimleri birebir oturur. ✅' },
                { role: 'user', content: 'MagSafe destekliyor mu?' },
                { role: 'bot', content: 'Evet! Dahili MagSafe mıknatısı var, kablosuz şarj ve MagSafe aksesuarlarıyla sorunsuz çalışır. Sipariş vermek ister misiniz?' },
                { role: 'user', content: 'Bir de ekran koruyucu var mı buna uygun?' },
                { role: 'ai', content: 'Intent: Çapraz Satış Fırsatı. İlgili ürünler sorgulanıyor.' },
                { role: 'bot', content: 'Tabii! iPhone 15 Pro Max tam kaplama temperli cam ekran koruyucumuz var, kılıfla birlikte seti 499 TL yerine 399 TL\'ye alabilirsiniz. 🎁' }
            ]
        },
        {
            id: 'flow-5',
            title: 'IG DM\'den Gelen Soru',
            description: 'Instagram DM üzerinden gelen ürün sorusu ve WhatsApp\'a yönlendirme.',
            requirements: {
                client: ['Instagram Business hesabı', 'WhatsApp numarası (yönlendirme için)'],
                provider: ['Instagram DM API entegrasyonu', 'Kanal geçiş (handoff) modülü']
            },
            steps: [
                { role: 'system', content: 'Kanal: Instagram DM — 24 saat yanıt penceresi aktif' },
                { role: 'user', content: 'Story\'deki elbise kaç TL? S beden var mı?' },
                { role: 'ai', content: 'Intent: Fiyat + Beden Sorusu (IG DM). Knowledge Base → Kampanya ürünü #6650.' },
                { role: 'bot', content: 'Merhaba! 🌟 Story\'deki elbise 899 TL ve S beden stokta mevcut. Detaylı bilgi ve sipariş için WhatsApp hattımızdan devam edelim mi? wa.me/905xxxxxxxxx' },
                { role: 'user', content: 'Tamam WhatsApp\'tan yazıyorum.' },
                { role: 'system', content: 'Kanal Geçişi: IG DM → WhatsApp. Lead kaynağı: Instagram Story.' },
                { role: 'bot', content: 'WhatsApp\'ta bekliyoruz! Elbiseyle ilgili tüm soruları orada cevaplayalım. 💬' }
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
                    S2: Satış Öncesi Ürün Soruları
                </h1>

                <p className="text-2xl text-t-secondary max-w-5xl font-light leading-relaxed">
                    WhatsApp ve Instagram DM'den gelen ürün sorularını AI Knowledge Base ile anında yanıtlayarak
                    cevap süresini dakikalardan saniyelere düşüren ve dönüşüm oranını artıran akıllı satış asistanı.
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
                                    <span><strong>Sektör:</strong> E-Ticaret (Pazaryeri + D2C Satıcıları)</span>
                                </li>
                                <li className="flex items-start gap-4 text-t-secondary text-lg">
                                    <CheckCircle size={24} className="text-emerald-500 mt-0.5" />
                                    <span><strong>Kanallar:</strong> WhatsApp Business, Instagram DM</span>
                                </li>
                                <li className="flex items-start gap-4 text-t-secondary text-lg">
                                    <CheckCircle size={24} className="text-emerald-500 mt-0.5" />
                                    <span><strong>Hacim:</strong> Günde 50+ ürün sorusu alan satıcılar</span>
                                </li>
                            </ul>
                        </FlatCard>

                        <FlatCard title="Entegre Servisler" icon={Database}>
                            <div className="flex flex-wrap gap-3">
                                <Badge color="blue">Knowledge RAG (C7)</Badge>
                                <Badge color="green">WhatsApp Business API</Badge>
                                <Badge color="purple">AI Agent Assist (C8)</Badge>
                                <Badge color="amber">Trendyol / Shopify API</Badge>
                            </div>
                            <div className="mt-6 text-base text-t-muted">
                                Ürün kataloğu Knowledge Base'e yüklenir. AI, gelen soruyu anlayarak katalogdan doğru cevabı bulur ve anında yanıtlar.
                            </div>
                        </FlatCard>
                    </div>

                    <FlatCard title="Sistem Çalışma Mantığı" icon={Zap} className="border-l-4 border-brand-500">
                        <div className="mt-4 space-y-4">
                            <Step number={1} title="Soru Alımı (Intake)" goal="Ürün sorusunu yakala">
                                <div className="flex gap-2 mb-2">
                                    <Badge color="purple">Sistem (Otomatik)</Badge>
                                    <Badge color="green">Tenant (WhatsApp/IG Hattı)</Badge>
                                </div>
                                WhatsApp veya Instagram DM'den gelen mesaj Unified Inbox'a düşer. AI, mesajın bir ürün sorusu olduğunu tespit eder (beden, renk, stok, uyumluluk, fiyat).
                            </Step>
                            <Step number={2} title="Bilgi Çekme (Retrieval)" goal="Doğru ürün bilgisini bul">
                                <div className="flex gap-2 mb-2">
                                    <Badge color="purple">Sistem (Knowledge RAG)</Badge>
                                    <Badge color="amber">Tenant (Ürün Kataloğu)</Badge>
                                </div>
                                Knowledge Base'den ilgili ürün kartı, beden tablosu, stok durumu ve teknik özellikler çekilir. Kaynaklı ve doğrulanmış bilgi kullanılır.
                            </Step>
                            <Step number={3} title="Yanıt Üretimi (Generation)" goal="Kişiselleştirilmiş cevap">
                                <div className="flex gap-2 mb-2">
                                    <Badge color="purple">Sistem (AI Model)</Badge>
                                    <Badge color="blue">Tenant (Yanıt Tonu Ayarları)</Badge>
                                </div>
                                AI, ürün bilgisini müşterinin sorusuna özel şekilde formatlar. Beden önerisi, renk karşılaştırması veya stok durumu bilgisi doğal bir dille sunulur.
                            </Step>
                            <Step number={4} title="Satışa Yönlendirme (Conversion)" goal="Soruyu satışa çevir">
                                <div className="flex gap-2 mb-2">
                                    <Badge color="purple">Sistem (Otonom)</Badge>
                                    <Badge color="green">Tenant (Sipariş Linki)</Badge>
                                </div>
                                Müşteri bilgiyi aldıktan sonra, AI sipariş bağlantısı veya çapraz satış önerisi sunar. Dönüşüm takibi Dashboard'da raporlanır.
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
                                    <Badge color="blue">AI Yanıt</Badge>
                                    <Badge color="green">Knowledge RAG</Badge>
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
                                            <Search size={24} />
                                        </div>
                                        <div>
                                            <h4 className="font-bold text-t-primary text-base">Satış Asistanı</h4>
                                            <span className="text-sm text-emerald-600 font-medium">Çevrimiçi</span>
                                        </div>
                                    </div>
                                    <Badge color="gray">Canlı Önizleme</Badge>
                                </div>

                                {/* Chat Content */}
                                <div className="flex-1 p-8 overflow-y-auto space-y-8 bg-gray-50">
                                    {flows[activeFlow].steps.map((step, idx) => {
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
                        Bu senaryo senkron çalışır. Müşteri mesajı geldiği anda Knowledge RAG pipeline'ı tetiklenir,
                        ortalama yanıt süresi 2-5 saniyedir. Ürün kataloğu değişikliklerinde Knowledge Base otomatik güncellenir.
                    </Callout>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-10">
                        <FlatCard title="Backend Servisleri" icon={Server}>
                            <div className="space-y-6">
                                <Step number="A" title="Knowledge RAG Service" goal="Ürün Bilgi Çekme">
                                    Ürün kataloğu (CSV/PDF/API) vektör veritabanına (pgvector) indekslenir. Gelen soru embedding'e dönüştürülür ve en yakın ürün bilgisi çekilir.
                                </Step>
                                <Step number="B" title="Agent Assist (C8)" goal="Yanıt Üretimi">
                                    RAG sonucu + müşteri konteksti AI modeline gönderilir. Kaynaklı, doğal dilde yanıt üretilir. Güven skoru düşükse insan temsilciye yönlendirilir.
                                </Step>
                                <Step number="C" title="Conversion Tracker" goal="Dönüşüm Takibi">
                                    Ürün sorusu → yanıt → sipariş zinciri izlenir. Dashboard'da "soru-satış dönüşüm oranı" raporlanır.
                                </Step>
                            </div>
                        </FlatCard>

                        <FlatCard title="API Entegrasyonları" icon={Database}>
                            <div className="space-y-6">
                                <Step number="A" title="Trendyol / Shopify Catalog API" goal="Ürün Verisi">
                                    Ürün kartı, beden tablosu, stok durumu ve fiyat bilgisi periyodik olarak senkronize edilir.
                                </Step>
                                <Step number="B" title="WhatsApp Cloud API" goal="Mesajlaşma">
                                    Gelen mesajlar webhook ile alınır, yanıtlar 24 saat penceresi içinde gönderilir.
                                </Step>
                                <Step number="C" title="Instagram Graph API" goal="DM Entegrasyonu">
                                    Instagram DM mesajları Unified Inbox'a akar. 24 saat yanıt penceresi kuralına uyulur.
                                </Step>
                            </div>
                        </FlatCard>
                    </div>

                    <FlatCard title="Örnek Konfigürasyon" icon={BookOpen}>
                        <pre className="bg-gray-50 border border-gray-200 rounded-lg p-6 text-sm font-mono overflow-x-auto text-t-primary">
{`{
  "scenario": "S2_PreSaleConversion",
  "knowledge_sources": [
    { "type": "csv", "path": "catalog/products.csv", "refresh": "6h" },
    { "type": "api", "endpoint": "trendyol/v2/products", "refresh": "1h" }
  ],
  "response_config": {
    "max_tokens": 250,
    "confidence_threshold": 0.75,
    "fallback_to_human": true,
    "tone": "friendly_sales"
  },
  "channels": ["whatsapp", "instagram_dm"],
  "cross_sell": {
    "enabled": true,
    "max_suggestions": 2
  }
}`}
                        </pre>
                    </FlatCard>
                </div>
            )}

            {/* Footer Impact Stats — Interactive ROI Calculator */}
            <div className="mt-16">
                <FlatCard className="bg-emerald-50/80 border-emerald-100">
                    <div className="mb-8">
                        <div className="flex items-center gap-3 mb-2">
                            <h2 className="text-3xl font-bold text-emerald-900">
                                Potansiyel Aylık Etki: ~{monthlyRevenue.toLocaleString('tr-TR')} TL
                            </h2>
                            <span className="text-xs font-mono bg-emerald-200 text-emerald-800 px-2 py-1 rounded">CANLI HESAPLAMA</span>
                        </div>
                        <p className="text-emerald-700 text-lg">
                            Aşağıdaki değerleri <strong>kendi işletmenize göre düzenleyin</strong> — sonuç anında güncellenir.
                        </p>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-4 gap-8 pt-8 border-t border-emerald-200">
                        <div>
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">Günlük Ürün Sorusu</span>
                            <input
                                type="number"
                                value={dailyQuestions}
                                onChange={(e) => setDailyQuestions(Math.max(0, Number(e.target.value) || 0))}
                                className="block text-3xl font-bold text-emerald-900 bg-transparent border-b-2 border-dashed border-emerald-400 w-24 text-center focus:outline-none focus:border-emerald-600 transition-colors"
                            />
                            <span className="text-emerald-700/80 text-sm mt-1 block">WhatsApp + IG DM</span>
                        </div>

                        <div>
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">Geç Cevaplanan</span>
                            <input
                                type="number"
                                value={lateAnswered}
                                onChange={(e) => setLateAnswered(Math.max(0, Number(e.target.value) || 0))}
                                className="block text-3xl font-bold text-emerald-900 bg-transparent border-b-2 border-dashed border-emerald-400 w-24 text-center focus:outline-none focus:border-emerald-600 transition-colors"
                            />
                            <span className="text-emerald-700/80 text-sm mt-1 block">{'>'}30dk yanıt süresi</span>
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
                            <span className="text-emerald-700/80 text-sm mt-1 block">Dönüşüm farkı: %35 (sabit)</span>
                        </div>

                        <div className="bg-emerald-100/50 rounded-lg p-4 -m-2">
                            <span className="block text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1">Kurtarılan Gelir (Aylık)</span>
                            <span className="block text-3xl font-bold text-emerald-900">{(monthlyRevenue / 1000).toFixed(1)}k ₺</span>
                            <span className="text-emerald-700/80 text-sm mt-1 block">{roi}x ROI (5.000₺ abonelik)</span>
                        </div>
                    </div>

                    <div className="mt-6 pt-4 border-t border-emerald-200">
                        <p className="text-emerald-600 text-sm font-mono">
                            Formül: {lateAnswered} geç cevap × %35 dönüşüm farkı × {avgBasket}₺ sepet × 30 gün = <strong>{monthlyRevenue.toLocaleString('tr-TR')} ₺/ay</strong>
                        </p>
                    </div>
                </FlatCard>
            </div>
        </div>
    );
};

export default S2_PreSaleConversion;
