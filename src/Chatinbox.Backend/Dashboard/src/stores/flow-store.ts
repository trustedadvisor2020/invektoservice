import { create } from 'zustand';
import {
  type Node,
  type Edge,
  type OnNodesChange,
  type OnEdgesChange,
  type OnConnect,
  type XYPosition,
  applyNodeChanges,
  applyEdgeChanges,
  addEdge,
} from '@xyflow/react';
import type { FlowConfigV2, FlowSettings, FlowMetadata, FlowNodeType, NodeData } from '../types/flow';
import type { WizardMessage } from '../types/wizard';
import { getNodeTypeInfo, createDefaultFlow } from '../types/flow';
import { generateNodeId, generateEdgeId } from '../lib/utils';
import { validateGraph, type ValidationError } from '../lib/graph-validator';
import { enumeratePaths } from '../lib/path-enumerator';
import { needsAutoLayout, autoLayoutNodes } from '../lib/auto-layout';

export interface FlowState {
  // State
  nodes: Node[];
  edges: Edge[];
  selectedNodeId: string | null;
  isDirty: boolean;
  flowMetadata: FlowMetadata;
  flowSettings: FlowSettings;

  // Wizard history (AI-generated flows)
  wizardHistory: WizardMessage[] | null;

  // Validation
  validationErrors: Map<string, ValidationError[]>;

  // AI diff overlay (node change markers when AI suggests changes)
  pendingDiff: Map<string, 'added' | 'modified' | 'removed'>;
  setPendingDiff: (diff: Map<string, 'added' | 'modified' | 'removed'>) => void;

  // Ghost path (AHA #3)
  ghostPathEnabled: boolean;
  ghostPathNodeIds: Set<string>;
  ghostPathEdgeIds: Set<string>;

  // History (simple undo/redo)
  history: Array<{ nodes: Node[]; edges: Edge[] }>;
  historyIndex: number;

  // Node/Edge change handlers (React Flow callbacks)
  onNodesChange: OnNodesChange;
  onEdgesChange: OnEdgesChange;
  onConnect: OnConnect;

  // Actions
  addNode: (type: FlowNodeType, position: XYPosition) => void;
  deleteNode: (id: string) => void;
  updateNodeData: (id: string, data: Record<string, unknown>) => void;
  selectNode: (id: string | null) => void;
  revalidate: () => void;

  // Ghost path toggle
  toggleGhostPath: () => void;

  // Flow operations
  loadFlow: (config: FlowConfigV2, wizardHistory?: WizardMessage[] | null) => void;
  toFlowConfig: () => FlowConfigV2;
  newFlow: () => void;
  setMetadata: (metadata: Partial<FlowMetadata>) => void;
  setSettings: (settings: Partial<FlowSettings>) => void;
  markClean: () => void;

  // Auto-layout
  applyAutoLayout: () => void;

  // Undo/Redo
  pushHistory: () => void;
  undo: () => void;
  redo: () => void;
}

const DEFAULT_FLOW = createDefaultFlow();

