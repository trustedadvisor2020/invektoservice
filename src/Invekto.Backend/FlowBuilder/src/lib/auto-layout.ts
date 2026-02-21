import type { Node, Edge } from '@xyflow/react';

const CENTER_X = 400;
const Y_GAP = 180;
const X_GAP = 250;

/**
 * Detect if nodes need auto-layout.
 * Returns true when 2+ nodes all sit at (0,0) — the default fallback position.
 */
export function needsAutoLayout(nodes: Node[]): boolean {
  if (nodes.length <= 1) return false;
  return nodes.every((n) => n.position.x === 0 && n.position.y === 0);
}

/**
 * BFS-based auto-layout: arranges nodes top-to-bottom, siblings left-to-right.
 * Handles cycles, orphan nodes, and multiple roots (trigger nodes).
 */
export function autoLayoutNodes(nodes: Node[], edges: Edge[]): Node[] {
  if (nodes.length === 0) return nodes;

  // Build adjacency: source → target list
  const children = new Map<string, string[]>();
  const hasParent = new Set<string>();
  for (const e of edges) {
    const list = children.get(e.source) ?? [];
    list.push(e.target);
    children.set(e.source, list);
    hasParent.add(e.target);
  }

  // Find roots: trigger nodes first, then nodes with no incoming edges
  const nodeIds = new Set(nodes.map((n) => n.id));
  const roots: string[] = [];
  for (const n of nodes) {
    if (n.type?.startsWith('trigger_')) roots.push(n.id);
  }
  if (roots.length === 0) {
    for (const n of nodes) {
      if (!hasParent.has(n.id)) roots.push(n.id);
    }
  }
  if (roots.length === 0) {
    roots.push(nodes[0].id);
  }

  // BFS to assign levels (handles cycles by not revisiting)
  const level = new Map<string, number>();
  const queue: string[] = [];
  for (const r of roots) {
    if (!level.has(r)) {
      level.set(r, 0);
      queue.push(r);
    }
  }

  while (queue.length > 0) {
    const nodeId = queue.shift()!;
    const currentLevel = level.get(nodeId)!;
    for (const child of children.get(nodeId) ?? []) {
      if (!level.has(child) && nodeIds.has(child)) {
        level.set(child, currentLevel + 1);
        queue.push(child);
      }
    }
  }

  // Assign orphan nodes (not reachable from roots) to next levels
  let maxLevel = 0;
  for (const l of level.values()) {
    if (l > maxLevel) maxLevel = l;
  }
  for (const n of nodes) {
    if (!level.has(n.id)) {
      level.set(n.id, ++maxLevel);
    }
  }

  // Group node ids by level
  const levels = new Map<number, string[]>();
  for (const [id, l] of level) {
    const group = levels.get(l) ?? [];
    group.push(id);
    levels.set(l, group);
  }

  // Compute positions: each level = Y row, siblings spread horizontally
  const positions = new Map<string, { x: number; y: number }>();
  for (const [l, ids] of levels) {
    const totalWidth = (ids.length - 1) * X_GAP;
    const startX = CENTER_X - totalWidth / 2;
    ids.forEach((id, i) => {
      positions.set(id, { x: startX + i * X_GAP, y: 50 + l * Y_GAP });
    });
  }

  return nodes.map((n) => ({
    ...n,
    position: positions.get(n.id) ?? n.position,
  }));
}
