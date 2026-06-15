/** Flow Builder Contract v2 - Node/Edge Graph Model */

export interface FlowConfigV2 {
  version: 2;
  metadata: FlowMetadata;
  nodes: FlowNode[];
  edges: FlowEdge[];
  settings: FlowSettings;
}

export interface FlowMetadata {
  name: string;
  description?: string;
  canvas_viewport?: { x: number; y: number; zoom: number };
}

export interface FlowNode {
  id: string;
  type: FlowNodeType;
  position: { x: number; y: number };
  data: NodeData;
}

export interface FlowEdge {
  id: string;
  source: string;
  target: string;
  sourceHandle?: string;
  targetHandle?: string;
}

export interface FlowSettings {
  off_hours_message?: string;
  unknown_input_message?: string;
  handoff_confidence_threshold: number;
  session_timeout_minutes: number;
  max_loop_count: number;
}

// -- Node Types --

export type FlowNodeType =
  | 'trigger_start'
  | 'webhook_trigger'
  | 'outbound_trigger'
  | 'schedule_trigger'
  | 'message_text'
  | 'message_menu'
  | 'logic_condition'
  | 'logic_switch'
  | 'ai_intent'
  | 'ai_faq'
  | 'ai_sentiment'
  | 'action_handoff'
  | 'action_api_call'
  | 'action_delay'
  | 'logic_working_hours'
  | 'utility_set_variable'
  | 'utility_note'
  | 'action_call_flow'
  | 'action_assign_group'
  | 'action_ecommerce'
  | 'customer_status_changed'
  | 'action_set_customer_status';

// Union type for all node data shapes
export type NodeData =
  | TriggerStartData
  | WebhookTriggerData
  | OutboundTriggerData
  | ScheduleTriggerData
  | MessageTextData
  | MessageMenuData
  | LogicConditionData
  | LogicSwitchData
  | AiIntentData
  | AiFaqData
  | AiSentimentData
  | ActionHandoffData
  | ActionApiCallData
  | ActionDelayData
  | LogicWorkingHoursData
  | UtilitySetVariableData
  | UtilityNoteData
  | ActionCallFlowData
  | ActionAssignGroupData
  | ActionEcommerceData
  | CustomerStatusChangedData
  | SetCustomerStatusData;

/** Base interface with index signature for React Flow compatibility */
interface BaseNodeData {
  label: string;
  [key: string]: unknown;
}

export interface TriggerStartData extends BaseNodeData {
  label: string;
}

export interface WebhookTriggerData extends BaseNodeData {
  label: string;
  secret_key?: string;
  payload_variable?: string;
}

export interface OutboundTriggerData extends BaseNodeData {
  label: string;
  campaign_variable?: string;
}

export interface ScheduleTriggerData extends BaseNodeData {
  label: string;
  cron_expression: string;
  timezone?: string;
}

/**
 * FEAT-INMA-PIPELINE-V2 C3b: INMA customer_status (feature-group) change trigger.
 * `feature_group_id` is OPTIONAL and serialized as a NUMERIC STRING (the cxapi catalog group id):
 *   - '' or absent => catch-all: the flow fires on ANY customer-status group change.
 *   - '<numeric id>' => fires only when that specific feature group changes.
 * The backend (C3a) reads this via GetData('feature_group_id') as a Dictionary<string,string>;
 * a NON-numeric value is silently skipped, so the picker only ever emits '' or a numeric string.
 * Matching is GROUP-level (fires on any change in the group) — there is NO from/to filtering.
 */
export interface CustomerStatusChangedData extends BaseNodeData {
  label: string;
  feature_group_id?: string;
}

/**
 * FEAT-INMA-PIPELINE-V2 C4: 'Set Customer Status' ACTION node — writes a lead's INMA feature-group
 * selection back via cxapi. Both fields serialize as STRINGS (read by the Automation handler via
 * GetData):
 *   - `feature_group_id`: REQUIRED numeric string (the catalog group id).
 *   - `feature_ids`: comma-separated feature ids forming the COMPLETE new selection for the group;
 *     '' or absent => CLEAR the group's selection. FULL-LIST semantics (the array replaces the whole
 *     group — for multi-select groups this REMOVES any unlisted features). Single + Multi only; text-mode
 *     (selectionMode===3) groups are disabled in the picker (the vendor /update has no text-write payload).
 */
