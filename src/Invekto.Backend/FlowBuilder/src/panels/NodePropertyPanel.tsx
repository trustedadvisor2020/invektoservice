import { useFlowStore } from '../store/flow-store';
import { getNodeTypeInfo, type FlowNodeType } from '../types/flow';
import type {
  MessageTextData,
  MessageMenuData,
  LogicConditionData,
  LogicSwitchData,
  AiIntentData,
  AiFaqData,
  ActionApiCallData,
  ActionDelayData,
  ActionHandoffData,
  UtilitySetVariableData,
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
        {nodeType === 'logic_condition' && (
          <LogicConditionProps data={selectedNode.data as LogicConditionData} onChange={update} />
        )}
        {nodeType === 'logic_switch' && (
          <LogicSwitchProps data={selectedNode.data as LogicSwitchData} onChange={update} />
        )}
        {nodeType === 'ai_intent' && (
          <AiIntentProps data={selectedNode.data as AiIntentData} onChange={update} />
        )}
        {nodeType === 'ai_faq' && (
          <AiFaqProps data={selectedNode.data as AiFaqData} onChange={update} />
        )}
        {nodeType === 'action_api_call' && (
          <ActionApiCallProps data={selectedNode.data as ActionApiCallData} onChange={update} />
        )}
        {nodeType === 'action_delay' && (
          <ActionDelayProps data={selectedNode.data as ActionDelayData} onChange={update} />
        )}
        {nodeType === 'action_handoff' && (
          <ActionHandoffProps data={selectedNode.data as ActionHandoffData} onChange={update} />
        )}
        {nodeType === 'utility_set_variable' && (
          <UtilitySetVariableProps data={selectedNode.data as UtilitySetVariableData} onChange={update} />
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

function LogicConditionProps({
  data,
  onChange,
}: {
  data: LogicConditionData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const operators = [
    { value: 'equals', label: 'Esittir (=)' },
    { value: 'contains', label: 'Icerir' },
    { value: 'starts_with', label: 'Baslar' },
    { value: 'greater_than', label: 'Buyuktur (>)' },
    { value: 'less_than', label: 'Kucuktur (<)' },
    { value: 'is_empty', label: 'Bos mu' },
    { value: 'regex', label: 'Regex' },
  ];

  return (
    <>
      <FieldGroup label="Degisken">
        <input
          type="text"
          value={data.variable ?? ''}
          onChange={(e) => onChange({ variable: e.target.value })}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
          placeholder="ornek: __last_input"
        />
      </FieldGroup>
      <FieldGroup label="Operator">
        <select
          value={data.operator ?? 'equals'}
          onChange={(e) => onChange({ operator: e.target.value })}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
        >
          {operators.map((op) => (
            <option key={op.value} value={op.value}>{op.label}</option>
          ))}
        </select>
      </FieldGroup>
      {data.operator !== 'is_empty' && (
        <FieldGroup label="Deger">
          <input
            type="text"
            value={data.value ?? ''}
            onChange={(e) => onChange({ value: e.target.value })}
            className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
            placeholder="Karsilastirilacak deger"
          />
        </FieldGroup>
      )}
    </>
  );
}

function LogicSwitchProps({
  data,
  onChange,
}: {
  data: LogicSwitchData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const cases = data.cases ?? [];
  const MAX_CASES = 10;

  const updateCase = (idx: number, value: string) => {
    const newCases = [...cases];
    newCases[idx] = { ...newCases[idx], value };
    onChange({ cases: newCases });
  };

  const addCase = () => {
    if (cases.length >= MAX_CASES) return;
    const nextIdx = cases.length + 1;
    const newCase = { value: '', handle_id: `case_${nextIdx}` };
    onChange({ cases: [...cases, newCase] });
  };

  const removeCase = (idx: number) => {
    onChange({ cases: cases.filter((_, i) => i !== idx) });
  };

  return (
    <>
      <FieldGroup label="Degisken">
        <input
          type="text"
          value={data.variable ?? ''}
          onChange={(e) => onChange({ variable: e.target.value })}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
          placeholder="ornek: musteri_tipi"
        />
      </FieldGroup>

      <FieldGroup label={`Durumlar (${cases.length}/${MAX_CASES})`}>
        <div className="space-y-2">
          {cases.map((c, idx) => (
            <div key={c.handle_id} className="flex items-center gap-1">
              <span className="text-xs text-slate-400 w-4 flex-shrink-0">{idx + 1}</span>
              <input
                type="text"
                value={c.value}
                onChange={(e) => updateCase(idx, e.target.value)}
                className="flex-1 bg-slate-50 border border-slate-300 rounded px-2 py-1 text-sm text-slate-700 outline-none focus:border-blue-500"
                placeholder="Deger..."
              />
              <button
                onClick={() => removeCase(idx)}
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
        {cases.length < MAX_CASES && (
          <button
            onClick={addCase}
            className="mt-2 w-full px-2 py-1 rounded border border-dashed border-slate-300 text-sm text-slate-500 hover:border-amber-500 hover:text-amber-600 transition-colors"
          >
            + Durum Ekle
          </button>
        )}
      </FieldGroup>

      <p className="text-xs text-slate-400">
        Hicbir durum eslesmediyse <strong>VARSAYILAN</strong> dala gider.
      </p>
    </>
  );
}

function ActionDelayProps({
  data,
  onChange,
}: {
  data: ActionDelayData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  return (
    <FieldGroup label="Bekleme Suresi (saniye)">
      <input
        type="number"
        min={1}
        max={300}
        value={data.seconds ?? 5}
        onChange={(e) => {
          const val = Math.max(1, Math.min(300, Number(e.target.value) || 1));
          onChange({ seconds: val });
        }}
        className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
      />
      <p className="text-xs text-slate-400 mt-1">Min: 1sn, Maks: 300sn (5dk)</p>
    </FieldGroup>
  );
}

function UtilitySetVariableProps({
  data,
  onChange,
}: {
  data: UtilitySetVariableData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  return (
    <>
      <FieldGroup label="Degisken Adi">
        <input
          type="text"
          value={data.variable_name ?? ''}
          onChange={(e) => onChange({ variable_name: e.target.value })}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
          placeholder="ornek: musteri_tipi"
        />
      </FieldGroup>
      <FieldGroup label="Deger Ifadesi">
        <textarea
          value={data.value_expression ?? ''}
          onChange={(e) => onChange({ value_expression: e.target.value })}
          rows={3}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-xs text-slate-700 outline-none focus:border-blue-500 resize-none font-mono"
          placeholder="ornek: {{__last_input}}"
        />
        <p className="text-xs text-slate-400 mt-1">
          {"{{degisken}}"} ile mevcut degiskenlere referans verebilirsiniz.
        </p>
      </FieldGroup>
    </>
  );
}

function AiIntentProps({
  data,
  onChange,
}: {
  data: AiIntentData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const intents = data.intents ?? [];
  const threshold = data.confidence_threshold ?? 0.5;

  const addIntent = () => {
    onChange({ intents: [...intents, ''] });
  };

  const updateIntent = (idx: number, value: string) => {
    const newIntents = [...intents];
    newIntents[idx] = value;
    onChange({ intents: newIntents });
  };

  const removeIntent = (idx: number) => {
    onChange({ intents: intents.filter((_, i) => i !== idx) });
  };

  return (
    <>
      <FieldGroup label={`Intentler (${intents.length})`}>
        <div className="space-y-1.5">
          {intents.map((intent, idx) => (
            <div key={idx} className="flex items-center gap-1">
              <input
                type="text"
                value={intent}
                onChange={(e) => updateIntent(idx, e.target.value)}
                className="flex-1 bg-slate-50 border border-slate-300 rounded px-2 py-1 text-xs text-slate-700 outline-none focus:border-purple-500 font-mono"
                placeholder="ornek: satin_alma"
              />
              <button
                onClick={() => removeIntent(idx)}
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
          onClick={addIntent}
          className="mt-2 w-full px-2 py-1 rounded border border-dashed border-slate-300 text-sm text-slate-500 hover:border-purple-500 hover:text-purple-600 transition-colors"
        >
          + Intent Ekle
        </button>
      </FieldGroup>

      <FieldGroup label={`Guven Esigi (${(threshold * 100).toFixed(0)}%)`}>
        <input
          type="range"
          min={0}
          max={100}
          value={Math.round(threshold * 100)}
          onChange={(e) => onChange({ confidence_threshold: Number(e.target.value) / 100 })}
          className="w-full accent-purple-500"
        />
        <div className="flex justify-between text-xs text-slate-400 mt-0.5">
          <span>0%</span>
          <span>50%</span>
          <span>100%</span>
        </div>
      </FieldGroup>

      <p className="text-xs text-slate-400">
        Musteri mesaji Claude AI ile analiz edilir. Guven esiginin uzerindeki intent'ler <strong>YUKSEK</strong> dalina yonlenir.
      </p>
    </>
  );
}

function AiFaqProps({
  data,
  onChange,
}: {
  data: AiFaqData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const minConf = data.min_confidence ?? 0.3;

  return (
    <>
      <FieldGroup label={`Min Guven (${(minConf * 100).toFixed(0)}%)`}>
        <input
          type="range"
          min={0}
          max={100}
          value={Math.round(minConf * 100)}
          onChange={(e) => onChange({ min_confidence: Number(e.target.value) / 100 })}
          className="w-full accent-purple-500"
        />
        <div className="flex justify-between text-xs text-slate-400 mt-0.5">
          <span>0%</span>
          <span>50%</span>
          <span>100%</span>
        </div>
      </FieldGroup>

      <p className="text-xs text-slate-400">
        Musteri mesaji FAQ veritabaninda aranir. Esik uzerindeki eslesmeler <strong>ESLESTI</strong> dalina yonlenir ve cevap otomatik gonderilir.
      </p>
    </>
  );
}

function ActionApiCallProps({
  data,
  onChange,
}: {
  data: ActionApiCallData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const methods = ['GET', 'POST', 'PUT', 'DELETE'];
  const timeoutMs = data.timeout_ms ?? 5000;
  const timeoutColor = timeoutMs <= 1000 ? 'text-green-500' : timeoutMs <= 5000 ? 'text-amber-500' : 'text-red-500';

  return (
    <>
      <FieldGroup label="HTTP Metot">
        <select
          value={data.method ?? 'GET'}
          onChange={(e) => onChange({ method: e.target.value })}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500"
        >
          {methods.map((m) => (
            <option key={m} value={m}>{m}</option>
          ))}
        </select>
      </FieldGroup>

      <FieldGroup label="URL">
        <input
          type="text"
          value={data.url ?? ''}
          onChange={(e) => onChange({ url: e.target.value })}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-xs text-slate-700 outline-none focus:border-blue-500 font-mono"
          placeholder="https://api.example.com/endpoint"
        />
        <p className="text-xs text-slate-400 mt-0.5">
          {"{{degisken}}"} destekler
        </p>
      </FieldGroup>

      <FieldGroup label="Body Template">
        <textarea
          value={data.body_template ?? ''}
          onChange={(e) => onChange({ body_template: e.target.value })}
          rows={3}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-xs text-slate-700 outline-none focus:border-blue-500 resize-none font-mono"
          placeholder='{"key": "{{degisken}}"}'
        />
      </FieldGroup>

      <FieldGroup label="Response Degiskeni">
        <input
          type="text"
          value={data.response_variable ?? 'api_response'}
          onChange={(e) => onChange({ response_variable: e.target.value })}
          className="w-full bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-700 outline-none focus:border-blue-500 font-mono"
          placeholder="api_response"
        />
      </FieldGroup>

      <FieldGroup label={`Zaman Asimi (${timeoutMs}ms)`}>
        <input
          type="range"
          min={100}
          max={30000}
          step={100}
          value={timeoutMs}
          onChange={(e) => onChange({ timeout_ms: Number(e.target.value) })}
          className="w-full accent-red-500"
        />
        <div className="flex justify-between text-xs mt-0.5">
          <span className="text-slate-400">100ms</span>
          <span className={timeoutColor}>{(timeoutMs / 1000).toFixed(1)}s</span>
          <span className="text-slate-400">30s</span>
        </div>
      </FieldGroup>
    </>
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
