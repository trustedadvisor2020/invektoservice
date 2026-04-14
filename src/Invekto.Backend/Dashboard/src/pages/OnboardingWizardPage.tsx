import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Building2, LayoutTemplate, BookOpen, Brain, GitBranch, Zap, Smartphone,
  CheckCircle2, RefreshCw, ArrowRight, PartyPopper, Rocket,
  ChevronRight, Lock,
} from 'lucide-react';
import { api, type OnboardingStatusResponse, type OnboardingStepResponse } from '../lib/api';
import { cn } from '../lib/utils';

/* ── Step metadata with per-step accent colors ─────────────── */

interface StepMeta {
  label: string;
  icon: React.ComponentType<{ className?: string; style?: React.CSSProperties }>;
  description: string;
  why: string;
  fallback: string;
  accentHex: string;
}

const STEP_META: Record<string, StepMeta> = {
  sector_selected: {
    label: 'Sektör Seçimi',
    icon: Building2,
    description: 'İşletmenizin sektörünü belirleyin. Sektör seçimi, şablonları ve içerikleri size özel hale getirir.',
    why: 'Sektörünüzü belirlediğinizde sistem size o alana özel hazır şablonlar ve chatbot senaryoları sunar. Kurulumu saatler yerine dakikalar içinde tamamlarsınız.',
    fallback: 'Henüz sektör seçilmedi',
    accentHex: '#06b6d4',
  },
  templates_adopted: {
    label: 'Şablon Ekleme',
    icon: LayoutTemplate,
    description: 'Sektörünüze uygun hazır şablonları kütüphanenize ekleyin. Sıfırdan oluşturmanıza gerek yok.',
    why: 'Şablonlar, sektörünüze özel hazırlanmış mesaj kalıplarıdır. Chatbotunuz ilk günden profesyonel yanıtlar verir — sıfırdan yazmanıza gerek kalmaz.',
    fallback: 'Henüz eklenen şablon yok',
    accentHex: '#8b5cf6',
  },
  knowledge_ready: {
    label: 'Bilgi Bankası',
    icon: BookOpen,
    description: 'Müşterilerinizin sık sorduğu soruları ve cevaplarını ekleyin. Chatbot bu bilgileri kullanarak yanıt verecek.',
    why: 'Bilgi bankası, chatbotunuzun doğru ve tutarlı yanıt vermesini sağlar. Sık sorulan soruları burada tanımladığınızda müşterileriniz 7/24 anında cevap alır.',
    fallback: 'Henüz SSS eklenmedi',
    accentHex: '#f59e0b',
  },
  intents_configured: {
    label: 'Niyet Tanımlama',
    icon: Brain,
    description: '"Fiyat öğrenmek", "randevu almak" gibi niyetleri tanımlayın ki chatbot müşterinin ne istediğini anlasın.',
    why: 'Niyetler, chatbotunuzun müşterinin ne istediğini anlamasını sağlar. "Fiyat sormak" ile "şikâyet bildirmek" farklı şeylerdir — niyetleri tanımladığınızda doğru yanıt doğru kişiye gider.',
    fallback: 'Henüz niyet tanımlanmadı',
    accentHex: '#f43f5e',
  },
  first_flow_created: {
    label: 'İlk Akış',
    icon: GitBranch,
    description: 'Chatbotunuzun nasıl konuşacağını belirleyen bir akış oluşturun. Adım adım bir sohbet senaryosu hazırlayın.',
    why: 'Akış, chatbotunuzun müşteriyle nasıl konuşacağını belirleyen senaryodur. Bir akış oluşturduğunuzda chatbot sadece cevap veren değil, müşteriyi yönlendiren bir asistana dönüşür.',
    fallback: 'Henüz akış oluşturulmadı',
    accentHex: '#3b82f6',
  },
  flow_activated: {
    label: 'Akış Aktivasyonu',
    icon: Zap,
    description: 'Oluşturduğunuz akışı aktif edin. Chatbotunuz gerçek müşterilerle konuşmaya başlasın.',
    why: 'Akışı aktif etmek, chatbotunuzu canlıya almak demektir. Bu adımdan sonra gerçek müşterileriniz chatbotunuzla konuşmaya başlar.',
    fallback: 'Henüz aktif akış yok',
    accentHex: '#10b981',
  },
  whatsapp_connected: {
    label: 'WhatsApp Bağlantısı',
    icon: Smartphone,
    description: 'WhatsApp hesabınızı bağlayın. Müşterileriniz WhatsApp üzerinden yazdığında chatbot devreye girsin.',
    why: 'WhatsApp, müşterilerinizin en çok kullandığı kanaldır. Bağlantıyı kurduğunuzda chatbot 7/24 hizmet verir — tek bir mesajı bile kaçırmazsınız.',
    fallback: 'Henüz bağlantı kurulmadı',
    accentHex: '#22c55e',
  },
};

