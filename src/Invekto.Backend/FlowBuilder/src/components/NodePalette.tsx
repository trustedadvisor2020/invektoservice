import { type DragEvent, type ReactNode } from 'react';
import { NODE_TYPE_REGISTRY, type NodeCategory, type NodeTypeInfo, type FlowNodeType } from '../types/flow';
import { cn } from '../lib/utils';

const CATEGORY_LABELS: Record<NodeCategory, string> = {
  trigger: 'Tetikleyici',
  message: 'Mesaj',
  logic: 'Mantik',
  ai: 'Yapay Zeka',
  action: 'Aksiyon',
  utility: 'Araclar',
};

const CATEGORY_ORDER: NodeCategory[] = ['trigger', 'message', 'logic', 'ai', 'action', 'utility'];

// SVG icons matching each node component (same as flow canvas)
const NODE_ICONS: Record<FlowNodeType, (color: string) => ReactNode> = {
  // Triggers
  trigger_start: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <polygon points="5 3 19 12 5 21 5 3" />
    </svg>
  ),
  webhook_trigger: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <path d="M18 16.98h1a2 2 0 0 0 1.72-3.01L12 2 3.28 13.97A2 2 0 0 0 5 16.98h1" />
      <circle cx="12" cy="17" r="5" />
      <path d="M12 14v3" />
    </svg>
  ),
  outbound_trigger: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <path d="M22 2 11 13" />
      <path d="M22 2 15 22 11 13 2 9z" />
    </svg>
  ),
  schedule_trigger: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  ),

  // Messages
  message_text: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
    </svg>
  ),
  message_menu: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <line x1="8" y1="6" x2="21" y2="6" />
      <line x1="8" y1="12" x2="21" y2="12" />
      <line x1="8" y1="18" x2="21" y2="18" />
      <line x1="3" y1="6" x2="3.01" y2="6" />
      <line x1="3" y1="12" x2="3.01" y2="12" />
      <line x1="3" y1="18" x2="3.01" y2="18" />
    </svg>
  ),

  // Logic
  logic_condition: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <circle cx="18" cy="18" r="3" />
      <circle cx="6" cy="6" r="3" />
      <path d="M6 21V9a9 9 0 0 0 9 9" />
    </svg>
  ),
  logic_switch: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <polyline points="16 3 21 3 21 8" />
      <line x1="4" y1="20" x2="21" y2="3" />
      <polyline points="21 16 21 21 16 21" />
      <line x1="15" y1="15" x2="21" y2="21" />
      <line x1="4" y1="4" x2="9" y2="9" />
    </svg>
  ),

  // AI
  ai_intent: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <path d="M12 2a4 4 0 0 1 4 4c0 1.95-1.4 3.58-3.25 3.93" />
      <path d="M12 2a4 4 0 0 0-4 4c0 1.95 1.4 3.58 3.25 3.93" />
      <path d="M12 10v4" />
      <path d="M8 18h8" />
      <path d="M10 22h4" />
    </svg>
  ),
  ai_faq: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <circle cx="12" cy="12" r="10" />
      <path d="M9 9h.01" />
      <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" />
      <path d="M12 17h.01" />
    </svg>
  ),
  ai_sentiment: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <circle cx="12" cy="12" r="10" />
      <path d="M8 14s1.5 2 4 2 4-2 4-2" />
      <line x1="9" y1="9" x2="9.01" y2="9" />
      <line x1="15" y1="9" x2="15.01" y2="9" />
    </svg>
  ),

  // Actions
  action_handoff: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="8.5" cy="7" r="4" />
      <polyline points="17 11 19 13 23 9" />
    </svg>
  ),
  action_api_call: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
      <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
    </svg>
  ),
  action_delay: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  ),

  // Utility
  utility_note: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <line x1="18" y1="2" x2="22" y2="6" />
      <path d="M7.5 20.5 19 9l-4-4L3.5 16.5 2 22z" />
    </svg>
  ),
  set_variable: (c) => (
    <svg viewBox="0 0 24 24" fill="none" stroke={c} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
      <polyline points="16 18 22 12 16 6" />
      <polyline points="8 6 2 12 8 18" />
    </svg>
  ),
};

function groupByCategory(items: NodeTypeInfo[]): Map<NodeCategory, NodeTypeInfo[]> {
  const map = new Map<NodeCategory, NodeTypeInfo[]>();
  for (const cat of CATEGORY_ORDER) {
    const nodes = items.filter((n) => n.category === cat);
    if (nodes.length > 0) map.set(cat, nodes);
  }
  return map;
}

function PaletteItem({ info }: { info: NodeTypeInfo }) {
  const onDragStart = (event: DragEvent<HTMLDivElement>, nodeType: FlowNodeType) => {
    event.dataTransfer.setData('application/invekto-node-type', nodeType);
    event.dataTransfer.effectAllowed = 'move';
  };

  const iconFn = NODE_ICONS[info.type];

  return (
    <div
      draggable
      onDragStart={(e) => onDragStart(e, info.type)}
      className={cn(
        'flex items-center gap-2 px-3 py-2 rounded-md cursor-grab active:cursor-grabbing',
        'border border-transparent hover:border-navy-200 transition-colors',
        'bg-navy-50/60 hover:bg-navy-50'
      )}
    >
      <div className="flex-shrink-0">
        {iconFn ? iconFn(info.color) : (
          <div
            className="w-4 h-4 rounded-sm"
            style={{ backgroundColor: info.color }}
          />
        )}
      </div>
      <div className="min-w-0">
        <div className="text-xs font-medium text-navy-700 truncate">{info.label}</div>
        <div className="text-[10px] text-navy-300 truncate">{info.description}</div>
      </div>
    </div>
  );
}

export function NodePalette() {
  const grouped = groupByCategory(NODE_TYPE_REGISTRY);

  return (
    <div className="w-52 bg-white border-r border-navy-100 overflow-y-auto flex-shrink-0">
      <div className="p-3 border-b border-navy-100">
        <h2 className="text-xs font-semibold text-navy-400 uppercase tracking-wider">
          Node'lar
        </h2>
        <p className="text-[10px] text-navy-300 mt-0.5">Surukle birak</p>
      </div>

      <div className="p-2 space-y-3">
        {CATEGORY_ORDER.map((cat) => {
          const items = grouped.get(cat);
          if (!items) return null;
          return (
            <div key={cat}>
              <div className="text-[10px] font-semibold text-navy-300 uppercase tracking-wider px-1 mb-1">
                {CATEGORY_LABELS[cat]}
              </div>
              <div className="space-y-1">
                {items.map((info) => (
                  <PaletteItem key={info.type} info={info} />
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
