import { useCallback, useMemo, useRef } from 'react';
import {
  ReactFlow,
  Background,
  Controls,

  BackgroundVariant,
  type ReactFlowInstance,
  type Connection,
  type Edge,
  type Node,
} from '@xyflow/react';
import { useFlowStore } from '../store/flow-store';
import { nodeTypes } from '../nodes';
import { DeleteEdge } from './DeleteEdgeButton';
import type { FlowNodeType } from '../types/flow';

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

  // Apply ghost path edge styling
  const styledEdges: Edge[] = useMemo(() => {
    if (!ghostPathEnabled) return edges;
    return edges.map((edge) => {
      const isOnPath = ghostPathEdgeIds.has(edge.id);
      return {
        ...edge,
        style: isOnPath
          ? { stroke: '#a855f7', strokeWidth: 3 }
          : { stroke: '#94a3b8', strokeWidth: 2, opacity: 0.3 },
        animated: isOnPath,
      };
    });
  }, [edges, ghostPathEnabled, ghostPathEdgeIds]);

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
      </ReactFlow>
    </div>
  );
}
