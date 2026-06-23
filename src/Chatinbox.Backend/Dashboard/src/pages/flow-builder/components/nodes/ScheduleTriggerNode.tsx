import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { ScheduleTriggerData } from '../../../../types/flow';

const ScheduleIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <circle cx="12" cy="12" r="10" />
    <polyline points="12 6 12 12 16 14" />
  </svg>
);

function ScheduleTriggerNodeComponent(props: NodeProps) {
  const data = props.data as ScheduleTriggerData;
  const cron = data.cron_expression || '—';

  return (
    <BaseNode
      nodeProps={props}
      color="#10b981"
      icon={<ScheduleIcon />}
      hasInput={false}
    >
      <span className="text-navy-400 text-xs font-mono">{cron}</span>
    </BaseNode>
  );
}

export const ScheduleTriggerNode = memo(ScheduleTriggerNodeComponent);
