import { useState, useEffect, useMemo, useCallback } from 'react';
import { useParams } from 'react-router-dom';
import { api } from '../lib/api';
import type { KanbanBoard, KanbanCard, KanbanStatusValue } from '../lib/api';
import { usePolling } from '../hooks/usePolling';
import { KanbanDrawer } from '../components/KanbanDrawer';
import { RefreshCw, AlertCircle, Kanban } from 'lucide-react';
import { cn } from '../lib/utils';

/**
 * FEAT-ROADMAP-V2 — SuperAdmin Yol Haritası Page (eski FEAT-PILOT-KANBAN).
 *
 * Q (Taner) "tum gidisat" izleme aracı. URL param :boardKey ile multi-board
 * destegi (fallback 'dent-pilot'). 5 kolon (BLOCKED/TODO/IN_PROGRESS/BACKLOG/DONE).
 * Read-only — mutation tek path /wrap workflow Step 3.5.
 *
 * Polling 60s, kart click -> KanbanDrawer (sagdan slide-in) ile tum detay.
 * Her kart 4-karakter ref_code (C001/K005/D021) prominent gosterir.
 */
const DEFAULT_BOARD_KEY = 'dent-pilot';

const COLUMNS: { id: KanbanStatusValue; label: string; cls: string; dot: string }[] = [
  { id: 'BLOCKED',     label: 'Blocked',     cls: 'border-slate-200/70 bg-white',         dot: 'bg-red-300' },
  { id: 'TODO',        label: 'Todo',        cls: 'border-slate-200/70 bg-white',         dot: 'bg-blue-300' },
  { id: 'IN_PROGRESS', label: 'In Progress', cls: 'border-slate-200/70 bg-white',         dot: 'bg-amber-300' },
  { id: 'BACKLOG',     label: 'Backlog',     cls: 'border-slate-200/70 bg-white',         dot: 'bg-purple-300' },
  { id: 'DONE',        label: 'Done',        cls: 'border-slate-200/70 bg-white',         dot: 'bg-emerald-300' },
];

const CATEGORY_LABEL: Record<string, string> = {
  CUSTOMER: 'Müşteri',
  OPS:      'Ops',
  DEV:      'Dev',
  DECISION: 'Karar',
  UI:       'UI',
  DOC:      'Doküman',
};

