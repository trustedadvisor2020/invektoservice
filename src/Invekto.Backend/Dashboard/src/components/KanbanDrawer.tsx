import { useEffect } from 'react';
import { X, ExternalLink, Calendar, Clock, User, AlertCircle } from 'lucide-react';
import type { KanbanCard } from '../lib/api';
import { cn } from '../lib/utils';

/**
 * FEAT-PILOT-KANBAN drawer — sagdan slide-in panel ile bir kanban kart`inin
 * tum detayini inline gosterir. Read-only: status edit/save UI YOK (Q karari
 * 2026-04-28: kart guncellemeleri sadece /wrap workflow Step 3.5 uzerinden).
 *
 * Davranis:
 *   - ESC tusu veya backdrop click ile kapanir
 *   - body kaydirma kilidi acikken (overflow-hidden on body)
 *   - md+ ekranlarda 480px sag panel, mobilde full-screen
 *   - body_markdown minimal subset render edilir (XSS-safe escape + pattern)
 */

interface KanbanDrawerProps {
  card: KanbanCard | null;
  onClose: () => void;
}

const STATUS_COPY: Record<string, { label: string; cls: string }> = {
  BLOCKED:     { label: 'Blocked',     cls: 'bg-red-100 text-red-700 border-red-200' },
  TODO:        { label: 'Todo',        cls: 'bg-blue-100 text-blue-700 border-blue-200' },
  IN_PROGRESS: { label: 'In Progress', cls: 'bg-amber-100 text-amber-700 border-amber-200' },
  BACKLOG:     { label: 'Backlog',     cls: 'bg-purple-100 text-purple-700 border-purple-200' },
  DONE:        { label: 'Done',        cls: 'bg-emerald-100 text-emerald-700 border-emerald-200' },
};

