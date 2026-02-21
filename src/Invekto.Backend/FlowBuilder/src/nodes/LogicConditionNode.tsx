import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { LogicConditionData } from '../types/flow';

const BranchIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <circle cx="18" cy="18" r="3" />
    <circle cx="6" cy="6" r="3" />
    <path d="M6 21V9a9 9 0 0 0 9 9" />
  </svg>
);

const OPERATOR_LABELS: Record<string, string> = {
  equals: '=',
  contains: '⊃',
  starts_with: '^',
  greater_than: '>',
  less_than: '<',
  is_empty: '∅',
  regex: '~',
};

function LogicConditionNodeComponent(props: NodeProps) {
  const data = props.data as LogicConditionData;
  const opLabel = OPERATOR_LABELS[data.operator] ?? data.operator;
  const hasCondition = data.variable && data.operator;

  const outputs = [
    { id: 'true_handle', label: <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 8.5l3.5 3.5L13 5" /></svg> },
    { id: 'false_handle', label: <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="#ef4444" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M4 4l8 8M12 4l-8 8" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#f59e0b"
      icon={<BranchIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      {hasCondition ? (
        <span className="text-slate-600 font-mono text-xs">
          {data.variable} {opLabel} {data.operator === 'is_empty' ? '' : data.value || '?'}
        </span>
      ) : (
        <span className="text-slate-500 italic">Kosul tanimlanmadi</span>
      )}
    </BaseNode>
  );
}

export const LogicConditionNode = memo(LogicConditionNodeComponent);
