import { useState, useEffect } from 'react';
import { useFlowStore } from '../../../stores/flow-store';
import { api, type FbValidationResult, type FbWorkingHoursInfo } from '../../../lib/api';
import { cn } from '../../../lib/utils';
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
  const flowMetadata = useFlowStore((s) => s.flowMetadata);
  const setMetadata = useFlowStore((s) => s.setMetadata);
  const wizardHistory = useFlowStore((s) => s.wizardHistory);
  const hasWizardHistory = wizardHistory != null && wizardHistory.length > 0;

  const [activeTab, setActiveTab] = useState<SettingsTab>('settings');
  const [validating, setValidating] = useState(false);
  const [validationResult, setFbValidationResult] = useState<FbValidationResult | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [jsonCopied, setJsonCopied] = useState(false);
  const [workingHours, setWorkingHours] = useState<FbWorkingHoursInfo | null>(null);

  // Reset state when modal opens + fetch working hours
  useEffect(() => {
    if (open) {
      setFbValidationResult(null);
      setValidationError(null);
      setJsonCopied(false);
      setActiveTab('settings');
      api.getFlowBuilderWorkingHours().then(setWorkingHours).catch(() => setWorkingHours(null));
    }
  }, [open]);

  const handleDownloadJson = () => {
    const config = toFlowConfig();
    const json = JSON.stringify(config, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const slug = (flowMetadata.name || 'flow').replace(/[^a-zA-Z0-9_-]/g, '_').toLowerCase();
    const a = document.createElement('a');
    a.href = url;
    a.download = `${slug}.json`;
    a.click();
    URL.revokeObjectURL(url);
    setJsonCopied(true);
    setTimeout(() => setJsonCopied(false), 2000);
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
    setFbValidationResult(null);
    setValidationError(null);
    try {
      const config = toFlowConfig();
      const result = await api.validateFlow(config);
      setFbValidationResult(result);
    } catch (err) {
      setValidationError(err instanceof Error ? err.message : 'Doğrulama başarısız');
    } finally {
      setValidating(false);
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/30" onMouseDown={onClose} />

      {/* Modal */}
      <div className="relative bg-white rounded-xl shadow-xl w-[420px] max-h-[80vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-navy-100">
          <div className="flex items-center gap-4">
            <h2 className="text-sm font-semibold text-navy-900">Flow Ayarları</h2>
            {hasWizardHistory && (
              <div className="flex border-b border-transparent -mb-4 pb-3">
                <button
                  onClick={() => setActiveTab('settings')}
                  className={cn(
                    'px-2 pb-1 text-xs font-medium border-b-2 transition-colors',
                    activeTab === 'settings'
                      ? 'border-brand-500 text-brand-600'
                      : 'border-transparent text-navy-300 hover:text-navy-600'
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
                      : 'border-transparent text-navy-300 hover:text-navy-600'
                  )}
                >
                  <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
                  </svg>
                  AI Geçmişi
                </button>
              </div>
            )}
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded hover:bg-navy-100 text-navy-300 hover:text-navy-600 transition-colors"
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
          <FieldGroup label="Flow Açıklaması" tooltip="Flow'un ne iş yaptığını kısa bir cümleyle açıklayın. Bu açıklama flow listesinde de görünür.">
            <textarea
              value={flowMetadata.description ?? ''}
              onChange={(e) => setMetadata({ description: e.target.value })}
              rows={2}
              className="w-full bg-navy-50 border border-navy-200 rounded-lg px-3 py-2 text-sm text-navy-700 outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20 resize-none"
              placeholder="Flow açıklaması..."
            />
          </FieldGroup>

          <div className="flex gap-2">
            {/* Validate button */}
            <button
              onClick={handleValidate}
              disabled={validating}
              className={cn(
                'flex-1 px-3 py-2 rounded-lg text-sm font-medium transition-colors',
                validating
                  ? 'bg-navy-100 text-navy-300 cursor-not-allowed'
                  : 'bg-brand-500 hover:bg-brand-600 text-white'
              )}
            >
              {validating ? 'Doğrulanıyor...' : 'Akışı Doğrula'}
            </button>

            {/* Copy JSON button */}
            <button
              onClick={handleDownloadJson}
              className={cn(
                'px-3 py-2 rounded-lg text-sm font-medium transition-colors border whitespace-nowrap flex items-center gap-1.5',
                jsonCopied
                  ? 'bg-emerald-50 text-emerald-600 border-emerald-200'
                  : 'bg-navy-50 hover:bg-navy-100 text-navy-500 border-navy-100'
              )}
              title="Flow JSON'u dosya olarak indir"
            >
              {jsonCopied ? 'İndirildi!' : (
                <>
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                  </svg>
                  JSON
                </>
              )}
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
                  Akış geçerli, sorun yok.
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
                  <span className="text-amber-700 font-medium">Uyarılar ({validationResult.warnings.length})</span>
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

          <FieldGroup
            label="Mesai Dışı Mesajı"
            tooltip={buildOffHoursTooltip(workingHours)}
          >
            <textarea
              value={settings.off_hours_message ?? ''}
              onChange={(e) => setSettings({ off_hours_message: e.target.value })}
              rows={3}
              className="w-full bg-navy-50 border border-navy-200 rounded-lg px-3 py-2 text-sm text-navy-700 outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20 resize-none"
              placeholder="Mesai saatleri dışında gönderilecek mesaj..."
            />
          </FieldGroup>

          <FieldGroup
            label="Bilinmeyen Girdi Mesajı"
            tooltip="Bot'un anlayamadığı veya eşleştiremediği mesajlara verilen yanıt. Örn: 'Anlayamadım, lütfen seçeneklerden birini seçin.' veya 'Bu konuda yardımcı olamıyorum, size nasıl yardımcı olabilirim?'"
          >
            <textarea
              value={settings.unknown_input_message ?? ''}
              onChange={(e) => setSettings({ unknown_input_message: e.target.value })}
              rows={2}
              className="w-full bg-navy-50 border border-navy-200 rounded-lg px-3 py-2 text-sm text-navy-700 outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20 resize-none"
              placeholder="Geçersiz girdi mesajı..."
            />
          </FieldGroup>

          <div className="grid grid-cols-3 gap-3">
            <FieldGroup
              label="Handoff Güven Eşiği"
              tooltip="AI'nin canlı temsilciye yönlendirme kararı için gereken minimum güven skoru (0-1 arası). 0.5 = orta güven, 0.8 = yüksek güven. Düşük değer daha fazla yönlendirme yapar."
            >
              <input
                type="number"
                min={0}
                max={1}
                step={0.1}
                value={settings.handoff_confidence_threshold}
                onChange={(e) => setSettings({ handoff_confidence_threshold: parseFloat(e.target.value) || 0.5 })}
                className="w-full bg-navy-50 border border-navy-200 rounded-lg px-3 py-2 text-sm text-navy-700 outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
              />
            </FieldGroup>

            <FieldGroup
              label="Session Zaman Aşımı (dk)"
              tooltip="Kullanıcının son mesajından sonra oturumun kapanacağı süre (dakika). Örn: 30 dk = yarım saat sessizlikte oturum sıfırlanır. 5 dk = hızlı işlemler için kısa oturum."
            >
              <input
                type="number"
                min={1}
                max={1440}
                value={settings.session_timeout_minutes}
                onChange={(e) => setSettings({ session_timeout_minutes: parseInt(e.target.value) || 30 })}
                className="w-full bg-navy-50 border border-navy-200 rounded-lg px-3 py-2 text-sm text-navy-700 outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
              />
            </FieldGroup>

            <FieldGroup
              label="Maks. Döngü Sayısı"
              tooltip="Bir akış adımının tekrarlanabileceği maksimum sayı. Sonsuz döngüyü önler. Örn: 10 = kullanıcı 10 kez yanlış girerse akış durur. 3 = daha katı, hızlı çıkış."
            >
              <input
                type="number"
                min={1}
                max={100}
                value={settings.max_loop_count}
                onChange={(e) => setSettings({ max_loop_count: parseInt(e.target.value) || 10 })}
                className="w-full bg-navy-50 border border-navy-200 rounded-lg px-3 py-2 text-sm text-navy-700 outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
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

const DAY_TR: Record<string, string> = {
  Monday: 'Pazartesi', Tuesday: 'Salı', Wednesday: 'Çarşamba',
  Thursday: 'Perşembe', Friday: 'Cuma', Saturday: 'Cumartesi', Sunday: 'Pazar',
};

function buildOffHoursTooltip(wh: FbWorkingHoursInfo | null): string {
  const base = 'Çalışma saatleri dışında gelen mesajlara otomatik gönderilen yanıt.';

  if (!wh?.configured)
    return `${base} Henüz mesai saatleri tanımlanmamış — tanımlandığında burada görünür.`;

  const parts: string[] = [base];

  if (wh.start && wh.end)
    parts.push(`Mevcut mesai: ${wh.start} – ${wh.end}`);

  if (wh.days_off && wh.days_off.length > 0) {
    const trDays = wh.days_off.map((d) => DAY_TR[d] ?? d).join(', ');
    parts.push(`Tatil günleri: ${trDays}`);
  }

  if (wh.timezone)
    parts.push(`Zaman dilimi: ${wh.timezone}`);

  return parts.join(' | ');
}

function FieldGroup({ label, tooltip, children }: { label: string; tooltip?: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="flex items-center gap-1 text-xs font-medium text-navy-400 uppercase tracking-wider mb-1.5">
        {label}
        {tooltip && <HelpTooltip text={tooltip} />}
      </label>
      {children}
    </div>
  );
}

function HelpTooltip({ text }: { text: string }) {
  const [show, setShow] = useState(false);

  return (
    <span
      className="relative inline-flex"
      onMouseEnter={() => setShow(true)}
      onMouseLeave={() => setShow(false)}
    >
      <span className="flex items-center justify-center w-3.5 h-3.5 rounded-full bg-navy-200 text-navy-400 hover:bg-navy-100 hover:text-brand-600 cursor-help transition-colors text-[9px] font-bold leading-none select-none">
        ?
      </span>
      {show && (
        <span className="absolute z-50 bottom-full left-1/2 -translate-x-1/2 mb-1.5 w-56 px-3 py-2 text-[11px] leading-relaxed font-normal normal-case tracking-normal text-navy-700 bg-white rounded-lg shadow-lg border border-navy-100 pointer-events-none">
          {text}
          <span className="absolute top-full left-1/2 -translate-x-1/2 -mt-px border-4 border-transparent border-t-white drop-shadow-sm" />
        </span>
      )}
    </span>
  );
}