export interface SetCustomerStatusData extends BaseNodeData {
  label: string;
  feature_group_id?: string;
  feature_ids?: string;
}

export interface AiSentimentData extends BaseNodeData {
  label: string;
  threshold?: number;
}

export interface MessageTextData extends BaseNodeData {
  label: string;
  text: string;
}

export interface MenuOption {
  key: string;
  label: string;
  handle_id: string;
}

export interface MessageMenuData extends BaseNodeData {
  label: string;
  text: string;
  options: MenuOption[];
}

export interface LogicConditionData extends BaseNodeData {
  label: string;
  variable: string;
  operator: 'equals' | 'contains' | 'starts_with' | 'greater_than' | 'less_than' | 'is_empty' | 'regex';
  value: string;
}

export interface LogicSwitchData extends BaseNodeData {
  label: string;
  variable: string;
  cases: Array<{ value: string; handle_id: string }>;
  default_handle_id: string;
}

export interface LogicWorkingHoursData extends BaseNodeData {
  label: string;
}

export interface AiIntentData extends BaseNodeData {
  label: string;
  intents?: string[];
  confidence_threshold?: number;
  ask_name?: boolean;
  greeting_message?: string;
}

export interface AiFaqData extends BaseNodeData {
  label: string;
  min_confidence?: number;
  search_source?: 'faq_only' | 'all';
}

export interface ActionHandoffData extends BaseNodeData {
  label: string;
  summary_template?: string;
}

export interface ActionAssignGroupData extends BaseNodeData {
  label: string;
  group_id: string;
  group_name?: string;
  summary_template?: string;
}

export interface ActionApiCallData extends BaseNodeData {
  label: string;
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  url: string;
  headers?: Record<string, string>;
  body_template?: string;
  response_variable?: string;
  timeout_ms?: number;
}

export interface ActionDelayData extends BaseNodeData {
  label: string;
  seconds: number;
}

export interface UtilitySetVariableData extends BaseNodeData {
  label: string;
  variable_name: string;
  value_expression: string;
}

export interface UtilityNoteData extends BaseNodeData {
  label: string;
  text: string;
  color?: string;
}

export interface ActionCallFlowData extends BaseNodeData {
  label: string;
  flow_id: string;
  input_map: string; // JSON: { "parent_var": "child_var" }
  output_map: string; // JSON: { "child_var": "parent_var" }
}

export type EcommerceOperation =
  | 'list_orders'
  | 'get_order'
  | 'list_products'
  | 'get_product'
  | 'list_customers'
  | 'fulfill_order'
  | 'update_order_status'
  | 'refund_order_line';

export interface ActionEcommerceData extends BaseNodeData {
  label: string;
  provider: string;
  operation: EcommerceOperation;
  order_id?: string;
  product_id?: string;
  filter_phone?: string;
  filter_email?: string;
  filter_search?: string;
  filter_status?: string;
  tracking_code?: string;
  cargo_provider?: string;
  new_status?: string;
  line_item_id?: string;
  refund_quantity?: string;
  refund_reason?: string;
  response_variable?: string;
}

// -- Node Category Metadata --

export type NodeCategory = 'trigger' | 'message' | 'logic' | 'ai' | 'action' | 'utility';

export interface NodeTypeInfo {
  type: FlowNodeType;
  category: NodeCategory;
  label: string;
  description: string;
  color: string;
  maxInstances?: number; // e.g., trigger_start = 1
  defaultData: NodeData;
}

