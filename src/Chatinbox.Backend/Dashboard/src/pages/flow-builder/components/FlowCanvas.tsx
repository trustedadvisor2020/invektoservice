import { useCallback, useMemo, useRef } from 'react';
import {
  ReactFlow,
  Background,
  Controls,

  BackgroundVariant,
  type ReactFlowInstance,
  type Connection,
  type Edge,
} from '@xyflow/react';
import { useFlowStore } from '../../../stores/flow-store';
import { useAiChatStore } from '../../../stores/ai-chat-store';
import { nodeTypes } from './nodes';
import { DeleteEdge } from './DeleteEdgeButton';
import type { FlowNodeType } from '../../../types/flow';
import { resolveEdgeLabel } from '../../../lib/node-metadata';

const edgeTypes = {
  deletable: DeleteEdge,
};

export function FlowCanvas() {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const reactFlowInstance = useRef<ReactFlowInstance | null>(null);

  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const onNodesChange = useFlowStore((s) => s.onNodesChange);
  const onEdgesChange = useFlowStore((s) => s.onEdgesChange);
  const onConnect = useFlowStore((s) => s.onConnect);
  const addNode = useFlowStore((s) => s.addNode);
  const selectNode = useFlowStore((s) => s.selectNode);
  const deleteNode = useFlowStore((s) => s.deleteNode);
  const ghostPathEnabled = useFlowStore((s) => s.ghostPathEnabled);
  const ghostPathEdgeIds = useFlowStore((s) => s.ghostPathEdgeIds);
  const pendingDiffSize = useFlowStore((s) => s.pendingDiff.size);
  const hasPendingConfig = useAiChatStore((s) => !!s.pendingFlowConfig);

  // Build node lookup for edge label resolution
  const nodeMap = useMemo(() => {
    const map = new Map<string, { type: FlowNodeType; data: Record<string, unknown> }>();
    for (const n of nodes) map.set(n.id, { type: n.type as FlowNodeType, data: n.data as Record<string, unknown> });
    return map;
  }, [nodes]);

  // Apply ghost path edge styling + edge labels
  const styledEdges: Edge[] = useMemo(() => {
    return edges.map((edge) => {
      const sourceNode = nodeMap.get(edge.source);
      const label = resolveEdgeLabel(sourceNode?.type, edge.sourceHandle, sourceNode?.data);
      const isOnPath = ghostPathEnabled && ghostPathEdgeIds.has(edge.id);
      const ghostStyle = ghostPathEnabled
        ? isOnPath
          ? { stroke: '#a855f7', strokeWidth: 3 }
          : { stroke: '#94a3b8', strokeWidth: 2, opacity: 0.3 }
        : undefined;
      return {
        ...edge,
        ...(ghostStyle && { style: ghostStyle }),
        ...(ghostPathEnabled && { animated: isOnPath }),
        data: { ...((edge.data as Record<string, unknown>) ?? {}), edgeLabel: label },
      };
    });
  }, [edges, ghostPathEnabled, ghostPathEdgeIds, nodeMap]);

  const onInit = useCallback((instance: ReactFlowInstance) => {
    reactFlowInstance.current = instance;
  }, []);

  const onDragOver = useCallback((event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      event.preventDefault();

      const type = event.dataTransfer.getData('application/invekto-node-type') as FlowNodeType;
      if (!type || !reactFlowInstance.current || !reactFlowWrapper.current) return;

      const bounds = reactFlowWrapper.current.getBoundingClientRect();
      const position = reactFlowInstance.current.screenToFlowPosition({
        x: event.clientX - bounds.left,
        y: event.clientY - bounds.top,
      });

      addNode(type, position);
    },
    [addNode]
  );

  // Self-connection prevention
  const isValidConnection = useCallback((connection: Connection | { source: string; target: string }) => {
    return connection.source !== connection.target;
  }, []);

  const onNodeDragStop = useCallback(
    () => {
      // Push history so drag is undoable (Ctrl+Z)
      useFlowStore.getState().pushHistory();
    },
    []
  );

  const onPaneClick = useCallback(() => {
    selectNode(null);
  }, [selectNode]);

  const onKeyDown = useCallback(
    (event: React.KeyboardEvent) => {
      if (event.key === 'Delete' || event.key === 'Backspace') {
        const selectedNodeId = useFlowStore.getState().selectedNodeId;
        if (selectedNodeId) {
          // Don't delete trigger_start
          const node = useFlowStore.getState().nodes.find((n) => n.id === selectedNodeId);
          if (node?.type === 'trigger_start') return;
          deleteNode(selectedNodeId);
        }
      }
    },
    [deleteNode]
  );

  return (
    <div ref={reactFlowWrapper} className="flex-1 min-h-0" onKeyDown={onKeyDown} tabIndex={0}>
      <ReactFlow
        nodes={nodes}
        edges={styledEdges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        onInit={onInit}
        onDrop={onDrop}
        onDragOver={onDragOver}
        onPaneClick={onPaneClick}
        onNodeDragStop={onNodeDragStop}
        isValidConnection={isValidConnection}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        deleteKeyCode={['Delete', 'Backspace']}
        defaultEdgeOptions={{
          type: 'deletable',
          animated: true,
          style: { stroke: '#94a3b8', strokeWidth: 2 },
          interactionWidth: 20,
        }}
        connectionLineStyle={{ stroke: '#3b82f6', strokeWidth: 2 }}
        edgesReconnectable
        proOptions={{ hideAttribution: true }}
      >
        <Background
          variant={BackgroundVariant.Dots}
          gap={20}
          size={1}
          color="#cbd5e1"
        />
        <Controls
          showInteractive={false}
          position="bottom-right"
        />
        <AutoLayoutButton />
        {hasPendingConfig && pendingDiffSize > 0 && <DiffLegend />}
      </ReactFlow>
    </div>
  );
}

function DiffLegend() {
  return (
    <div className="absolute top-3 right-3 bg-white/90 backdrop-blur-sm border border-navy-100 rounded-lg px-3 py-2 text-xs space-y-1 z-10">
      <div className="flex items-center gap-2">
        <span className="w-3 h-3 rounded" style={{ boxShadow: '0 0 0 2px #4ade80' }} />
        <span className="text-navy-600">Eklendi</span>
      </div>
      <div className="flex items-center gap-2">
        <span className="w-3 h-3 rounded" style={{ boxShadow: '0 0 0 2px #fbbf24' }} />
        <span className="text-navy-600">Degisti</span>
      </div>
      <div className="flex items-center gap-2">
        <span className="w-3 h-3 rounded opacity-50" style={{ boxShadow: '0 0 0 2px #f87171' }} />
        <span className="text-navy-600">Silindi</span>
      </div>
    </div>
  );
}

function AutoLayoutButton() {
  const applyAutoLayout = useFlowStore((s) => s.applyAutoLayout);
  const nodeCount = useFlowStore((s) => s.nodes.length);

  if (nodeCount <= 1) return null;

  return (
    <div className="absolute bottom-[120px] right-2 z-10">
      <button
        onClick={applyAutoLayout}
        className="p-2 bg-white border border-navy-200 rounded-lg shadow-sm hover:bg-navy-50 transition-colors text-navy-500"
        title="Otomatik Yerlesim (Ctrl+Z ile geri alinabilir)"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
          <rect x="3" y="3" width="7" height="7" rx="1" />
          <rect x="14" y="3" width="7" height="7" rx="1" />
          <rect x="8" y="14" width="7" height="7" rx="1" />
          <line x1="6.5" y1="10" x2="6.5" y2="14" />
          <line x1="17.5" y1="10" x2="17.5" y2="14" />
        </svg>
      </button>
    </div>
  );
}
