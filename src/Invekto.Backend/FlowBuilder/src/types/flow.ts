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
  | 'message_text'
  | 'message_menu'
  | 'logic_condition'
  | 'logic_switch'
  | 'ai_intent'
  | 'ai_faq'
  | 'action_handoff'
  | 'action_api_call'
  | 'action_delay'
  | 'utility_set_variable'
  | 'utility_note';

// Union type for all node data shapes
export type NodeData =
  | TriggerStartData
  | MessageTextData
  | MessageMenuData
  | LogicConditionData
  | LogicSwitchData
  | AiIntentData
  | AiFaqData
  | ActionHandoffData
  | ActionApiCallData
  | ActionDelayData
  | UtilitySetVariableData
  | UtilityNoteData;

/** Base interface with index signature for React Flow compatibility */
interface BaseNodeData {
  label: string;
  [key: string]: unknown;
}

export interface TriggerStartData extends BaseNodeData {
  label: string;
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

export interface AiIntentData extends BaseNodeData {
  label: string;
  intents?: string[];
  confidence_threshold?: number;
}

export interface AiFaqData extends BaseNodeData {
  label: string;
  min_confidence?: number;
}

export interface ActionHandoffData extends BaseNodeData {
  label: string;
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
    label: 'Baslangic',
    description: 'Flow giris noktasi',
    color: '#10b981',
    maxInstances: 1,
    defaultData: { label: 'Baslangic' } as TriggerStartData,
  },
  {
    type: 'message_text',
    category: 'message',
    label: 'Mesaj',
    description: 'Metin mesaj gonder',
    color: '#3b82f6',
    defaultData: { label: 'Mesaj', text: '' } as MessageTextData,
  },
  {
    type: 'message_menu',
    category: 'message',
    label: 'Menu',
    description: 'Secenekli menu goster',
    color: '#3b82f6',
    defaultData: {
      label: 'Menu',
      text: 'Secim yapin:',
      options: [
        { key: '1', label: 'Secenek 1', handle_id: 'opt_1' },
      ],
    } as MessageMenuData,
  },
  {
    type: 'logic_condition',
    category: 'logic',
    label: 'Kosul',
    description: 'If/else dallanma',
    color: '#f59e0b',
    defaultData: { label: 'Kosul', variable: '', operator: 'equals', value: '' } as LogicConditionData,
  },
  {
    type: 'logic_switch',
    category: 'logic',
    label: 'Switch',
    description: 'Coklu dallanma',
    color: '#f59e0b',
    defaultData: {
      label: 'Switch',
      variable: '',
      cases: [{ value: '', handle_id: 'case_1' }],
      default_handle_id: 'default',
    } as LogicSwitchData,
  },
  {
    type: 'ai_intent',
    category: 'ai',
    label: 'Intent Algilama',
    description: 'Claude AI ile niyet tespiti',
    color: '#8b5cf6',
    defaultData: { label: 'Intent', confidence_threshold: 0.5 } as AiIntentData,
  },
  {
    type: 'ai_faq',
    category: 'ai',
    label: 'FAQ Arama',
    description: 'SSS veritabaninda ara',
    color: '#8b5cf6',
    defaultData: { label: 'FAQ', min_confidence: 0.3 } as AiFaqData,
  },
  {
    type: 'action_handoff',
    category: 'action',
    label: 'Temsilciye Aktar',
    description: 'Insana yonlendir (terminal)',
    color: '#ef4444',
    defaultData: { label: 'Temsilciye Aktar' } as ActionHandoffData,
  },
  {
    type: 'action_api_call',
    category: 'action',
    label: 'API Cagrisi',
    description: 'Harici API endpoint cagir',
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
    type: 'utility_set_variable',
    category: 'utility',
    label: 'Degisken Ata',
    description: 'Session degiskeni ayarla',
    color: '#6b7280',
    defaultData: { label: 'Degisken', variable_name: '', value_expression: '' } as UtilitySetVariableData,
  },
  {
    type: 'utility_note',
    category: 'utility',
    label: 'Not',
    description: 'Gorsel yorum (calistirilmaz)',
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
        data: { label: 'Baslangic' },
      },
    ],
    edges: [],
    settings: {
      off_hours_message: 'Su anda mesai saatleri disindayiz.',
      unknown_input_message: 'Anlayamadim. Lutfen gecerli bir secenek girin.',
      handoff_confidence_threshold: 0.5,
      session_timeout_minutes: 30,
      max_loop_count: 10,
    },
  };
}