export const NODE_TYPE_REGISTRY: NodeTypeInfo[] = [
  {
    type: 'trigger_start',
    category: 'trigger',
    label: 'Başlangıç',
    description: 'Flow giriş noktası',
    color: '#10b981',
    maxInstances: 1,
    defaultData: { label: 'Başlangıç' } as TriggerStartData,
  },
  {
    type: 'webhook_trigger',
    category: 'trigger',
    label: 'Webhook',
    description: 'Dış sistemden gelen event tetikleyici',
    color: '#10b981',
    maxInstances: 1,
    defaultData: { label: 'Webhook' } as WebhookTriggerData,
  },
  {
    type: 'outbound_trigger',
    category: 'trigger',
    label: 'Outbound',
    description: 'Outbound kampanya tetikleyici',
    color: '#10b981',
    maxInstances: 1,
    defaultData: { label: 'Outbound' } as OutboundTriggerData,
  },
  {
    type: 'schedule_trigger',
    category: 'trigger',
    label: 'Zamanlayıcı',
    description: 'Cron tabanlı zamanlanmış tetikleyici',
    color: '#10b981',
    maxInstances: 1,
    defaultData: { label: 'Zamanlayıcı', cron_expression: '0 9 * * *' } as ScheduleTriggerData,
  },
  {
    type: 'customer_status_changed',
    category: 'trigger',
    label: 'Müşteri Durumu Değişti',
    description: 'INMA müşteri durumu (feature grubu) değiştiğinde tetikler',
    color: '#10b981',
    maxInstances: 1,
    // defaultData carries ONLY label → fresh node = catch-all (no feature_group_id key).
    defaultData: { label: 'Müşteri Durumu Değişti' } as CustomerStatusChangedData,
  },
  {
    type: 'message_text',
    category: 'message',
    label: 'Mesaj',
    description: 'Metin mesaj gönder',
    color: '#3b82f6',
    defaultData: { label: 'Mesaj', text: '' } as MessageTextData,
  },
  {
    type: 'message_menu',
    category: 'message',
    label: 'Menü',
    description: 'Seçenekli menü göster',
    color: '#3b82f6',
    defaultData: {
      label: 'Menü',
      text: 'Seçim yapın:',
      options: [
        { key: '1', label: 'Seçenek 1', handle_id: 'opt_1' },
      ],
    } as MessageMenuData,
  },
  {
    type: 'logic_condition',
    category: 'logic',
    label: 'Koşul',
    description: 'If/else dallanma',
    color: '#f59e0b',
    defaultData: { label: 'Koşul', variable: '', operator: 'equals', value: '' } as LogicConditionData,
  },
  {
    type: 'logic_switch',
    category: 'logic',
    label: 'Switch',
    description: 'Çoklu dallanma',
    color: '#f59e0b',
    defaultData: {
      label: 'Switch',
      variable: '',
      cases: [{ value: '', handle_id: 'case_1' }],
      default_handle_id: 'default',
    } as LogicSwitchData,
  },
  {
    type: 'logic_working_hours',
    category: 'logic',
    label: 'Mesai Saati',
    description: 'Mesai saati içi/dışı dallanma',
    color: '#f59e0b',
    defaultData: { label: 'Mesai Saati' } as LogicWorkingHoursData,
  },
  {
    type: 'ai_intent',
    category: 'ai',
    label: 'Intent Algılama',
    description: 'Claude AI ile niyet tespiti',
    color: '#8b5cf6',
    defaultData: { label: 'Intent', confidence_threshold: 0.5, ask_name: true } as AiIntentData,
  },
  {
    type: 'ai_faq',
    category: 'ai',
    label: 'FAQ Arama',
    description: 'SSS veritabanında ara',
    color: '#8b5cf6',
    defaultData: { label: 'FAQ', min_confidence: 0.65 } as AiFaqData,
  },
  {
    type: 'ai_sentiment',
    category: 'ai',
    label: 'Duygu Analizi',
    description: 'Claude AI ile müşteri duygu tespiti',
    color: '#8b5cf6',
    defaultData: { label: 'Duygu Analizi', threshold: 0.5 } as AiSentimentData,
  },
  {
    type: 'action_handoff',
    category: 'action',
    label: 'Temsilciye Aktar',
    description: 'İnsana yönlendir (terminal)',
    color: '#ef4444',
    defaultData: { label: 'Temsilciye Aktar' } as ActionHandoffData,
  },
  {
    type: 'action_assign_group',
    category: 'action',
    label: 'Gruba Ata',
    description: 'INMA grubuna yönlendir (terminal)',
    color: '#ef4444',
    defaultData: { label: 'Gruba Ata', group_id: '' } as ActionAssignGroupData,
  },
  {
    type: 'action_api_call',
    category: 'action',
    label: 'API Çağrısı',
    description: 'Harici API endpoint çağır',
    color: '#ef4444',
    defaultData: { label: 'API', method: 'POST', url: '', timeout_ms: 5000 } as ActionApiCallData,
  },
  {
    type: 'action_delay',
    category: 'action',
    label: 'Bekle',
    description: 'N saniye bekle',
    color: '#ef4444',
    defaultData: { label: 'Bekle', seconds: 5 } as ActionDelayData,
  },
  {
    type: 'action_call_flow',
    category: 'action',
    label: 'Alt Flow',
    description: 'Başka bir flow çağır ve tamamlanmasını bekle',
    color: '#ef4444',
    defaultData: { label: 'Alt Flow', flow_id: '', input_map: '{}', output_map: '{}' } as ActionCallFlowData,
  },
  {
    type: 'action_ecommerce',
    category: 'action',
    label: 'E-Ticaret',
    description: 'E-ticaret işlemleri (sipariş, ürün, müşteri)',
    color: '#ef4444',
    defaultData: { label: 'E-Ticaret', provider: 'ikas', operation: 'list_orders', response_variable: 'ecom_result' } as ActionEcommerceData,
  },
  {
    // FEAT-INMA-PIPELINE-V2 C4: writes the lead's INMA customer status (feature-group selection) back via cxapi.
    type: 'action_set_customer_status',
    category: 'action',
    label: 'Müşteri Durumu Ata',
    description: 'INMA müşteri durumunu (feature grubu) güncelle',
    color: '#ef4444',
    // Fresh node carries only label → invalid until a group is picked (feature_group_id is required).
    defaultData: { label: 'Müşteri Durumu Ata' } as SetCustomerStatusData,
  },
  {
    type: 'utility_set_variable',
    category: 'utility',
    label: 'Değişken Ata',
    description: 'Session değişkeni ayarla',
    color: '#6b7280',
    defaultData: { label: 'Değişken', variable_name: '', value_expression: '' } as UtilitySetVariableData,
  },
  {
    type: 'utility_note',
    category: 'utility',
    label: 'Not',
    description: 'Görsel yorum (çalıştırılmaz)',
    color: '#6b7280',
    defaultData: { label: 'Not', text: '' } as UtilityNoteData,
  },
];

