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
import { getNodeTypeInfo, createDefaultFlow } from '../types/flow';
import { generateNodeId, generateEdgeId } from '../lib/utils';
import { validateGraph, type ValidationError } from '../lib/graph-validator';

export interface FlowState {
  // State
  nodes: Node[];
  edges: Edge[];
  selectedNodeId: string | null;
  isDirty: boolean;
  flowMetadata: FlowMetadata;
  flowSettings: FlowSettings;

  // Validation
  validationErrors: Map<string, ValidationError[]>;

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

  // Flow operations
  loadFlow: (config: FlowConfigV2) => void;
  toFlowConfig: () => FlowConfigV2;
  newFlow: () => void;
  setMetadata: (metadata: Partial<FlowMetadata>) => void;
  setSettings: (settings: Partial<FlowSettings>) => void;
  markClean: () => void;

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
  validationErrors: new Map(),
  history: [],
  historyIndex: -1,

  revalidate: () => {
    const state = get();
    const errors = validateGraph(state.nodes, state.edges);
    set({ validationErrors: errors });
  },

  onNodesChange: (changes) => {
    set((state) => ({
      nodes: applyNodeChanges(changes, state.nodes),
      isDirty: true,
    }));
    queueMicrotask(() => get().revalidate());
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

    // Check maxInstances
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

  loadFlow: (config) => {
    const defaults = createDefaultFlow();

    const nodes: Node[] = (config.nodes ?? []).map((n) => ({
      id: n.id,
      type: n.type,
      position: n.position,
      data: n.data as Record<string, unknown>,
    }));

    const edges: Edge[] = (config.edges ?? []).map((e) => ({
      id: e.id,
      source: e.source,
      target: e.target,
      sourceHandle: e.sourceHandle,
      targetHandle: e.targetHandle,
    }));

    set({
      nodes,
      edges,
      selectedNodeId: null,
      isDirty: false,
      flowMetadata: { ...defaults.metadata, ...config.metadata },
      flowSettings: { ...defaults.settings, ...config.settings },
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
