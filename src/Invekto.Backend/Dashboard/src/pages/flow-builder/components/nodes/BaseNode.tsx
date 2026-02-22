import { memo, type ReactNode } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { cn } from '../../../../lib/utils';
import { useFlowStore } from '../../../../stores/flow-store';
import { useSimulationStore } from '../../../../stores/simulation-store';
import { getValidationRingColor, getValidationTooltip, type ValidationError } from '../../../../lib/graph-validator';

// Stable reference to prevent Zustand re-renders when node has no errors
const EMPTY_ERRORS: ValidationError[] = [];

interface BaseNodeProps {
  nodeProps: NodeProps;
  color: string;
  icon: ReactNode;
  children?: ReactNode;
  outputs?: Array<{ id: string; label?: ReactNode }>;
  hasInput?: boolean;
  hasDefaultOutput?: boolean;
  outputPosition?: 'bottom' | 'right';
}

function BaseNodeComponent({
  nodeProps,
  color,
  icon,
  children,
  outputs,
  hasInput = true,
  hasDefaultOutput = true,
  outputPosition = 'bottom',
}: BaseNodeProps) {
  const { id, data, selected } = nodeProps;
  const selectNode = useFlowStore((s) => s.selectNode);
  const validationErrors = useFlowStore((s) => s.validationErrors.get(id)) ?? EMPTY_ERRORS;
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
        selected ? 'shadow-xl ring-2 ring-brand-400/50' : 'shadow-md',
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
        <span className="text-base font-medium truncate text-navy-700">
          {label}
        </span>
      </div>

      {/* Body */}
      {children && (
        <div className="px-3 py-2 text-sm text-navy-500">
          {children}
        </div>
      )}

      {/* Input handle - GREEN */}
      {hasInput && (
        <Handle
          type="target"
          position={Position.Top}
          className="!w-5 !h-5 !border-2 !bg-emerald-500 !border-emerald-300 hover:!bg-emerald-400 hover:!border-emerald-200 transition-colors"
        />
      )}

      {/* Default output handle - RED */}
      {hasDefaultOutput && !outputs && (
        <Handle
          type="source"
          position={Position.Bottom}
          className="!w-5 !h-5 !border-2 !bg-red-500 !border-red-300 hover:!bg-red-400 hover:!border-red-200 transition-colors"
        />
      )}

      {/* Multiple output handles - BOTTOM */}
      {outputs && outputs.length > 0 && outputPosition === 'bottom' && (
        <div className="relative pb-6">
          {outputs.map((output, idx) => {
            const total = outputs.length;
            const spread = total === 2 ? 50 : 80;
            const margin = total === 2 ? 25 : 10;
            const offset = total === 1 ? 50 : (idx / (total - 1)) * spread + margin;
            return (
              <div key={output.id}>
                <Handle
                  type="source"
                  position={Position.Bottom}
                  id={output.id}
                  className="!w-5 !h-5 !border-2 !bg-red-500 !border-red-300 hover:!bg-red-400 hover:!border-red-200 transition-colors"
                  style={{ left: `${offset}%`, bottom: '-2px' }}
                />
                {output.label && (
                  <span
                    className="absolute whitespace-nowrap"
                    style={{
                      left: `${offset}%`,
                      bottom: '12px',
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

      {/* Multiple output handles - RIGHT */}
      {outputs && outputs.length > 0 && outputPosition === 'right' && (
        <div className="border-t py-1" style={{ borderColor: `${color}30` }}>
          {outputs.map((output) => (
            <div key={output.id} className="relative h-7 flex items-center px-3">
              <span className="text-xs text-navy-600 font-medium">{output.label}</span>
              <Handle
                type="source"
                position={Position.Right}
                id={output.id}
                className="!w-5 !h-5 !border-2 !bg-red-500 !border-red-300 hover:!bg-red-400 hover:!border-red-200 transition-colors"
                style={{ right: '-10px' }}
              />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export const BaseNode = memo(BaseNodeComponent);
