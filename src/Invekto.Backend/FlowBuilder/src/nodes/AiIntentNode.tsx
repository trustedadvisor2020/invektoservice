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
    { id: 'high_confidence', label: <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M8 13V3M4 7l4-4 4 4" /></svg> },
    { id: 'low_confidence', label: <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="#f59e0b" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M8 3v10M4 9l4 4 4-4" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#8b5cf6"
      icon={<IntentIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      {intentCount > 0 ? (
        <span className="text-slate-600 text-xs">
          {intentCount} intent &middot; esik: {(threshold * 100).toFixed(0)}%
        </span>
      ) : (
        <span className="text-slate-500 italic text-xs">Intent tanimlanmadi</span>
      )}
    </BaseNode>
  );
}

export const AiIntentNode = memo(AiIntentNodeComponent);
