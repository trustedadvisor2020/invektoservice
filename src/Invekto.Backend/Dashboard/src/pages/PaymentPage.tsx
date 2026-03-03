import { useState } from 'react';
import {
  CreditCard, Copy, Check, ArrowRight, ShieldCheck,
  Building2, User, Phone, Mail, FileText, Smartphone,
  Layers, Calculator, Contact, Loader2,
} from 'lucide-react';
import { api } from '../lib/api';

// ── helpers ────────────────────────────────────────────────────────────────
function cn(...classes: (string | boolean | undefined | null)[]): string {
  return classes.filter(Boolean).join(' ');
}

// ── translations ───────────────────────────────────────────────────────────
const t = {
  ssl: '256-bit SSL Güvenli Ödeme',
  title: 'Ödemenizi Tamamlayın',
  subtitle: 'Kart bilgilerinizi girin ve ödemeyi başlatın.',
  detailsTitle: 'Ödeme Detayları',
  serviceTitle: 'Hizmet ve Tutar',
  paymentType: 'Ödeme Türü',
  types: { lisans: 'Lisans Ödemesi', kontor: 'Kontör Paket Ödemesi', sms: 'SMS Ödemesi' },
  loadPhone: 'Yüklenecek Numara',
  amount: 'Ödenecek Tutar',
  contactTitle: 'İletişim Bilgileri',
  companyName: 'Firma Adı',
  phone: 'Telefon',
  email: 'E-posta',
  note: 'Varsa ödeme notunuz...',
  totalLabel: 'Toplam Ödenecek',
  vatIncluded: 'KDV Dahil',
  card: 'Kredi Kartı',
  transfer: 'Havale / EFT',
  cardInfo: 'Kart Bilgileri',
  cardNumber: 'Kart Numarası',
  expiry: 'Son Kul. Tarihi',
  cvc: 'CVV / CVC',
  holder: 'Kart Sahibi',
  pay: 'güvenli öde',
  invalidCard: 'Geçersiz kart numarası',
  transferNotification: 'Havale Bildirimi',
  transferDesc: 'Ödemeyi yaparken açıklama kısmına <strong>{company}</strong> yazmayı unutmayınız.',
  companyPlaceholder: 'Firma Adınızı',
  transferWarning: 'Ödemeyi yapacak kişi ile fatura düzenlenecek kişi bilgilerinin birebir uyuşması gerekmektedir.',
  copy: 'Kopyala',
  copied: 'Kopyalandı',
};

