import {
  BaseEdge,
  EdgeLabelRenderer,
  getBezierPath,
  useReactFlow,
  type EdgeProps,
} from '@xyflow/react';

export function DeleteEdge({
  id,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  style = {},
  markerEnd,
  selected,
}: EdgeProps) {
  const { setEdges } = useReactFlow();
  const [edgePath, labelX, labelY] = getBezierPath({
    sourceX,
    sourceY,
    sourcePosition,
    targetX,
    targetY,
    targetPosition,
  });

  const onDelete = (event: React.MouseEvent) => {
    event.stopPropagation();
    setEdges((edges) => edges.filter((e) => e.id !== id));
  };

  return (
    <>
      <BaseEdge
        path={edgePath}
        markerEnd={markerEnd}
        style={{
          ...style,
          stroke: selected ? '#3b82f6' : '#94a3b8',
          strokeWidth: selected ? 3 : 2,
        }}
      />
      <EdgeLabelRenderer>
        <div
          className="nodrag nopan pointer-events-auto group"
          style={{
            position: 'absolute',
            transform: `translate(-50%, -50%) translate(${labelX}px,${labelY}px)`,
          }}
        >
          <button
            onClick={onDelete}
            className="w-5 h-5 rounded-full bg-white border border-slate-300 flex items-center justify-center opacity-0 group-hover:opacity-100 hover:bg-red-500 hover:border-red-400 transition-all duration-150 cursor-pointer shadow-sm"
            title="Baglantiyi Sil"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" className="w-2.5 h-2.5 text-slate-500 hover:text-white">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>
      </EdgeLabelRenderer>
    </>
  );
}
