import { useFlowStore } from '../store/flow-store';

export function FlowSettingsPanel() {
  const settings = useFlowStore((s) => s.flowSettings);
  const setSettings = useFlowStore((s) => s.setSettings);

  return (
    <div className="w-64 bg-white border-l border-slate-200 flex-shrink-0 overflow-y-auto">
      <div className="p-3 border-b border-slate-200">
        <span className="text-base font-medium text-slate-700">Flow Ayarlari</span>
      </div>

      <div className="p-3 space-y-3">
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
