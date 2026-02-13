import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { LogicSwitchData } from '../types/flow';

const SwitchIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <polyline points="16 3 21 3 21 8" />
    <line x1="4" y1="20" x2="21" y2="3" />
    <polyline points="21 16 21 21 16 21" />
    <line x1="15" y1="15" x2="21" y2="21" />
    <line x1="4" y1="4" x2="9" y2="9" />
  </svg>
);

function LogicSwitchNodeComponent(props: NodeProps) {
  const data = props.data as LogicSwitchData;
  const cases = data.cases ?? [];

  const outputs = [
    ...cases.map((c) => ({
      id: c.handle_id,
      label: c.value || c.handle_id,
    })),
    { id: data.default_handle_id || 'default', label: 'VARSAYILAN' },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#f59e0b"
      icon={<SwitchIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <div className="space-y-1">
        {!data.variable && cases.length === 0 && (
          <span className="text-slate-500 italic">Switch tanimlanmadi</span>
        )}
        {data.variable && (
          <span className="text-slate-400 text-xs font-mono">{data.variable}</span>
        )}
        {cases.length > 0 && (
          <div className="flex flex-wrap gap-1 mt-1">
            {cases.map((c) => (
              <span
                key={c.handle_id}
                className="px-1.5 py-0.5 rounded bg-amber-500/20 text-amber-400 text-xs"
              >
                {c.value || '?'}
              </span>
            ))}
          </div>
        )}
      </div>
    </BaseNode>
  );
}

export const LogicSwitchNode = memo(LogicSwitchNodeComponent);
