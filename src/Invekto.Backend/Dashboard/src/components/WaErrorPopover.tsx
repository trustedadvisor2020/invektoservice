// Rich, click-to-pin Turkish explanation for a WhatsApp/Meta send error.
//
// The Outbound report 'Hata' column (and the test/bulk-send result banners)
// carry cxapi's RAW English provider text with the numeric Meta code embedded
// as "(NNNNN)". This component resolves that to a curated Turkish "neden + ne
// yapmalı" card. Click the trigger to pin the card; X / outside-click / Esc /
// scroll closes it. The card is portalled to <body> so it escapes the table's
// overflow clipping (same pattern as the row-content hover preview).
//
// Degradation: text with no recognizable code (e.g. our own Turkish "numara
// filtrelendi" reasons) renders as plain red text — no empty popover.
import { useEffect, useRef, useState, type CSSProperties } from 'react';
import { createPortal } from 'react-dom';
import { X, ExternalLink } from 'lucide-react';
import { resolveWaError, WA_SEVERITY_META, WA_DOC_URL } from '../lib/wa-error-codes';

interface WaErrorPopoverProps {
  raw: string | null | undefined;
  /** Extra classes for the inline trigger (e.g. cell width constraints). */
  className?: string;
}

export function WaErrorPopover({ raw, className = '' }: WaErrorPopoverProps) {
  const { info, code, rawText } = resolveWaError(raw);
  const [open, setOpen] = useState(false);
  const [rect, setRect] = useState<DOMRect | null>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const popRef = useRef<HTMLDivElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);

  // Close on Esc, outside-click and scroll/resize (a pinned card must not drift
  // away from its row while the report table scrolls).
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
    const onDown = (e: MouseEvent) => {
      const t = e.target as Node;
      if (popRef.current?.contains(t) || triggerRef.current?.contains(t)) return;
      setOpen(false);
    };
    const onScroll = () => setOpen(false);
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onDown);
    window.addEventListener('scroll', onScroll, true);
    window.addEventListener('resize', onScroll);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onDown);
      window.removeEventListener('scroll', onScroll, true);
      window.removeEventListener('resize', onScroll);
    };
  }, [open]);

  // Move focus into the card on open; restore to the trigger when it closes (a11y).
  useEffect(() => {
    if (!open) return;
    const trigger = triggerRef.current;
    closeRef.current?.focus();
    return () => { trigger?.focus(); };
  }, [open]);

  if (!rawText) return <span className="text-navy-300">—</span>;

  // No Meta code in the text → it is already an operator-facing Turkish reason.
  if (!info) {
    return <span className={`text-red-600 ${className}`} title={rawText}>{rawText}</span>;
  }

  const sev = WA_SEVERITY_META[info.severity];

  const toggle = () => {
    if (open) { setOpen(false); return; }
    setRect(triggerRef.current?.getBoundingClientRect() ?? null);
    setOpen(true);
  };

  // Height-aware placement: pin the card to whichever side of the trigger has more
  // room and CAP its height to that room, so a long card never spills off-screen
  // (horizontal is clamped too). Replaces a midpoint-only flip that could overflow.
  const POPOVER_W = 360;
  const GAP = 6;  // trigger ↔ card gap
  const EDGE = 8; // min viewport margin kept on every side
  let style: CSSProperties | undefined;
  if (rect) {
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    // Width shrinks on narrow viewports so the card never exceeds the screen width.
    const width = Math.min(POPOVER_W, vw - 2 * EDGE);
    const spaceBelow = vh - rect.bottom - GAP - EDGE;
    const spaceAbove = rect.top - GAP - EDGE;
    const below = spaceBelow >= spaceAbove;
    style = {
      position: 'fixed',
      width,
      left: Math.max(EDGE, Math.min(rect.left, vw - width - EDGE)),
      // STRICT cap to the chosen side's room (no min floor) so the card — with the
      // GAP/EDGE the anchor leaves — can never extend past the viewport edge.
      maxHeight: Math.max(0, Math.floor(below ? spaceBelow : spaceAbove)),
      ...(below ? { top: rect.bottom + GAP } : { bottom: vh - rect.top + GAP }),
    };
  }

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        onClick={toggle}
        aria-haspopup="dialog"
        aria-expanded={open}
        title="Hata açıklamasını göster"
        className={`inline-flex items-center gap-1.5 max-w-full text-left text-red-600 hover:text-red-700 ${className}`}
      >
        <span className={`shrink-0 inline-flex items-center gap-1 rounded px-1 py-0.5 text-[11px] font-semibold tabular-nums border ${sev.pill}`}>
          <span className={`w-1.5 h-1.5 rounded-full ${sev.dot}`} />
          {code}
        </span>
        <span className="truncate border-b border-dotted border-red-300">{info.title}</span>
      </button>

      {open && createPortal(
        <div
          ref={popRef}
          role="dialog"
          aria-label={`WhatsApp hata ${code}: ${info.title}`}
          style={style}
          className="z-[70] flex flex-col overflow-hidden rounded-xl bg-white border border-navy-100 shadow-soft text-left"
        >
          <div className="shrink-0 flex items-start gap-2 px-3.5 py-2.5 border-b border-navy-50">
            <span className={`shrink-0 inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium border ${sev.pill}`}>
              <span className={`w-1.5 h-1.5 rounded-full ${sev.dot}`} />
              {sev.label}
            </span>
            <span className="text-[11px] font-semibold tabular-nums text-navy-400 mt-0.5">#{code}</span>
            <button
              ref={closeRef}
              type="button"
              onClick={() => setOpen(false)}
              aria-label="Kapat"
              className="ml-auto shrink-0 text-navy-300 hover:text-navy-600"
            >
              <X className="w-4 h-4" />
            </button>
          </div>

          <div className="flex-1 min-h-0 px-3.5 py-3 space-y-2.5 overflow-auto">
            <div className="text-sm font-semibold text-navy-900">{info.title}</div>

            <div>
              <div className="text-[11px] font-medium uppercase tracking-wide text-navy-400 mb-0.5">Neden geldi?</div>
              <p className="text-sm text-navy-700 leading-snug">{info.neden}</p>
            </div>

            <div>
              <div className="text-[11px] font-medium uppercase tracking-wide text-navy-400 mb-0.5">Ne yapmalı?</div>
              <ul className="list-disc pl-4 space-y-0.5 text-sm text-navy-700 leading-snug">
                {info.neYapmali.map((step, i) => <li key={i}>{step}</li>)}
              </ul>
            </div>

            <div className="pt-1.5 border-t border-navy-50">
              <div className="text-[11px] font-medium uppercase tracking-wide text-navy-400 mb-0.5">Sağlayıcı mesajı</div>
              <p className="text-xs text-navy-500 break-words whitespace-pre-wrap">{rawText}</p>
            </div>

            <a
              href={WA_DOC_URL}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-700"
            >
              Meta hata-kodları dokümanı <ExternalLink className="w-3 h-3" />
            </a>
          </div>
        </div>,
        document.body,
      )}
    </>
  );
}
