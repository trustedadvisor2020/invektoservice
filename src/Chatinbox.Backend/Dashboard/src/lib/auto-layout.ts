import type { Node, Edge } from '@xyflow/react';

const CENTER_X = 400;
const Y_GAP = 240;
const X_GAP = 320;
/** Estimated node dimensions for overlap detection */
export const NODE_WIDTH = 260;
export const NODE_HEIGHT = 160;

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

/**
 * Check if two node rects overlap (with a small padding).
 */
function rectsOverlap(
  a: { x: number; y: number },
  b: { x: number; y: number },
  pad = 20,
): boolean {
  return (
    a.x < b.x + NODE_WIDTH + pad &&
    a.x + NODE_WIDTH + pad > b.x &&
    a.y < b.y + NODE_HEIGHT + pad &&
    a.y + NODE_HEIGHT + pad > b.y
  );
}

/**
 * Resolve overlap for a dragged node by nudging it to the nearest
 * non-overlapping position. Returns the corrected position.
 */
export function resolveOverlap(
  draggedId: string,
  draggedPos: { x: number; y: number },
  allNodes: Node[],
): { x: number; y: number } {
  const others = allNodes.filter((n) => n.id !== draggedId);
  const hasOverlap = others.some((n) => rectsOverlap(draggedPos, n.position));
  if (!hasOverlap) return draggedPos;

  // Try nudging in 8 directions with increasing distance
  const directions = [
    { dx: 1, dy: 0 },
    { dx: -1, dy: 0 },
    { dx: 0, dy: 1 },
    { dx: 0, dy: -1 },
    { dx: 1, dy: 1 },
    { dx: -1, dy: 1 },
    { dx: 1, dy: -1 },
    { dx: -1, dy: -1 },
  ];
  const step = 20;
  for (let dist = step; dist <= 600; dist += step) {
    for (const dir of directions) {
      const candidate = {
        x: draggedPos.x + dir.dx * dist,
        y: draggedPos.y + dir.dy * dist,
      };
      if (!others.some((n) => rectsOverlap(candidate, n.position))) {
        return candidate;
      }
    }
  }
  return draggedPos;
}
