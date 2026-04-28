import { useState, useEffect, useMemo, useCallback } from 'react';
import { api } from '../lib/api';
import type { KanbanBoard, KanbanCard, KanbanStatusValue } from '../lib/api';
import { usePolling } from '../hooks/usePolling';
import { KanbanDrawer } from '../components/KanbanDrawer';
import { RefreshCw, AlertCircle, Kanban } from 'lucide-react';
import { cn } from '../lib/utils';

/**
 * FEAT-PILOT-KANBAN — SuperAdmin Pilot Kanban Page.
 *
 * Q (Taner) "tum gidisat" izleme aracı. board_key='dent-pilot' kart`larini
 * 5 kolonda (BLOCKED/TODO/IN_PROGRESS/BACKLOG/DONE) gosterir. Read-only:
 * Q manuel kart edit yapmaz; mutation tek path /wrap workflow Step 3.5.
 *
 * Polling 60s, kart click -> KanbanDrawer (sagdan slide-in) ile tum detay.
 */
const BOARD_KEY = 'dent-pilot';

const COLUMNS: { id: KanbanStatusValue; label: string; icon: string; cls: string }[] = [
  { id: 'BLOCKED',     label: 'Blocked',     icon: '🔴', cls: 'border-red-200 bg-red-50/50' },
  { id: 'TODO',        label: 'Todo',        icon: '📋', cls: 'border-blue-200 bg-blue-50/50' },
  { id: 'IN_PROGRESS', label: 'In Progress', icon: '🚧', cls: 'border-amber-200 bg-amber-50/50' },
  { id: 'BACKLOG',     label: 'Backlog',     icon: '⏸',  cls: 'border-purple-200 bg-purple-50/50' },
  { id: 'DONE',        label: 'Done',        icon: '✅', cls: 'border-emerald-200 bg-emerald-50/50' },
];

const CATEGORY_BADGE: Record<string, string> = {
  CUSTOMER: 'bg-amber-100 text-amber-700',
  OPS:      'bg-blue-100 text-blue-700',
  DEV:      'bg-purple-100 text-purple-700',
  DECISION: 'bg-red-100 text-red-700',
  UI:       'bg-emerald-100 text-emerald-700',
  DOC:      'bg-slate-100 text-slate-600',
};

const CATEGORY_LABEL: Record<string, string> = {
  CUSTOMER: 'Müşteri',
  OPS:      'Ops',
  DEV:      'Dev',
  DECISION: 'Karar',
  UI:       'UI',
  DOC:      'Doküman',
};

