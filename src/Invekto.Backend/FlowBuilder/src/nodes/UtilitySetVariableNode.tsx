import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { UtilitySetVariableData } from '../types/flow';

const VariableIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <polyline points="16 18 22 12 16 6" />
    <polyline points="8 6 2 12 8 18" />
  </svg>
);

function UtilitySetVariableNodeComponent(props: NodeProps) {
  const data = props.data as UtilitySetVariableData;
  const hasAssignment = data.variable_name && data.value_expression;

  return (
    <BaseNode
      nodeProps={props}
      color="#6b7280"
      icon={<VariableIcon />}
    >
      {hasAssignment ? (
        <span className="text-slate-300 font-mono text-xs">
          {data.variable_name} = {data.value_expression.length > 30
            ? data.value_expression.substring(0, 30) + '...'
            : data.value_expression}
        </span>
      ) : (
        <span className="text-slate-500 italic">Degisken atamasi tanimlanmadi</span>
      )}
    </BaseNode>
  );
}

export const UtilitySetVariableNode = memo(UtilitySetVariableNodeComponent);
