import { useMemo, useCallback } from 'react';
import { ReactFlow, Background, type Node, type Edge } from '@xyflow/react';
import { useWizardStore } from '../../../stores/wizard-store';
import { autoLayoutNodes } from '../../../lib/auto-layout';
import { computeFlowDiff, getDiffClassName } from '../../../lib/flow-diff';
import { NODE_COLORS } from './NodeChip';
import type { FlowPrerequisite } from '../../../types/wizard';

export function WizardPreview() {
  const currentFlowPreview = useWizardStore(s => s.currentFlowPreview);
  const previousFlowPreview = useWizardStore(s => s.previousFlowPreview);
  const prerequisites = useWizardStore(s => s.prerequisites);

  const { nodes, edges, diffs } = useMemo(() => {
    if (!currentFlowPreview || !currentFlowPreview.nodes?.length) {
      return { nodes: [] as Node[], edges: [] as Edge[], diffs: new Map() };
    }

    const diffMap = computeFlowDiff(previousFlowPreview, currentFlowPreview);

    let flowNodes: Node[] = currentFlowPreview.nodes.map(n => ({
      id: n.id,
      type: 'default',
      position: n.position || { x: 0, y: 0 },
      data: {
        label: n.data?.label || n.type,
      },
      className: getDiffClassName(diffMap.get(n.id)?.status || 'unchanged'),
      style: getNodeStyle(n.type),
    }));

    // Add removed nodes (from previous config) with removed style
    if (previousFlowPreview) {
      const currentIds = new Set(currentFlowPreview.nodes.map(n => n.id));
      for (const oldNode of previousFlowPreview.nodes) {
        if (!currentIds.has(oldNode.id)) {
          flowNodes.push({
            id: oldNode.id,
            type: 'default',
            position: oldNode.position || { x: 0, y: 0 },
            data: { label: `${oldNode.data?.label || oldNode.type} (silindi)` },
            className: getDiffClassName('removed'),
            style: { ...getNodeStyle(oldNode.type), opacity: 0.4 },
          });
        }
      }
    }

    const flowEdges: Edge[] = currentFlowPreview.edges.map(e => ({
      id: e.id,
      source: e.source,
      target: e.target,
      sourceHandle: e.sourceHandle || undefined,
      targetHandle: e.targetHandle || undefined,
      animated: true,
      style: { stroke: '#94a3b8', strokeWidth: 1.5 },
    }));

    // Auto-layout if positions are all zero
    const allZero = flowNodes.every(n => n.position.x === 0 && n.position.y === 0);
    if (allZero && flowNodes.length > 0) {
      flowNodes = autoLayoutNodes(flowNodes, flowEdges);
    }

    return { nodes: flowNodes, edges: flowEdges, diffs: diffMap };
  }, [currentFlowPreview, previousFlowPreview]);

  const hasContent = nodes.length > 0;
  const hasDiffs = useMemo(() =>
    Array.from(diffs.values()).some(d => d.status !== 'unchanged'),
    [diffs]
  );

  const proOptions = useMemo(() => ({ hideAttribution: true }), []);
  const onInit = useCallback((instance: { fitView: () => void }) => {
    setTimeout(() => instance.fitView(), 100);
  }, []);

  return (
    <div className="flex flex-col h-full">
      {/* Preview canvas */}
      <div className="flex-1 bg-navy-25 relative">
        {!hasContent ? (
          <div className="flex flex-col items-center justify-center h-full text-center px-6">
            <div className="w-12 h-12 rounded-xl bg-navy-100 flex items-center justify-center mb-3">
              <svg className="w-6 h-6 text-navy-300" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6A2.25 2.25 0 016 3.75h2.25A2.25 2.25 0 0110.5 6v2.25a2.25 2.25 0 01-2.25 2.25H6a2.25 2.25 0 01-2.25-2.25V6zM3.75 15.75A2.25 2.25 0 016 13.5h2.25a2.25 2.25 0 012.25 2.25V18a2.25 2.25 0 01-2.25 2.25H6A2.25 2.25 0 013.75 18v-2.25zM13.5 6a2.25 2.25 0 012.25-2.25H18A2.25 2.25 0 0120.25 6v2.25A2.25 2.25 0 0118 10.5h-2.25a2.25 2.25 0 01-2.25-2.25V6z" />
              </svg>
            </div>
            <p className="text-sm text-navy-400">AI akis yapisi olusturdugunda burada on izleme gorunecek</p>
          </div>
        ) : (
          <ReactFlow
            nodes={nodes}
            edges={edges}
            nodesDraggable={false}
            nodesConnectable={false}
            elementsSelectable={false}
            panOnDrag
            zoomOnScroll
            fitView
            proOptions={proOptions}
            onInit={onInit}
          >
            <Background gap={16} size={1} color="#e2e8f0" />
          </ReactFlow>
        )}

        {/* Diff legend */}
        {hasDiffs && (
          <div className="absolute top-3 right-3 bg-white/90 backdrop-blur-sm border border-navy-100 rounded-lg px-3 py-2 text-xs space-y-1">
            <div className="flex items-center gap-2">
              <span className="w-3 h-3 rounded ring-2 ring-green-400" />
              <span className="text-navy-600">Eklendi</span>
            </div>
            <div className="flex items-center gap-2">
              <span className="w-3 h-3 rounded ring-2 ring-amber-400" />
              <span className="text-navy-600">Degisti</span>
            </div>
            <div className="flex items-center gap-2">
              <span className="w-3 h-3 rounded ring-2 ring-red-400 opacity-50" />
              <span className="text-navy-600">Silindi</span>
            </div>
          </div>
        )}
      </div>

      {/* Prerequisites */}
      {prerequisites && prerequisites.length > 0 && (
        <div className="border-t border-navy-100 p-3 bg-amber-50 max-h-48 overflow-y-auto">
          <h4 className="text-xs font-semibold text-amber-800 mb-2 flex items-center gap-1.5">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126z" />
            </svg>
            Akisin calismasi icin yapilmasi gerekenler
          </h4>
          <div className="space-y-2">
            {prerequisites.map((p, i) => (
              <PrerequisiteItem key={i} item={p} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function PrerequisiteItem({ item }: { item: FlowPrerequisite }) {
  const iconColor = item.type === 'action_required' ? 'text-red-500' : item.type === 'integration' ? 'text-blue-500' : 'text-amber-500';

  return (
    <div className="flex gap-2 text-xs">
      <svg className={`w-4 h-4 flex-shrink-0 mt-0.5 ${iconColor}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M11.25 11.25l.041-.02a.75.75 0 011.063.852l-.708 2.836a.75.75 0 001.063.853l.041-.021M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9-3.75h.008v.008H12V8.25z" />
      </svg>
      <div>
        <span className="font-medium text-navy-800">{item.title}</span>
        <span className="block text-navy-500 mt-0.5">{item.description}</span>
      </div>
    </div>
  );
}

function getNodeStyle(type: string): React.CSSProperties {
  const color = NODE_COLORS[type] || '#6b7280';
  return {
    background: 'white',
    border: `2px solid ${color}`,
    borderRadius: '12px',
    padding: '8px 16px',
    fontSize: '12px',
    minWidth: '120px',
    textAlign: 'center' as const,
  };
}
