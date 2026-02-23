import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Building2, LayoutTemplate, BookOpen, Brain, GitBranch, Zap, Smartphone,
  CheckCircle2, Circle, RefreshCw, ArrowRight, PartyPopper, Rocket,
} from 'lucide-react';
import { api, type OnboardingStatusResponse, type OnboardingStepResponse } from '../lib/api';
import { cn } from '../lib/utils';

// Step metadata keyed by API step.key
const STEP_META: Record<string, { label: string; icon: React.ComponentType<{ className?: string }>; description: string }> = {
  sector_selected:    { label: 'Sektor Secimi',        icon: Building2,      description: 'Isletmenizin sektorunu belirleyin. Sektor secimi sablonlari ve icerikleri kisisellestirir.' },
  templates_adopted:  { label: 'Sablon Ekleme',        icon: LayoutTemplate, description: 'Sektorunuze uygun sablonlari icerik kutuphanenize ekleyin.' },
  knowledge_ready:    { label: 'Bilgi Bankasi',        icon: BookOpen,       description: 'SSS ve bilgi icerikleri olusturun. En az 5 aktif SSS gerekli.' },
  intents_configured: { label: 'Niyet Tanimlama',      icon: Brain,          description: 'Musteri niyetlerini tanimlayin. En az 3 niyet kalibi gerekli.' },
  first_flow_created: { label: 'Ilk Akis',             icon: GitBranch,      description: 'Chatbot akisinizi olusturun.' },
  flow_activated:     { label: 'Akis Aktivasyonu',     icon: Zap,            description: 'Bir chatbot akisini aktif hale getirin.' },
  whatsapp_connected: { label: 'WhatsApp Baglantisi',  icon: Smartphone,     description: 'WhatsApp entegrasyonunuzu tamamlayin.' },
};

export function OnboardingWizardPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const fetchStatus = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.getOnboardingStatus();
      setStatus(result);
      // Auto-select next_step or first incomplete
      if (!selectedKey) {
        setSelectedKey(result.next_step?.key ?? result.steps.find(s => !s.completed)?.key ?? result.steps[0]?.key ?? null);
      }
    } catch (err) {
      const detail = err instanceof Error ? err.message : String(err);
      setError(`Kurulum durumu alinamadi: ${detail}`);
    } finally {
      setLoading(false);
    }
  }, [selectedKey]);

  useEffect(() => { fetchStatus(); }, []);  // eslint-disable-line react-hooks/exhaustive-deps

  const isComplete = status != null && status.progress_pct === 100;
  const selectedStep = status?.steps.find(s => s.key === selectedKey) ?? null;
  const meta = selectedKey ? STEP_META[selectedKey] : null;
  const isNextStep = status?.next_step?.key === selectedKey;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Rocket className="w-6 h-6 text-brand-500" />
          <div>
            <h1 className="text-xl font-semibold text-navy-900">Kurulum Sihirbazi</h1>
            <p className="text-sm text-navy-400">Sisteminizi adim adim yapilandirin</p>
          </div>
        </div>
        <button
          onClick={fetchStatus}
          disabled={loading}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm text-navy-500 hover:text-navy-700 hover:bg-navy-50 rounded-lg transition-colors disabled:opacity-50"
        >
          <RefreshCw className={cn('w-4 h-4', loading && 'animate-spin')} />
          Yenile
        </button>
      </div>

      {/* Progress bar */}
      {status && (
        <div className="bg-white rounded-xl border border-navy-100 p-4">
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm font-medium text-navy-700">Ilerleme</span>
            <span className="text-sm font-semibold text-brand-600">%{status.progress_pct}</span>
          </div>
          <div className="h-2.5 bg-navy-100 rounded-full overflow-hidden">
            <div
              className="h-full bg-gradient-to-r from-brand-500 to-emerald-500 rounded-full transition-all duration-700 ease-out"
              style={{ width: `${status.progress_pct}%` }}
            />
          </div>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="p-3 bg-red-50 border border-red-100 rounded-xl text-sm text-red-700">
          {error}
        </div>
      )}

      {/* Loading */}
      {loading && !status && (
        <div className="py-12 text-center text-navy-300">Yukleniyor...</div>
      )}

      {/* Completion celebration */}
      {isComplete && status && <CompletionCard onGoHome={() => navigate('/')} />}

      {/* Stepper layout */}
      {status && !isComplete && (
        <div className="flex gap-6">
          {/* Left: Step sidebar */}
          <div className="w-72 shrink-0 space-y-1">
            {status.steps.map((step, idx) => (
              <StepItem
                key={step.key}
                step={step}
                index={idx}
                isSelected={step.key === selectedKey}
                isNext={step.key === status.next_step?.key}
                onClick={() => setSelectedKey(step.key)}
              />
            ))}
          </div>

          {/* Right: Detail panel */}
          <div className="flex-1 min-w-0">
            {selectedStep && meta && (
              <StepDetail
                step={selectedStep}
                meta={meta}
                isNext={isNextStep}
                actionUrl={isNextStep ? status.next_step?.action_url : null}
                hint={isNextStep ? status.next_step?.hint : null}
                onNavigate={(url) => navigate(url.replace('/app', ''))}
              />
            )}
          </div>
        </div>
      )}

      {/* Confetti overlay */}
      {isComplete && <ConfettiOverlay />}
    </div>
  );
}