export function PilotKanbanPage() {
  const { boardKey: urlBoardKey } = useParams<{ boardKey?: string }>();
  const boardKey = urlBoardKey || DEFAULT_BOARD_KEY;
  const [selectedCard, setSelectedCard] = useState<KanbanCard | null>(null);
  const [categoryFilter, setCategoryFilter] = useState<string | null>(null);

  const { data: board, isLoading, error, refresh } = usePolling<KanbanBoard>({
    fetcher: () => api.getKanbanBoard(boardKey),
    interval: 60000,
  });

  // Open drawer when card click; sync if board reloads (preserve selection by slug).
  useEffect(() => {
    if (!selectedCard || !board) return;
    const fresh = board.cards.find(c => c.card_slug === selectedCard.card_slug);
    if (fresh && fresh.updated_at !== selectedCard.updated_at) {
      setSelectedCard(fresh);
    }
  }, [board, selectedCard]);

  const grouped = useMemo(() => {
    const map: Record<KanbanStatusValue, KanbanCard[]> = {
      BLOCKED: [], TODO: [], IN_PROGRESS: [], BACKLOG: [], DONE: [],
    };
    if (!board) return map;
    for (const card of board.cards) {
      if (categoryFilter && card.category !== categoryFilter) continue;
      const bucket = map[card.status];
      if (bucket) bucket.push(card);
    }
    return map;
  }, [board, categoryFilter]);

  const totals = useMemo(() => {
    if (!board) return { total: 0, blocked: 0, inProgress: 0, done: 0 };
    return {
      total:      board.cards.length,
      blocked:    board.cards.filter(c => c.status === 'BLOCKED').length,
      inProgress: board.cards.filter(c => c.status === 'IN_PROGRESS').length,
      done:       board.cards.filter(c => c.status === 'DONE').length,
    };
  }, [board]);

  const categories = useMemo(() => {
    if (!board) return [] as string[];
    const set = new Set<string>();
    board.cards.forEach(c => set.add(c.category));
    return Array.from(set).sort();
  }, [board]);

  const handleCardClick = useCallback((card: KanbanCard) => {
    setSelectedCard(card);
  }, []);

  return (
    <>
      <div className="space-y-4">
        {/* Header — sade */}
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h1 className="text-xl font-medium text-slate-800 flex items-center gap-2">
              <Kanban className="w-5 h-5 text-slate-400" strokeWidth={1.5} />
              Yol Haritası
              <span className="text-xs text-slate-400 font-mono ml-1.5">{boardKey}</span>
            </h1>
          </div>
          <button
            type="button"
            onClick={refresh}
            disabled={isLoading}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs text-slate-500 hover:text-slate-700 transition-colors disabled:opacity-50"
            title="Yenile"
          >
            <RefreshCw className={cn('w-3.5 h-3.5', isLoading && 'animate-spin')} />
            Yenile
          </button>
        </div>

        {/* Stats + filter — tek satır, sade */}
        <div className="flex flex-wrap gap-x-4 gap-y-2 items-center text-xs text-slate-500 pb-3 border-b border-slate-100">
          <StatPill label="toplam"     value={totals.total} />
          <StatPill label="blocked"     value={totals.blocked}    accent="text-red-500" />
          <StatPill label="in progress" value={totals.inProgress} accent="text-amber-500" />
          <StatPill label="done"        value={totals.done}       accent="text-emerald-500" />
          {categories.length > 0 && (
            <div className="flex flex-wrap gap-1 items-center ml-auto">
              <button
                type="button"
                onClick={() => setCategoryFilter(null)}
                className={cn(
                  'text-[11px] px-2 py-0.5 rounded transition-colors',
                  categoryFilter === null
                    ? 'text-slate-700 bg-slate-100'
                    : 'text-slate-400 hover:text-slate-600',
                )}
              >
                hepsi
              </button>
              {categories.map(cat => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => setCategoryFilter(cat)}
                  className={cn(
                    'text-[11px] px-2 py-0.5 rounded transition-colors',
                    categoryFilter === cat
                      ? 'text-slate-700 bg-slate-100'
                      : 'text-slate-400 hover:text-slate-600',
                  )}
                >
                  {(CATEGORY_LABEL[cat] ?? cat).toLowerCase()}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Loading / Error */}
        {isLoading && !board && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-3">
            {COLUMNS.map(col => (
              <div key={col.id} className="bg-slate-50 border border-slate-200 rounded-lg p-3 h-64 animate-pulse" />
            ))}
          </div>
        )}

        {error && (
          <div className="flex items-start gap-2 p-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">
            <AlertCircle className="w-4 h-4 flex-shrink-0 mt-0.5" />
            <div>
              <div className="font-medium">Kanban yüklenemedi</div>
              <div className="text-xs text-red-600 mt-0.5">{String(error.message ?? error)}</div>
            </div>
            <button
              type="button"
              onClick={refresh}
              className="ml-auto text-xs px-2 py-1 bg-white border border-red-200 rounded hover:bg-red-50"
            >
              Tekrar dene
            </button>
          </div>
        )}

        {/* Board — light, sade kolonlar */}
        {board && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-3.5">
            {COLUMNS.map(col => {
              const cards = grouped[col.id] ?? [];
              return (
                <div key={col.id} className="flex flex-col min-h-[200px]">
                  <div className="flex items-center gap-2 mb-3 px-1">
                    <span className={cn('w-2 h-2 rounded-full', col.dot)} />
                    <h3 className="text-xs text-slate-600 lowercase tracking-wide flex-1 font-medium">
                      {col.label.toLowerCase()}
                    </h3>
                    <span className="text-xs text-slate-500 tabular-nums">
                      {cards.length}
                    </span>
                  </div>
                  <div className="space-y-2.5 flex-1">
                    {cards.length === 0 ? (
                      <div className="text-xs text-slate-300 px-1 py-3">—</div>
                    ) : (
                      cards.map(card => (
                        <CardItem key={card.id} card={card} onClick={() => handleCardClick(card)} />
                      ))
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      <KanbanDrawer card={selectedCard} onClose={() => setSelectedCard(null)} />
    </>
  );
}

// — Internal subcomponents —

function CardItem({ card, onClick }: { card: KanbanCard; onClick: () => void }) {
  const isP0 = card.priority === 'P0';
  const hasRefCode = card.ref_code && card.ref_code !== '----';
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'w-full text-left bg-white border border-slate-300 rounded-md px-3.5 py-3 transition-colors',
        'hover:border-slate-400 hover:bg-slate-50',
        'focus:outline-none focus:ring-1 focus:ring-slate-400',
      )}
      title={card.summary ?? card.title}
    >
      <div className="flex items-start gap-2.5">
        {hasRefCode && (
          <code className="text-[11px] font-mono text-slate-500 mt-0.5 flex-shrink-0 tabular-nums font-medium">
            {card.ref_code}
          </code>
        )}
        <h4 className="text-sm font-normal text-slate-800 leading-snug line-clamp-2 flex-1">
          {card.title}
        </h4>
        {isP0 && (
          <span
            className="w-2 h-2 rounded-full bg-red-500 flex-shrink-0 mt-1.5"
            title="Pilot blocker (P0)"
          />
        )}
      </div>
    </button>
  );
}

function StatPill({ label, value, accent }: { label: string; value: number; accent?: string }) {
  return (
    <span className="inline-flex items-baseline gap-1">
      <span className={cn('text-sm font-medium tabular-nums', accent ?? 'text-slate-700')}>{value}</span>
      <span className="text-[11px] text-slate-400 lowercase">{label}</span>
    </span>
  );
}
