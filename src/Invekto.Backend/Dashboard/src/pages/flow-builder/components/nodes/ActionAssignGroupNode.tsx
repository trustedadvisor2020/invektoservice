import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { ActionAssignGroupData } from '../../../../types/flow';

const UsersIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
    <circle cx="9" cy="7" r="4" />
    <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
    <path d="M16 3.13a4 4 0 0 1 0 7.75" />
  </svg>
);

function ActionAssignGroupNodeComponent(props: NodeProps) {
  const data = props.data as ActionAssignGroupData;

  return (
    <BaseNode
      nodeProps={props}
      color="#ef4444"
      icon={<UsersIcon />}
      hasDefaultOutput={false}
    >
      <div className="space-y-1">
        <span className="text-red-400/70">Terminal - gruba yonlendirilir</span>
        {data.group_name && (
          <div className="text-navy-500 text-[10px] mt-1 truncate">
            Grup: {data.group_name}
          </div>
        )}
        {data.group_id && !data.group_name && (
          <div className="text-navy-400 text-[10px] font-mono truncate">
            ID: {data.group_id}
          </div>
        )}
      </div>
    </BaseNode>
  );
}

export const ActionAssignGroupNode = memo(ActionAssignGroupNodeComponent);
