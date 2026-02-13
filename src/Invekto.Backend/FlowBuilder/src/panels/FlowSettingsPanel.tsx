import { useState } from 'react';
import { useFlowStore } from '../store/flow-store';
import { validateFlow, type ValidationResult } from '../lib/api';
import { cn } from '../lib/utils';

export function FlowSettingsPanel() {
  const settings = useFlowStore((s) => s.flowSettings);
  const setSettings = useFlowStore((s) => s.setSettings);
  const toFlowConfig = useFlowStore((s) => s.toFlowConfig);

  const [validating, setValidating] = useState(false);
  const [validationResult, setValidationResult] = useState<ValidationResult | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

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

  return (
    <div className="w-64 bg-white border-l border-slate-200 flex-shrink-0 overflow-y-auto">
      <div className="p-3 border-b border-slate-200">
        <span className="text-base font-medium text-slate-700">Flow Ayarlari</span>
      </div>

      <div className="p-3 space-y-3">
        {/* Validate button */}
        <button
          onClick={handleValidate}
          disabled={validating}
          className={cn(
            'w-full px-3 py-2 rounded-lg text-sm font-medium transition-colors',
            validating
              ? 'bg-slate-100 text-slate-400 cursor-not-allowed'
              : 'bg-blue-600 hover:bg-blue-500 text-white'
          )}
        >
          {validating ? 'Dogrulaniyor...' : 'Akisi Dogrula'}
        </button>

        {/* Validation results */}
        {validationResult && (
          <div className={cn(
            'rounded-lg border p-2.5 text-xs space-y-1',
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
          <div className="rounded-lg border border-red-200 bg-red-50 p-2.5 text-xs text-red-600">
            {validationError}
          </div>
        )}

        <FieldGroup label="Mesai Disi Mesaji">
          <textarea
            value={settings.off_hours_message ?? ''}
            onChange={(e) => setSettings({ off_hours_message: e.target.value })}
            rows={3}
            className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500 resize-none"
            placeholder="Mesai saatleri disinda gonderilecek mesaj..."
          />
        </FieldGroup>

        <FieldGroup label="Bilinmeyen Girdi Mesaji">
          <textarea
            value={settings.unknown_input_message ?? ''}
            onChange={(e) => setSettings({ unknown_input_message: e.target.value })}
            rows={2}
            className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500 resize-none"
            placeholder="Gecersiz girdi mesaji..."
          />
        </FieldGroup>

        <FieldGroup label="Handoff Guven Esigi">
          <input
            type="number"
            min={0}
            max={1}
            step={0.1}
            value={settings.handoff_confidence_threshold}
            onChange={(e) => setSettings({ handoff_confidence_threshold: parseFloat(e.target.value) || 0.5 })}
            className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
          />
        </FieldGroup>

        <FieldGroup label="Session Zaman Asimi (dk)">
          <input
            type="number"
            min={1}
            max={1440}
            value={settings.session_timeout_minutes}
            onChange={(e) => setSettings({ session_timeout_minutes: parseInt(e.target.value) || 30 })}
            className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
          />
        </FieldGroup>

        <FieldGroup label="Maks. Dongu Sayisi">
          <input
            type="number"
            min={1}
            max={100}
            value={settings.max_loop_count}
            onChange={(e) => setSettings({ max_loop_count: parseInt(e.target.value) || 10 })}
            className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
          />
        </FieldGroup>
      </div>
    </div>
  );
}

function FieldGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="block text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">
        {label}
      </label>
      {children}
    </div>
  );
}
