import { useFlowStore } from '../store/flow-store';
import { getNodeTypeInfo, type FlowNodeType } from '../types/flow';
import type {
  MessageTextData,
  MessageMenuData,
  ActionHandoffData,
  UtilityNoteData,
} from '../types/flow';

export function NodePropertyPanel() {
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const nodes = useFlowStore((s) => s.nodes);
  const updateNodeData = useFlowStore((s) => s.updateNodeData);
  const deleteNode = useFlowStore((s) => s.deleteNode);

  const selectedNode = selectedNodeId
    ? nodes.find((n) => n.id === selectedNodeId)
    : null;

  if (!selectedNode) {
    return (
      <div className="w-64 bg-white border-l border-slate-200 flex-shrink-0 flex items-center justify-center">
        <p className="text-sm text-slate-400 text-center px-4">
          Ozelliklerini duzenlemek icin bir node secin
        </p>
      </div>
    );
  }

  const nodeType = selectedNode.type as FlowNodeType;
  const info = getNodeTypeInfo(nodeType);

  const update = (data: Record<string, unknown>) => {
    updateNodeData(selectedNode.id, data);
  };

  return (
    <div className="w-64 bg-white border-l border-slate-200 flex-shrink-0 overflow-y-auto">
      {/* Header */}
      <div className="p-3 border-b border-slate-200">
        <div className="flex items-center gap-2">
          <div
            className="w-3 h-3 rounded-sm flex-shrink-0"
            style={{ backgroundColor: info?.color ?? '#6b7280' }}
          />
          <span className="text-base font-medium text-slate-700">
            {info?.label ?? nodeType}
          </span>
        </div>
        <p className="text-xs text-slate-400 mt-0.5">{info?.description}</p>
      </div>

      {/* Common: Label */}
      <div className="p-3 space-y-3">
        <FieldGroup label="Etiket">
          <input
            type="text"
            value={(selectedNode.data as { label: string }).label ?? ''}
            onChange={(e) => update({ label: e.target.value })}
            className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
          />
        </FieldGroup>

        {/* Type-specific fields */}
        {nodeType === 'trigger_start' && (
          <TriggerStartProps />
        )}
        {nodeType === 'message_text' && (
          <MessageTextProps data={selectedNode.data as MessageTextData} onChange={update} />
        )}
        {nodeType === 'message_menu' && (
          <MessageMenuProps data={selectedNode.data as MessageMenuData} onChange={update} />
        )}
        {nodeType === 'action_handoff' && (
          <ActionHandoffProps data={selectedNode.data as ActionHandoffData} onChange={update} />
        )}
        {nodeType === 'utility_note' && (
          <UtilityNoteProps data={selectedNode.data as UtilityNoteData} onChange={update} />
        )}

        {/* Delete button (not for trigger_start) */}
        {nodeType !== 'trigger_start' && (
          <div className="pt-3 border-t border-slate-200">
            <button
              onClick={() => deleteNode(selectedNode.id)}
              className="w-full px-3 py-1.5 rounded-md text-sm font-medium bg-red-50 text-red-600 hover:bg-red-100 transition-colors"
            >
              Node'u Sil
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

// -- Field Components --

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

function TriggerStartProps() {
  return (
    <p className="text-xs text-slate-400">
      Baslangic node'u yapilandirma gerektirmez. Musteri mesaj gonderdiginde bu node'dan akis baslar.
    </p>
  );
}

function MessageTextProps({
  data,
  onChange,
}: {
  data: MessageTextData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  return (
    <FieldGroup label="Mesaj Metni">
      <textarea
        value={data.text ?? ''}
        onChange={(e) => onChange({ text: e.target.value })}
        rows={4}
        className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-xs text-slate-700 outline-none focus:border-blue-500 resize-none"
        placeholder="Gonderilecek mesaj..."
      />
    </FieldGroup>
  );
}

function MessageMenuProps({
  data,
  onChange,
}: {
  data: MessageMenuData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const options = data.options ?? [];

  const updateOption = (idx: number, field: string, value: string) => {
    const newOptions = [...options];
    newOptions[idx] = { ...newOptions[idx], [field]: value };
    onChange({ options: newOptions });
  };

  const addOption = () => {
    const nextKey = String(options.length + 1);
    const newOpt = {
      key: nextKey,
      label: `Secenek ${nextKey}`,
      handle_id: `opt_${nextKey}`,
    };
    onChange({ options: [...options, newOpt] });
  };

  const removeOption = (idx: number) => {
    onChange({ options: options.filter((_, i) => i !== idx) });
  };

  return (
    <>
      <FieldGroup label="Menu Metni">
        <input
          type="text"
          value={data.text ?? ''}
          onChange={(e) => onChange({ text: e.target.value })}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
          placeholder="Secim yapin:"
        />
      </FieldGroup>

      <FieldGroup label="Secenekler">
        <div className="space-y-2">
          {options.map((opt, idx) => (
            <div key={opt.handle_id} className="flex items-center gap-1">
              <input
                type="text"
                value={opt.key}
                onChange={(e) => updateOption(idx, 'key', e.target.value)}
                className="w-8 bg-slate-50 border border-slate-300 rounded px-1 py-1 text-xs text-slate-700 outline-none focus:border-blue-500 text-center"
              />
              <input
                type="text"
                value={opt.label}
                onChange={(e) => updateOption(idx, 'label', e.target.value)}
                className="flex-1 bg-slate-50 border border-slate-300 rounded px-2 py-1 text-sm text-slate-700 outline-none focus:border-blue-500"
              />
              <button
                onClick={() => removeOption(idx)}
                className="p-0.5 text-slate-400 hover:text-red-500 transition-colors"
                title="Kaldir"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
                  <line x1="18" y1="6" x2="6" y2="18" />
                  <line x1="6" y1="6" x2="18" y2="18" />
                </svg>
              </button>
            </div>
          ))}
        </div>
        <button
          onClick={addOption}
          className="mt-2 w-full px-2 py-1 rounded border border-dashed border-slate-300 text-sm text-slate-500 hover:border-blue-500 hover:text-blue-600 transition-colors"
        >
          + Secenek Ekle
        </button>
      </FieldGroup>
    </>
  );
}

function ActionHandoffProps({
  data,
  onChange,
}: {
  data: ActionHandoffData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  return (
    <FieldGroup label="Ozet Sablonu">
      <textarea
        value={data.summary_template ?? ''}
        onChange={(e) => onChange({ summary_template: e.target.value })}
        rows={3}
        className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-xs text-slate-700 outline-none focus:border-blue-500 resize-none"
        placeholder="Temsilciye aktarilacak ozet..."
      />
    </FieldGroup>
  );
}

function UtilityNoteProps({
  data,
  onChange,
}: {
  data: UtilityNoteData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const colors = [
    { label: 'Sari', value: '#fef3c7' },
    { label: 'Mavi', value: '#dbeafe' },
    { label: 'Yesil', value: '#dcfce7' },
    { label: 'Kirmizi', value: '#fee2e2' },
    { label: 'Mor', value: '#ede9fe' },
  ];

  return (
    <>
      <FieldGroup label="Not Metni">
        <textarea
          value={data.text ?? ''}
          onChange={(e) => onChange({ text: e.target.value })}
          rows={5}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-xs text-slate-700 outline-none focus:border-blue-500 resize-none"
          placeholder="Notunuz..."
        />
      </FieldGroup>
      <FieldGroup label="Renk">
        <div className="flex gap-1.5">
          {colors.map((c) => (
            <button
              key={c.value}
              onClick={() => onChange({ color: c.value })}
              className="w-6 h-6 rounded border-2 transition-transform hover:scale-110"
              style={{
                backgroundColor: c.value,
                borderColor: (data.color || '#fef3c7') === c.value ? '#3b82f6' : 'transparent',
              }}
              title={c.label}
            />
          ))}
        </div>
      </FieldGroup>
    </>
  );
}
