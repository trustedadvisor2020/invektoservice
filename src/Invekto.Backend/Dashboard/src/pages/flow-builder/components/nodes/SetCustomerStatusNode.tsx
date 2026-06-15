import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { SetCustomerStatusData } from '../../../../types/flow';

// FEAT-INMA-PIPELINE-V2 C4: 'Set Customer Status' action node — writes the lead's INMA feature-group
// selection back via cxapi. Mirrors ActionApiCallNode (input + success/error output handles, no default
// output). The full group name + picked features live in the property panel (which has the catalog).
const TagIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M20.59 13.41 13.42 20.58a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z" />
    <line x1="7" y1="7" x2="7.01" y2="7" />
    <path d="m9 12 2 2 4-4" />
  </svg>
);

function SetCustomerStatusNodeComponent(props: NodeProps) {
  const data = props.data as SetCustomerStatusData;
  const fg = (data.feature_group_id ?? '').trim();
  const ids = (data.feature_ids ?? '').trim();
  const summary = fg === ''
    ? 'Grup seçilmedi'
    : (ids === '' ? `Grup #${fg} → temizle` : `Grup #${fg} → [${ids}]`);

  const outputs = [
    { id: 'success', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 8.5l3.5 3.5L13 5" /></svg> },
    { id: 'error', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#ef4444" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M8 3v6M8 12h.01" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#ef4444"
      icon={<TagIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <span className={fg === '' ? 'text-navy-400 italic text-xs' : 'text-navy-500 text-xs'}>
        {summary}
      </span>
    </BaseNode>
  );
}

export const SetCustomerStatusNode = memo(SetCustomerStatusNodeComponent);
