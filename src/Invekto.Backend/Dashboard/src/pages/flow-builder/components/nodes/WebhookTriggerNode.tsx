import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import { BaseNode } from './BaseNode';
import type { WebhookTriggerData } from '../../../../types/flow';

const WebhookIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
    <path d="M18 16.98h1a2 2 0 0 0 1.72-3.01L12 2 3.28 13.97A2 2 0 0 0 5 16.98h1" />
    <circle cx="12" cy="17" r="5" />
    <path d="M12 14v3" />
  </svg>
);

function WebhookTriggerNodeComponent(props: NodeProps) {
  const data = props.data as WebhookTriggerData;
  const hasSecret = !!data.secret_key;

  return (
    <BaseNode
      nodeProps={props}
      color="#10b981"
      icon={<WebhookIcon />}
      hasInput={false}
    >
      <div className="flex items-center gap-1 text-xs">
        <span className="text-navy-400">POST webhook</span>
        {hasSecret && (
          <span className="text-emerald-500" title="HMAC doğrulama aktif">&#x1f512;</span>
        )}
      </div>
    </BaseNode>
  );
}

export const WebhookTriggerNode = memo(WebhookTriggerNodeComponent);