const CATEGORY_COPY: Record<string, { label: string; cls: string }> = {
  CUSTOMER: { label: 'Müşteri',  cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  OPS:      { label: 'Ops',      cls: 'bg-blue-50 text-blue-700 border-blue-200' },
  DEV:      { label: 'Dev',      cls: 'bg-purple-50 text-purple-700 border-purple-200' },
  DECISION: { label: 'Karar',    cls: 'bg-red-50 text-red-700 border-red-200' },
  UI:       { label: 'UI',       cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  DOC:      { label: 'Doküman',  cls: 'bg-slate-100 text-slate-600 border-slate-200' },
};

const PRIORITY_COPY: Record<string, { label: string; cls: string }> = {
  P0: { label: 'P0 — Pilot Blocker',          cls: 'bg-red-100 text-red-700' },
  P1: { label: 'P1 — Stage 2-3 yetiştir',      cls: 'bg-amber-100 text-amber-700' },
  P2: { label: 'P2 — Standart',                cls: 'bg-slate-100 text-slate-600' },
  P3: { label: 'P3 — Nice-to-have',            cls: 'bg-slate-50 text-slate-500' },
};

export function KanbanDrawer({ card, onClose }: KanbanDrawerProps) {
  // ESC tusu + body scroll kilit (kart open while)
  useEffect(() => {
    if (!card) return;
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKey);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', handleKey);
      document.body.style.overflow = '';
    };
  }, [card, onClose]);

  if (!card) return null;

  const status = STATUS_COPY[card.status] ?? { label: card.status, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  const category = CATEGORY_COPY[card.category] ?? { label: card.category, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  const priority = PRIORITY_COPY[card.priority] ?? { label: card.priority, cls: 'bg-slate-100 text-slate-600' };

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-slate-900/40 z-40 transition-opacity"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Drawer */}
      <aside
        className={cn(
          'fixed top-0 right-0 h-screen z-50 bg-white shadow-2xl border-l border-slate-200',
          'w-full md:w-[480px] flex flex-col',
          'animate-[slideIn_.18s_ease-out]',
        )}
        role="dialog"
        aria-modal="true"
        aria-label={`Kart detayı: ${card.title}`}
      >
        {/* Header */}
        <div className="flex items-start justify-between px-6 py-4 border-b border-slate-100">
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap gap-1.5 mb-2">
              <span className={cn('text-xs px-2 py-0.5 rounded border font-semibold', status.cls)}>
                {status.label}
              </span>
              <span className={cn('text-xs px-2 py-0.5 rounded border', category.cls)}>
                {category.label}
              </span>
              <span className={cn('text-xs px-2 py-0.5 rounded font-semibold', priority.cls)}>
                {priority.label}
              </span>
            </div>
            <h2 className="text-lg font-semibold text-slate-900 leading-snug break-words">
              {card.title}
            </h2>
            <code className="text-[11px] text-slate-400 font-mono mt-1 block">
              {card.card_slug}
            </code>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="ml-3 p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors flex-shrink-0"
            title="Kapat (ESC)"
            aria-label="Kapat"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Scrollable body */}
        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-5">
          {/* Meta grid */}
          <dl className="grid grid-cols-2 gap-3 text-sm">
            {card.owner && (
              <div className="space-y-0.5">
                <dt className="text-xs font-medium text-slate-400 uppercase tracking-wide flex items-center gap-1">
                  <User className="w-3 h-3" /> Sorumlu
                </dt>
                <dd className="text-slate-700">{card.owner}</dd>
              </div>
            )}
            {card.eta && (
              <div className="space-y-0.5">
                <dt className="text-xs font-medium text-slate-400 uppercase tracking-wide flex items-center gap-1">
                  <Clock className="w-3 h-3" /> Süre
                </dt>
                <dd className="text-slate-700">{card.eta}</dd>
              </div>
            )}
            <div className="space-y-0.5">
              <dt className="text-xs font-medium text-slate-400 uppercase tracking-wide flex items-center gap-1">
                <Calendar className="w-3 h-3" /> Güncellendi
              </dt>
              <dd className="text-slate-700">{formatDate(card.updated_at)}</dd>
            </div>
            {card.completed_at && (
              <div className="space-y-0.5">
                <dt className="text-xs font-medium text-slate-400 uppercase tracking-wide flex items-center gap-1">
                  <Calendar className="w-3 h-3" /> Tamamlandı
                </dt>
                <dd className="text-emerald-700 font-medium">{formatDate(card.completed_at)}</dd>
              </div>
            )}
          </dl>

          {/* Summary */}
          {card.summary && (
            <div>
              <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5 flex items-center gap-1">
                <AlertCircle className="w-3 h-3" /> Özet
              </h3>
              <p className="text-sm text-slate-700 leading-relaxed">{card.summary}</p>
            </div>
          )}

          {/* Body markdown */}
          {card.body_markdown && (
            <div>
              <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">
                Detay
              </h3>
              <div className="text-sm text-slate-700 leading-relaxed space-y-2">
                <MarkdownLite text={card.body_markdown} />
              </div>
            </div>
          )}

          {/* Source link */}
          {card.source_file && (
            <div className="pt-2 border-t border-slate-100">
              <h3 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">
                Kaynak
              </h3>
              <a
                href={buildSourceHref(card.source_file, card.source_anchor)}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1.5 text-sm text-blue-600 hover:text-blue-700 hover:underline font-mono break-all"
              >
                <ExternalLink className="w-3.5 h-3.5 flex-shrink-0" />
                <span>{card.source_file}{card.source_anchor ? card.source_anchor : ''}</span>
              </a>
            </div>
          )}
        </div>

        {/* Footer hint */}
        <div className="px-6 py-3 border-t border-slate-100 bg-slate-50 text-[11px] text-slate-500">
          Kart durumu sadece <code className="text-slate-700 bg-white px-1.5 py-0.5 rounded border border-slate-200">/wrap</code> workflow Step 3.5 üzerinden güncellenir (read-only Dashboard).
        </div>
      </aside>

      {/* slide-in keyframe (Tailwind arbitrary support yok JIT keyframes icin — inline) */}
      <style>{`
        @keyframes slideIn {
          from { transform: translateX(100%); opacity: 0.6; }
          to   { transform: translateX(0);    opacity: 1; }
        }
      `}</style>
    </>
  );
}

// — Helpers —

