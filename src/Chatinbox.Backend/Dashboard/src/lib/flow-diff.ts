import type { FlowConfigV2 } from '../types/flow';

export interface NodeDiff {
  id: string;
  status: 'added' | 'removed' | 'modified' | 'unchanged';
}

export function computeFlowDiff(
  oldConfig: FlowConfigV2 | null,
  newConfig: FlowConfigV2 | null
): Map<string, NodeDiff> {
  const diffs = new Map<string, NodeDiff>();

  if (!newConfig) return diffs;
  if (!oldConfig) {
    // All nodes are new
    for (const node of newConfig.nodes) {
      diffs.set(node.id, { id: node.id, status: 'added' });
    }
    return diffs;
  }

  const oldNodes = new Map(oldConfig.nodes.map(n => [n.id, n]));
  const newNodes = new Map(newConfig.nodes.map(n => [n.id, n]));

  // Check for added and modified nodes
  for (const [id, newNode] of newNodes) {
    const oldNode = oldNodes.get(id);
    if (!oldNode) {
      diffs.set(id, { id, status: 'added' });
    } else {
      const oldData = JSON.stringify(oldNode.data);
      const newData = JSON.stringify(newNode.data);
      diffs.set(id, { id, status: oldData !== newData ? 'modified' : 'unchanged' });
    }
  }

  // Check for removed nodes
  for (const [id] of oldNodes) {
    if (!newNodes.has(id)) {
      diffs.set(id, { id, status: 'removed' });
    }
  }

  return diffs;
}

export function getDiffClassName(status: NodeDiff['status']): string {
  switch (status) {
    case 'added': return 'ring-2 ring-green-400 ring-offset-2';
    case 'removed': return 'ring-2 ring-red-400 opacity-50';
    case 'modified': return 'ring-2 ring-amber-400 ring-offset-1';
    default: return '';
  }
}