/* ── Motivating messages by progress ───────────────────────── */

function getMotivation(pct: number, done: number) {
  if (pct === 0) return { title: 'Hadi başlayalım!', sub: 'İlk adımınızı atın, gerisini birlikte hallederiz.' };
  if (pct <= 28) return { title: 'Harika başlangıç!', sub: `${done} adımı tamamladınız. Devam edin!` };
  if (pct <= 50) return { title: 'Yarı yoldasınız!', sub: 'Chatbotunuz şekillenmeye başlıyor.' };
  if (pct <= 71) return { title: 'Yarıdan fazlası tamam!', sub: 'Harika gidiyorsunuz, az kaldı.' };
  if (pct <= 85) return { title: 'Neredeyse hazır!', sub: 'Son birkaç adım kaldı.' };
  return { title: 'Son hamle!', sub: 'Bir adım daha ve canlıya çıkıyorsunuz!' };
}

/* ── Main Page ─────────────────────────────────────────────── */

export function OnboardingWizardPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);

  const fetchStatus = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.getOnboardingStatus();
      setStatus(result);
      if (!expandedKey) {
        setExpandedKey(result.next_step?.key ?? result.steps.find(s => !s.completed)?.key ?? null);
      }
    } catch (err) {
      const detail = err instanceof Error ? err.message : String(err);
      setError(`Kurulum durumu alınamadı: ${detail}`);
    } finally {
      setLoading(false);
    }
  }, [expandedKey]);

  useEffect(() => { fetchStatus(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const isComplete = status != null && status.progress_pct === 100;
  const completedCount = status?.steps.filter(s => s.completed).length ?? 0;

  return (
    <div className="max-w-3xl mx-auto">
      {/* Error */}
      {error && (
        <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-2xl text-sm text-red-700 onboarding-fade-in">
          {error}
        </div>
      )}

      {/* Loading skeleton */}
      {loading && !status && <LoadingSkeleton />}

      {/* Hero */}
      {status && !isComplete && (
        <HeroSection
          progressPct={status.progress_pct}
          completedCount={completedCount}
          totalSteps={status.steps.length}
          loading={loading}
          onRefresh={fetchStatus}
        />
      )}

      {/* Journey steps */}
      {status && !isComplete && (
        <div className="mt-8">
          {status.steps.map((step, idx) => (
            <JourneyStep
              key={step.key}
              step={step}
              index={idx}
              total={status.steps.length}
              isLast={idx === status.steps.length - 1}
              isNext={step.key === status.next_step?.key}
              isExpanded={step.key === expandedKey}
              onToggle={() => setExpandedKey(expandedKey === step.key ? null : step.key)}
              actionUrl={step.key === status.next_step?.key ? status.next_step?.action_url : undefined}
              hint={step.key === status.next_step?.key ? status.next_step?.hint : undefined}
              onNavigate={(url) => navigate(url.replace('/app', ''))}
            />
          ))}
        </div>
      )}

      {/* Completion celebration */}
      {isComplete && status && <CompletionCelebration onGoHome={() => navigate('/')} />}
      {isComplete && <ConfettiOverlay />}
    </div>
  );
}

/* ── Hero Section ──────────────────────────────────────────── */

function HeroSection({ progressPct, completedCount, totalSteps, loading, onRefresh }: {
  progressPct: number;
  completedCount: number;
  totalSteps: number;
  loading: boolean;
  onRefresh: () => void;
}) {
  const { title, sub } = getMotivation(progressPct, completedCount);

  return (
    <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-navy-900 via-[#1e1b4b] to-navy-800 p-8 onboarding-fade-in">
      {/* Decorative blurred orbs */}
      <div className="absolute -top-10 -right-10 w-64 h-64 rounded-full bg-brand-500/10 blur-3xl pointer-events-none" />
      <div className="absolute -bottom-8 -left-8 w-48 h-48 rounded-full bg-emerald-500/8 blur-3xl pointer-events-none" />
      {/* Dot grid texture */}
      <div
        className="absolute inset-0 opacity-[0.03] pointer-events-none"
        style={{
          backgroundImage: 'radial-gradient(circle at 1px 1px, white 1px, transparent 0)',
          backgroundSize: '24px 24px',
        }}
      />

      {/* Refresh */}
      <button
        onClick={onRefresh}
        disabled={loading}
        className="absolute top-4 right-4 p-2 text-white/30 hover:text-white/60 rounded-lg hover:bg-white/5 transition-colors disabled:opacity-30"
      >
        <RefreshCw className={cn('w-4 h-4', loading && 'animate-spin')} />
      </button>

      {/* Content */}
      <div className="relative flex items-center gap-8">
        <CircularProgress percent={progressPct} />

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1.5">
            <Rocket className="w-4 h-4 text-brand-400" />
            <span className="text-[11px] font-semibold text-brand-400 uppercase tracking-widest">
              Kurulum Sihirbazı
            </span>
          </div>
          <h1 className="text-2xl font-bold text-white mb-1.5 tracking-tight font-logo">
            {title}
          </h1>
          <p className="text-sm text-white/50 mb-4">{sub}</p>

          {/* Mini step dots */}
          <div className="flex items-center gap-3">
            <div className="flex gap-1">
              {Array.from({ length: totalSteps }).map((_, i) => (
                <div
                  key={i}
                  className={cn(
                    'w-2.5 h-2.5 rounded-full transition-all duration-500',
                    i < completedCount ? 'bg-emerald-400 scale-100' : 'bg-white/10 scale-90',
                  )}
                />
              ))}
            </div>
            <span className="text-xs text-white/30 font-medium">
              {completedCount}/{totalSteps} tamamlandı
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ── Circular Progress (SVG) ───────────────────────────────── */

function CircularProgress({ percent }: { percent: number }) {
  const r = 46;
  const circ = 2 * Math.PI * r;
  const offset = circ * (1 - percent / 100);

  return (
    <div className="relative shrink-0">
      <svg className="w-28 h-28 -rotate-90" viewBox="0 0 120 120">
        {/* Track */}
        <circle cx="60" cy="60" r={r} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth="10" />
        {/* Progress arc */}
        <circle
          cx="60" cy="60" r={r} fill="none"
          stroke="url(#onb-progress-grad)" strokeWidth="10" strokeLinecap="round"
          strokeDasharray={circ} strokeDashoffset={offset}
          className="transition-all duration-1000 ease-out"
        />
        <defs>
          <linearGradient id="onb-progress-grad" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#635BFF" />
            <stop offset="100%" stopColor="#10B981" />
          </linearGradient>
        </defs>
      </svg>
      {/* Center percentage */}
      <div className="absolute inset-0 flex items-center justify-center">
        <span className="text-2xl font-bold text-white tracking-tight">
          %{percent}
        </span>
      </div>
    </div>
  );
}

/* ── Journey Step (Timeline item) ──────────────────────────── */

function JourneyStep({ step, index, total, isLast, isNext, isExpanded, onToggle, actionUrl, hint, onNavigate }: {
  step: OnboardingStepResponse;
  index: number;
  total: number;
  isLast: boolean;
  isNext: boolean;
  isExpanded: boolean;
  onToggle: () => void;
  actionUrl?: string;
  hint?: string;
  onNavigate: (url: string) => void;
}) {
  const meta = STEP_META[step.key];
  if (!meta) return null;
  const Icon = meta.icon;
  const isPending = !step.completed && !isNext;

  return (
    <div
      className="flex gap-5 onboarding-fade-in"
      style={{ animationDelay: `${index * 70}ms` }}
    >
      {/* ── Timeline column ── */}
      <div className="flex flex-col items-center w-10 shrink-0">
        {/* Dot */}
        <div
          className={cn(
            'w-10 h-10 rounded-full flex items-center justify-center shrink-0 transition-all duration-300',
            step.completed && 'shadow-md',
            isNext && 'onboarding-pulse',
          )}
          style={{
            backgroundColor: step.completed
              ? '#10b981'
              : isNext
                ? meta.accentHex
                : 'transparent',
            border: isPending ? '2px solid #cbd5e1' : 'none',
            '--pulse-color': isNext ? `${meta.accentHex}50` : undefined,
            boxShadow: isNext ? `0 0 16px ${meta.accentHex}30` : undefined,
          } as React.CSSProperties}
        >
          {step.completed ? (
            <CheckCircle2 className="w-5 h-5 text-white" />
          ) : isNext ? (
            <Icon className="w-5 h-5 text-white" />
          ) : (
            <Lock className="w-4 h-4 text-navy-300" />
          )}
        </div>

        {/* Connecting line */}
        {!isLast && (
          <div
            className="w-0.5 flex-1 min-h-[16px] transition-colors duration-500"
            style={{ backgroundColor: step.completed ? '#10b981' : '#e2e8f0' }}
          />
        )}
      </div>

      {/* ── Card column ── */}
      <div className={cn('flex-1 min-w-0', isLast ? 'pb-0' : 'pb-4')}>
        <div
          className={cn(
            'rounded-xl border transition-all duration-300 overflow-hidden',
            step.completed && 'bg-white border-emerald-200/60 hover:border-emerald-300',
            isNext && 'bg-white',
            isPending && 'bg-navy-50/50 border-navy-100 opacity-60 hover:opacity-80',
          )}
          style={isNext ? {
            borderColor: `${meta.accentHex}30`,
            boxShadow: `0 4px 24px ${meta.accentHex}10`,
          } : undefined}
        >
          {/* Clickable header */}
          <button
            onClick={onToggle}
            className="w-full text-left px-5 py-4 flex items-center gap-3 hover:bg-navy-50/30 transition-colors"
          >
            {/* Mini icon */}
            <div
              className={cn(
                'w-8 h-8 rounded-lg flex items-center justify-center shrink-0',
                step.completed && 'bg-emerald-50',
                isPending && 'bg-navy-100',
              )}
              style={isNext ? { backgroundColor: `${meta.accentHex}12` } : undefined}
            >
              <Icon
                className="w-4 h-4"
                style={{ color: step.completed ? '#10b981' : isNext ? meta.accentHex : '#94a3b8' }}
              />
            </div>

            {/* Label + brief */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2">
                <h3 className={cn(
                  'text-sm font-semibold truncate',
                  step.completed ? 'text-navy-700' : isNext ? 'text-navy-900' : 'text-navy-400',
                )}>
                  {meta.label}
                </h3>
                {step.completed && (
                  <span className="text-[10px] font-semibold text-emerald-600 bg-emerald-50 px-1.5 py-0.5 rounded-full shrink-0">
                    Tamam
                  </span>
                )}
                {isNext && (
                  <span
                    className="text-[10px] font-semibold px-1.5 py-0.5 rounded-full shrink-0"
                    style={{ color: meta.accentHex, backgroundColor: `${meta.accentHex}12` }}
                  >
                    Sıradaki
                  </span>
                )}
              </div>
              {!isExpanded && (
                <p className={cn(
                  'text-xs truncate mt-0.5',
                  step.completed ? 'text-emerald-600/70' :
                  step.detail && step.detail !== 'bilinmiyor' ? 'text-navy-400' : 'text-navy-300 italic',
                )}>
                  {step.detail && step.detail !== 'bilinmiyor' ? step.detail : meta.fallback}
                </p>
              )}
            </div>

            {/* Counter + chevron */}
            <div className="flex items-center gap-2 shrink-0">
              <span className="text-[10px] text-navy-300 font-medium">{index + 1}/{total}</span>
              <ChevronRight className={cn(
                'w-4 h-4 text-navy-300 transition-transform duration-200',
                isExpanded && 'rotate-90',
              )} />
            </div>
          </button>

          {/* Expanded detail panel */}
          {isExpanded && (
            <div className="px-5 pb-5 onboarding-step-expand">
              <div className="pt-3 border-t border-navy-100 space-y-3">
                {/* Detail info from API or fallback */}
                <div className={cn(
                  'text-sm px-3 py-2 rounded-lg',
                  step.completed ? 'bg-emerald-50 text-emerald-700' :
                  step.detail && step.detail !== 'bilinmiyor' ? 'bg-navy-50 text-navy-600' :
                  'bg-navy-50 text-navy-400 italic',
                )}>
                  {step.detail && step.detail !== 'bilinmiyor' ? step.detail : meta.fallback}
                </div>

                {/* Why this step matters */}
                {!step.completed && (
                  <div
                    className="text-sm px-3.5 py-3 rounded-lg"
                    style={{
                      backgroundColor: `${meta.accentHex}06`,
                      borderLeft: `3px solid ${meta.accentHex}50`,
                    }}
                  >
                    <span className="font-semibold text-navy-700">Neden önemli?</span>{' '}
                    <span className="text-navy-500">{meta.why}</span>
                  </div>
                )}

                {/* Hint + CTA for next step */}
                {isNext && hint && (
                  <div className="pt-1 space-y-3">
                    <p className="text-sm text-navy-500">{hint}</p>
                    {actionUrl && (
                      <button
                        onClick={() => onNavigate(actionUrl)}
                        className="inline-flex items-center gap-2.5 px-6 py-2.5 text-sm font-semibold text-white rounded-xl transition-all duration-200 hover:shadow-lg hover:-translate-y-px active:translate-y-0"
                        style={{
                          background: `linear-gradient(135deg, ${meta.accentHex}, ${meta.accentHex}cc)`,
                          boxShadow: `0 4px 14px ${meta.accentHex}30`,
                        }}
                      >
                        Hadi Başlayalım
                        <ArrowRight className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/* ── Completion Celebration ────────────────────────────────── */

function CompletionCelebration({ onGoHome }: { onGoHome: () => void }) {
  return (
    <div
      className="relative overflow-hidden rounded-2xl p-10 text-center onboarding-fade-in"
      style={{
        background: 'linear-gradient(135deg, #065f46 0%, #1e1b4b 50%, #312e81 100%)',
      }}
    >
      {/* Decorative */}
      <div className="absolute top-0 left-1/2 -translate-x-1/2 w-96 h-96 bg-emerald-400/10 rounded-full blur-3xl pointer-events-none" />
      <div
        className="absolute inset-0 opacity-[0.04] pointer-events-none"
        style={{
          backgroundImage: 'radial-gradient(circle at 1px 1px, white 1px, transparent 0)',
          backgroundSize: '20px 20px',
        }}
      />

      <div className="relative">
        <div className="w-20 h-20 mx-auto mb-6 rounded-full bg-emerald-500/20 flex items-center justify-center">
          <PartyPopper className="w-10 h-10 text-emerald-400" />
        </div>
        <h2 className="text-3xl font-bold text-white mb-3 tracking-tight font-logo">
          Tebrikler!
        </h2>
        <p className="text-emerald-200/70 mb-8 max-w-md mx-auto">
          Tüm kurulum adımlarını tamamladınız. Chatbotunuz artık tam kapasite çalışmaya hazır.
        </p>
        <button
          onClick={onGoHome}
          className="inline-flex items-center gap-2 px-8 py-3 bg-white text-navy-900 font-semibold rounded-xl hover:bg-emerald-50 transition-all hover:shadow-lg hover:-translate-y-px active:translate-y-0"
        >
          Ana Sayfaya Dön
          <ArrowRight className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}

/* ── Confetti Overlay ──────────────────────────────────────── */

function ConfettiOverlay() {
  return (
    <div className="fixed inset-0 pointer-events-none z-50 overflow-hidden" aria-hidden="true">
      {Array.from({ length: 40 }).map((_, i) => (
        <div
          key={i}
          className="absolute animate-confetti"
          style={{
            left: `${Math.random() * 100}%`,
            top: '-10px',
            animationDelay: `${Math.random() * 2}s`,
            animationDuration: `${2 + Math.random() * 3}s`,
          }}
        >
          <div
            className="w-2.5 h-2.5 rounded-sm"
            style={{
              backgroundColor: ['#635BFF', '#10B981', '#F59E0B', '#EF4444', '#6366F1', '#EC4899'][i % 6],
              transform: `rotate(${Math.random() * 360}deg)`,
            }}
          />
        </div>
      ))}
    </div>
  );
}

/* ── Loading Skeleton ──────────────────────────────────────── */

function LoadingSkeleton() {
  return (
    <div className="space-y-6 max-w-3xl mx-auto">
      {/* Hero skeleton */}
      <div className="rounded-2xl bg-navy-800/50 h-44 animate-pulse" />
      {/* Step skeletons */}
      <div className="space-y-4 mt-8">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="flex gap-5" style={{ opacity: 1 - i * 0.15 }}>
            <div className="w-10 h-10 rounded-full bg-navy-200 animate-pulse shrink-0" />
            <div className="flex-1 h-16 rounded-xl bg-navy-100 animate-pulse" />
          </div>
        ))}
      </div>
    </div>
  );
}