export function getNodeTypeInfo(type: FlowNodeType): NodeTypeInfo | undefined {
  return NODE_TYPE_REGISTRY.find(n => n.type === type);
}

// -- Default flow for new tenants --

export function createDefaultFlow(): FlowConfigV2 {
  return {
    version: 2,
    metadata: {
      name: 'Yeni Flow',
      canvas_viewport: { x: 0, y: 0, zoom: 1 },
    },
    nodes: [
      {
        id: 'trigger_start_1',
        type: 'trigger_start',
        position: { x: 300, y: 50 },
        data: { label: 'Başlangıç' },
      },
    ],
    edges: [],
    settings: {
      off_hours_message: 'Şu anda mesai saatleri dışındayız.',
      unknown_input_message: 'Anlayamadım. Lütfen geçerli bir seçenek girin.',
      handoff_confidence_threshold: 0.5,
      session_timeout_minutes: 30,
      max_loop_count: 10,
    },
  };
}

// ============================================================
// Flow Execution Log types
// ============================================================

export interface FlowExecutionSummary {
  id: number;
  flow_id: number;
  chat_id: string | null;
  phone: string | null;
  trigger_message: string | null;
  started_at: string;
  completed_at: string | null;
  status: 'running' | 'completed' | 'error' | 'handed_off' | 'waiting';
  node_count: number;
}

export interface FlowExecutionDetail extends FlowExecutionSummary {
  instance_id: string | null;
  node_trace: NodeTraceEntry[];
  variables_final: Record<string, string> | null;
  error_detail: string | null;
}

export interface NodeTraceEntry {
  node_id: string;
  node_type: string;
  label: string | null;
  entered_at: string;
  exit_handle: string | null;
  duration_ms: number | null;
  user_input?: string;
  bot_messages?: string[];
  variables?: Record<string, string>;
}

// ============================================================
// Flow Monitor types (cross-flow)
// ============================================================

export interface MonitorExecutionSummary {
  id: number;
  flow_id: number;
  flow_name: string;
  chat_id: string | null;
  phone: string | null;
  trigger_message: string | null;
  started_at: string;
  completed_at: string | null;
  status: 'running' | 'completed' | 'error' | 'handed_off' | 'waiting';
  node_count: number;
}

export interface MonitorFilters {
  flow_id?: number;
  status?: string;
  date_from?: string;
  date_to?: string;
  phone?: string;
}
