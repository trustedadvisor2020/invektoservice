import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { ActionApiCallData } from '../types/flow';

const ApiIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
    <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
  </svg>
);

function ActionApiCallNodeComponent(props: NodeProps) {
  const data = props.data as ActionApiCallData;
  const method = data.method || 'GET';
  const url = data.url || '';
  const truncatedUrl = url.length > 25 ? url.substring(0, 25) + '...' : url;

  const outputs = [
    { id: 'success', label: 'BASARILI' },
    { id: 'error', label: 'HATA' },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#ef4444"
      icon={<ApiIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      {url ? (
        <span className="text-slate-300 font-mono text-xs">
          {method} {truncatedUrl}
        </span>
      ) : (
        <span className="text-slate-500 italic text-xs">URL girilmedi</span>
      )}
    </BaseNode>
  );
}

export const ActionApiCallNode = memo(ActionApiCallNodeComponent);
