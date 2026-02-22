import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { AiIntentData } from '../types/flow';

const IntentIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M12 2a4 4 0 0 1 4 4c0 1.95-1.4 3.58-3.25 3.93" />
    <path d="M12 2a4 4 0 0 0-4 4c0 1.95 1.4 3.58 3.25 3.93" />
    <path d="M12 10v4" />
    <path d="M8 18h8" />
    <path d="M10 22h4" />
  </svg>
);

function AiIntentNodeComponent(props: NodeProps) {
  const data = props.data as AiIntentData;
  const intentCount = data.intents?.length ?? 0;
  const threshold = data.confidence_threshold ?? 0.5;

  const outputs = [
    { id: 'high_confidence', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M8 13V3M4 7l4-4 4 4" /></svg> },
    { id: 'low_confidence', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#f59e0b" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M8 3v10M4 9l4 4 4-4" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#8b5cf6"
      icon={<IntentIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <span className="text-navy-500 text-xs">
        {intentCount > 0 ? `${intentCount} intent` : <i className="text-navy-300">varsayilan</i>}
        {' '}&middot; esik: {(threshold * 100).toFixed(0)}%
      </span>
      {data.ask_name !== false && (
        <span className="text-purple-400 text-[10px] block mt-0.5">isim sorar &middot; sohbet eder</span>
      )}
    </BaseNode>
  );
}

export const AiIntentNode = memo(AiIntentNodeComponent);