export function PilotKanbanPage() {
  const [selectedCard, setSelectedCard] = useState<KanbanCard | null>(null);
  const [categoryFilter, setCategoryFilter] = useState<string | null>(null);

  const { data: board, isLoading, error, refresh } = usePolling<KanbanBoard>({
    fetcher: () => api.getKanbanBoard(BOARD_KEY),
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
      <div className="space-y-5">
        {/* Header */}
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900 flex items-center gap-2">
              <Kanban className="w-6 h-6 text-slate-600" />
              Pilot Kanban
            </h1>
            <p className="text-sm text-slate-500 mt-0.5">
              Dent Adavista pilot gidişatı. Read-only — güncellemeler <code className="text-slate-700 bg-slate-100 px-1.5 py-0.5 rounded text-[0.85em] font-mono">/wrap</code> workflow Step 3.5 üzerinden.
            </p>
          </div>
          <button
            type="button"
            onClick={refresh}
            disabled={isLoading}
            className="flex items-center gap-1.5 px-3 py-2 text-sm bg-white border border-slate-200 rounded-lg text-slate-600 hover:text-slate-900 hover:border-slate-300 transition-colors disabled:opacity-50"
            title="Yenile"
          >
            <RefreshCw className={cn('w-4 h-4', isLoading && 'animate-spin')} />
            Yenile
          </button>
        </div>

        {/* Stats bar */}
        <div className="flex flex-wrap gap-3 text-sm">
          <StatPill label="Toplam"      value={totals.total}      cls="bg-slate-100 text-slate-700" />
          <StatPill label="Blocked"      value={totals.blocked}    cls="bg-red-100 text-red-700" />
          <StatPill label="In Progress"  value={totals.inProgress} cls="bg-amber-100 text-amber-700" />
          <StatPill label="Done"         value={totals.done}       cls="bg-emerald-100 text-emerald-700" />
          {board && (
            <span className="ml-auto text-xs text-slate-400 self-center">
              Son güncelleme: {formatShortDate(board.updated_at)}
            </span>
          )}
        </div>

        {/* Category filter */}
        {categories.length > 0 && (
          <div className="flex flex-wrap gap-1.5 items-center">
            <span className="text-xs text-slate-500 mr-1">Kategori:</span>
            <button
              type="button"
              onClick={() => setCategoryFilter(null)}
              className={cn(
                'text-xs px-2.5 py-1 rounded-full border transition-colors',
                categoryFilter === null
                  ? 'bg-slate-900 text-white border-slate-900'
                  : 'bg-white text-slate-600 border-slate-200 hover:border-slate-400'
              )}
            >
              Hepsi
            </button>
            {categories.map(cat => (
              <button
                key={cat}
                type="button"
                onClick={() => setCategoryFilter(cat)}
                className={cn(
                  'text-xs px-2.5 py-1 rounded-full border transition-colors',
                  categoryFilter === cat
                    ? 'bg-slate-900 text-white border-slate-900'
                    : 'bg-white text-slate-600 border-slate-200 hover:border-slate-400'
                )}
              >
                {CATEGORY_LABEL[cat] ?? cat}
              </button>
            ))}
          </div>
        )}

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

        {/* Board */}
        {board && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-3">
            {COLUMNS.map(col => {
              const cards = grouped[col.id] ?? [];
              return (
                <div
                  key={col.id}
                  className={cn(
                    'border rounded-lg p-3 flex flex-col min-h-[200px]',
                    col.cls,
                  )}
                >
                  <div className="flex items-center justify-between mb-2.5 px-1">
                    <h3 className="text-xs font-bold uppercase tracking-wider text-slate-700 flex items-center gap-1.5">
                      <span>{col.icon}</span>{col.label}
                    </h3>
                    <span className="text-xs font-medium text-slate-500 bg-white border border-slate-200 px-2 py-0.5 rounded-full">
                      {cards.length}
                    </span>
                  </div>
                  <div className="space-y-2 flex-1">
                    {cards.length === 0 ? (
                      <div className="text-xs text-slate-400 italic px-1 py-3 text-center">
                        Boş
                      </div>
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
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'w-full text-left bg-white border border-slate-200 rounded-md p-2.5 transition-all',
        'hover:border-slate-400 hover:shadow-sm hover:-translate-y-px',
        'focus:outline-none focus:ring-2 focus:ring-slate-900/10',
        isP0 && 'border-l-2 border-l-red-500',
      )}
      title={card.summary ?? card.title}
    >
      <h4 className="text-sm font-medium text-slate-900 leading-snug line-clamp-2 mb-1.5">
        {card.title}
      </h4>
      <div className="flex items-center gap-1.5 flex-wrap">
        <span className={cn(
          'text-[10px] px-1.5 py-0.5 rounded font-medium',
          CATEGORY_BADGE[card.category] ?? 'bg-slate-100 text-slate-600',
        )}>
          {CATEGORY_LABEL[card.category] ?? card.category}
        </span>
        {card.priority !== 'P2' && (
          <span className={cn(
            'text-[10px] px-1.5 py-0.5 rounded font-bold',
            card.priority === 'P0' && 'bg-red-100 text-red-700',
            card.priority === 'P1' && 'bg-amber-100 text-amber-700',
            card.priority === 'P3' && 'bg-slate-100 text-slate-500',
          )}>
            {card.priority}
          </span>
        )}
        {card.eta && (
          <span className="text-[10px] text-slate-500 ml-auto">{card.eta}</span>
        )}
      </div>
      {card.owner && (
        <div className="text-[10px] text-slate-500 mt-1 truncate">
          {card.owner}
        </div>
      )}
    </button>
  );
}

function StatPill({ label, value, cls }: { label: string; value: number; cls: string }) {
  return (
    <span className={cn('inline-flex items-baseline gap-1.5 px-3 py-1 rounded-md font-medium', cls)}>
      <span className="text-base font-bold tabular-nums">{value}</span>
      <span className="text-xs uppercase tracking-wide opacity-75">{label}</span>
    </span>
  );
}

function formatShortDate(iso: string): string {
  try {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return iso;
    return d.toLocaleString('tr-TR', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' });
  } catch {
    return iso;
  }
}
