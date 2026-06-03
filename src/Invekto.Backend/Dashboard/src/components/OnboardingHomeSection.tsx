import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Rocket, ChevronDown, CheckCircle2, ArrowRight, Lock } from 'lucide-react';
import { api, type OnboardingStatusResponse } from '../lib/api';
import { useAuth } from '../hooks/useAuth';
import { STEP_META } from '../pages/OnboardingWizardPage';
import { cn } from '../lib/utils';

/**
 * Home dashboard onboarding section.
 *
 * Collapsible "kurulum" band shown above the always-visible stats panel until
 * onboarding reaches 100%. Collapsed = thin progress strip; expanded = compact
 * checklist. Renders nothing when complete (progress_pct === 100) or when the
 * status cannot be fetched (fails silent so stats are never blocked).
 *
 * Full guided experience lives at /onboarding (OnboardingWizardPage) — this is
 * the lightweight home surface that links there via "Tüm adımları gör".
 */
export function OnboardingHomeSection() {
  const navigate = useNavigate();
  const { session } = useAuth();
  const tenantId = session?.tenantId;

  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [loaded, setLoaded] = useState(false);

  // Collapse state persists per tenant so we don't nag on every visit.
  const storageKey = tenantId != null ? `invekto:onboarding-home-collapsed:${tenantId}` : null;
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    if (!storageKey) return false;
    return localStorage.getItem(storageKey) === '1';
  });

  useEffect(() => {
    let cancelled = false;
    api.getOnboardingStatus()
      .then(result => { if (!cancelled) setStatus(result); })
      .catch(err => console.warn('[OnboardingHomeSection] status fetch failed:', err))
      .finally(() => { if (!cancelled) setLoaded(true); });
    return () => { cancelled = true; };
  }, [tenantId]);

  const toggle = useCallback(() => {
    setCollapsed(prev => {
      const next = !prev;
      if (storageKey) localStorage.setItem(storageKey, next ? '1' : '0');
      return next;
    });
  }, [storageKey]);

  const goNext = useCallback(() => {
    const url = status?.next_step?.action_url;
    if (url) navigate(url.replace('/app', ''));
  }, [status, navigate]);

  // Fail silent: nothing to show until loaded, on error, or once complete.
  if (!loaded || !status || status.progress_pct >= 100) return null;

  const completedCount = status.steps.filter(s => s.completed).length;
  const total = status.steps.length;
  const nextKey = status.next_step?.key;
  const nextMeta = nextKey ? STEP_META[nextKey] : undefined;

  return (
    <div className="rounded-2xl border border-navy-100 bg-white overflow-hidden shadow-sm">
      {/* ── Header / thin progress strip (always visible) ── */}
      <button
        onClick={toggle}
        className="w-full text-left px-5 py-4 flex items-center gap-4 hover:bg-navy-50/40 transition-colors"
      >
        <div className="w-9 h-9 rounded-xl bg-brand-50 flex items-center justify-center shrink-0">
          <Rocket className="w-4 h-4 text-brand-500" />
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-navy-900">Kurulumunuzu tamamlayın</span>
            <span className="text-xs font-medium text-brand-600">%{status.progress_pct}</span>
          </div>
          {/* Linear progress bar */}
          <div className="mt-2 h-1.5 w-full rounded-full bg-navy-100 overflow-hidden">
            <div
              className="h-full rounded-full transition-all duration-700"
              style={{
                width: `${status.progress_pct}%`,
                background: 'linear-gradient(90deg, #635BFF, #10B981)',
              }}
            />
          </div>
          <p className="text-xs text-navy-400 mt-1.5">
            {collapsed && nextMeta
              ? `Sıradaki: ${nextMeta.label}`
              : `${completedCount}/${total} adım tamamlandı`}
          </p>
        </div>

        <ChevronDown
          className={cn(
            'w-4 h-4 text-navy-300 transition-transform duration-200 shrink-0',
            !collapsed && 'rotate-180',
          )}
        />
      </button>

      {/* ── Expanded compact checklist ── */}
      {!collapsed && (
        <div className="px-5 pb-5 pt-1 border-t border-navy-100">
          <div className="space-y-1.5 mt-3">
            {status.steps.map(step => {
              const meta = STEP_META[step.key];
              if (!meta) return null;
              const Icon = meta.icon;
              const isNext = step.key === nextKey;

              return (
                <div
                  key={step.key}
                  className={cn(
                    'flex items-center gap-3 rounded-xl px-3 py-2.5 transition-colors',
                    isNext ? 'bg-navy-50' : 'hover:bg-navy-50/50',
                  )}
                >
                  <div
                    className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
                    style={{
                      backgroundColor: step.completed
                        ? '#ecfdf5'
                        : isNext
                          ? `${meta.accentHex}12`
                          : '#f1f5f9',
                    }}
                  >
                    {step.completed ? (
                      <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                    ) : isNext ? (
                      <Icon className="w-4 h-4" style={{ color: meta.accentHex }} />
                    ) : (
                      <Lock className="w-3.5 h-3.5 text-navy-300" />
                    )}
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className={cn(
                        'text-sm font-medium truncate',
                        step.completed ? 'text-navy-600' : isNext ? 'text-navy-900' : 'text-navy-400',
                      )}>
                        {meta.label}
                      </span>
                      {step.completed && (
                        <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500 shrink-0" />
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
                    <p className={cn(
                      'text-xs truncate mt-0.5',
                      step.completed ? 'text-navy-300' :
                      step.detail && step.detail !== 'bilinmiyor' ? 'text-navy-400' : 'text-navy-300 italic',
                    )}>
                      {step.detail && step.detail !== 'bilinmiyor' ? step.detail : meta.fallback}
                    </p>
                  </div>
                </div>
              );
            })}
          </div>

          {/* Actions */}
          <div className="flex items-center justify-between gap-3 mt-4">
            <button
              onClick={() => navigate('/onboarding')}
              className="text-xs font-medium text-navy-400 hover:text-navy-600 transition-colors"
            >
              Tüm adımları gör
            </button>
            {status.next_step && (
              <button
                onClick={goNext}
                className="inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold text-white rounded-xl transition-all hover:shadow-md hover:-translate-y-px active:translate-y-0"
                style={{
                  background: nextMeta
                    ? `linear-gradient(135deg, ${nextMeta.accentHex}, ${nextMeta.accentHex}cc)`
                    : 'linear-gradient(135deg, #635BFF, #635BFFcc)',
                }}
              >
                Kuruluma devam et
                <ArrowRight className="w-4 h-4" />
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
