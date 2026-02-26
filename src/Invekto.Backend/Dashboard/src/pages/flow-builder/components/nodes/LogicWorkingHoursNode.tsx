import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';

const ClockIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <circle cx="12" cy="12" r="10" />
    <polyline points="12 6 12 12 16 14" />
  </svg>
);

function LogicWorkingHoursNodeComponent(props: NodeProps) {
  const outputs = [
    { id: 'within_hours', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 8.5l3.5 3.5L13 5" /></svg> },
    { id: 'outside_hours', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#ef4444" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M4 4l8 8M12 4l-8 8" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#f59e0b"
      icon={<ClockIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <span className="text-navy-400 italic text-xs">Tenant mesai ayarlarindan okur</span>
    </BaseNode>
  );
}

export const LogicWorkingHoursNode = memo(LogicWorkingHoursNodeComponent);
