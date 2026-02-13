import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { AiFaqData } from '../types/flow';

const FaqIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <circle cx="12" cy="12" r="10" />
    <path d="M9 9h.01" />
    <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" />
    <path d="M12 17h.01" />
  </svg>
);

function AiFaqNodeComponent(props: NodeProps) {
  const data = props.data as AiFaqData;
  const minConf = data.min_confidence ?? 0.3;

  const outputs = [
    { id: 'matched', label: 'ESLESTI' },
    { id: 'no_match', label: 'ESLESMEDI' },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#8b5cf6"
      icon={<FaqIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <span className="text-slate-300 text-xs">
        Min guven: {(minConf * 100).toFixed(0)}%
      </span>
    </BaseNode>
  );
}

export const AiFaqNode = memo(AiFaqNodeComponent);