// ── BankAccount sub-component ───────────────────────────────────────────────
function BankAccount({
  bankName, iban, owner, copiedIban, onCopy,
}: {
  bankName: string;
  iban: string;
  owner: string;
  copiedIban: string | null;
  onCopy: (text: string) => void;
}) {
  const isCopied = copiedIban === iban;
  return (
    <div className="p-4 rounded-xl border border-navy-100 bg-white hover:border-brand-300 hover:shadow-md transition-all group relative overflow-hidden">
      <div className="flex justify-between items-start mb-3">
        <div>
          <div className="font-bold text-navy-900 text-lg">{bankName}</div>
          <div className="text-sm text-navy-400">{owner}</div>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <div className="flex-1 bg-navy-50 border border-navy-100 px-3 py-2 rounded-lg font-mono text-sm text-navy-900 flex items-center">
          <span className="select-all">{iban}</span>
        </div>
        <button
          onClick={() => onCopy(iban)}
          className={cn(
            'p-2.5 rounded-lg transition-all font-medium flex items-center gap-2 shadow-sm text-xs',
            isCopied ? 'bg-green-100 text-green-700' : 'bg-brand-500 text-white hover:bg-brand-600',
          )}
        >
          {isCopied ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
          <span className="hidden sm:inline">{isCopied ? t.copied : t.copy}</span>
        </button>
      </div>
    </div>
  );
}

// ── main component ─────────────────────────────────────────────────────────
export function PaymentPage() {
  const [method, setMethod] = useState<'card' | 'transfer'>('card');
  const [copiedIban, setCopiedIban] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [amount, setAmount] = useState('');
  const [paymentType, setPaymentType] = useState('lisans');
  const [phone, setPhone] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [contactNumber, setContactNumber] = useState('');
  const [email, setEmail] = useState('');
  const [note, setNote] = useState('');

  // Card state
  const [cardNumber, setCardNumber] = useState('');
  const [expiry, setExpiry] = useState('');
  const [cvc, setCvc] = useState('');
  const [cardName, setCardName] = useState('');
  const [isCardValid, setIsCardValid] = useState(true);

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedIban(text);
    setTimeout(() => setCopiedIban(null), 2000);
  };

  const getTypeIcon = () => {
    switch (paymentType) {
      case 'lisans': return ShieldCheck;
      case 'kontor': return Layers;
      case 'sms': return Smartphone;
      default: return FileText;
    }
  };
  const TypeIcon = getTypeIcon();

  const handleCardNumberChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    let value = e.target.value.replace(/\D/g, '');
    if (value.length > 16) value = value.slice(0, 16);
    if (value.length === 16) {
      let sum = 0; let shouldDouble = false;
      for (let i = value.length - 1; i >= 0; i--) {
        let digit = parseInt(value.charAt(i));
        if (shouldDouble) { if ((digit *= 2) > 9) digit -= 9; }
        sum += digit; shouldDouble = !shouldDouble;
      }
      setIsCardValid(sum % 10 === 0);
    } else { setIsCardValid(true); }
    const parts: string[] = [];
    for (let i = 0; i < value.length; i += 4) parts.push(value.substring(i, i + 4));
    setCardNumber(parts.join(' '));
  };

  const handleExpiryChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    let value = e.target.value.replace(/\D/g, '');
    if (value.length > 4) value = value.slice(0, 4);
    if (value.length >= 2) {
      const month = parseInt(value.substring(0, 2));
      if (month > 12) value = '12' + value.substring(2);
      if (month === 0) value = '01' + value.substring(2);
      value = value.substring(0, 2) + ' / ' + value.substring(2);
    }
    setExpiry(value);
  };

  const handleCvcChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    let value = e.target.value.replace(/\D/g, '');
    if (value.length > 3) value = value.slice(0, 3);
    setCvc(value);
  };

  const handlePay = async () => {
    setError(null);
    const amountNum = parseFloat(amount);
    if (isNaN(amountNum) || amountNum <= 0) { setError('Geçerli bir tutar girin.'); return; }
    if (!isCardValid) { setError('Kart numarası geçersiz.'); return; }
    const rawCard = cardNumber.replace(/\s/g, '');
    if (rawCard.length !== 16) { setError('Kart numarası 16 haneli olmalıdır.'); return; }
    const expiryRaw = expiry.replace(/\s/g, '').replace('/', '');
    if (expiryRaw.length !== 4) { setError('Son kullanma tarihi eksik.'); return; }
    if (cvc.length < 3) { setError('CVV eksik.'); return; }

    const month = expiryRaw.substring(0, 2);
    const year  = expiryRaw.substring(2);

    setLoading(true);
    try {
      const result = await api.initiatePayment({
        amount: amountNum,
        card_number: rawCard,
        card_expire_month: month,
        card_expire_year: year,
        cvv: cvc,
        card_holder_name: cardName,
      });
      // Banka 3D sayfasına yönlendir (mevcut sekme)
      document.open();
      document.write(result.redirect_html);
      document.close();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Ödeme başlatılamadı.';
      setError(msg);
      setLoading(false);
    }
  };

  return (
    <div className="min-h-full bg-navy-50 flex flex-col items-center justify-start p-4 md:p-8">

      {/* Header */}
      <div className="mb-6 text-center w-full max-w-2xl px-4">
        <div className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-white shadow-soft border border-brand-100 text-brand-600 text-sm font-medium mb-3">
          <ShieldCheck className="w-4 h-4" />
          <span>{t.ssl}</span>
        </div>
        <h1 className="text-2xl md:text-3xl font-bold text-navy-900 mb-1">{t.title}</h1>
        <p className="text-navy-400 text-sm">{t.subtitle}</p>
      </div>

      <div className="w-full max-w-5xl bg-white rounded-2xl shadow-elevated border border-navy-100 overflow-hidden flex flex-col lg:flex-row">

        {/* LEFT: Payment Config */}
        <div className="w-full lg:w-5/12 bg-navy-50 p-6 md:p-8 flex flex-col border-b lg:border-b-0 lg:border-r border-navy-100 gap-6">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-brand-50 text-brand-600 rounded-xl">
              <FileText className="w-5 h-5" />
            </div>
            <h2 className="text-lg font-bold text-navy-900">{t.detailsTitle}</h2>
          </div>

          <div className="space-y-4">
            {/* Service & Amount */}
            <div className="bg-white rounded-xl border border-navy-100 overflow-hidden">
              <div className="px-5 py-3 border-b border-navy-100 bg-navy-50 flex items-center gap-2">
                <Calculator className="w-4 h-4 text-brand-500" />
                <span className="text-xs font-bold text-navy-500 uppercase tracking-wider">{t.serviceTitle}</span>
              </div>
              <div className="p-5 space-y-4">
                <div>
                  <label className="block text-xs font-medium text-navy-400 mb-1.5 ml-1">{t.paymentType}</label>
                  <div className="relative">
                    <select
                      value={paymentType}
                      onChange={e => setPaymentType(e.target.value)}
                      className="w-full pl-10 pr-4 py-3 bg-navy-50 rounded-xl border border-navy-100 focus:bg-white focus:border-brand-400 focus:ring-2 focus:ring-brand-400/20 outline-none transition-all appearance-none text-sm font-medium text-navy-900"
                    >
                      <option value="lisans">{t.types.lisans}</option>
                      <option value="kontor">{t.types.kontor}</option>
                      <option value="sms">{t.types.sms}</option>
                    </select>
                    <TypeIcon className="w-4 h-4 text-brand-500 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
                  </div>
                </div>

                {paymentType === 'kontor' && (
                  <div>
                    <label className="block text-xs font-medium text-navy-400 mb-1.5 ml-1">{t.loadPhone}</label>
                    <div className="relative">
                      <Phone className="w-4 h-4 text-navy-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
                      <input type="tel" value={phone} onChange={e => setPhone(e.target.value)}
                        placeholder="5XX XXX XX XX"
                        className="w-full pl-10 pr-4 py-3 bg-navy-50 rounded-xl border border-navy-100 focus:bg-white focus:border-brand-400 focus:ring-2 focus:ring-brand-400/20 outline-none transition-all font-mono text-sm"
                      />
                    </div>
                  </div>
                )}

                <div>
                  <label className="block text-xs font-medium text-navy-400 mb-1.5 ml-1">{t.amount}</label>
                  <div className="relative">
                    <span className="absolute left-4 top-1/2 -translate-y-1/2 text-navy-900 font-bold">₺</span>
                    <input type="number" value={amount} onChange={e => setAmount(e.target.value)}
                      placeholder="0.00"
                      className="w-full pl-8 pr-4 py-3 bg-navy-50 rounded-xl border border-navy-100 focus:bg-white focus:border-brand-400 focus:ring-2 focus:ring-brand-400/20 outline-none transition-all font-mono text-lg font-bold text-brand-600 placeholder:text-navy-300"
                    />
                  </div>
                </div>
              </div>
            </div>

            {/* Contact Info */}
            <div className="bg-white rounded-xl border border-navy-100 overflow-hidden">
              <div className="px-5 py-3 border-b border-navy-100 bg-navy-50 flex items-center gap-2">
                <Contact className="w-4 h-4 text-brand-500" />
                <span className="text-xs font-bold text-navy-500 uppercase tracking-wider">{t.contactTitle}</span>
              </div>
              <div className="p-5 space-y-3">
                <div className="relative">
                  <Building2 className="w-4 h-4 text-navy-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
                  <input type="text" value={companyName} onChange={e => setCompanyName(e.target.value)}
                    placeholder={t.companyName}
                    className="w-full pl-10 pr-4 py-3 bg-white rounded-xl border border-navy-100 focus:border-brand-400 focus:ring-2 focus:ring-brand-400/20 outline-none transition-all text-sm"
                  />
                </div>
                <div className="relative">
                  <Phone className="w-4 h-4 text-navy-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
                  <input type="tel" value={contactNumber} onChange={e => setContactNumber(e.target.value)}
                    placeholder={t.phone}
                    className="w-full pl-10 pr-4 py-3 bg-white rounded-xl border border-navy-100 focus:border-brand-400 focus:ring-2 focus:ring-brand-400/20 outline-none transition-all text-sm"
                  />
                </div>
                <div className="relative">
                  <Mail className="w-4 h-4 text-navy-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
                  <input type="email" value={email} onChange={e => setEmail(e.target.value)}
                    placeholder={t.email}
                    className="w-full pl-10 pr-4 py-3 bg-white rounded-xl border border-navy-100 focus:border-brand-400 focus:ring-2 focus:ring-brand-400/20 outline-none transition-all text-sm"
                  />
                </div>
                <textarea rows={2} value={note} onChange={e => setNote(e.target.value)}
                  placeholder={t.note}
                  className="w-full px-4 py-3 bg-white rounded-xl border border-navy-100 focus:border-brand-400 focus:ring-2 focus:ring-brand-400/20 outline-none transition-all resize-none text-sm"
                />
              </div>
            </div>
          </div>

          {/* Total */}
          <div className="mt-auto px-1 pt-2">
            <div className="flex justify-between items-end p-5 bg-white border-2 border-brand-500 rounded-2xl shadow-card">
              <div>
                <div className="text-brand-600 text-xs font-bold uppercase tracking-wider mb-1">{t.totalLabel}</div>
                <div className="text-sm text-navy-400 font-medium">{t.vatIncluded}</div>
              </div>
              <div className="text-4xl font-black tracking-tight font-mono text-brand-600">
                {amount ? parseFloat(amount).toLocaleString('tr-TR', { minimumFractionDigits: 2 }) : '0,00'}{' '}
                <span className="text-2xl text-brand-400 font-bold">₺</span>
              </div>
            </div>
          </div>
        </div>

        {/* RIGHT: Payment Method */}
        <div className="w-full lg:w-7/12 p-6 md:p-8 flex flex-col bg-white">
          {/* Method selector */}
          <div className="flex gap-4 mb-8 pb-2">
            {(['card', 'transfer'] as const).map(m => (
              <button key={m} onClick={() => setMethod(m)}
                className={cn(
                  'flex-1 flex flex-col items-center gap-3 p-4 rounded-xl border-2 transition-all duration-200',
                  method === m ? 'border-brand-500 bg-brand-50' : 'border-navy-100 hover:border-brand-200 hover:bg-navy-50',
                )}
              >
                <div className={cn('p-2 rounded-full transition-colors', method === m ? 'bg-brand-500 text-white' : 'bg-navy-100 text-navy-400')}>
                  {m === 'card' ? <CreditCard className="w-5 h-5" /> : <Building2 className="w-5 h-5" />}
                </div>
                <div className="text-sm font-semibold text-navy-900">{m === 'card' ? t.card : t.transfer}</div>
              </button>
            ))}
          </div>

          <div className="flex-1">
            {method === 'card' ? (
              <div className="space-y-6 max-w-md mx-auto">
                <h3 className="text-lg font-semibold text-navy-900">{t.cardInfo}</h3>

                {/* Card Number */}
                <div>
                  <label className="block text-xs font-medium text-navy-500 mb-1.5 uppercase tracking-wider">{t.cardNumber}</label>
                  <div className="relative">
                    <input type="text" value={cardNumber} onChange={handleCardNumberChange}
                      placeholder="0000 0000 0000 0000"
                      className={cn(
                        'w-full pl-12 pr-4 py-3.5 rounded-xl border bg-navy-50 focus:bg-white focus:ring-2 outline-none transition-all font-mono',
                        isCardValid ? 'border-navy-100 focus:ring-brand-400/20 focus:border-brand-400' : 'border-red-400 focus:ring-red-400/20 focus:border-red-400',
                      )}
                    />
                    <CreditCard className="w-5 h-5 text-navy-400 absolute left-4 top-1/2 -translate-y-1/2" />
                  </div>
                  {!isCardValid && <p className="text-red-500 text-xs mt-1">{t.invalidCard}</p>}
                </div>

                {/* Expiry + CVC */}
                <div className="grid grid-cols-2 gap-5">
                  <div>
                    <label className="block text-xs font-medium text-navy-500 mb-1.5 uppercase tracking-wider">{t.expiry}</label>
                    <input type="text" value={expiry} onChange={handleExpiryChange}
                      placeholder="AA / YY"
                      className="w-full px-4 py-3.5 rounded-xl border border-navy-100 bg-navy-50 focus:bg-white focus:ring-2 focus:ring-brand-400/20 focus:border-brand-400 outline-none transition-all font-mono text-center"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-navy-500 mb-1.5 uppercase tracking-wider">{t.cvc}</label>
                    <div className="relative">
                      <input type="text" value={cvc} onChange={handleCvcChange}
                        placeholder="•••"
                        className="w-full px-4 py-3.5 rounded-xl border border-navy-100 bg-navy-50 focus:bg-white focus:ring-2 focus:ring-brand-400/20 focus:border-brand-400 outline-none transition-all font-mono text-center"
                      />
                      <ShieldCheck className="w-4 h-4 text-navy-400 absolute right-4 top-1/2 -translate-y-1/2" />
                    </div>
                  </div>
                </div>

                {/* Card Holder */}
                <div>
                  <label className="block text-xs font-medium text-navy-500 mb-1.5 uppercase tracking-wider">{t.holder}</label>
                  <div className="relative">
                    <input type="text" value={cardName} onChange={e => setCardName(e.target.value)}
                      placeholder="Ad Soyad"
                      className="w-full pl-12 pr-4 py-3.5 rounded-xl border border-navy-100 bg-navy-50 focus:bg-white focus:ring-2 focus:ring-brand-400/20 focus:border-brand-400 outline-none transition-all"
                    />
                    <User className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-navy-400" />
                  </div>
                </div>

                {/* Error */}
                {error && (
                  <div className="px-4 py-3 bg-red-50 border border-red-100 rounded-xl text-sm text-red-600">
                    {error}
                  </div>
                )}

                {/* Pay button */}
                <div className="pt-4 border-t border-navy-100">
                  <button
                    type="button"
                    onClick={handlePay}
                    disabled={loading}
                    className="w-full bg-brand-500 hover:bg-brand-600 disabled:opacity-60 text-white font-bold py-4 rounded-xl shadow-card active:scale-[0.98] transition-all flex items-center justify-center gap-3 text-lg"
                  >
                    {loading ? (
                      <Loader2 className="w-5 h-5 animate-spin" />
                    ) : (
                      <>
                        <span>{amount ? `${parseFloat(amount).toLocaleString('tr-TR', { minimumFractionDigits: 2 })} ₺` : '0,00 ₺'} {t.pay}</span>
                        <ArrowRight className="w-5 h-5" />
                      </>
                    )}
                  </button>
                </div>
              </div>
            ) : (
              <div className="space-y-6 max-w-md mx-auto">
                <div className="bg-amber-50 border border-amber-200 rounded-2xl p-4 flex gap-4 items-start">
                  <div className="bg-white p-2 rounded-lg border border-amber-200 shrink-0">
                    <Building2 className="w-5 h-5 text-amber-600" />
                  </div>
                  <div>
                    <h4 className="text-sm font-bold text-amber-900 mb-1">{t.transferNotification}</h4>
                    <p className="text-sm text-amber-900/90 leading-relaxed mb-2"
                      dangerouslySetInnerHTML={{ __html: t.transferDesc.replace('{company}', companyName || t.companyPlaceholder) }}
                    />
                    <p className="text-sm font-bold text-amber-900">{t.transferWarning}</p>
                  </div>
                </div>

                <div className="space-y-3">
                  <BankAccount
                    bankName="Ziraat Bankası"
                    iban="TR12 0001 0002 0003 0004 0005 01"
                    owner="Regex Danışmanlık A.Ş."
                    copiedIban={copiedIban}
                    onCopy={handleCopy}
                  />
                  <BankAccount
                    bankName="Garanti BBVA"
                    iban="TR45 0006 0007 0008 0009 0010 02"
                    owner="Regex Danışmanlık A.Ş."
                    copiedIban={copiedIban}
                    onCopy={handleCopy}
                  />
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