function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return iso;
    return d.toLocaleString('tr-TR', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

function buildSourceHref(file: string, anchor: string | null): string {
  // source_file dent-pilot board icin "DentAdavista/plan/dent-golive.html" gibi
  // repo-relative path. Dashboard prod ortaminda bu dosya deploy edilmiyorsa
  // kullanici GitHub uzerinden de okuyabilir; pilot icin local file:// veya
  // VS Code workspace path acilir. Anchor varsa eklenir.
  const safeAnchor = anchor && anchor.startsWith('#') ? anchor : (anchor ? `#${anchor}` : '');
  return `/${file}${safeAnchor}`;
}

/**
 * MarkdownLite — XSS-safe minimal markdown render. SADECE migration 035 seed
 * body_markdown formatlari icin destekli pattern`lar:
 *   - Bold:    **text**
 *   - Italic:  *text* (iki yandan tek asterix)
 *   - Code:    `text`
 *   - Link:    [text](url)
 *   - Liste:   "- item" veya "1. item"
 *   - Heading: "**Header:**" satiri (bold prefix)
 *   - Newline ile paragraflar ayrilir.
 *
 * Cikan HTML her zaman escape edilir (no dangerouslySetInnerHTML iceriksiz);
 * pattern'lar React element olarak render edilir.
 */
function MarkdownLite({ text }: { text: string }) {
  const lines = text.split(/\n/);
  const blocks: React.ReactNode[] = [];
  let listBuffer: string[] = [];
  let listType: 'ul' | 'ol' | null = null;

  const flushList = (key: number) => {
    if (listBuffer.length === 0) return;
    const items = listBuffer.map((item, i) => (
      <li key={i} className="ml-4 list-disc">{renderInline(item)}</li>
    ));
    blocks.push(
      listType === 'ol'
        ? <ol key={`l-${key}`} className="space-y-1 list-decimal pl-4">{items}</ol>
        : <ul key={`l-${key}`} className="space-y-1">{items}</ul>
    );
    listBuffer = [];
    listType = null;
  };

  lines.forEach((rawLine, i) => {
    const line = rawLine.trim();
    if (!line) {
      flushList(i);
      return;
    }
    const ulMatch = line.match(/^[-*]\s+(.*)$/);
    const olMatch = line.match(/^\d+\.\s+(.*)$/);
    if (ulMatch) {
      if (listType !== 'ul') flushList(i);
      listType = 'ul';
      listBuffer.push(ulMatch[1]);
      return;
    }
    if (olMatch) {
      if (listType !== 'ol') flushList(i);
      listType = 'ol';
      listBuffer.push(olMatch[1]);
      return;
    }
    flushList(i);
    blocks.push(<p key={`p-${i}`} className="leading-relaxed">{renderInline(line)}</p>);
  });
  flushList(lines.length);

  return <>{blocks}</>;
}

/**
 * renderInline — bir text satiri icindeki **bold** / *italic* / `code` /
 * [link](url) pattern`larini React element`e cevirir. Tum text icerigi
 * React tarafindan otomatik HTML-escape edilir; URL'ler safe protokollere
 * (http/https) sinirlanmistir.
 */
function renderInline(text: string): React.ReactNode {
  // Token-based parse (XSS-safe — no innerHTML).
  const parts: React.ReactNode[] = [];
  let remaining = text;
  let key = 0;

  // Pattern oncelik sirasi: link > code > bold > italic
  const re = /(\[([^\]]+)\]\(([^)]+)\))|(`([^`]+)`)|(\*\*([^*]+)\*\*)|(\*([^*]+)\*)/;
  while (remaining.length > 0) {
    const match = re.exec(remaining);
    if (!match) {
      parts.push(remaining);
      break;
    }
    const before = remaining.slice(0, match.index);
    if (before) parts.push(before);

    if (match[1]) {
      // Link
      const label = match[2];
      const url = match[3];
      const safe = url.startsWith('http://') || url.startsWith('https://') || url.startsWith('/')
        ? url
        : '#';
      parts.push(
        <a
          key={`a-${key++}`}
          href={safe}
          target={safe.startsWith('/') ? undefined : '_blank'}
          rel="noopener noreferrer"
          className="text-blue-600 hover:underline"
        >{label}</a>
      );
    } else if (match[4]) {
      // Inline code
      parts.push(
        <code key={`c-${key++}`} className="bg-slate-100 text-slate-800 px-1 py-0.5 rounded text-[0.85em] font-mono">
          {match[5]}
        </code>
      );
    } else if (match[6]) {
      // Bold
      parts.push(
        <strong key={`b-${key++}`} className="font-semibold text-slate-900">{match[7]}</strong>
      );
    } else if (match[8]) {
      // Italic
      parts.push(<em key={`i-${key++}`} className="italic">{match[9]}</em>);
    }

    remaining = remaining.slice(match.index + match[0].length);
  }

  return <>{parts}</>;
}
