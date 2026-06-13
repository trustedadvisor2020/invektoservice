import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { CustomerStatusChangedData } from '../../../../types/flow';

// FEAT-INMA-PIPELINE-V2 C3b: INMA customer_status (feature-group) change trigger.
// Source-only (hasInput=false) — mirrors WebhookTriggerNode. The canvas badge shows the
// matching scope; the full group NAME is shown in the property panel (which has the catalog).
const StatusChangeIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M20.59 13.41 13.42 20.58a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z" />
    <line x1="7" y1="7" x2="7.01" y2="7" />
  </svg>
);

function CustomerStatusTriggerNodeComponent(props: NodeProps) {
  const data = props.data as CustomerStatusChangedData;
  const fg = (data.feature_group_id ?? '').trim();
  const scope = fg === '' ? 'Tüm durumlar' : `Grup #${fg}`;

  return (
    <BaseNode
      nodeProps={props}
      color="#10b981"
      icon={<StatusChangeIcon />}
      hasInput={false}
    >
      <div className="flex items-center gap-1 text-xs">
        <span className="text-navy-400">{scope}</span>
      </div>
    </BaseNode>
  );
}

export const CustomerStatusTriggerNode = memo(CustomerStatusTriggerNodeComponent);
