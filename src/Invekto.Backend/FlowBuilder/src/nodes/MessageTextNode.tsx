import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { MessageTextData } from '../types/flow';

const MessageIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
  </svg>
);

function MessageTextNodeComponent(props: NodeProps) {
  const data = props.data as MessageTextData;
  const preview = data.text
    ? data.text.length > 60 ? data.text.substring(0, 60) + '...' : data.text
    : 'Mesaj metni girilmedi';

  return (
    <BaseNode
      nodeProps={props}
      color="#3b82f6"
      icon={<MessageIcon />}
    >
      <span className={data.text ? 'text-slate-300' : 'text-slate-500 italic'}>
        {preview}
      </span>
    </BaseNode>
  );
}

export const MessageTextNode = memo(MessageTextNodeComponent);