export const useFlowStore = create<FlowState>((set, get) => ({
  nodes: [],
  edges: [],
  selectedNodeId: null,
  isDirty: false,
  flowMetadata: { ...DEFAULT_FLOW.metadata },
  flowSettings: { ...DEFAULT_FLOW.settings },
  wizardHistory: null,
  validationErrors: new Map(),
  pendingDiff: new Map(),
  setPendingDiff: (diff) => set({ pendingDiff: diff }),
  ghostPathEnabled: true,
  ghostPathNodeIds: new Set(),
  ghostPathEdgeIds: new Set(),
  history: [],
  historyIndex: -1,

  revalidate: () => {
    const state = get();
    const errors = validateGraph(state.nodes, state.edges);
    const updates: Partial<FlowState> = { validationErrors: errors };

    // Always compute ghost paths (reachable from start)
    const result = enumeratePaths(state.nodes, state.edges);
    updates.ghostPathNodeIds = result.reachableNodeIds;
    updates.ghostPathEdgeIds = result.reachableEdgeIds;

    set(updates);
  },

  toggleGhostPath: () => {
    // Ghost path is always enabled — no-op
  },

  onNodesChange: (changes) => {
    set((state) => ({
      nodes: applyNodeChanges(changes, state.nodes),
      isDirty: true,
    }));
    // Only revalidate on structural changes (node removal).
    // Position/dimension/select changes don't affect graph validation
    // and triggering revalidate here causes infinite re-render loops (React #185).
    if (changes.some((c) => c.type === 'remove')) {
      queueMicrotask(() => get().revalidate());
    }
  },

  onEdgesChange: (changes) => {
    set((state) => ({
      edges: applyEdgeChanges(changes, state.edges),
      isDirty: true,
    }));
    queueMicrotask(() => get().revalidate());
  },

  onConnect: (connection) => {
    const id = generateEdgeId(
      connection.source ?? '',
      connection.target ?? '',
      connection.sourceHandle ?? undefined
    );
    set((state) => {
      get().pushHistory();
      return {
        edges: addEdge({ ...connection, id }, state.edges),
        isDirty: true,
      };
    });
    queueMicrotask(() => get().revalidate());
  },

  addNode: (type, position) => {
    const info = getNodeTypeInfo(type);
    if (!info) return;

    const state = get();

    // Single-trigger slot (FEAT-INMA-PIPELINE-V2 C3b): a flow may have EXACTLY ONE trigger
    // (the backend FlowValidator rejects 0 or >1 trigger nodes). Every new flow is seeded with
    // an undeletable trigger_start, so when a trigger-category node is dropped onto a flow that
    // already has a (different) trigger we REPLACE the existing trigger instead of adding a
    // second one — otherwise the flow becomes unsaveable and alternative triggers (incl.
    // customer_status_changed) would be unusable. Preserves the old trigger's outgoing edges +
    // canvas position; selects the new node; undoable via pushHistory.
    if (info.category === 'trigger') {
      const existingTrigger = state.nodes.find(
        (n) => getNodeTypeInfo(n.type as FlowNodeType)?.category === 'trigger'
      );
      if (existingTrigger) {
        // Re-dropping the SAME trigger type is a no-op (it is already the active trigger).
        if (existingTrigger.type === type) return;

        get().pushHistory();
        const replacedId = generateNodeId(type);
        const replacementNode: Node = {
          id: replacedId,
          type,
          position: existingTrigger.position,
          data: { ...info.defaultData } as Record<string, unknown>,
        };
        set({
          nodes: state.nodes.map((n) => (n.id === existingTrigger.id ? replacementNode : n)),
          // Re-point edges leaving the old trigger to the new one (triggers are source-only).
          edges: state.edges.map((e) =>
            e.source === existingTrigger.id ? { ...e, source: replacedId } : e
          ),
          selectedNodeId: replacedId,
          isDirty: true,
        });
        queueMicrotask(() => get().revalidate());
        return;
      }
    }

    // Check maxInstances (non-trigger nodes, or the FIRST trigger when a flow has none yet)
    if (info.maxInstances) {
      const count = state.nodes.filter((n) => n.type === type).length;
      if (count >= info.maxInstances) return;
    }

    get().pushHistory();

    const id = generateNodeId(type);
    const newNode: Node = {
      id,
      type,
      position,
      data: { ...info.defaultData } as Record<string, unknown>,
    };

    set({
      nodes: [...state.nodes, newNode],
      selectedNodeId: id,
      isDirty: true,
    });
    queueMicrotask(() => get().revalidate());
  },

  deleteNode: (id) => {
    get().pushHistory();
    set((state) => ({
      nodes: state.nodes.filter((n) => n.id !== id),
      edges: state.edges.filter((e) => e.source !== id && e.target !== id),
      selectedNodeId: state.selectedNodeId === id ? null : state.selectedNodeId,
      isDirty: true,
    }));
    queueMicrotask(() => get().revalidate());
  },

  updateNodeData: (id, data) => {
    set((state) => ({
      nodes: state.nodes.map((n) =>
        n.id === id ? { ...n, data: { ...n.data, ...data } } : n
      ),
      isDirty: true,
    }));
    queueMicrotask(() => get().revalidate());
  },

  selectNode: (id) => {
    set({ selectedNodeId: id });
  },

  loadFlow: (config, wizardHistory) => {
    const defaults = createDefaultFlow();

    // Fix double-JSON-encoded fields in node data (DB may store arrays as strings)
    const ARRAY_FIELDS = ['options', 'cases', 'intents'];
    function normalizeNodeData(data: Record<string, unknown>): Record<string, unknown> {
      const result = { ...data };
      for (const field of ARRAY_FIELDS) {
        const val = result[field];
        if (typeof val === 'string') {
          try { result[field] = JSON.parse(val); } catch { result[field] = []; }
        }
      }
      return result;
    }

    let nodes: Node[] = (config.nodes ?? []).map((n) => ({
      id: n.id,
      type: n.type,
      position: n.position && typeof n.position.x === 'number' && typeof n.position.y === 'number'
        ? n.position
        : { x: 0, y: 0 },
      data: normalizeNodeData(n.data as Record<string, unknown>),
    }));

    const edges: Edge[] = (config.edges ?? []).map((e) => ({
      id: e.id,
      source: e.source,
      target: e.target,
      sourceHandle: e.sourceHandle,
      targetHandle: e.targetHandle,
    }));

    // Auto-layout when all nodes lack valid positions (stacked at origin)
    if (needsAutoLayout(nodes)) {
      nodes = autoLayoutNodes(nodes, edges);
    }

    set({
      nodes,
      edges,
      selectedNodeId: null,
      isDirty: false,
      flowMetadata: { ...defaults.metadata, ...config.metadata },
      flowSettings: { ...defaults.settings, ...config.settings },
      wizardHistory: wizardHistory ?? null,
      history: [],
      historyIndex: -1,
    });
    queueMicrotask(() => get().revalidate());
  },

  toFlowConfig: (): FlowConfigV2 => {
    const state = get();
    return {
      version: 2,
      metadata: state.flowMetadata,
      nodes: state.nodes.map((n) => ({
        id: n.id,
        type: n.type as FlowNodeType,
        position: n.position,
        data: n.data as unknown as NodeData,
      })),
      edges: state.edges.map((e) => ({
        id: e.id,
        source: e.source,
        target: e.target,
        sourceHandle: e.sourceHandle ?? undefined,
        targetHandle: e.targetHandle ?? undefined,
      })),
      settings: state.flowSettings,
    };
  },

  newFlow: () => {
    const flow = createDefaultFlow();
    get().loadFlow(flow);
    set({ isDirty: false });
  },

  setMetadata: (metadata) => {
    set((state) => ({
      flowMetadata: { ...state.flowMetadata, ...metadata },
      isDirty: true,
    }));
  },

  setSettings: (settings) => {
    set((state) => ({
      flowSettings: { ...state.flowSettings, ...settings },
      isDirty: true,
    }));
  },

  markClean: () => set({ isDirty: false }),

  applyAutoLayout: () => {
    const state = get();
    if (state.nodes.length <= 1) return;
    get().pushHistory();
    const layouted = autoLayoutNodes(state.nodes, state.edges);
    set({ nodes: layouted, isDirty: true });
    queueMicrotask(() => get().revalidate());
  },

  pushHistory: () => {
    const state = get();
    const snapshot = {
      nodes: JSON.parse(JSON.stringify(state.nodes)),
      edges: JSON.parse(JSON.stringify(state.edges)),
    };

    const newHistory = state.history.slice(0, state.historyIndex + 1);
    newHistory.push(snapshot);

    // Keep max 50 history entries
    if (newHistory.length > 50) newHistory.shift();

    set({
      history: newHistory,
      historyIndex: newHistory.length - 1,
    });
  },

  undo: () => {
    const state = get();
    if (state.historyIndex < 0) return;

    const snapshot = state.history[state.historyIndex];
    set({
      nodes: snapshot.nodes,
      edges: snapshot.edges,
      historyIndex: state.historyIndex - 1,
      isDirty: true,
    });
    queueMicrotask(() => get().revalidate());
  },

  redo: () => {
    const state = get();
    if (state.historyIndex >= state.history.length - 1) return;

    const nextIndex = state.historyIndex + 1;
    if (nextIndex + 1 < state.history.length) {
      const snapshot = state.history[nextIndex + 1];
      set({
        nodes: snapshot.nodes,
        edges: snapshot.edges,
        historyIndex: nextIndex,
        isDirty: true,
      });
      queueMicrotask(() => get().revalidate());
    }
  },
}));
