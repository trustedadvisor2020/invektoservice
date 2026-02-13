import { memo, type ReactNode } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { cn } from '../lib/utils';
import { useFlowStore } from '../store/flow-store';
import { useSimulationStore } from '../store/simulation-store';
import { getValidationRingColor, getValidationTooltip } from '../lib/graph-validator';

interface BaseNodeProps {
  nodeProps: NodeProps;
  color: string;
  icon: ReactNode;
  children?: ReactNode;
  outputs?: Array<{ id: string; label?: string }>;
  hasInput?: boolean;
  hasDefaultOutput?: boolean;
}

function BaseNodeComponent({
  nodeProps,
  color,
  icon,
  children,
  outputs,
  hasInput = true,
  hasDefaultOutput = true,
}: BaseNodeProps) {
  const { id, data, selected } = nodeProps;
  const selectNode = useFlowStore((s) => s.selectNode);
  const validationErrors = useFlowStore((s) => s.validationErrors.get(id) ?? []);
  const label = (data as { label?: string }).label ?? '';

  // Simulation active node highlight
  const simCurrentNodeId = useSimulationStore((s) => s.currentNodeId);
  const simIsOpen = useSimulationStore((s) => s.isOpen);
  const isSimActive = simIsOpen && simCurrentNodeId === id;

  // Ghost path dimming
  const ghostPathEnabled = useFlowStore((s) => s.ghostPathEnabled);
  const isOnGhostPath = useFlowStore((s) => s.ghostPathNodeIds.has(id));
  const isGhostDimmed = ghostPathEnabled && !isOnGhostPath;

  const ringColor = getValidationRingColor(validationErrors);
  const tooltipText = getValidationTooltip(validationErrors);

  // Simulation highlight takes priority over validation ring
  const resolvedBoxShadow = isSimActive
    ? '0 0 0 3px #10b98160, 0 0 12px #10b98140'
    : ringColor && !selected
      ? `0 0 0 3px ${ringColor}40, 0 0 8px ${ringColor}30`
      : undefined;

  return (
    <div
      className={cn(
        'min-w-[180px] max-w-[260px] rounded-lg border-2 shadow-lg transition-all',
        selected ? 'shadow-xl ring-2 ring-blue-400/50' : 'shadow-md',
        isSimActive && 'ring-2 ring-emerald-400/60'
      )}
      style={{
        borderColor: isSimActive ? '#10b981' : selected ? '#60a5fa' : color,
        background: '#ffffff',
        opacity: isGhostDimmed ? 0.3 : 1,
        ...(resolvedBoxShadow ? { boxShadow: resolvedBoxShadow } : {}),
      }}
      onClick={() => selectNode(id)}
      title={tooltipText || undefined}
    >
      {/* Header */}
      <div
        className="flex items-center gap-2 px-3 py-2 rounded-t-md"
        style={{ background: `${color}20` }}
      >
        <span className="flex-shrink-0 w-5 h-5" style={{ color }}>
          {icon}
        </span>
        <span className="text-base font-medium truncate text-slate-700">
          {label}
        </span>
      </div>

      {/* Body */}
      {children && (
        <div className="px-3 py-2 text-sm text-slate-500">
          {children}
        </div>
      )}

      {/* Input handle - BLUE */}
      {hasInput && (
        <Handle
          type="target"
          position={Position.Top}
          className="!w-3 !h-3 !border-2 !bg-blue-500 !border-blue-300 hover:!bg-blue-300 hover:!border-blue-100 transition-colors"
        />
      )}

      {/* Default output handle - GREEN */}
      {hasDefaultOutput && !outputs && (
        <Handle
          type="source"
          position={Position.Bottom}
          className="!w-3 !h-3 !border-2 !bg-emerald-500 !border-emerald-300 hover:!bg-emerald-300 hover:!border-emerald-100 transition-colors"
        />
      )}

      {/* Multiple output handles - GREEN */}
      {outputs && outputs.length > 0 && (
        <div className="relative pb-3">
          {outputs.map((output, idx) => {
            const total = outputs.length;
            const offset = total === 1 ? 50 : (idx / (total - 1)) * 80 + 10;
            return (
              <div key={output.id}>
                <Handle
                  type="source"
                  position={Position.Bottom}
                  id={output.id}
                  className="!w-3 !h-3 !border-2 !bg-emerald-500 !border-emerald-300 hover:!bg-emerald-300 hover:!border-emerald-100 transition-colors"
                  style={{ left: `${offset}%` }}
                />
                {output.label && (
                  <span
                    className="absolute text-[11px] text-slate-400 whitespace-nowrap"
                    style={{
                      left: `${offset}%`,
                      bottom: '2px',
                      transform: 'translateX(-50%)',
                    }}
                  >
                    {output.label}
                  </span>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export const BaseNode = memo(BaseNodeComponent);
