import { memo, useMemo } from 'react';
import { useFlowStore } from '../store/flow-store';
import { summarizeFlow, type SummaryLine } from '../lib/flow-summarizer';

function getNodeTypeIcon(nodeType: string): string {
  switch (nodeType) {
    case 'trigger_start': return '\u25B6';
    case 'message_text': return '\uD83D\uDCAC';
    case 'message_menu': return '\uD83D\uDCCB';
    case 'action_handoff': return '\uD83D\uDC64';
    case 'ai_intent': return '\uD83E\uDDE0';
    case 'ai_faq': return '\uD83D\uDCD6';
    case 'logic_condition': return '\u2753';
    case 'logic_switch': return '\uD83D\uDD00';
    case 'action_api_call': return '\uD83C\uDF10';
    case 'action_delay': return '\u23F3';
    case 'utility_set_variable': return '\uD83D\uDCDD';
    default: return '\u2022';
  }
}

function getBranchColor(text: string): string {
  switch (text) {
    case 'YUKSEK':
    case 'DOGRU':
    case 'BASARILI':
    case 'ESLESTI':
      return 'text-emerald-600 font-semibold';
    case 'DUSUK':
    case 'YANLIS':
    case 'HATA':
    case 'ESLESMEDI':
      return 'text-red-500 font-semibold';
    case 'VARSAYILAN':
      return 'text-amber-600 font-semibold';
    default:
      return '';
  }
}

function SummaryLineItem({ line }: { line: SummaryLine }) {
  const paddingLeft = line.indent * 14;
  const branchColor = getBranchColor(line.text);

  return (
    <div
      className="flex items-start gap-1.5 text-xs leading-5 min-w-0"
      style={{ paddingLeft }}
    >
      <span className="flex-shrink-0 text-[10px] mt-0.5 opacity-60">
        {getNodeTypeIcon(line.nodeType)}
      </span>
      <span className={`break-words min-w-0 ${branchColor || 'text-navy-500'}`}>
        {line.text}
      </span>
    </div>
  );
}

interface FlowPreviewPanelProps {
  open: boolean;
}

function FlowPreviewPanelComponent({ open }: FlowPreviewPanelProps) {
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);

  const summary = useMemo(
    () => summarizeFlow(nodes, edges),
    [nodes, edges]
  );

  if (!open) return null;

  const hasContent = summary.lines.length > 0;

  return (
    <div className="w-60 bg-white border-l border-navy-100 flex-shrink-0 flex flex-col overflow-hidden">
      {/* Header */}
      <div className="px-3 py-2.5 border-b border-navy-100 flex items-center justify-between">
        <span className="text-xs font-semibold text-navy-700">Canli Onizleme</span>
        {hasContent && (
          <span className="text-[10px] text-navy-300 bg-navy-100 px-1.5 py-0.5 rounded-full">
            {summary.totalSteps} adim
          </span>
        )}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto px-3 py-2 space-y-0.5">
        {summary.hasErrors && (
          <div className="text-xs text-red-500 italic py-1">
            Baslangic adimi bulunamadi
          </div>
        )}

        {!hasContent && !summary.hasErrors && (
          <div className="text-xs text-navy-300 italic py-1">
            Henuz adim eklenmedi
          </div>
        )}

        {summary.lines.map((line, idx) => (
          <SummaryLineItem key={idx} line={line} />
        ))}
      </div>
    </div>
  );
}

export const FlowPreviewPanel = memo(FlowPreviewPanelComponent);
