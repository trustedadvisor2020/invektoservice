import { memo, useMemo, useState, useEffect } from 'react';
import { useFlowStore } from '../store/flow-store';
import { summarizeFlow, truncateSummary, type SummaryLine } from '../lib/flow-summarizer';

const COLLAPSE_KEY = 'invekto_flow_summary_collapsed';

function getNodeTypeIcon(nodeType: string): string {
  switch (nodeType) {
    case 'trigger_start': return '\u25B6';
    case 'message_text': return '\uD83D\uDCAC';
    case 'message_menu': return '\uD83D\uDCCB';
    case 'action_handoff': return '\uD83D\uDC64';
    default: return '\u2022';
  }
}

function SummaryLineItem({ line }: { line: SummaryLine }) {
  const paddingLeft = line.indent * 16;
  return (
    <div
      className="flex items-center gap-1.5 text-xs text-slate-600 leading-5"
      style={{ paddingLeft }}
    >
      <span className="flex-shrink-0 text-[10px]">
        {getNodeTypeIcon(line.nodeType)}
      </span>
      <span className="truncate">{line.text}</span>
    </div>
  );
}

function FlowSummaryBarComponent() {
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);

  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(COLLAPSE_KEY) === 'true';
    } catch (err) {
      console.warn('FlowSummaryBar: localStorage okunamadi', err);
      return false;
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem(COLLAPSE_KEY, String(collapsed));
    } catch (err) {
      console.warn('FlowSummaryBar: localStorage yazilamadi', err);
    }
  }, [collapsed]);

  const summary = useMemo(
    () => summarizeFlow(nodes, edges),
    [nodes, edges]
  );

  const { displayLines, truncatedCount } = useMemo(
    () => truncateSummary(summary),
    [summary]
  );

  const hasContent = summary.lines.length > 0;

  return (
    <div className="border-t border-slate-200 bg-white flex-shrink-0">
      {/* Toggle header */}
      <button
        onClick={() => setCollapsed((v) => !v)}
        className="w-full flex items-center justify-between px-4 py-1.5 text-xs font-medium text-slate-500 hover:bg-slate-50 transition-colors"
      >
        <div className="flex items-center gap-2">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            className={`w-3 h-3 transition-transform ${collapsed ? '' : 'rotate-180'}`}
          >
            <polyline points="6 9 12 15 18 9" />
          </svg>
          <span>Canli Onizleme</span>
          {hasContent && (
            <span className="text-slate-400">({summary.totalSteps} adim)</span>
          )}
        </div>
        {summary.hasErrors && (
          <span className="text-red-500 text-[10px]">Baslangic bulunamadi</span>
        )}
      </button>

      {/* Collapsible content */}
      {!collapsed && (
        <div className="px-4 pb-2 space-y-0.5">
          {!hasContent && !summary.hasErrors && (
            <div className="text-xs text-slate-400 italic py-1">
              Henuz adim eklenmedi
            </div>
          )}
          {displayLines.map((line, idx) => (
            <SummaryLineItem key={idx} line={line} />
          ))}
          {truncatedCount > 0 && (
            <div className="text-xs text-slate-400 italic mt-1">
              ... ve {truncatedCount} adim daha
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export const FlowSummaryBar = memo(FlowSummaryBarComponent);
