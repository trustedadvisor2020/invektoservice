import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { ActionDelayData } from '../types/flow';

const ClockIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <circle cx="12" cy="12" r="10" />
    <polyline points="12 6 12 12 16 14" />
  </svg>
);

function ActionDelayNodeComponent(props: NodeProps) {
  const data = props.data as ActionDelayData;
  const seconds = data.seconds ?? 5;

  return (
    <BaseNode
      nodeProps={props}
      color="#ef4444"
      icon={<ClockIcon />}
    >
      <span className="text-slate-300">
        {seconds} saniye
      </span>
    </BaseNode>
  );
}

export const ActionDelayNode = memo(ActionDelayNodeComponent);
