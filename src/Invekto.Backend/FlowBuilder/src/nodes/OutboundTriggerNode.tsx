import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';

const OutboundIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M22 2 11 13" />
    <path d="M22 2 15 22 11 13 2 9z" />
  </svg>
);

function OutboundTriggerNodeComponent(props: NodeProps) {
  return (
    <BaseNode
      nodeProps={props}
      color="#10b981"
      icon={<OutboundIcon />}
      hasInput={false}
    >
      <span className="text-slate-500 text-xs">Outbound kampanya tetikleyici</span>
    </BaseNode>
  );
}

export const OutboundTriggerNode = memo(OutboundTriggerNodeComponent);