// --- Subcomponents ---

function StepItem({
  step, index, isSelected, isNext, onClick,
}: {
  step: OnboardingStepResponse;
  index: number;
  isSelected: boolean;
  isNext: boolean;
  onClick: () => void;
}) {
  const meta = STEP_META[step.key];
  if (!meta) return null;
  const Icon = meta.icon;

  return (
    <button
      onClick={onClick}
      className={cn(
        'w-full flex items-center gap-3 px-3 py-3 rounded-xl text-left transition-all',
        isSelected && !step.completed && !isNext && 'bg-navy-50 border border-navy-200',
        isSelected && step.completed && 'bg-emerald-50 border border-emerald-200',
        isSelected && isNext && 'bg-brand-50 border border-brand-200',
        !isSelected && 'hover:bg-navy-50 border border-transparent',
        isNext && !isSelected && 'border-l-2 border-l-brand-400',
      )}
    >
      {/* Step indicator */}
      <div className="shrink-0">
        {step.completed ? (
          <CheckCircle2 className="w-5 h-5 text-emerald-500" />
        ) : isNext ? (
          <div className="w-5 h-5 rounded-full border-2 border-brand-400 flex items-center justify-center">
            <div className="w-2 h-2 rounded-full bg-brand-400 animate-pulse" />
          </div>
        ) : (
          <Circle className="w-5 h-5 text-navy-200" />
        )}
      </div>

      {/* Step content */}
      <div className="flex-1 min-w-0">
        <div className={cn(
          'text-sm font-medium truncate',
          step.completed ? 'text-emerald-700' : isNext ? 'text-brand-700' : 'text-navy-500',
        )}>
          {meta.label}
        </div>
        {step.detail && (
          <div className={cn(
            'text-xs truncate mt-0.5',
            step.completed ? 'text-emerald-500' : 'text-navy-400',
          )}>
            {step.detail}
          </div>
        )}
      </div>

      {/* Step number */}
      <span className="text-xs text-navy-300 shrink-0">{index + 1}/7</span>
    </button>
  );
}

