import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { ActionHandoffData } from '../types/flow';

const UserCheckIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
    <circle cx="8.5" cy="7" r="4" />
    <polyline points="17 11 19 13 23 9" />
  </svg>
);

function ActionHandoffNodeComponent(props: NodeProps) {
  const data = props.data as ActionHandoffData;

  return (
    <BaseNode
      nodeProps={props}
      color="#ef4444"
      icon={<UserCheckIcon />}
      hasDefaultOutput={false}
    >
      <div className="space-y-1">
        <span className="text-red-400/70">Terminal - session biter</span>
        {data.summary_template && (
          <div className="text-slate-400 text-[10px] mt-1 truncate">
            Ozet: {data.summary_template}
          </div>
        )}
      </div>
    </BaseNode>
  );
}

export const ActionHandoffNode = memo(ActionHandoffNodeComponent);
