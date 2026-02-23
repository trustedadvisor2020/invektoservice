import { useState, useEffect, useCallback } from 'react';
import { useParams } from 'react-router-dom';
import { useFlowStore } from '../../../stores/flow-store';
import { useAuth } from '../../../hooks/useAuth';
import { getNodeTypeInfo, type FlowNodeType } from '../../../types/flow';
import { api, type FbAvailableInstance, type FlowSummary } from '../../../lib/api';
import type {
  MessageTextData,
  MessageMenuData,
  LogicConditionData,
  LogicSwitchData,
  AiIntentData,
  AiFaqData,
  AiSentimentData,
  WebhookTriggerData,
  ScheduleTriggerData,
  ActionApiCallData,
  ActionDelayData,
  ActionHandoffData,
  ActionCallFlowData,
  UtilitySetVariableData,
  UtilityNoteData,
} from '../../../types/flow';

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
      <div className="w-64 bg-white border-l border-navy-100 flex-shrink-0 flex items-center justify-center">
        <p className="text-sm text-navy-300 text-center px-4">
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
    <div className="w-64 bg-white border-l border-navy-100 flex-shrink-0 overflow-y-auto">
      {/* Header */}
      <div className="p-3 border-b border-navy-100">
        <div className="flex items-center gap-2">
          <div
            className="w-3 h-3 rounded-sm flex-shrink-0"
            style={{ backgroundColor: info?.color ?? '#6b7280' }}
          />
          <span className="text-base font-medium text-navy-700">
            {info?.label ?? nodeType}
          </span>
        </div>
        <p className="text-xs text-navy-300 mt-0.5">{info?.description}</p>
      </div>

      {/* Common: Label */}
      <div className="p-3 space-y-3">
        <FieldGroup label="Etiket">
          <input
            type="text"
            value={(selectedNode.data as { label: string }).label ?? ''}
            onChange={(e) => update({ label: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          />
        </FieldGroup>

        {/* Type-specific fields */}
        {nodeType === 'trigger_start' && (
          <TriggerStartProps data={selectedNode.data as Record<string, unknown>} onChange={update} />
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
        {nodeType === 'ai_sentiment' && (
          <AiSentimentProps data={selectedNode.data as AiSentimentData} onChange={update} />
        )}
        {nodeType === 'webhook_trigger' && (
          <WebhookTriggerProps data={selectedNode.data as WebhookTriggerData} onChange={update} />
        )}
        {nodeType === 'outbound_trigger' && (
          <OutboundTriggerProps />
        )}
        {nodeType === 'schedule_trigger' && (
          <ScheduleTriggerProps data={selectedNode.data as ScheduleTriggerData} onChange={update} />
        )}
        {nodeType === 'action_call_flow' && (
          <CallFlowProps data={selectedNode.data as ActionCallFlowData} onChange={update} />
        )}

        {/* Delete button (not for trigger types) */}
        {nodeType !== 'trigger_start' && nodeType !== 'webhook_trigger' && nodeType !== 'outbound_trigger' && nodeType !== 'schedule_trigger' && (
          <div className="pt-3 border-t border-navy-100">
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
      <label className="block text-xs font-medium text-navy-400 uppercase tracking-wider mb-1">
        {label}
      </label>
      {children}
    </div>
  );
}

const INSTANCE_TYPE_LABELS: Record<number, string> = { 1: 'WhatsApp', 2: 'Web', 5: 'Kanal', 6: 'SMS' };

function TriggerStartProps({ data, onChange }: { data: Record<string, unknown>; onChange: (d: Record<string, unknown>) => void }) {
  const { session } = useAuth();
  const { flowId: flowIdParam } = useParams<{ flowId: string }>();
  const flowId = flowIdParam ? parseInt(flowIdParam, 10) : undefined;
  const tenantId = session?.tenantId;

  const [instances, setInstances] = useState<FbAvailableInstance[]>([]);
  const [loading, setLoading] = useState(false);

  const selectedIds: string[] = Array.isArray(data.allowed_instance_ids) ? data.allowed_instance_ids as string[] : [];

  const fetchInstances = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    try {
      const result = await api.getFlowBuilderInstances(flowId);
      setInstances(result.instances);
    } catch (err: unknown) {
      console.warn('[TriggerStartProps] Instance fetch failed:', err instanceof Error ? err.message : err);
    } finally {
      setLoading(false);
    }
  }, [tenantId, flowId]);

  useEffect(() => {
    fetchInstances();
  }, [fetchInstances]);

  const toggleInstance = (instanceId: string) => {
    const newIds = selectedIds.includes(instanceId)
      ? selectedIds.filter(id => id !== instanceId)
      : [...selectedIds, instanceId];
    onChange({ allowed_instance_ids: newIds });
  };

  if (instances.length === 0 && !loading) {
    return (
      <p className="text-xs text-navy-300">
        Musteri mesaj gonderdiginde bu node'dan akis baslar.
        {tenantId && <span className="block mt-1 text-navy-200">Hat secimi icin Ayarlar sayfasindan WapCRM hatlarini yukleyin.</span>}
      </p>
    );
  }

  return (
    <FieldGroup label={`Hatlar (${selectedIds.length}/${instances.length})`}>
      {loading ? (
        <p className="text-xs text-navy-300">Yukleniyor...</p>
      ) : (
        <div className="space-y-1.5">
          {instances.map(inst => {
            const isOtherFlow = inst.assignedFlowId != null && inst.assignedFlowId !== flowId;
            return (
              <label key={inst.instanceId} className="flex items-start gap-2 cursor-pointer group" title={`ID: ${inst.instanceId}`}>
                <input
                  type="checkbox"
                  checked={selectedIds.includes(inst.instanceId)}
                  onChange={() => toggleInstance(inst.instanceId)}
                  className="w-3.5 h-3.5 rounded border-navy-200 text-emerald-600 focus:ring-emerald-500 mt-0.5"
                />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-1">
                    <span className="text-xs text-navy-700 group-hover:text-navy-900 truncate">
                      {inst.instanceName}
                    </span>
                    <span className="text-[10px] text-navy-300 flex-shrink-0">
                      {INSTANCE_TYPE_LABELS[inst.instanceType] || 'Diger'}
                    </span>
                  </div>
                  {isOtherFlow && (
                    <span className="text-[10px] text-amber-600 leading-tight block">
                      {inst.assignedFlowName ?? 'Baska akis'} akisinda
                    </span>
                  )}
                </div>
              </label>
            );
          })}
        </div>
      )}
      <p className="text-xs text-navy-300 mt-2">
        Secili hatlardan gelen mesajlar bu akisa yonlendirilir.
      </p>
    </FieldGroup>
  );
}

function MessageTextProps({
  data,
  onChange,
}: {
  data: MessageTextData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const waitForInput = data.wait_for_input === true;

  return (
    <>
      <FieldGroup label="Mesaj Metni">
        <textarea
          value={data.text ?? ''}
          onChange={(e) => onChange({ text: e.target.value })}
          rows={4}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-brand-500 resize-none"
          placeholder="Gonderilecek mesaj..."
        />
      </FieldGroup>
      <FieldGroup label="Davranis">
        <label className="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            checked={waitForInput}
            onChange={(e) => onChange({ wait_for_input: e.target.checked })}
            className="w-4 h-4 rounded border-navy-200 text-blue-600 focus:ring-blue-500 accent-blue-500"
          />
          <span className="text-xs text-navy-500">Kullanici yanitini bekle</span>
        </label>
        {waitForInput && (
          <p className="text-[10px] text-navy-400 mt-1">
            Mesaj gonderildikten sonra akis durur ve kullanicinin yanitini bekler.
            Yanit, sonraki node'larda {'{{user_input}}'} olarak kullanilabilir.
          </p>
        )}
      </FieldGroup>
    </>
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
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
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
                className="w-8 bg-navy-50 border border-navy-200 rounded px-1 py-1 text-xs text-navy-700 outline-none focus:border-brand-500 text-center"
              />
              <input
                type="text"
                value={opt.label}
                onChange={(e) => updateOption(idx, 'label', e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-2 py-1 text-sm text-navy-700 outline-none focus:border-brand-500"
              />
              <button
                onClick={() => removeOption(idx)}
                className="p-0.5 text-navy-300 hover:text-red-500 transition-colors"
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
          className="mt-2 w-full px-2 py-1 rounded border border-dashed border-navy-200 text-sm text-navy-400 hover:border-brand-500 hover:text-brand-600 transition-colors"
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
        className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-brand-500 resize-none"
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
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          placeholder="ornek: __last_input"
        />
      </FieldGroup>
      <FieldGroup label="Operator">
        <select
          value={data.operator ?? 'equals'}
          onChange={(e) => onChange({ operator: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
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
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
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
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          placeholder="ornek: musteri_tipi"
        />
      </FieldGroup>

      <FieldGroup label={`Durumlar (${cases.length}/${MAX_CASES})`}>
        <div className="space-y-2">
          {cases.map((c, idx) => (
            <div key={c.handle_id} className="flex items-center gap-1">
              <span className="text-xs text-navy-300 w-4 flex-shrink-0">{idx + 1}</span>
              <input
                type="text"
                value={c.value}
                onChange={(e) => updateCase(idx, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-2 py-1 text-sm text-navy-700 outline-none focus:border-brand-500"
                placeholder="Deger..."
              />
              <button
                onClick={() => removeCase(idx)}
                className="p-0.5 text-navy-300 hover:text-red-500 transition-colors"
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
            className="mt-2 w-full px-2 py-1 rounded border border-dashed border-navy-200 text-sm text-navy-400 hover:border-amber-500 hover:text-amber-600 transition-colors"
          >
            + Durum Ekle
          </button>
        )}
      </FieldGroup>

      <p className="text-xs text-navy-300">
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
        className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
      />
      <p className="text-xs text-navy-300 mt-1">Min: 1sn, Maks: 300sn (5dk)</p>
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
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          placeholder="ornek: musteri_tipi"
        />
      </FieldGroup>
      <FieldGroup label="Deger Ifadesi">
        <textarea
          value={data.value_expression ?? ''}
          onChange={(e) => onChange({ value_expression: e.target.value })}
          rows={3}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-brand-500 resize-none font-mono"
          placeholder="ornek: {{__last_input}}"
        />
        <p className="text-xs text-navy-300 mt-1">
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
  const rawIntents = data.intents ?? [];
  const intents: string[] = Array.isArray(rawIntents)
    ? rawIntents
    : typeof rawIntents === 'string'
      ? (() => { try { const p = JSON.parse(rawIntents); return Array.isArray(p) ? p : []; } catch { return []; } })()
      : [];
  const threshold = typeof data.confidence_threshold === 'string'
    ? parseFloat(data.confidence_threshold) || 0.5
    : data.confidence_threshold ?? 0.5;
  const askName = data.ask_name !== false; // default true

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
      <FieldGroup label="Konusma Ayarlari">
        <label className="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            checked={askName}
            onChange={(e) => onChange({ ask_name: e.target.checked })}
            className="w-4 h-4 rounded border-navy-200 text-purple-600 focus:ring-purple-500 accent-purple-500"
          />
          <span className="text-xs text-navy-500">Musteri ismini sor</span>
        </label>
        <p className="text-[10px] text-navy-300 mt-1">
          Acikken musteri ismini sorar, dogrular ve isimle hitap eder. Anlayamadigi mesajlarda sohbete devam eder.
        </p>
        {askName && (
          <div className="mt-2">
            <input
              type="text"
              value={data.greeting_message ?? ''}
              onChange={(e) => onChange({ greeting_message: e.target.value || undefined })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-purple-500"
              placeholder="Merhaba! Isminizi ogrenebilir miyim?"
            />
            <p className="text-[10px] text-navy-300 mt-0.5">Karsilama mesaji (bos = varsayilan)</p>
          </div>
        )}
      </FieldGroup>

      <FieldGroup label={`Intentler (${intents.length})`}>
        <div className="space-y-1.5">
          {intents.map((intent, idx) => (
            <div key={idx} className="flex items-center gap-1">
              <input
                type="text"
                value={intent}
                onChange={(e) => updateIntent(idx, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-2 py-1 text-xs text-navy-700 outline-none focus:border-purple-500 font-mono"
                placeholder="ornek: satin_alma"
              />
              <button
                onClick={() => removeIntent(idx)}
                className="p-0.5 text-navy-300 hover:text-red-500 transition-colors"
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
          className="mt-2 w-full px-2 py-1 rounded border border-dashed border-navy-200 text-sm text-navy-400 hover:border-purple-500 hover:text-purple-600 transition-colors"
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
        <div className="flex justify-between text-xs text-navy-300 mt-0.5">
          <span>0%</span>
          <span>50%</span>
          <span>100%</span>
        </div>
      </FieldGroup>

      <p className="text-xs text-navy-300">
        Musteri mesaji Claude AI ile analiz edilir. Orta guvendeki intent'ler icin &quot;bunu mu demek istiyorsunuz?&quot; onay sorusu sorulur. Dusuk guvende sohbet devam eder.
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
        <div className="flex justify-between text-xs text-navy-300 mt-0.5">
          <span>0%</span>
          <span>50%</span>
          <span>100%</span>
        </div>
      </FieldGroup>

      <p className="text-xs text-navy-300">
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
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
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
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="https://api.example.com/endpoint"
        />
        <p className="text-xs text-navy-300 mt-0.5">
          {"{{degisken}}"} destekler
        </p>
      </FieldGroup>

      <FieldGroup label="Body Template">
        <textarea
          value={data.body_template ?? ''}
          onChange={(e) => onChange({ body_template: e.target.value })}
          rows={3}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-brand-500 resize-none font-mono"
          placeholder='{"key": "{{degisken}}"}'
        />
      </FieldGroup>

      <FieldGroup label="Response Degiskeni">
        <input
          type="text"
          value={data.response_variable ?? 'api_response'}
          onChange={(e) => onChange({ response_variable: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
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
          <span className="text-navy-300">100ms</span>
          <span className={timeoutColor}>{(timeoutMs / 1000).toFixed(1)}s</span>
          <span className="text-navy-300">30s</span>
        </div>
      </FieldGroup>
    </>
  );
}

function AiSentimentProps({
  data,
  onChange,
}: {
  data: AiSentimentData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const threshold = data.threshold ?? 0.5;

  return (
    <>
      <FieldGroup label={`Duygu Esigi (${(threshold * 100).toFixed(0)}%)`}>
        <input
          type="range"
          min={0}
          max={100}
          value={Math.round(threshold * 100)}
          onChange={(e) => onChange({ threshold: Number(e.target.value) / 100 })}
          className="w-full accent-purple-500"
        />
        <div className="flex justify-between text-xs text-navy-300 mt-0.5">
          <span>0%</span>
          <span>50%</span>
          <span>100%</span>
        </div>
      </FieldGroup>

      <p className="text-xs text-navy-300">
        Musteri mesaji Claude AI ile analiz edilir. Skor esik uzerindeyse <strong>POZITIF</strong>, altindaysa <strong>NEGATIF</strong> dalina yonlenir.
      </p>
    </>
  );
}

function WebhookTriggerProps({
  data,
  onChange,
}: {
  data: WebhookTriggerData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  return (
    <>
      <FieldGroup label="Secret Key (Opsiyonel)">
        <input
          type="text"
          value={data.secret_key ?? ''}
          onChange={(e) => onChange({ secret_key: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="HMAC-SHA256 dogrulama anahtari"
        />
      </FieldGroup>

      <FieldGroup label="Payload Degiskeni">
        <input
          type="text"
          value={data.payload_variable ?? 'webhook_payload'}
          onChange={(e) => onChange({ payload_variable: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="webhook_payload"
        />
      </FieldGroup>

      <p className="text-xs text-navy-300">
        Dis sistemlerden gelen HTTP POST istekleriyle tetiklenir. Payload belirtilen degiskene atanir.
      </p>
    </>
  );
}

function OutboundTriggerProps() {
  return (
    <p className="text-xs text-navy-300">
      Outbound kampanyasi bu flow'u tetiklediginde akis baslar. Kampanya bilgileri <code className="bg-navy-100 px-1 rounded">campaign_id</code> degiskenine atanir.
    </p>
  );
}

function ScheduleTriggerProps({
  data,
  onChange,
}: {
  data: ScheduleTriggerData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const timezones = [
    { value: 'Europe/Istanbul', label: 'Turkiye (UTC+3)' },
    { value: 'UTC', label: 'UTC' },
    { value: 'Europe/London', label: 'Londra (UTC+0/+1)' },
    { value: 'Europe/Berlin', label: 'Berlin (UTC+1/+2)' },
  ];

  return (
    <>
      <FieldGroup label="Cron Ifadesi">
        <input
          type="text"
          value={data.cron_expression ?? ''}
          onChange={(e) => onChange({ cron_expression: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="0 9 * * *"
        />
        <p className="text-xs text-navy-300 mt-1">
          Format: dakika saat gun ay haftanin_gunu
        </p>
      </FieldGroup>

      <FieldGroup label="Saat Dilimi">
        <select
          value={data.timezone ?? 'Europe/Istanbul'}
          onChange={(e) => onChange({ timezone: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
        >
          {timezones.map((tz) => (
            <option key={tz.value} value={tz.value}>{tz.label}</option>
          ))}
        </select>
      </FieldGroup>

      <p className="text-xs text-navy-300">
        Belirtilen cron zamanlamasina gore otomatik tetiklenir.
      </p>
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
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-xs text-navy-700 outline-none focus:border-brand-500 resize-none"
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

function CallFlowProps({
  data,
  onChange,
}: {
  data: ActionCallFlowData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const { session } = useAuth();
  const { flowId: flowIdParam } = useParams<{ flowId: string }>();
  const currentFlowId = flowIdParam ? parseInt(flowIdParam, 10) : undefined;
  const tenantId = session?.tenantId;

  const [flows, setFlows] = useState<FlowSummary[]>([]);
  const [loading, setLoading] = useState(false);

  const fetchFlows = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    try {
      const result = await api.listFlows(tenantId);
      // Exclude current flow to prevent self-call
      setFlows(result.filter((f) => f.flow_id !== currentFlowId));
    } catch (err: unknown) {
      console.warn('[CallFlowProps] Flow list fetch failed:', err instanceof Error ? err.message : err);
    } finally {
      setLoading(false);
    }
  }, [tenantId, currentFlowId]);

  useEffect(() => {
    fetchFlows();
  }, [fetchFlows]);

  // Parse input/output maps safely — preserves original on invalid JSON
  const parseMap = (json: string): Array<[string, string]> => {
    if (!json || json === '{}') return [];
    try {
      const obj = JSON.parse(json);
      return Object.entries(obj) as Array<[string, string]>;
    } catch (err) {
      console.warn('[CallFlowProps] Invalid variable map JSON:', json, err);
      return [];
    }
  };

  const serializeMap = (entries: Array<[string, string]>): string => {
    const obj: Record<string, string> = {};
    for (const [k, v] of entries) {
      const key = k.trim();
      if (key) obj[key] = v;
    }
    return JSON.stringify(obj);
  };

  const inputEntries = parseMap(data.input_map);
  const outputEntries = parseMap(data.output_map);

  const updateMapEntry = (
    mapType: 'input_map' | 'output_map',
    entries: Array<[string, string]>,
    idx: number,
    pos: 0 | 1,
    value: string,
  ) => {
    const newEntries = [...entries];
    const pair = [...newEntries[idx]] as [string, string];
    pair[pos] = value;
    newEntries[idx] = pair;
    onChange({ [mapType]: serializeMap(newEntries) });
  };

  const addMapEntry = (mapType: 'input_map' | 'output_map', entries: Array<[string, string]>) => {
    const newEntries = [...entries, ['', ''] as [string, string]];
    onChange({ [mapType]: serializeMap(newEntries) });
  };

  const removeMapEntry = (mapType: 'input_map' | 'output_map', entries: Array<[string, string]>, idx: number) => {
    onChange({ [mapType]: serializeMap(entries.filter((_, i) => i !== idx)) });
  };

  const selectedFlow = flows.find((f) => String(f.flow_id) === data.flow_id);

  return (
    <>
      <FieldGroup label="Hedef Flow">
        {loading ? (
          <p className="text-xs text-navy-300">Yukleniyor...</p>
        ) : (
          <select
            value={data.flow_id ?? ''}
            onChange={(e) => onChange({ flow_id: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          >
            <option value="">Flow seciniz...</option>
            {flows.map((f) => (
              <option key={f.flow_id} value={String(f.flow_id)}>
                {f.flow_name} {!f.is_active ? '(pasif)' : ''}
              </option>
            ))}
          </select>
        )}
        {selectedFlow && (
          <p className="text-[10px] text-navy-300 mt-0.5">
            {selectedFlow.node_count} node, {selectedFlow.is_active ? 'aktif' : 'pasif'}
          </p>
        )}
      </FieldGroup>

      <FieldGroup label={`Girdi Esleme (${inputEntries.length})`}>
        <div className="space-y-1.5">
          {inputEntries.map(([parentVar, childVar], idx) => (
            <div key={idx} className="flex items-center gap-1">
              <input
                type="text"
                value={parentVar}
                onChange={(e) => updateMapEntry('input_map', inputEntries, idx, 0, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-xs text-navy-700 outline-none focus:border-brand-500 font-mono"
                placeholder="parent_var"
              />
              <span className="text-navy-300 text-xs flex-shrink-0">&rarr;</span>
              <input
                type="text"
                value={childVar}
                onChange={(e) => updateMapEntry('input_map', inputEntries, idx, 1, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-xs text-navy-700 outline-none focus:border-brand-500 font-mono"
                placeholder="child_var"
              />
              <button
                onClick={() => removeMapEntry('input_map', inputEntries, idx)}
                className="p-0.5 text-navy-300 hover:text-red-500 transition-colors"
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
          onClick={() => addMapEntry('input_map', inputEntries)}
          className="mt-1.5 w-full px-2 py-1 rounded border border-dashed border-navy-200 text-xs text-navy-400 hover:border-red-400 hover:text-red-500 transition-colors"
        >
          + Girdi Ekle
        </button>
        <p className="text-[10px] text-navy-300 mt-0.5">
          Parent degisken &rarr; Alt flow degisken
        </p>
      </FieldGroup>

      <FieldGroup label={`Cikti Esleme (${outputEntries.length})`}>
        <div className="space-y-1.5">
          {outputEntries.map(([childVar, parentVar], idx) => (
            <div key={idx} className="flex items-center gap-1">
              <input
                type="text"
                value={childVar}
                onChange={(e) => updateMapEntry('output_map', outputEntries, idx, 0, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-xs text-navy-700 outline-none focus:border-brand-500 font-mono"
                placeholder="child_var"
              />
              <span className="text-navy-300 text-xs flex-shrink-0">&rarr;</span>
              <input
                type="text"
                value={parentVar}
                onChange={(e) => updateMapEntry('output_map', outputEntries, idx, 1, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-xs text-navy-700 outline-none focus:border-brand-500 font-mono"
                placeholder="parent_var"
              />
              <button
                onClick={() => removeMapEntry('output_map', outputEntries, idx)}
                className="p-0.5 text-navy-300 hover:text-red-500 transition-colors"
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
          onClick={() => addMapEntry('output_map', outputEntries)}
          className="mt-1.5 w-full px-2 py-1 rounded border border-dashed border-navy-200 text-xs text-navy-400 hover:border-red-400 hover:text-red-500 transition-colors"
        >
          + Cikti Ekle
        </button>
        <p className="text-[10px] text-navy-300 mt-0.5">
          Alt flow degisken &rarr; Parent degisken
        </p>
      </FieldGroup>

      <p className="text-xs text-navy-300">
        Alt flow tamamlandiginda <strong>completed</strong>, hata olursa <strong>error</strong> dalina yonlenir.
      </p>
    </>
  );
}
