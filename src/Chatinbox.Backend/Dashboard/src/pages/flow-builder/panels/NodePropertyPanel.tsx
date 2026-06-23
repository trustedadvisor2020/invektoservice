import { useState, useEffect, useCallback, useRef } from 'react';
import { useParams } from 'react-router-dom';
import { useFlowStore } from '../../../stores/flow-store';
import { useAuth } from '../../../hooks/useAuth';
import { getNodeTypeInfo, type FlowNodeType } from '../../../types/flow';
import { api, ApiClientError, type FbAvailableInstance, type FlowSummary, type CustomerFeatureGroup } from '../../../lib/api';
import { PlaceholderPicker } from '../../../components/PlaceholderPicker';
import {
  NODE_GUIDES,
  NODE_OUTPUT_VARS,
  SYSTEM_VARIABLES,
  describeCron,
} from '../../../lib/node-metadata';
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
  CustomerStatusChangedData,
  SetCustomerStatusData,
  ActionApiCallData,
  ActionDelayData,
  ActionHandoffData,
  ActionAssignGroupData,
  ActionCallFlowData,
  UtilitySetVariableData,
  UtilityNoteData,
  ActionEcommerceData,
  EcommerceOperation,
} from '../../../types/flow';

export function NodePropertyPanel() {
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const nodes = useFlowStore((s) => s.nodes);
  const updateNodeData = useFlowStore((s) => s.updateNodeData);
  const deleteNode = useFlowStore((s) => s.deleteNode);

  const selectedNode = selectedNodeId
    ? nodes.find((n) => n.id === selectedNodeId)
    : null;

  const isOpen = !!selectedNode;

  const nodeType = selectedNode ? (selectedNode.type as FlowNodeType) : null;
  const info = nodeType ? getNodeTypeInfo(nodeType) : null;

  const update = (data: Record<string, unknown>) => {
    if (selectedNode) updateNodeData(selectedNode.id, data);
  };

  return (
    <div
      className="bg-white border-l border-navy-100 flex-shrink-0 overflow-hidden transition-[width] duration-300 ease-in-out"
      style={{ width: isOpen ? 360 : 0 }}
    >
      {!selectedNode ? null : (
      <div className="w-[360px] overflow-y-auto h-full">
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
        <p className="text-sm text-navy-300 mt-0.5">{info?.description}</p>
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
        {nodeType === 'logic_working_hours' && (
          <LogicWorkingHoursProps />
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
        {nodeType === 'action_assign_group' && (
          <ActionAssignGroupProps data={selectedNode.data as ActionAssignGroupData} onChange={update} />
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
        {nodeType === 'customer_status_changed' && (
          <CustomerStatusChangedProps data={selectedNode.data as CustomerStatusChangedData} onChange={update} />
        )}
        {nodeType === 'action_set_customer_status' && (
          <SetCustomerStatusActionProps data={selectedNode.data as SetCustomerStatusData} onChange={update} />
        )}
        {nodeType === 'action_call_flow' && (
          <CallFlowProps data={selectedNode.data as ActionCallFlowData} onChange={update} />
        )}
        {nodeType === 'action_ecommerce' && (
          <ActionEcommerceProps data={selectedNode.data as ActionEcommerceData} onChange={update} />
        )}

        {/* Output variables (GR-6) */}
        {nodeType && <NodeOutputVarsSection nodeType={nodeType} />}

        {/* Variable explorer (GR-1) */}
        <VariableExplorerSection />

        {/* Delete button (not for trigger types) */}
        {nodeType !== 'trigger_start' && nodeType !== 'webhook_trigger' && nodeType !== 'outbound_trigger' && nodeType !== 'schedule_trigger' && nodeType !== 'customer_status_changed' && (
          <div className="pt-3 border-t border-navy-100">
            <button
              onClick={() => deleteNode(selectedNode.id)}
              className="w-full px-3 py-1.5 rounded-md text-sm font-medium bg-red-50 text-red-600 hover:bg-red-100 transition-colors"
            >
              Node'u Sil
            </button>
          </div>
        )}

        {/* Help guide (GR-10) */}
        {nodeType && <NodeHelpSection nodeType={nodeType} />}
      </div>
      </div>
      )}
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

// -- Shared Enhancement Components --

const WA_MAX_LENGTH = 4096;

function MessageLengthCounter({ text }: { text: string }) {
  const len = (text ?? '').length;
  const pct = Math.min((len / WA_MAX_LENGTH) * 100, 100);
  const color = len > WA_MAX_LENGTH ? 'text-red-600' : len > WA_MAX_LENGTH * 0.85 ? 'text-amber-600' : 'text-navy-400';
  const barColor = len > WA_MAX_LENGTH ? 'bg-red-500' : len > WA_MAX_LENGTH * 0.85 ? 'bg-amber-400' : 'bg-emerald-400';

  return (
    <div className="mt-1">
      <div className="flex justify-between items-center mb-0.5">
        <span className={`text-[10px] ${color}`}>{len} / {WA_MAX_LENGTH}</span>
        {len > WA_MAX_LENGTH && <span className="text-[10px] text-red-600 font-medium">Limit aşıldı!</span>}
      </div>
      <div className="w-full h-1 bg-navy-100 rounded-full overflow-hidden">
        <div className={`h-full ${barColor} transition-all duration-200 rounded-full`} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

function NodeOutputVarsSection({ nodeType }: { nodeType: FlowNodeType }) {
  const vars = NODE_OUTPUT_VARS[nodeType];
  if (!vars || vars.length === 0) return null;

  return (
    <div className="pt-2 border-t border-navy-100">
      <p className="text-[10px] font-medium text-navy-400 uppercase tracking-wider mb-1.5">Çıktı Değişkenleri</p>
      <div className="space-y-1">
        {vars.map((v) => (
          <div key={v.name} className="flex items-start gap-1.5 group">
            <code className="text-[10px] bg-navy-50 text-purple-600 px-1 py-0.5 rounded font-mono flex-shrink-0 select-all">{`{{${v.name}}}`}</code>
            <span className="text-[10px] text-navy-400 leading-tight">{v.description}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function VariableExplorerSection() {
  const [open, setOpen] = useState(false);

  return (
    <div className="pt-2 border-t border-navy-100">
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-1 text-[10px] font-medium text-navy-400 uppercase tracking-wider hover:text-navy-600 transition-colors w-full"
      >
        <svg className={`w-3 h-3 transition-transform ${open ? 'rotate-90' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
        </svg>
        Kullanılabilir Değişkenler
      </button>
      {open && (
        <div className="mt-1.5 space-y-1">
          {SYSTEM_VARIABLES.map((v) => (
            <div key={v.name} className="flex items-start gap-1.5">
              <code className="text-[10px] bg-navy-50 text-blue-600 px-1 py-0.5 rounded font-mono flex-shrink-0 select-all">{`{{${v.name}}}`}</code>
              <span className="text-[10px] text-navy-400 leading-tight">{v.description}</span>
            </div>
          ))}
          <p className="text-[10px] text-navy-300 mt-1">Değişken Ata ve API Çağrısı node\'ları ek değişkenler oluşturur.</p>
        </div>
      )}
    </div>
  );
}

function NodeHelpSection({ nodeType }: { nodeType: FlowNodeType }) {
  const [open, setOpen] = useState(false);
  const guide = NODE_GUIDES[nodeType];
  if (!guide) return null;

  return (
    <div className="pt-2 border-t border-navy-100">
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center gap-1.5 w-full text-left group"
      >
        <svg className={`w-3.5 h-3.5 text-navy-300 transition-transform ${open ? 'rotate-90' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
        </svg>
        <svg className="w-3.5 h-3.5 text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9.879 7.519c1.171-1.025 3.071-1.025 4.242 0 1.172 1.025 1.172 2.687 0 3.712-.203.179-.43.326-.67.442-.745.361-1.45.999-1.45 1.827v.75M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9 5.25h.008v.008H12v-.008z" />
        </svg>
        <span className="text-sm text-navy-500 group-hover:text-navy-700 transition-colors">Kullanım Kılavuzu</span>
      </button>
      {open && (
        <div className="mt-2 space-y-3 text-sm text-navy-600 bg-navy-25 rounded-lg p-3">
          {/* Summary */}
          <p className="font-medium text-navy-700">{guide.summary}</p>

          {/* Detail */}
          <div>
            <p className="text-[10px] font-medium text-navy-400 uppercase tracking-wider mb-1">Detaylı Açıklama</p>
            <p className="whitespace-pre-line leading-relaxed">{guide.detail}</p>
          </div>

          {/* Scenarios */}
          <div>
            <p className="text-[10px] font-medium text-navy-400 uppercase tracking-wider mb-1">Kullanım Senaryoları</p>
            <p className="whitespace-pre-line leading-relaxed">{guide.scenarios}</p>
          </div>

          {/* Anti-patterns */}
          <div>
            <p className="text-[10px] font-medium text-red-400 uppercase tracking-wider mb-1">Dikkat Edilmesi Gerekenler</p>
            <p className="whitespace-pre-line leading-relaxed text-navy-500">{guide.antiPatterns}</p>
          </div>
        </div>
      )}
    </div>
  );
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <button
      onClick={handleCopy}
      className="p-1 text-navy-300 hover:text-navy-600 transition-colors flex-shrink-0"
      title="Kopyala"
    >
      {copied ? (
        <svg className="w-3.5 h-3.5 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
        </svg>
      ) : (
        <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M15.666 3.888A2.25 2.25 0 0013.5 2.25h-3c-1.03 0-1.9.693-2.166 1.638m7.332 0c.055.194.084.4.084.612v0a.75.75 0 01-.75.75H9.75a.75.75 0 01-.75-.75v0c0-.212.03-.418.084-.612m7.332 0c.646.049 1.288.11 1.927.184 1.1.128 1.907 1.077 1.907 2.185V19.5a2.25 2.25 0 01-2.25 2.25H6.75A2.25 2.25 0 014.5 19.5V6.257c0-1.108.806-2.057 1.907-2.185a48.208 48.208 0 011.927-.184" />
        </svg>
      )}
    </button>
  );
}

function HeadersEditor({
  headers,
  onChange,
}: {
  headers: Record<string, string>;
  onChange: (headers: Record<string, string>) => void;
}) {
  const entries = Object.entries(headers ?? {});

  const updateEntry = (idx: number, key: string, value: string) => {
    const newEntries = [...entries];
    newEntries[idx] = [key, value];
    onChange(Object.fromEntries(newEntries.filter(([k]) => k.trim())));
  };

  const addEntry = () => {
    onChange({ ...headers, '': '' });
  };

  const removeEntry = (idx: number) => {
    const newEntries = entries.filter((_, i) => i !== idx);
    onChange(Object.fromEntries(newEntries));
  };

  return (
    <div className="space-y-1.5">
      {entries.map(([key, value], idx) => (
        <div key={idx} className="flex items-center gap-1">
          <input
            type="text"
            value={key}
            onChange={(e) => updateEntry(idx, e.target.value, value)}
            className="w-[40%] bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-[10px] text-navy-700 outline-none focus:border-brand-500 font-mono"
            placeholder="Header"
          />
          <input
            type="text"
            value={value}
            onChange={(e) => updateEntry(idx, key, e.target.value)}
            className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-[10px] text-navy-700 outline-none focus:border-brand-500 font-mono"
            placeholder="Değer"
          />
          <button
            onClick={() => removeEntry(idx)}
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
      <button
        onClick={addEntry}
        className="w-full px-2 py-1 rounded border border-dashed border-navy-200 text-xs text-navy-400 hover:border-red-400 hover:text-red-500 transition-colors"
      >
        + Header Ekle
      </button>
    </div>
  );
}

const INSTANCE_TYPE_LABELS: Record<number, string> = { 1: 'WhatsApp', 2: 'Web', 5: 'Kanal', 6: 'SMS' };

// FEAT-INMA-PIPELINE-V2 C3b: customer_status_changed trigger config — a feature-group picker fed
// by the cached cxapi catalog proxy. Catch-all = '' (backend-sanctioned); a specific group = the
// numeric id as a string. Text-mode (selectionMode===3) groups are shown disabled AND blocked in
// onChange (they arrive as featureGroupId=null so a specific match never fires). Tenants without a
// WapCRM secret get an info note + the node works as catch-all.
function CustomerStatusChangedProps({ data, onChange }: { data: Record<string, unknown>; onChange: (d: Record<string, unknown>) => void }) {
  const { session } = useAuth();
  const tenantId = session?.tenantId;

  const [groups, setGroups] = useState<CustomerFeatureGroup[]>([]);
  const [loading, setLoading] = useState(false);
  const [notConfigured, setNotConfigured] = useState(false);
  const [fetchError, setFetchError] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  // Current selection. '' = catch-all. Defensive read-normalize (a stored number -> string);
  // the picker only ever WRITES '' or a numeric string, so this is just belt-and-suspenders.
  const rawFg = data.feature_group_id;
  const currentFg = typeof rawFg === 'number'
    ? String(rawFg)
    : (typeof rawFg === 'string' ? rawFg.trim() : '');

  const loadGroups = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setNotConfigured(false);
    setFetchError(false);
    try {
      const result = await api.getCustomerFeatureGroups();
      setGroups(result.data ?? []);
    } catch (err: unknown) {
      // 422 INV-BE-132 = tenant has no WapCRM secret (distinct from a transient fetch failure).
      if (err instanceof ApiClientError && err.status === 422) {
        setNotConfigured(true);
      } else {
        setFetchError(true);
        console.warn('[CustomerStatusChangedProps] catalog fetch failed:', err instanceof Error ? err.message : err);
      }
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => { loadGroups(); }, [loadGroups]);

  const refresh = async () => {
    setRefreshing(true);
    try {
      await api.invalidateCustomerFeatureGroupsCache();
    } catch (err: unknown) {
      console.warn('[CustomerStatusChangedProps] cache invalidate failed:', err instanceof Error ? err.message : err);
    } finally {
      setRefreshing(false);
    }
    await loadGroups();
  };

  const handleSelect = (value: string) => {
    if (value === '') { onChange({ feature_group_id: '' }); return; }
    // Hard-guard: ignore unknown / text-mode (selectionMode===3) selections so a never-fires group
    // can never be committed even if the disabled <option> is bypassed.
    const grp = groups.find((g) => String(g.id) === value);
    if (!grp || grp.selectionMode === 3) return;
    onChange({ feature_group_id: value });
  };

  const selectedGroup = currentFg !== '' ? groups.find((g) => String(g.id) === currentFg) : undefined;

  return (
    <div className="space-y-3">
      {notConfigured ? (
        <p className="text-sm text-navy-300">
          WapCRM bağlantısı yok. Akış <strong>her durum değişikliğinde</strong> tetiklenir.
          <span className="block mt-1 text-navy-200">
            Belirli bir gruba bağlamak için Ayarlar &gt; Entegrasyon bölümünden WapCRM bağlantısını tamamlayın.
          </span>
        </p>
      ) : (
        <FieldGroup label="Durum Grubu">
          {loading ? (
            <p className="text-sm text-navy-300">Yükleniyor...</p>
          ) : (
            <>
              <select
                value={currentFg}
                onChange={(e) => handleSelect(e.target.value)}
                className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
              >
                <option value="">Tüm durum değişiklikleri (her grup)</option>
                {groups.map((g) => (
                  <option key={g.id} value={String(g.id)} disabled={g.selectionMode === 3}>
                    {g.name}{g.selectionMode === 3 ? ' (metin — tetiklenemez)' : ''}
                  </option>
                ))}
              </select>
              {fetchError && (
                <p className="text-sm text-amber-600 mt-1">Gruplar yüklenemedi — Yenile ile tekrar deneyin.</p>
              )}
              <button
                type="button"
                onClick={refresh}
                disabled={refreshing}
                className="text-xs text-brand-600 hover:text-brand-700 mt-1.5 disabled:opacity-50"
              >
                {refreshing ? 'Yenileniyor...' : 'Kataloğu yenile'}
              </button>
            </>
          )}
        </FieldGroup>
      )}

      {selectedGroup ? (
        <div className="text-xs text-navy-400 bg-navy-50 rounded p-2 space-y-1">
          <p>
            Bu tetikleyici <strong>{selectedGroup.name}</strong> grubundaki <strong>herhangi bir</strong> durum
            değişikliğinde çalışır. Tek bir durum seçilemez.
          </p>
          {selectedGroup.features.length > 0 && (
            <p className="text-navy-300">
              Gruptaki durumlar: {selectedGroup.features.map((f) => f.name).join(', ')}
            </p>
          )}
        </div>
      ) : (!notConfigured && !loading && (
        <p className="text-xs text-navy-300">
          Grup seçilmezse akış her durum değişikliğinde tetiklenir. Sonraki adımda
          {' '}<code className="text-navy-400">{'{{new_customer_status}}'}</code> ile dallanabilirsiniz.
        </p>
      ))}
    </div>
  );
}

// FEAT-INMA-PIPELINE-V2 C4: 'Set Customer Status' action config — a group picker (cached cxapi catalog) +
// a feature picker (single=radio, multi=checkbox). FULL-LIST replace semantics: the picked set is the
// group's COMPLETE new selection — for multi-select groups, unpicked features are REMOVED (an explicit
// warning is shown). Empty selection clears the group. Text-mode (selectionMode===3) groups are disabled
// (the vendor /update has no text-write payload). No WapCRM secret => info note (the node cannot be configured).
function SetCustomerStatusActionProps({ data, onChange }: { data: Record<string, unknown>; onChange: (d: Record<string, unknown>) => void }) {
  const [groups, setGroups] = useState<CustomerFeatureGroup[]>([]);
  const [loading, setLoading] = useState(false);
  const [notConfigured, setNotConfigured] = useState(false);
  const [fetchError, setFetchError] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  const rawFg = data.feature_group_id;
  const currentFg = typeof rawFg === 'number'
    ? String(rawFg)
    : (typeof rawFg === 'string' ? rawFg.trim() : '');

  const rawIds = data.feature_ids;
  const currentIds = (typeof rawIds === 'string' ? rawIds : '')
    .split(',')
    .map((s) => parseInt(s.trim(), 10))
    .filter((n) => Number.isInteger(n) && n > 0);

  const loadGroups = useCallback(async () => {
    setLoading(true);
    setNotConfigured(false);
    setFetchError(false);
    try {
      const result = await api.getCustomerFeatureGroups();
      setGroups(result.data ?? []);
    } catch (err: unknown) {
      // 422 INV-BE-132 = tenant has no WapCRM secret (distinct from a transient fetch failure).
      if (err instanceof ApiClientError && err.status === 422) {
        setNotConfigured(true);
      } else {
        setFetchError(true);
        console.warn('[SetCustomerStatusActionProps] catalog fetch failed:', err instanceof Error ? err.message : err);
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadGroups(); }, [loadGroups]);

  const refresh = async () => {
    setRefreshing(true);
    try {
      await api.invalidateCustomerFeatureGroupsCache();
    } catch (err: unknown) {
      console.warn('[SetCustomerStatusActionProps] cache invalidate failed:', err instanceof Error ? err.message : err);
    } finally {
      setRefreshing(false);
    }
    await loadGroups();
  };

  const selectedGroup = currentFg !== '' ? groups.find((g) => String(g.id) === currentFg) : undefined;

  const handleSelectGroup = (value: string) => {
    if (value === '') { onChange({ feature_group_id: '', feature_ids: '' }); return; }
    const grp = groups.find((g) => String(g.id) === value);
    // Hard-guard: text-mode (selectionMode===3) groups cannot be written by the vendor /update API.
    if (!grp || grp.selectionMode === 3) return;
    // Changing the group invalidates any previously-picked features.
    onChange({ feature_group_id: value, feature_ids: '' });
  };

  const writeIds = (ids: number[]) => onChange({ feature_ids: ids.join(',') });
  const selectSingle = (id: number) => writeIds([id]);
  const toggleMulti = (id: number) =>
    writeIds(currentIds.includes(id) ? currentIds.filter((x) => x !== id) : [...currentIds, id]);

  return (
    <div className="space-y-3">
      {notConfigured ? (
        <p className="text-sm text-navy-300">
          WapCRM bağlantısı yok — bu adım <strong>çalışamaz</strong>.
          <span className="block mt-1 text-navy-200">
            Müşteri durumu yazabilmek için Ayarlar &gt; Entegrasyon bölümünden WapCRM bağlantısını tamamlayın.
          </span>
        </p>
      ) : (
        <>
          <FieldGroup label="Durum Grubu">
            {loading ? (
              <p className="text-sm text-navy-300">Yükleniyor...</p>
            ) : (
              <>
                <select
                  value={currentFg}
                  onChange={(e) => handleSelectGroup(e.target.value)}
                  className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
                >
                  <option value="">Grup seçin...</option>
                  {groups.map((g) => (
                    <option key={g.id} value={String(g.id)} disabled={g.selectionMode === 3}>
                      {g.name}{g.selectionMode === 3 ? ' (metin — yazılamaz)' : ''}
                    </option>
                  ))}
                </select>
                {fetchError && (
                  <p className="text-sm text-amber-600 mt-1">Gruplar yüklenemedi — Yenile ile tekrar deneyin.</p>
                )}
                <button
                  type="button"
                  onClick={refresh}
                  disabled={refreshing}
                  className="text-xs text-brand-600 hover:text-brand-700 mt-1.5 disabled:opacity-50"
                >
                  {refreshing ? 'Yenileniyor...' : 'Kataloğu yenile'}
                </button>
              </>
            )}
          </FieldGroup>

          {selectedGroup && (
            <FieldGroup label={selectedGroup.selectionMode === 1 ? 'Durumlar (çoklu)' : 'Durum (tek)'}>
              {selectedGroup.selectionMode === 1 && (
                <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded p-2 mb-2">
                  ⚠️ Bu çoklu-seçim grubunun <strong>TÜM seçimini</strong> aşağıdakiyle değiştirir —
                  işaretlemedikleriniz müşteriden <strong>kaldırılır</strong>.
                </p>
              )}
              {selectedGroup.features.length === 0 ? (
                <p className="text-sm text-navy-300">Bu grupta seçilebilir durum yok.</p>
              ) : (
                <div className="space-y-1">
                  {selectedGroup.features.map((f) => (
                    <label key={f.id} className="flex items-center gap-2 text-sm text-navy-700 cursor-pointer">
                      <input
                        type={selectedGroup.selectionMode === 1 ? 'checkbox' : 'radio'}
                        name={`scs_${selectedGroup.id}`}
                        checked={currentIds.includes(f.id)}
                        onChange={() => (selectedGroup.selectionMode === 1 ? toggleMulti(f.id) : selectSingle(f.id))}
                      />
                      {f.name}
                    </label>
                  ))}
                </div>
              )}
              <button
                type="button"
                onClick={() => writeIds([])}
                className="text-xs text-navy-400 hover:text-navy-600 mt-2"
              >
                Seçimi temizle (grubu boşalt)
              </button>
            </FieldGroup>
          )}

          {!selectedGroup && !loading && (
            <p className="text-xs text-navy-300">
              Bir grup seçin, ardından atanacak durum(lar)ı işaretleyin. Sonuç{' '}
              <code className="text-navy-400">success</code> / <code className="text-navy-400">error</code> dallarına göre devam eder.
            </p>
          )}
        </>
      )}
    </div>
  );
}

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
      <p className="text-sm text-navy-300">
        Müşteri mesaj gönderdiğinde bu node'dan akış başlar.
        {tenantId && <span className="block mt-1 text-navy-200">Hat seçimi için Ayarlar sayfasından WapCRM hatlarını yükleyin.</span>}
      </p>
    );
  }

  return (
    <FieldGroup label={`Hatlar (${selectedIds.length}/${instances.length})`}>
      {loading ? (
        <p className="text-sm text-navy-300">Yükleniyor...</p>
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
                      {INSTANCE_TYPE_LABELS[inst.instanceType] || 'Diğer'}
                    </span>
                  </div>
                  {isOtherFlow && (
                    <span className="text-[10px] text-amber-600 leading-tight block">
                      {inst.assignedFlowName ?? 'Başka akış'} akışında
                    </span>
                  )}
                </div>
              </label>
            );
          })}
        </div>
      )}
      <p className="text-sm text-navy-300 mt-2">
        Seçili hatlardan gelen mesajlar bu akışa yönlendirilir.
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
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);

  // FEAT-DMP: insert INMA placeholder '{{cf1}}' at the cursor caret. Falls back to
  // append-to-end when the textarea hasn't been focused yet.
  const insertPlaceholder = (token: string) => {
    const el = textareaRef.current;
    const current = data.text ?? '';
    if (!el) {
      onChange({ text: current + token });
      return;
    }
    const start = el.selectionStart ?? current.length;
    const end = el.selectionEnd ?? current.length;
    const next = current.slice(0, start) + token + current.slice(end);
    onChange({ text: next });
    // Restore caret after React reconciliation.
    requestAnimationFrame(() => {
      if (textareaRef.current) {
        const caret = start + token.length;
        textareaRef.current.focus();
        textareaRef.current.setSelectionRange(caret, caret);
      }
    });
  };

  return (
    <>
      <FieldGroup label="Mesaj Metni">
        <textarea
          ref={textareaRef}
          value={data.text ?? ''}
          onChange={(e) => onChange({ text: e.target.value })}
          rows={4}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 resize-none"
          placeholder="Gönderilecek mesaj..."
        />
        <div className="flex items-center justify-between mt-1">
          <MessageLengthCounter text={data.text ?? ''} />
          <PlaceholderPicker onInsert={insertPlaceholder} position="above" tfmAware />
        </div>
      </FieldGroup>
      <FieldGroup label="Davranış">
        <label className="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            checked={waitForInput}
            onChange={(e) => onChange({ wait_for_input: e.target.checked })}
            className="w-4 h-4 rounded border-navy-200 text-blue-600 focus:ring-blue-500 accent-blue-500"
          />
          <span className="text-sm text-navy-500">Kullanıcı yanıtını bekle</span>
        </label>
        {waitForInput && (
          <p className="text-[10px] text-navy-400 mt-1">
            Mesaj gönderildikten sonra akış durur ve kullanıcının yanıtını bekler.
            Yanıt, sonraki node'larda {'{{user_input}}'} olarak kullanılabilir.
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
      label: `Seçenek ${nextKey}`,
      handle_id: `opt_${nextKey}`,
    };
    onChange({ options: [...options, newOpt] });
  };

  const removeOption = (idx: number) => {
    onChange({ options: options.filter((_, i) => i !== idx) });
  };

  return (
    <>
      <FieldGroup label="Menü Metni">
        <input
          type="text"
          value={data.text ?? ''}
          onChange={(e) => onChange({ text: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          placeholder="Seçim yapın:"
        />
        <MessageLengthCounter text={data.text ?? ''} />
      </FieldGroup>

      <FieldGroup label="Seçenekler">
        <div className="space-y-2">
          {options.map((opt, idx) => (
            <div key={opt.handle_id} className="flex items-center gap-1">
              <input
                type="text"
                value={opt.key}
                onChange={(e) => updateOption(idx, 'key', e.target.value)}
                className="w-8 bg-navy-50 border border-navy-200 rounded px-1 py-1 text-sm text-navy-700 outline-none focus:border-brand-500 text-center"
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
          + Seçenek Ekle
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
    <FieldGroup label="Özet Şablonu">
      <textarea
        value={data.summary_template ?? ''}
        onChange={(e) => onChange({ summary_template: e.target.value })}
        rows={3}
        className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 resize-none"
        placeholder="Temsilciye aktarılacak özet..."
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
    { value: 'equals', label: 'Eşittir (=)' },
    { value: 'contains', label: 'İçerir' },
    { value: 'starts_with', label: 'Başlar' },
    { value: 'greater_than', label: 'Büyüktür (>)' },
    { value: 'less_than', label: 'Küçüktür (<)' },
    { value: 'is_empty', label: 'Boş mu' },
    { value: 'regex', label: 'Regex' },
  ];

  return (
    <>
      <FieldGroup label="Değişken">
        <input
          type="text"
          value={data.variable ?? ''}
          onChange={(e) => onChange({ variable: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          placeholder="örnek: __last_input"
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
        <FieldGroup label="Değer">
          <input
            type="text"
            value={data.value ?? ''}
            onChange={(e) => onChange({ value: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
            placeholder="Karşılaştırılacak değer"
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
      <FieldGroup label="Değişken">
        <input
          type="text"
          value={data.variable ?? ''}
          onChange={(e) => onChange({ variable: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          placeholder="örnek: musteri_tipi"
        />
      </FieldGroup>

      <FieldGroup label={`Durumlar (${cases.length}/${MAX_CASES})`}>
        <div className="space-y-2">
          {cases.map((c, idx) => (
            <div key={c.handle_id} className="flex items-center gap-1">
              <span className="text-sm text-navy-300 w-4 flex-shrink-0">{idx + 1}</span>
              <input
                type="text"
                value={c.value}
                onChange={(e) => updateCase(idx, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-2 py-1 text-sm text-navy-700 outline-none focus:border-brand-500"
                placeholder="Değer..."
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

      <p className="text-sm text-navy-300">
        Hiçbir durum eşleşmediyse <strong>VARSAYILAN</strong> dala gider.
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
    <FieldGroup label="Bekleme Süresi (saniye)">
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
      <p className="text-sm text-navy-300 mt-1">Min: 1sn, Maks: 300sn (5dk)</p>
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
      <FieldGroup label="Değişken Adı">
        <input
          type="text"
          value={data.variable_name ?? ''}
          onChange={(e) => onChange({ variable_name: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          placeholder="örnek: musteri_tipi"
        />
      </FieldGroup>
      <FieldGroup label="Değer İfadesi">
        <textarea
          value={data.value_expression ?? ''}
          onChange={(e) => onChange({ value_expression: e.target.value })}
          rows={3}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 resize-none font-mono"
          placeholder="örnek: {{__last_input}}"
        />
        <p className="text-sm text-navy-300 mt-1">
          {"{{değişken}}"} ile mevcut değişkenlere referans verebilirsiniz.
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
      <FieldGroup label="Konuşma Ayarları">
        <label className="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            checked={askName}
            onChange={(e) => onChange({ ask_name: e.target.checked })}
            className="w-4 h-4 rounded border-navy-200 text-purple-600 focus:ring-purple-500 accent-purple-500"
          />
          <span className="text-sm text-navy-500">Müşteri ismini sor</span>
        </label>
        <p className="text-[10px] text-navy-300 mt-1">
          Açıkken müşteri ismini sorar, doğrular ve isimle hitap eder. Anlayamadığı mesajlarda sohbete devam eder.
        </p>
        {askName && (
          <div className="mt-2">
            <input
              type="text"
              value={data.greeting_message ?? ''}
              onChange={(e) => onChange({ greeting_message: e.target.value || undefined })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-purple-500"
              placeholder="Merhaba! İsminizi öğrenebilir miyim?"
            />
            <p className="text-[10px] text-navy-300 mt-0.5">Karşılama mesajı (boş = varsayılan)</p>
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
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-2 py-1 text-sm text-navy-700 outline-none focus:border-purple-500 font-mono"
                placeholder="örnek: satin_alma"
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

      <FieldGroup label={`Güven Eşiği (${(threshold * 100).toFixed(0)}%)`}>
        <input
          type="range"
          min={0}
          max={100}
          value={Math.round(threshold * 100)}
          onChange={(e) => onChange({ confidence_threshold: Number(e.target.value) / 100 })}
          className="w-full accent-purple-500"
        />
        <div className="flex justify-between text-sm text-navy-300 mt-0.5">
          <span>0%</span>
          <span>50%</span>
          <span>100%</span>
        </div>
      </FieldGroup>

      <p className="text-sm text-navy-300">
        Müşteri mesajı Claude AI ile analiz edilir. Orta güvendeki intent'ler için &quot;bunu mu demek istiyorsunuz?&quot; onay sorusu sorulur. Düşük güvende sohbet devam eder.
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
  const minConf = data.min_confidence ?? 0.65;
  const searchSource = data.search_source ?? 'all';

  return (
    <>
      <FieldGroup label={`Min Güven (${(minConf * 100).toFixed(0)}%)`}>
        <input
          type="range"
          min={0}
          max={100}
          value={Math.round(minConf * 100)}
          onChange={(e) => onChange({ min_confidence: Number(e.target.value) / 100 })}
          className="w-full accent-purple-500"
        />
        <div className="flex justify-between text-sm text-navy-300 mt-0.5">
          <span>0%</span>
          <span>50%</span>
          <span>100%</span>
        </div>
      </FieldGroup>

      <FieldGroup label="Arama Kaynağı">
        <select
          value={searchSource}
          onChange={(e) => onChange({ search_source: e.target.value })}
          className="w-full rounded-md border border-navy-600 bg-navy-700 text-navy-100 px-3 py-1.5 text-sm focus:ring-1 focus:ring-purple-500"
        >
          <option value="all">FAQ + Dökümanlar</option>
          <option value="faq_only">Sadece FAQ</option>
        </select>
      </FieldGroup>

      <p className="text-sm text-navy-300">
        Bilgi bankasında semantik arama yapılır. FAQ eşleşirse cevap otomatik gönderilir. Döküman eşleşirse AI ile özetlenip gönderilir. Eşik altındaki sonuçlar <strong>EŞLEŞMEDİ</strong> dalına yönlenir.
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
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="https://api.example.com/endpoint"
        />
        <p className="text-sm text-navy-300 mt-0.5">
          {"{{değişken}}"} destekler
        </p>
      </FieldGroup>

      <FieldGroup label="Headers">
        <HeadersEditor
          headers={(data.headers as Record<string, string>) ?? {}}
          onChange={(h) => onChange({ headers: Object.keys(h).length > 0 ? h : undefined })}
        />
      </FieldGroup>

      <FieldGroup label="Body Template">
        <textarea
          value={data.body_template ?? ''}
          onChange={(e) => onChange({ body_template: e.target.value })}
          rows={3}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 resize-none font-mono"
          placeholder='{"key": "{{degisken}}"}'
        />
      </FieldGroup>

      <FieldGroup label="Response Değişkeni">
        <input
          type="text"
          value={data.response_variable ?? 'api_response'}
          onChange={(e) => onChange({ response_variable: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="api_response"
        />
      </FieldGroup>

      <FieldGroup label={`Zaman Aşımı (${timeoutMs}ms)`}>
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
      <FieldGroup label={`Duygu Eşiği (${(threshold * 100).toFixed(0)}%)`}>
        <input
          type="range"
          min={0}
          max={100}
          value={Math.round(threshold * 100)}
          onChange={(e) => onChange({ threshold: Number(e.target.value) / 100 })}
          className="w-full accent-purple-500"
        />
        <div className="flex justify-between text-sm text-navy-300 mt-0.5">
          <span>0%</span>
          <span>50%</span>
          <span>100%</span>
        </div>
      </FieldGroup>

      <p className="text-sm text-navy-300">
        Müşteri mesajı Claude AI ile analiz edilir. Skor eşik üzerindeyse <strong>POZİTİF</strong>, altındaysa <strong>NEGATİF</strong> dalına yönlenir.
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
  const { flowId } = useParams<{ flowId: string }>();
  const webhookUrl = flowId ? `${window.location.origin}/api/flow-builder/webhook/${flowId}` : '';

  return (
    <>
      {webhookUrl && (
        <FieldGroup label="Webhook URL">
          <div className="flex items-center gap-1 bg-navy-50 border border-navy-200 rounded px-2 py-1.5">
            <code className="text-[10px] text-navy-600 font-mono truncate flex-1 select-all">{webhookUrl}</code>
            <CopyButton text={webhookUrl} />
          </div>
          <p className="text-[10px] text-navy-300 mt-0.5">Bu URL'ye POST isteği göndererek akışı tetikleyebilirsiniz.</p>
        </FieldGroup>
      )}

      <FieldGroup label="Secret Key (Opsiyonel)">
        <input
          type="text"
          value={data.secret_key ?? ''}
          onChange={(e) => onChange({ secret_key: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="HMAC-SHA256 doğrulama anahtarı"
        />
      </FieldGroup>

      <FieldGroup label="Payload Değişkeni">
        <input
          type="text"
          value={data.payload_variable ?? 'webhook_payload'}
          onChange={(e) => onChange({ payload_variable: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="webhook_payload"
        />
      </FieldGroup>

      <p className="text-sm text-navy-300">
        Dış sistemlerden gelen HTTP POST istekleriyle tetiklenir. Payload belirtilen değişkene atanır.
      </p>
    </>
  );
}

function OutboundTriggerProps() {
  return (
    <p className="text-sm text-navy-300">
      Outbound kampanyası bu flow'u tetiklediğinde akış başlar. Kampanya bilgileri <code className="bg-navy-100 px-1 rounded">campaign_id</code> değişkenine atanır.
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
    { value: 'Europe/Istanbul', label: 'Türkiye (UTC+3)' },
    { value: 'UTC', label: 'UTC' },
    { value: 'Europe/London', label: 'Londra (UTC+0/+1)' },
    { value: 'Europe/Berlin', label: 'Berlin (UTC+1/+2)' },
  ];

  return (
    <>
      <FieldGroup label="Cron İfadesi">
        <input
          type="text"
          value={data.cron_expression ?? ''}
          onChange={(e) => onChange({ cron_expression: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="0 9 * * *"
        />
        {data.cron_expression && (() => {
          const desc = describeCron(data.cron_expression);
          const isError = desc?.startsWith('Gecersiz');
          return desc ? (
            <div className={`mt-1 px-2 py-1 rounded text-[10px] ${isError ? 'bg-red-50 text-red-600' : 'bg-emerald-50 text-emerald-700'}`}>
              {desc}
            </div>
          ) : null;
        })()}
        <p className="text-[10px] text-navy-300 mt-1">
          Format: dakika saat gün ay haftanın_günü
        </p>
        <div className="text-[10px] text-navy-300 mt-0.5 space-y-0.5">
          <p><code className="bg-navy-100 px-0.5 rounded">0 9 * * *</code> Her gün 09:00</p>
          <p><code className="bg-navy-100 px-0.5 rounded">0 9 * * 1-5</code> Hafta içi 09:00</p>
          <p><code className="bg-navy-100 px-0.5 rounded">*/30 * * * *</code> Her 30 dk</p>
        </div>
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

      <p className="text-sm text-navy-300">
        Belirtilen cron zamanlamasına göre otomatik tetiklenir.
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
    { label: 'Sarı', value: '#fef3c7' },
    { label: 'Mavi', value: '#dbeafe' },
    { label: 'Yeşil', value: '#dcfce7' },
    { label: 'Kırmızı', value: '#fee2e2' },
    { label: 'Mor', value: '#ede9fe' },
  ];

  return (
    <>
      <FieldGroup label="Not Metni">
        <textarea
          value={data.text ?? ''}
          onChange={(e) => onChange({ text: e.target.value })}
          rows={5}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 resize-none"
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
          <p className="text-sm text-navy-300">Yükleniyor...</p>
        ) : (
          <select
            value={data.flow_id ?? ''}
            onChange={(e) => onChange({ flow_id: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
          >
            <option value="">Flow seçiniz...</option>
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

      <FieldGroup label={`Girdi Eşleme (${inputEntries.length})`}>
        <div className="space-y-1.5">
          {inputEntries.map(([parentVar, childVar], idx) => (
            <div key={idx} className="flex items-center gap-1">
              <input
                type="text"
                value={parentVar}
                onChange={(e) => updateMapEntry('input_map', inputEntries, idx, 0, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
                placeholder="parent_var"
              />
              <span className="text-navy-300 text-xs flex-shrink-0">&rarr;</span>
              <input
                type="text"
                value={childVar}
                onChange={(e) => updateMapEntry('input_map', inputEntries, idx, 1, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
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
          Parent değişken &rarr; Alt flow değişken
        </p>
      </FieldGroup>

      <FieldGroup label={`Çıktı Eşleme (${outputEntries.length})`}>
        <div className="space-y-1.5">
          {outputEntries.map(([childVar, parentVar], idx) => (
            <div key={idx} className="flex items-center gap-1">
              <input
                type="text"
                value={childVar}
                onChange={(e) => updateMapEntry('output_map', outputEntries, idx, 0, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
                placeholder="child_var"
              />
              <span className="text-navy-300 text-xs flex-shrink-0">&rarr;</span>
              <input
                type="text"
                value={parentVar}
                onChange={(e) => updateMapEntry('output_map', outputEntries, idx, 1, e.target.value)}
                className="flex-1 bg-navy-50 border border-navy-200 rounded px-1.5 py-1 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
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
          + Çıktı Ekle
        </button>
        <p className="text-[10px] text-navy-300 mt-0.5">
          Alt flow değişken &rarr; Parent değişken
        </p>
      </FieldGroup>

      <p className="text-sm text-navy-300">
        Alt flow tamamlandığında <strong>completed</strong>, hata olursa <strong>error</strong> dalına yönlenir.
      </p>
    </>
  );
}

const ECOM_OPERATIONS: { value: EcommerceOperation; label: string }[] = [
  { value: 'list_orders', label: 'Siparişleri Listele' },
  { value: 'get_order', label: 'Sipariş Detay' },
  { value: 'list_products', label: 'Ürünleri Listele' },
  { value: 'get_product', label: 'Ürün Detay' },
  { value: 'list_customers', label: 'Müşterileri Listele' },
  { value: 'fulfill_order', label: 'Siparişi Kargola' },
  { value: 'update_order_status', label: 'Sipariş Durumu Güncelle' },
  { value: 'refund_order_line', label: 'İade Yap' },
];

function ActionEcommerceProps({
  data,
  onChange,
}: {
  data: ActionEcommerceData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  const op = data.operation || 'list_orders';
  const needsOrderId = ['get_order', 'fulfill_order', 'update_order_status', 'refund_order_line'].includes(op);
  const needsProductId = op === 'get_product';
  const isListOrders = op === 'list_orders';
  const isListProducts = op === 'list_products';
  const isListCustomers = op === 'list_customers';
  const isFulfill = op === 'fulfill_order';
  const isUpdateStatus = op === 'update_order_status';
  const isRefund = op === 'refund_order_line';

  return (
    <>
      <FieldGroup label="Sağlayıcı">
        <input
          type="text"
          value={data.provider || 'ikas'}
          readOnly
          className="w-full bg-navy-100 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-500 outline-none cursor-not-allowed"
        />
      </FieldGroup>

      <FieldGroup label="İşlem">
        <select
          value={op}
          onChange={(e) => onChange({ operation: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
        >
          {ECOM_OPERATIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
      </FieldGroup>

      {needsOrderId && (
        <FieldGroup label="Sipariş ID">
          <input
            type="text"
            value={data.order_id ?? ''}
            onChange={(e) => onChange({ order_id: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
            placeholder="{{siparis_id}}"
          />
        </FieldGroup>
      )}

      {needsProductId && (
        <FieldGroup label="Ürün ID">
          <input
            type="text"
            value={data.product_id ?? ''}
            onChange={(e) => onChange({ product_id: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
            placeholder="{{urun_id}}"
          />
        </FieldGroup>
      )}

      {(isListOrders || isListCustomers) && (
        <FieldGroup label="Telefon Filtre">
          <input
            type="text"
            value={data.filter_phone ?? ''}
            onChange={(e) => onChange({ filter_phone: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
            placeholder="{{__phone}}"
          />
        </FieldGroup>
      )}

      {isListCustomers && (
        <>
          <FieldGroup label="E-posta Filtre">
            <input
              type="text"
              value={data.filter_email ?? ''}
              onChange={(e) => onChange({ filter_email: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
              placeholder="{{email}}"
            />
          </FieldGroup>
          <FieldGroup label="Arama">
            <input
              type="text"
              value={data.filter_search ?? ''}
              onChange={(e) => onChange({ filter_search: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
              placeholder="İsim veya anahtar kelime"
            />
          </FieldGroup>
        </>
      )}

      {isListOrders && (
        <FieldGroup label="Durum Filtre">
          <input
            type="text"
            value={data.filter_status ?? ''}
            onChange={(e) => onChange({ filter_status: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
            placeholder="created, approved, shipped..."
          />
        </FieldGroup>
      )}

      {isListProducts && (
        <>
          <FieldGroup label="Arama">
            <input
              type="text"
              value={data.filter_search ?? ''}
              onChange={(e) => onChange({ filter_search: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
              placeholder="Ürün adı"
            />
          </FieldGroup>
          <FieldGroup label="Durum Filtre">
            <input
              type="text"
              value={data.filter_status ?? ''}
              onChange={(e) => onChange({ filter_status: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
              placeholder="active, passive..."
            />
          </FieldGroup>
        </>
      )}

      {isFulfill && (
        <>
          <FieldGroup label="Takip Kodu">
            <input
              type="text"
              value={data.tracking_code ?? ''}
              onChange={(e) => onChange({ tracking_code: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
              placeholder="{{takip_kodu}}"
            />
          </FieldGroup>
          <FieldGroup label="Kargo Firması">
            <input
              type="text"
              value={data.cargo_provider ?? ''}
              onChange={(e) => onChange({ cargo_provider: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
              placeholder="yurtici, aras, mng..."
            />
          </FieldGroup>
        </>
      )}

      {isUpdateStatus && (
        <FieldGroup label="Yeni Durum">
          <input
            type="text"
            value={data.new_status ?? ''}
            onChange={(e) => onChange({ new_status: e.target.value })}
            className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
            placeholder="approved, shipped, delivered..."
          />
        </FieldGroup>
      )}

      {isRefund && (
        <>
          <FieldGroup label="Kalem ID">
            <input
              type="text"
              value={data.line_item_id ?? ''}
              onChange={(e) => onChange({ line_item_id: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
              placeholder="{{kalem_id}}"
            />
          </FieldGroup>
          <FieldGroup label="İade Adedi">
            <input
              type="text"
              value={data.refund_quantity ?? ''}
              onChange={(e) => onChange({ refund_quantity: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
              placeholder="1"
            />
          </FieldGroup>
          <FieldGroup label="İade Nedeni">
            <input
              type="text"
              value={data.refund_reason ?? ''}
              onChange={(e) => onChange({ refund_reason: e.target.value })}
              className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
              placeholder="Müşteri iade talebi"
            />
          </FieldGroup>
        </>
      )}

      <FieldGroup label="Sonuç Değişkeni">
        <input
          type="text"
          value={data.response_variable ?? 'ecom_result'}
          onChange={(e) => onChange({ response_variable: e.target.value })}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
          placeholder="ecom_result"
        />
      </FieldGroup>

      <p className="text-sm text-navy-300">
        Başarılı sonuç <strong>success</strong>, hata ise <strong>error</strong> dalına yönlenir.
      </p>
    </>
  );
}

function LogicWorkingHoursProps() {
  return (
    <div className="p-2 rounded-md bg-amber-50 border border-amber-100">
      <p className="text-sm text-amber-700">
        Bu node tenant ayarlarındaki mesai saatlerini kullanır.
        Çıkış: <strong>within_hours</strong> (mesai içi) / <strong>outside_hours</strong> (mesai dışı).
      </p>
    </div>
  );
}

function ActionAssignGroupProps({
  data,
  onChange,
}: {
  data: ActionAssignGroupData;
  onChange: (d: Record<string, unknown>) => void;
}) {
  return (
    <>
      <FieldGroup label="Grup ID">
        <input
          type="text"
          value={data.group_id ?? ''}
          onChange={(e) => onChange({ group_id: e.target.value })}
          placeholder="INMA'dan gelen grup ID"
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 font-mono"
        />
      </FieldGroup>
      <FieldGroup label="Grup Adı (Görsel)">
        <input
          type="text"
          value={data.group_name ?? ''}
          onChange={(e) => onChange({ group_name: e.target.value })}
          placeholder="Örneğin: Satış Ekibi"
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500"
        />
      </FieldGroup>
      <FieldGroup label="Özet Şablonu (Opsiyonel)">
        <textarea
          value={data.summary_template ?? ''}
          onChange={(e) => onChange({ summary_template: e.target.value })}
          placeholder="Değişken kullanabilirsiniz: {{musteri_adi}}"
          rows={3}
          className="w-full bg-navy-50 border border-navy-200 rounded px-2 py-1.5 text-sm text-navy-700 outline-none focus:border-brand-500 resize-none"
        />
      </FieldGroup>
    </>
  );
}
