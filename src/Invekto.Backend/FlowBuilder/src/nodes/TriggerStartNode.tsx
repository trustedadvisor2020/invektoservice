import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';

const PlayIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <polygon points="5 3 19 12 5 21 5 3" />
  </svg>
);

function TriggerStartNodeComponent(props: NodeProps) {
  return (
    <BaseNode
      nodeProps={props}
      color="#10b981"
      icon={<PlayIcon />}
      hasInput={false}
    >
      <span className="text-emerald-400/70">Musteri mesaj gonderince baslar</span>
    </BaseNode>
  );
}

export const TriggerStartNode = memo(TriggerStartNodeComponent);
