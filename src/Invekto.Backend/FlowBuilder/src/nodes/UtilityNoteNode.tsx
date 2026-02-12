import { memo } from 'react';
import { type NodeProps } from '@xyflow/react';
import type { UtilityNoteData } from '../types/flow';
import { useFlowStore } from '../store/flow-store';

function UtilityNoteNodeComponent(props: NodeProps) {
  const { id, data, selected } = props;
  const noteData = data as unknown as UtilityNoteData;
  const selectNode = useFlowStore((s) => s.selectNode);

  const bgColor = noteData.color || '#fef3c7';

  return (
    <div
      className="min-w-[160px] max-w-[280px] rounded-lg shadow-md transition-shadow"
      style={{
        background: bgColor,
        border: selected ? '2px solid #60a5fa' : '2px dashed #d4a574',
        boxShadow: selected ? '0 0 0 2px rgba(96,165,250,0.3)' : undefined,
      }}
      onClick={() => selectNode(id)}
    >
      {/* Header */}
      <div className="flex items-center gap-2 px-3 py-1.5 border-b border-amber-300/50">
        <svg viewBox="0 0 24 24" fill="none" stroke="#92400e" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4 flex-shrink-0">
          <line x1="18" y1="2" x2="22" y2="6" />
          <path d="M7.5 20.5 19 9l-4-4L3.5 16.5 2 22z" />
        </svg>
        <span className="text-xs font-medium text-amber-900 truncate">
          {noteData.label || 'Not'}
        </span>
      </div>

      {/* Body */}
      {noteData.text && (
        <div className="px-3 py-2 text-xs text-amber-800 whitespace-pre-wrap break-words">
          {noteData.text.length > 120 ? noteData.text.substring(0, 120) + '...' : noteData.text}
        </div>
      )}
      {!noteData.text && (
        <div className="px-3 py-2 text-xs text-amber-600/60 italic">
          Not metni girilmedi
        </div>
      )}
    </div>
  );
}

export const UtilityNoteNode = memo(UtilityNoteNodeComponent);
