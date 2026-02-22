import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { ActionCallFlowData } from '../types/flow';

const SubFlowIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <rect x="2" y="3" width="8" height="8" rx="1.5" />
    <rect x="14" y="13" width="8" height="8" rx="1.5" />
    <path d="M10 7h4v6h-4" />
    <path d="M14 17h-4v-6h4" />
  </svg>
);

function CallFlowNodeComponent(props: NodeProps) {
  const data = props.data as ActionCallFlowData;
  const flowId = data.flow_id;

  const outputs = [
    { id: 'completed', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 8.5l3.5 3.5L13 5" /></svg> },
    { id: 'error', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#ef4444" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M8 3v6M8 12h.01" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#ef4444"
      icon={<SubFlowIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      {flowId ? (
        <span className="text-navy-500 text-xs">
          Flow #{flowId}
        </span>
      ) : (
        <span className="text-navy-400 italic text-xs">Flow seciniz</span>
      )}
    </BaseNode>
  );
}

export const CallFlowNode = memo(CallFlowNodeComponent);
