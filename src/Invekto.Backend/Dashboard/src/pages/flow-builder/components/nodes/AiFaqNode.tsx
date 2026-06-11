import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { AiFaqData } from '../../../../types/flow';

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
  const minConf = data.min_confidence ?? 0.65;
  const searchSource = data.search_source ?? 'all';

  const outputs = [
    { id: 'matched', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 8.5l3.5 3.5L13 5" /></svg> },
    { id: 'no_match', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#ef4444" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M4 4l8 8M12 4l-8 8" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#8b5cf6"
      icon={<FaqIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <div className="flex flex-col gap-0.5">
        <span className="text-navy-500 text-xs">
          Min güven: {(minConf * 100).toFixed(0)}%
        </span>
        <span className="text-navy-400 text-[10px]">
          {searchSource === 'all' ? 'FAQ + Dökümanlar' : 'Sadece FAQ'}
        </span>
      </div>
    </BaseNode>
  );
}

export const AiFaqNode = memo(AiFaqNodeComponent);
