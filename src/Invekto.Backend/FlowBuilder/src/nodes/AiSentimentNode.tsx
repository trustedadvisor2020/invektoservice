import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { AiSentimentData } from '../types/flow';

const SentimentIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <circle cx="12" cy="12" r="10" />
    <path d="M8 14s1.5 2 4 2 4-2 4-2" />
    <line x1="9" y1="9" x2="9.01" y2="9" />
    <line x1="15" y1="9" x2="15.01" y2="9" />
  </svg>
);

function AiSentimentNodeComponent(props: NodeProps) {
  const data = props.data as AiSentimentData;
  const threshold = data.threshold ?? 0.5;

  const outputs = [
    { id: 'positive', label: <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="#10b981" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="8" cy="8" r="6" /><path d="M5.5 9.5s1 1.5 2.5 1.5 2.5-1.5 2.5-1.5" /><circle cx="6" cy="6.5" r="0.7" fill="#10b981" stroke="none" /><circle cx="10" cy="6.5" r="0.7" fill="#10b981" stroke="none" /></svg> },
    { id: 'negative', label: <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="#ef4444" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="8" cy="8" r="6" /><path d="M5.5 10.5s1-1.5 2.5-1.5 2.5 1.5 2.5 1.5" /><circle cx="6" cy="6.5" r="0.7" fill="#ef4444" stroke="none" /><circle cx="10" cy="6.5" r="0.7" fill="#ef4444" stroke="none" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#8b5cf6"
      icon={<SentimentIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <span className="text-slate-600 text-xs">
        Duygu esigi: {(threshold * 100).toFixed(0)}%
      </span>
    </BaseNode>
  );
}

export const AiSentimentNode = memo(AiSentimentNodeComponent);
