/** Graph validation for Flow Builder - SPA-side real-time validation
 *  Shared foundation for Phase 2.5 (#2 Kirmizi Kenar), Phase 3c (#3 Ghost Path, #5 Saglik Skoru)
 */

import type { Node, Edge } from '@xyflow/react';
import type {
  FlowNodeType,
  MessageTextData,
  MessageMenuData,
  LogicConditionData,
  LogicSwitchData,
  AiIntentData,
  ActionApiCallData,
  ActionDelayData,
  UtilitySetVariableData,
} from '../types/flow';

// --- Types ---

export type ValidationSeverity = 'error' | 'warning' | 'info';
export type ValidationType = 'orphan' | 'dead_end' | 'empty_field';

export interface ValidationError {
  type: ValidationType;
  severity: ValidationSeverity;
  message: string;
}

/** Map from nodeId -> array of validation errors */
export type ValidationMap = Map<string, ValidationError[]>;

// --- Terminal / Source node types ---

/** Node types that are NOT expected to have input edges */
const SOURCE_NODE_TYPES: Set<FlowNodeType> = new Set([
  'trigger_start',
]);

/** Node types that are NOT expected to have output edges (terminal nodes) */
const TERMINAL_NODE_TYPES: Set<FlowNodeType> = new Set([
  'action_handoff',
  'utility_note',
]);

// --- Validation Rules ---

function checkOrphan(
  nodeType: FlowNodeType,
  incomingEdgeCount: number,
): ValidationError | null {
  if (SOURCE_NODE_TYPES.has(nodeType)) return null;
  if (incomingEdgeCount > 0) return null;

  return {
    type: 'orphan',
    severity: 'error',
    message: 'Bu adima hicbir yerden ulasilamiyor',
  };
}

function checkDeadEnd(
  nodeType: FlowNodeType,
  outgoingEdgeCount: number,
): ValidationError | null {
  if (TERMINAL_NODE_TYPES.has(nodeType)) return null;
  if (outgoingEdgeCount > 0) return null;

  return {
    type: 'dead_end',
    severity: 'warning',
    message: 'Bu adimdan sonra devam eden bir baglanti yok',
  };
}

function checkEmptyField(node: Node): ValidationError | null {
  const nodeType = node.type as FlowNodeType;
  const data = node.data as Record<string, unknown>;

  switch (nodeType) {
    case 'message_text': {
      const text = (data as MessageTextData).text;
      if (!text || text.trim() === '') {
        return {
          type: 'empty_field',
          severity: 'warning',
          message: 'Mesaj metni bos',
        };
      }
      break;
    }
    case 'message_menu': {
      const options = (data as MessageMenuData).options;
      if (!options || options.length === 0) {
        return {
          type: 'empty_field',
          severity: 'warning',
          message: 'Menu secenegi eklenmedi',
        };
      }
      break;
    }
    case 'logic_condition': {
      const cond = data as LogicConditionData;
      if (!cond.variable || cond.variable.trim() === '') {
        return { type: 'empty_field', severity: 'warning', message: 'Kosul degiskeni bos' };
      }
      if (cond.operator !== 'is_empty' && (!cond.value || cond.value.trim() === '')) {
        return { type: 'empty_field', severity: 'warning', message: 'Kosul degeri bos' };
      }
      break;
    }
    case 'logic_switch': {
      const sw = data as LogicSwitchData;
      if (!sw.variable || sw.variable.trim() === '') {
        return { type: 'empty_field', severity: 'warning', message: 'Switch degiskeni bos' };
      }
      if (!sw.cases || sw.cases.length === 0) {
        return { type: 'empty_field', severity: 'warning', message: 'Switch durumu eklenmedi' };
      }
      break;
    }
    case 'action_delay': {
      const delay = data as ActionDelayData;
      if (!delay.seconds || delay.seconds <= 0) {
        return { type: 'empty_field', severity: 'warning', message: 'Bekleme suresi gecersiz' };
      }
      break;
    }
    case 'utility_set_variable': {
      const sv = data as UtilitySetVariableData;
      if (!sv.variable_name || sv.variable_name.trim() === '') {
        return { type: 'empty_field', severity: 'warning', message: 'Degisken adi bos' };
      }
      if (!sv.value_expression || sv.value_expression.trim() === '') {
        return { type: 'empty_field', severity: 'warning', message: 'Deger ifadesi bos' };
      }
      break;
    }
    case 'ai_intent': {
      const ai = data as AiIntentData;
      if (!ai.intents || ai.intents.length === 0) {
        return { type: 'empty_field', severity: 'warning', message: 'Intent listesi bos' };
      }
      break;
    }
    case 'action_api_call': {
      const api = data as ActionApiCallData;
      if (!api.url || api.url.trim() === '') {
        return { type: 'empty_field', severity: 'warning', message: 'API URL bos' };
      }
      if (!api.method) {
        return { type: 'empty_field', severity: 'warning', message: 'HTTP metodu secilmedi' };
      }
      break;
    }
  }

  return null;
}

// --- Main Validator ---

export function validateGraph(nodes: Node[], edges: Edge[]): ValidationMap {
  const result: ValidationMap = new Map();

  // Pre-compute edge counts per node
  const incomingCount = new Map<string, number>();
  const outgoingCount = new Map<string, number>();

  for (const node of nodes) {
    incomingCount.set(node.id, 0);
    outgoingCount.set(node.id, 0);
  }

  for (const edge of edges) {
    incomingCount.set(edge.target, (incomingCount.get(edge.target) ?? 0) + 1);
    outgoingCount.set(edge.source, (outgoingCount.get(edge.source) ?? 0) + 1);
  }

  for (const node of nodes) {
    const errors: ValidationError[] = [];
    const nodeType = node.type as FlowNodeType;
    const incoming = incomingCount.get(node.id) ?? 0;
    const outgoing = outgoingCount.get(node.id) ?? 0;

    const orphanErr = checkOrphan(nodeType, incoming);
    if (orphanErr) errors.push(orphanErr);

    const deadEndErr = checkDeadEnd(nodeType, outgoing);
    if (deadEndErr) errors.push(deadEndErr);

    const emptyErr = checkEmptyField(node);
    if (emptyErr) errors.push(emptyErr);

    if (errors.length > 0) {
      result.set(node.id, errors);
    }
  }

  return result;
}

// --- Utility for ring color (used by BaseNode) ---

export function getValidationRingColor(errors: ValidationError[]): string | null {
  if (errors.length === 0) return null;

  // Priority: error (red) > warning (orange)
  const hasError = errors.some((e) => e.severity === 'error');
  if (hasError) return '#ef4444'; // red-500

  return '#f97316'; // orange-500
}

export function getValidationTooltip(errors: ValidationError[]): string {
  return errors.map((e) => e.message).join('\n');
}
