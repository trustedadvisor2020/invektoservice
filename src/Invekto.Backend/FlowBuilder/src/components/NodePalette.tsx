import { type DragEvent } from 'react';
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

  return (
    <div
      draggable
      onDragStart={(e) => onDragStart(e, info.type)}
      className={cn(
        'flex items-center gap-2 px-3 py-2 rounded-md cursor-grab active:cursor-grabbing',
        'border border-transparent hover:border-slate-300 transition-colors',
        'bg-slate-50 hover:bg-slate-100'
      )}
    >
      <div
        className="w-3 h-3 rounded-sm flex-shrink-0"
        style={{ backgroundColor: info.color }}
      />
      <div className="min-w-0">
        <div className="text-xs font-medium text-slate-700 truncate">{info.label}</div>
        <div className="text-[10px] text-slate-400 truncate">{info.description}</div>
      </div>
    </div>
  );
}

export function NodePalette() {
  const grouped = groupByCategory(NODE_TYPE_REGISTRY);

  return (
    <div className="w-52 bg-white border-r border-slate-200 overflow-y-auto flex-shrink-0">
      <div className="p-3 border-b border-slate-200">
        <h2 className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
          Node'lar
        </h2>
        <p className="text-[10px] text-slate-400 mt-0.5">Surukle birak</p>
      </div>

      <div className="p-2 space-y-3">
        {CATEGORY_ORDER.map((cat) => {
          const items = grouped.get(cat);
          if (!items) return null;
          return (
            <div key={cat}>
              <div className="text-[10px] font-semibold text-slate-400 uppercase tracking-wider px-1 mb-1">
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
