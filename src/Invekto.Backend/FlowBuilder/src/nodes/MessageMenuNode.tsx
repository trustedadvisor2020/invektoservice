import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { MessageMenuData } from '../types/flow';

const ListIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <line x1="8" y1="6" x2="21" y2="6" />
    <line x1="8" y1="12" x2="21" y2="12" />
    <line x1="8" y1="18" x2="21" y2="18" />
    <line x1="3" y1="6" x2="3.01" y2="6" />
    <line x1="3" y1="12" x2="3.01" y2="12" />
    <line x1="3" y1="18" x2="3.01" y2="18" />
  </svg>
);

function MessageMenuNodeComponent(props: NodeProps) {
  const data = props.data as MessageMenuData;
  const options = data.options ?? [];

  const outputs = options.map((opt) => ({
    id: opt.handle_id,
    label: opt.key,
  }));

  return (
    <BaseNode
      nodeProps={props}
      color="#3b82f6"
      icon={<ListIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <div className="space-y-1">
        {options.length === 0 && (
          <span className="text-slate-400 italic">Secenek eklenmedi</span>
        )}
        {options.map((opt) => (
          <div key={opt.handle_id} className="flex items-center gap-1.5">
            <span className="w-5 h-5 rounded bg-blue-500/20 text-blue-400 flex items-center justify-center text-xs font-bold flex-shrink-0">
              {opt.key}
            </span>
            <span className="text-slate-600 truncate">{opt.label}</span>
          </div>
        ))}
      </div>
    </BaseNode>
  );
}

export const MessageMenuNode = memo(MessageMenuNodeComponent);
