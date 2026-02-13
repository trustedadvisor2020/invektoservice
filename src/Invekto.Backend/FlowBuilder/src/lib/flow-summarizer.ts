/** Flow Summarizer - DFS traversal for human-readable flow preview
 *  Used by FlowSummaryBar (always-visible, updates on every change)
 */

import type { Node, Edge } from '@xyflow/react';
import type { FlowNodeType, MessageTextData, MessageMenuData } from '../types/flow';

export interface SummaryLine {
  text: string;
  indent: number;
  nodeType: FlowNodeType;
}

export interface FlowSummaryResult {
  lines: SummaryLine[];
  totalSteps: number;
  hasErrors: boolean;
}

const MAX_DISPLAY_LINES = 5;

function getNodeLabel(node: Node): string {
  const data = node.data as Record<string, unknown>;
  const nodeType = node.type as FlowNodeType;

  switch (nodeType) {
    case 'trigger_start':
      return 'Musteri mesaj gonderir';
    case 'message_text': {
      const text = (data as MessageTextData).text;
      if (!text || text.trim() === '') return 'Mesaj (bos)';
      return text.length > 40 ? text.substring(0, 40) + '...' : text;
    }
    case 'message_menu': {
      const menuData = data as MessageMenuData;
      return `Menu: ${menuData.text || 'Secim yapin'}`;
    }
    case 'action_handoff':
      return 'Temsilciye aktar';
    case 'utility_note':
      return '';
    default:
      return (data.label as string) || nodeType;
  }
}

export function summarizeFlow(nodes: Node[], edges: Edge[]): FlowSummaryResult {
  if (nodes.length === 0) {
    return { lines: [], totalSteps: 0, hasErrors: false };
  }

  const startNode = nodes.find((n) => n.type === 'trigger_start');
  if (!startNode) {
    return {
      lines: [{ text: 'Baslangic adimi bulunamadi', indent: 0, nodeType: 'trigger_start' }],
      totalSteps: 0,
      hasErrors: true,
    };
  }

  // Build adjacency: source -> [{target, sourceHandle}]
  const adjacency = new Map<string, Array<{ target: string; sourceHandle?: string }>>();
  for (const edge of edges) {
    const list = adjacency.get(edge.source) ?? [];
    list.push({ target: edge.target, sourceHandle: edge.sourceHandle ?? undefined });
    adjacency.set(edge.source, list);
  }

  const nodeMap = new Map<string, Node>(nodes.map((n) => [n.id, n]));
  const allLines: SummaryLine[] = [];
  const visited = new Set<string>();

  function dfs(nodeId: string, indent: number) {
    if (visited.has(nodeId)) return;
    visited.add(nodeId);

    const node = nodeMap.get(nodeId);
    if (!node) return;

    const nodeType = node.type as FlowNodeType;

    // Skip utility_note in summary (non-executable)
    if (nodeType !== 'utility_note') {
      const label = getNodeLabel(node);
      if (label) {
        allLines.push({ text: label, indent, nodeType });
      }
    }

    // For message_menu, show each option as a sub-line, then recurse per branch
    if (nodeType === 'message_menu') {
      const menuData = node.data as MessageMenuData;
      const children = adjacency.get(nodeId) ?? [];

      for (const opt of (menuData.options ?? [])) {
        allLines.push({
          text: `${opt.key}. ${opt.label}`,
          indent: indent + 1,
          nodeType: 'message_menu',
        });

        const matchEdge = children.find((c) => c.sourceHandle === opt.handle_id);
        if (matchEdge) {
          dfs(matchEdge.target, indent + 2);
        }
      }
      return;
    }

    // For all other nodes, follow output edges
    const children = adjacency.get(nodeId) ?? [];
    for (const child of children) {
      dfs(child.target, indent);
    }
  }

  dfs(startNode.id, 0);

  return {
    lines: allLines,
    totalSteps: allLines.length,
    hasErrors: false,
  };
}

export function truncateSummary(summary: FlowSummaryResult): {
  displayLines: SummaryLine[];
  truncatedCount: number;
} {
  if (summary.lines.length <= MAX_DISPLAY_LINES) {
    return { displayLines: summary.lines, truncatedCount: 0 };
  }

  return {
    displayLines: summary.lines.slice(0, MAX_DISPLAY_LINES),
    truncatedCount: summary.lines.length - MAX_DISPLAY_LINES,
  };
}
