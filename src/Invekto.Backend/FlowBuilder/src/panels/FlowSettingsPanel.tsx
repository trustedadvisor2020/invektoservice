import { useState, useEffect } from 'react';
import { useFlowStore } from '../store/flow-store';
import { validateFlow, type ValidationResult } from '../lib/api';
import { cn } from '../lib/utils';
import { WizardHistoryTab } from '../components/WizardHistoryTab';

type SettingsTab = 'settings' | 'ai_history';

interface FlowSettingsModalProps {
  open: boolean;
  onClose: () => void;
}

export function FlowSettingsModal({ open, onClose }: FlowSettingsModalProps) {
  const settings = useFlowStore((s) => s.flowSettings);
  const setSettings = useFlowStore((s) => s.setSettings);
  const toFlowConfig = useFlowStore((s) => s.toFlowConfig);
  const wizardHistory = useFlowStore((s) => s.wizardHistory);
  const hasWizardHistory = wizardHistory != null && wizardHistory.length > 0;

  const [activeTab, setActiveTab] = useState<SettingsTab>('settings');
  const [validating, setValidating] = useState(false);
  const [validationResult, setValidationResult] = useState<ValidationResult | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [jsonCopied, setJsonCopied] = useState(false);

  // Reset state when modal opens
  useEffect(() => {
    if (open) {
      setValidationResult(null);
      setValidationError(null);
      setJsonCopied(false);
      setActiveTab('settings');
    }
  }, [open]);

  const handleCopyJson = async () => {
    try {
      const config = toFlowConfig();
      await navigator.clipboard.writeText(JSON.stringify(config, null, 2));
      setJsonCopied(true);
      setTimeout(() => setJsonCopied(false), 2000);
    } catch (_e) {
      // Clipboard API may fail in non-secure contexts (http://) or when denied by permissions policy
      console.warn('Clipboard write failed — non-secure context or permission denied');
    }
  };

  // Close on Escape
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  const handleValidate = async () => {
    setValidating(true);
    setValidationResult(null);
    setValidationError(null);
    try {
      const config = toFlowConfig();
      const result = await validateFlow(config);
      setValidationResult(result);
    } catch (err) {
      setValidationError(err instanceof Error ? err.message : 'Dogrulama basarisiz');
    } finally {
      setValidating(false);
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/30" onClick={onClose} />

      {/* Modal */}
      <div className="relative bg-white rounded-xl shadow-xl w-[420px] max-h-[80vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-slate-200">
          <div className="flex items-center gap-4">
            <h2 className="text-sm font-semibold text-slate-800">Flow Ayarlari</h2>
            {hasWizardHistory && (
              <div className="flex border-b border-transparent -mb-4 pb-3">
                <button
                  onClick={() => setActiveTab('settings')}
                  className={cn(
                    'px-2 pb-1 text-xs font-medium border-b-2 transition-colors',
                    activeTab === 'settings'
                      ? 'border-blue-500 text-blue-600'
                      : 'border-transparent text-slate-400 hover:text-slate-600'
                  )}
                >
                  Ayarlar
                </button>
                <button
                  onClick={() => setActiveTab('ai_history')}
                  className={cn(
                    'px-2 pb-1 text-xs font-medium border-b-2 transition-colors flex items-center gap-1',
                    activeTab === 'ai_history'
                      ? 'border-purple-500 text-purple-600'
                      : 'border-transparent text-slate-400 hover:text-slate-600'
                  )}
                >
                  <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
                  </svg>
                  AI Gecmisi
                </button>
              </div>
            )}
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded hover:bg-slate-100 text-slate-400 hover:text-slate-600 transition-colors"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-4 overflow-y-auto">
        {activeTab === 'ai_history' ? (
          <WizardHistoryTab />
        ) : (
          <>
          <div className="flex gap-2">
            {/* Validate button */}
            <button
              onClick={handleValidate}
              disabled={validating}
              className={cn(
                'flex-1 px-3 py-2 rounded-lg text-sm font-medium transition-colors',
                validating
                  ? 'bg-slate-100 text-slate-400 cursor-not-allowed'
                  : 'bg-blue-600 hover:bg-blue-500 text-white'
              )}
            >
              {validating ? 'Dogrulaniyor...' : 'Akisi Dogrula'}
            </button>

            {/* Copy JSON button */}
            <button
              onClick={handleCopyJson}
              className={cn(
                'px-3 py-2 rounded-lg text-sm font-medium transition-colors border whitespace-nowrap',
                jsonCopied
                  ? 'bg-emerald-50 text-emerald-600 border-emerald-200'
                  : 'bg-slate-50 hover:bg-slate-100 text-slate-600 border-slate-200'
              )}
              title="Flow JSON'u panoya kopyala"
            >
              {jsonCopied ? 'Kopyalandi!' : 'JSON'}
            </button>
          </div>

          {/* Validation results */}
          {validationResult && (
            <div className={cn(
              'rounded-lg border p-3 text-xs space-y-1',
              validationResult.is_valid
                ? 'bg-green-50 border-green-200'
                : validationResult.errors.length > 0
                  ? 'bg-red-50 border-red-200'
                  : 'bg-amber-50 border-amber-200'
            )}>
              {validationResult.is_valid && validationResult.warnings.length === 0 && (
                <div className="flex items-center gap-1.5 text-green-700 font-medium">
                  <svg viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                  Akis gecerli, sorun yok.
                </div>
              )}

              {validationResult.errors.length > 0 && (
                <div>
                  <span className="text-red-700 font-medium">Hatalar ({validationResult.errors.length})</span>
                  <ul className="mt-1 space-y-0.5">
                    {validationResult.errors.map((e, i) => (
                      <li key={i} className="text-red-600 leading-tight">{e}</li>
                    ))}
                  </ul>
                </div>
              )}

              {validationResult.warnings.length > 0 && (
                <div>
                  <span className="text-amber-700 font-medium">Uyarilar ({validationResult.warnings.length})</span>
                  <ul className="mt-1 space-y-0.5">
                    {validationResult.warnings.map((w, i) => (
                      <li key={i} className="text-amber-600 leading-tight">{w}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}

          {validationError && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-600">
              {validationError}
            </div>
          )}

          <FieldGroup label="Mesai Disi Mesaji">
            <textarea
              value={settings.off_hours_message ?? ''}
              onChange={(e) => setSettings({ off_hours_message: e.target.value })}
              rows={3}
              className="w-full bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-sm text-slate-700 outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/20 resize-none"
              placeholder="Mesai saatleri disinda gonderilecek mesaj..."
            />
          </FieldGroup>

          <FieldGroup label="Bilinmeyen Girdi Mesaji">
            <textarea
              value={settings.unknown_input_message ?? ''}
              onChange={(e) => setSettings({ unknown_input_message: e.target.value })}
              rows={2}
              className="w-full bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-sm text-slate-700 outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/20 resize-none"
              placeholder="Gecersiz girdi mesaji..."
            />
          </FieldGroup>

          <div className="grid grid-cols-3 gap-3">
            <FieldGroup label="Handoff Guven Esigi">
              <input
                type="number"
                min={0}
                max={1}
                step={0.1}
                value={settings.handoff_confidence_threshold}
                onChange={(e) => setSettings({ handoff_confidence_threshold: parseFloat(e.target.value) || 0.5 })}
                className="w-full bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-sm text-slate-700 outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/20"
              />
            </FieldGroup>

            <FieldGroup label="Session Zaman Asimi (dk)">
              <input
                type="number"
                min={1}
                max={1440}
                value={settings.session_timeout_minutes}
                onChange={(e) => setSettings({ session_timeout_minutes: parseInt(e.target.value) || 30 })}
                className="w-full bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-sm text-slate-700 outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/20"
              />
            </FieldGroup>

            <FieldGroup label="Maks. Dongu Sayisi">
              <input
                type="number"
                min={1}
                max={100}
                value={settings.max_loop_count}
                onChange={(e) => setSettings({ max_loop_count: parseInt(e.target.value) || 10 })}
                className="w-full bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-sm text-slate-700 outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/20"
              />
            </FieldGroup>
          </div>
          </>
        )}
        </div>
      </div>
    </div>
  );
}

function FieldGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="block text-xs font-medium text-slate-500 uppercase tracking-wider mb-1.5">
        {label}
      </label>
      {children}
    </div>
  );
}
