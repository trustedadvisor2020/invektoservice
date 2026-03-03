import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { ActionEcommerceData, EcommerceOperation } from '../../../../types/flow';

const EcommerceIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <circle cx="9" cy="21" r="1" />
    <circle cx="20" cy="21" r="1" />
    <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
  </svg>
);

const OP_LABELS: Record<EcommerceOperation, string> = {
  list_orders: 'Siparisler',
  get_order: 'Siparis Detay',
  list_products: 'Urunler',
  get_product: 'Urun Detay',
  list_customers: 'Musteriler',
  fulfill_order: 'Kargola',
  update_order_status: 'Durum Guncelle',
  refund_order_line: 'Iade',
};

function ActionEcommerceNodeComponent(props: NodeProps) {
  const data = props.data as ActionEcommerceData;
  const operation = data.operation || 'list_orders';

  const outputs = [
    { id: 'success', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#10b981" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M3 8.5l3.5 3.5L13 5" /></svg> },
    { id: 'error', label: <svg viewBox="0 0 16 16" width="20" height="20" fill="none" stroke="#ef4444" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M8 3v6M8 12h.01" /></svg> },
  ];

  return (
    <BaseNode
      nodeProps={props}
      color="#ef4444"
      icon={<EcommerceIcon />}
      hasDefaultOutput={false}
      outputs={outputs}
    >
      <span className="text-navy-500 text-xs">
        {OP_LABELS[operation] ?? operation}
      </span>
    </BaseNode>
  );
}

export const ActionEcommerceNode = memo(ActionEcommerceNodeComponent);
