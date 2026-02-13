import type { Node, Edge } from '@xyflow/react';

const MAX_PATHS = 10;

interface PathResult {
  paths: string[][];
  reachableNodeIds: Set<string>;
  reachableEdgeIds: Set<string>;
}

/**
 * Enumerate all paths from trigger_start to terminal nodes via DFS.
 * Cycle detection via visited set prevents infinite loops.
 * Max 10 paths to keep computation bounded.
 */
export function enumeratePaths(nodes: Node[], edges: Edge[]): PathResult {
  const result: PathResult = {
    paths: [],
    reachableNodeIds: new Set(),
    reachableEdgeIds: new Set(),
  };

  // Build adjacency: source -> { target, edgeId }[]
  const adjacency = new Map<string, Array<{ target: string; edgeId: string }>>();
  for (const edge of edges) {
    if (!adjacency.has(edge.source)) {
      adjacency.set(edge.source, []);
    }
    adjacency.get(edge.source)!.push({ target: edge.target, edgeId: edge.id });
  }

  // Find trigger_start
  const startNode = nodes.find((n) => n.type === 'trigger_start');
  if (!startNode) return result;

  // Terminal types (no outgoing expected)
  const terminalTypes = new Set(['action_handoff']);
  const nodeTypeMap = new Map<string, string>();
  for (const n of nodes) {
    nodeTypeMap.set(n.id, n.type ?? '');
  }

  // DFS
  const currentPath: string[] = [];
  const currentEdges: string[] = [];
  const visited = new Set<string>();

  function dfs(nodeId: string) {
    if (result.paths.length >= MAX_PATHS) return;

    currentPath.push(nodeId);
    visited.add(nodeId);
    result.reachableNodeIds.add(nodeId);

    const neighbors = adjacency.get(nodeId) ?? [];
    const isTerminal = terminalTypes.has(nodeTypeMap.get(nodeId) ?? '') || neighbors.length === 0;

    if (isTerminal) {
      // Record path
      result.paths.push([...currentPath]);
      // Add all edges in current path
      for (const eid of currentEdges) {
        result.reachableEdgeIds.add(eid);
      }
    } else {
      for (const { target, edgeId } of neighbors) {
        if (!visited.has(target)) {
          result.reachableEdgeIds.add(edgeId);
          result.reachableNodeIds.add(target);
          currentEdges.push(edgeId);
          dfs(target);
          currentEdges.pop();
        }
      }
    }

    currentPath.pop();
    visited.delete(nodeId);
  }

  dfs(startNode.id);

  return result;
}

/**
 * Get the set of reachable node IDs from trigger_start.
 */
export function getReachableNodeIds(nodes: Node[], edges: Edge[]): Set<string> {
  return enumeratePaths(nodes, edges).reachableNodeIds;
}