function StepDetail({
  step, meta, isNext, actionUrl, hint, onNavigate,
}: {
  step: OnboardingStepResponse;
  meta: { label: string; icon: React.ComponentType<{ className?: string }>; description: string };
  isNext: boolean;
  actionUrl: string | null | undefined;
  hint: string | null | undefined;
  onNavigate: (url: string) => void;
}) {
  const Icon = meta.icon;

  return (
    <div className={cn(
      'rounded-xl border p-6',
      step.completed ? 'bg-emerald-50/50 border-emerald-200' :
      isNext ? 'bg-brand-50/50 border-brand-200' :
      'bg-white border-navy-100',
    )}>
      {/* Icon + title */}
      <div className="flex items-center gap-4 mb-4">
        <div className={cn(
          'w-12 h-12 rounded-xl flex items-center justify-center',
          step.completed ? 'bg-emerald-100' : isNext ? 'bg-brand-100' : 'bg-navy-100',
        )}>
          <Icon className={cn(
            'w-6 h-6',
            step.completed ? 'text-emerald-600' : isNext ? 'text-brand-600' : 'text-navy-400',
          )} />
        </div>
        <div>
          <h2 className={cn(
            'text-lg font-semibold',
            step.completed ? 'text-emerald-800' : isNext ? 'text-brand-800' : 'text-navy-800',
          )}>
            {meta.label}
          </h2>
          {step.completed && (
            <span className="inline-flex items-center gap-1 text-xs font-medium text-emerald-600 bg-emerald-100 px-2 py-0.5 rounded-full">
              <CheckCircle2 className="w-3 h-3" /> Tamamlandi
            </span>
          )}
        </div>
      </div>

      {/* Description */}
      <p className="text-sm text-navy-600 mb-4">{meta.description}</p>

      {/* Detail info */}
      {step.detail && step.detail !== 'bilinmiyor' && (
        <div className={cn(
          'text-sm px-3 py-2 rounded-lg mb-4',
          step.completed ? 'bg-emerald-100 text-emerald-700' : 'bg-navy-50 text-navy-600',
        )}>
          {step.detail}
        </div>
      )}

      {/* Degraded state */}
      {step.detail === 'bilinmiyor' && (
        <div className="text-sm px-3 py-2 rounded-lg mb-4 bg-amber-50 text-amber-700">
          Bu bilgi su an alinamiyor. Daha sonra tekrar deneyin.
        </div>
      )}

      {/* Hint + action */}
      {isNext && hint && (
        <div className="mt-4 p-3 bg-brand-50 border border-brand-100 rounded-lg">
          <p className="text-sm text-brand-700 mb-3">{hint}</p>
          {actionUrl && (
            <button
              onClick={() => onNavigate(actionUrl)}
              className="inline-flex items-center gap-2 px-4 py-2 bg-brand-600 text-white text-sm font-medium rounded-lg hover:bg-brand-700 transition-colors"
            >
              Basla
              <ArrowRight className="w-4 h-4" />
            </button>
          )}
        </div>
      )}

      {/* Completed step — navigate to review */}
      {step.completed && actionUrl && !isNext && (
        <button
          onClick={() => onNavigate(actionUrl)}
          className="inline-flex items-center gap-2 px-3 py-1.5 text-sm text-navy-500 hover:text-navy-700 hover:bg-navy-50 rounded-lg transition-colors"
        >
          Incele
          <ArrowRight className="w-3.5 h-3.5" />
        </button>
      )}
    </div>
  );
}

function CompletionCard({ onGoHome }: { onGoHome: () => void }) {
  return (
    <div className="bg-gradient-to-br from-emerald-50 to-brand-50 border border-emerald-200 rounded-xl p-8 text-center">
      <PartyPopper className="w-16 h-16 text-emerald-500 mx-auto mb-4" />
      <h2 className="text-2xl font-bold text-navy-900 mb-2">Tebrikler!</h2>
      <p className="text-navy-600 mb-6">Sisteminiz hazir. Tum kurulum adimlari tamamlandi.</p>
      <button
        onClick={onGoHome}
        className="inline-flex items-center gap-2 px-6 py-2.5 bg-brand-600 text-white font-medium rounded-lg hover:bg-brand-700 transition-colors"
      >
        Ana Sayfaya Don
        <ArrowRight className="w-4 h-4" />
      </button>
    </div>
  );
}

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
