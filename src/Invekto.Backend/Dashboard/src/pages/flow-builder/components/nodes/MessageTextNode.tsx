import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { MessageTextData } from '../../../../types/flow';

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
      <span className={data.text ? 'text-navy-500' : 'text-navy-400 italic'}>
        {preview}
      </span>
      {data.wait_for_input && (
        <span className="mt-1 inline-flex items-center gap-1 text-[10px] text-blue-500 font-medium">
          <PauseIcon /> Yanit bekler
        </span>
      )}
    </BaseNode>
  );
}

const PauseIcon = () => (
  <svg viewBox="0 0 16 16" fill="currentColor" className="w-3 h-3">
    <path d="M5.5 3.5A1.5 1.5 0 0 1 7 5v6a1.5 1.5 0 0 1-3 0V5a1.5 1.5 0 0 1 1.5-1.5zm5 0A1.5 1.5 0 0 1 12 5v6a1.5 1.5 0 0 1-3 0V5a1.5 1.5 0 0 1 1.5-1.5z" />
  </svg>
);

export const MessageTextNode = memo(MessageTextNodeComponent);
