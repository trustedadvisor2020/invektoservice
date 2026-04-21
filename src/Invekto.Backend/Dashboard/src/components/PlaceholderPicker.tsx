import { useState, useRef, useEffect } from 'react';
import { RefreshCw, Variable, Loader2, AlertTriangle } from 'lucide-react';
import { useDynamicFields } from '../hooks/useDynamicFields';
import { useFieldMapping } from '../hooks/useFieldMapping';
import { Button } from './ui/Button';

// FEAT-DMP: dropdown that lists INMA placeholders (name/email/note/pushname/datalistname
// + active cf1..cf10) and calls onInsert with the raw token wrapper '{{cf1}}'.
// FieldName (tenant label) is shown as the primary text; FieldKey as subdued monospace.
//
// FEAT-TFM-FLOW (P4): when `tfmAware` is true, a second group "Semantic Alanlar" is
// rendered ABOVE the INMA Ham Alanlar group, sourced from useFieldMapping. Clicking a
// semantic entry emits `{{<semantic>}}` literal — the backend DMP pipeline resolves it
// to the configured cf via ITenantFieldMappingResolver at render time. Raw INMA entries
// still emit `{{cf1}}` as before (FEAT-DMP contract preserved for tfmAware=false default).

export interface PlaceholderPickerProps {
  onInsert: (token: string) => void;
  /** Optional label for the trigger button (default: "Dinamik alan ekle"). */
  triggerLabel?: string;
  /** Hide trigger button and render inline (when consumer provides its own trigger). */
  inline?: boolean;
  /** Position above/below; defaults to below. */
  position?: 'above' | 'below';
  /**
   * FEAT-TFM-FLOW (P4): opt-in for 2-group rendering (Semantic Alanlar + INMA Ham Alanlar).
   * Default false preserves FEAT-DMP single-list behaviour for any consumer that has not
   * opted in. If the tenant has no mapping or useFieldMapping fails, the Semantic group
   * is silently hidden (tenant without TFM still sees the raw INMA list unchanged).
   */
  tfmAware?: boolean;
}

export function PlaceholderPicker({
  onInsert,
  triggerLabel = 'Dinamik alan ekle',
  inline = false,
  position = 'below',
  tfmAware = false,
}: PlaceholderPickerProps) {
  const [open, setOpen] = useState(false);
  const { fields, isLoading, error, errorKind, refresh } = useDynamicFields();
  // useFieldMapping is always called (preserves React hook call order across renders)
  // but the `enabled` flag gates the actual fetch so FEAT-DMP-only mounts (tfmAware=false)
  // stay zero-cost on TFM. When enabled the hook mirrors useDynamicFields: module-cache +
  // single-flight GET coalesced across picker instances in the same session.
  const tfmState = useFieldMapping(tfmAware);
  const containerRef = useRef<HTMLDivElement | null>(null);

  // CQ10/CQ2 fix: distinguish "no WapCRM secret" / "INMA unreachable" / "cache drop failed
  // but fetch ok" / generic. invalidate_partial is a SOFT warning — list still rendered,
  // just up to 1h stale. Other kinds suppress the list and show the hard error instead.
  const errorMessage = !error
    ? null
    : errorKind === 'not_configured'
      ? 'INMA entegrasyonu yapilandirilmamis. Yonetici tenant ayarlarindan eklemelidir.'
      : errorKind === 'upstream_fail'
        ? 'INMA dinamik alan listesine ulasilamadi. Kisa sure sonra tekrar deneyin.'
        : errorKind === 'invalidate_partial'
          ? 'Onbellek yenileme kismen basarisiz; liste 1 saate kadar eski olabilir.'
          : 'INMA alanlari alinamadi. Yenile butonu ile tekrar deneyin.';
  const isSoftWarning = errorKind === 'invalidate_partial';

  useEffect(() => {
    if (!open) return;
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [open]);

  const handleSelectInma = (fieldKey: string) => {
    const token = `{{${fieldKey.toLowerCase()}}}`;
    onInsert(token);
    setOpen(false);
  };

  const handleSelectSemantic = (semanticName: string) => {
    // Emit semantic literal — backend TFM resolver maps to the configured cf slot at
    // render time, so template/flow stays stable across tenant mapping changes.
    const token = `{{${semanticName.toLowerCase()}}}`;
    onInsert(token);
    setOpen(false);
  };

  // AC5: TFM Semantic group is hidden on fetch fail OR empty mapping. FEAT-DMP UX
  // unchanged for tenants without TFM configured yet — no soft-warn banner inside
  // the dropdown (awareness lives on /settings/field-mapping).
  const showSemanticGroup = tfmAware && !tfmState.error && tfmState.entries.length > 0;

  return (
    <div ref={containerRef} className="relative inline-block">
      {!inline && (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => setOpen((v) => !v)}
          title="INMA alanlarini mesaj metnine ekle"
        >
          <Variable className="w-4 h-4 mr-1" />
          {triggerLabel}
        </Button>
      )}

      {(open || inline) && (
        <div
          className={`${
            inline ? '' : `absolute z-50 ${position === 'above' ? 'bottom-full mb-1' : 'top-full mt-1'}`
          } w-72 rounded-md border border-gray-200 bg-white shadow-lg`}
        >
          <div className="flex items-center justify-between px-3 py-2 border-b border-gray-100">
            <span className="text-xs font-medium text-gray-600">
              {tfmAware ? 'Alanlar' : 'INMA Dinamik Alanlar'}
            </span>
            <button
              type="button"
              onClick={refresh}
              className="text-gray-400 hover:text-gray-700"
              title="Listeyi yenile"
            >
              <RefreshCw className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="max-h-64 overflow-y-auto py-1">
            {/* Semantic Alanlar group — tfmAware + mapping has entries */}
            {showSemanticGroup && (
              <>
                <div className="px-3 py-1 bg-gray-50 border-b border-gray-100">
                  <span className="text-[11px] font-semibold uppercase tracking-wide text-gray-500">
                    Semantic Alanlar
                  </span>
                </div>
                {tfmState.entries.map((entry) => (
                  <button
                    key={`semantic-${entry.semanticName}`}
                    type="button"
                    onClick={() => handleSelectSemantic(entry.semanticName)}
                    className="w-full text-left px-3 py-1.5 hover:bg-gray-50 flex items-center justify-between gap-2"
                    title={`Imlec konumuna {{${entry.semanticName}}} eklenir (INMA kaynak: ${entry.source})`}
                  >
                    <span className="text-sm text-gray-900 truncate">{entry.semanticName}</span>
                    <span className="text-xs font-mono text-gray-500">{`{{${entry.semanticName}}}`}</span>
                  </button>
                ))}
                <div className="px-3 py-1 bg-gray-50 border-y border-gray-100">
                  <span className="text-[11px] font-semibold uppercase tracking-wide text-gray-500">
                    INMA Ham Alanlar
                  </span>
                </div>
              </>
            )}

            {/* INMA Ham Alanlar group — always rendered (or single-list when tfmAware=false). */}
            {isLoading && (
              <div className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500">
                <Loader2 className="w-4 h-4 animate-spin" />
                Alanlar yukleniyor...
              </div>
            )}

            {!isLoading && error && !isSoftWarning && (
              <div className="flex items-start gap-2 px-3 py-2 text-sm text-amber-700">
                <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
                <span>{errorMessage}</span>
              </div>
            )}

            {!isLoading && isSoftWarning && (
              <div className="flex items-start gap-2 px-3 py-2 text-xs text-amber-700 bg-amber-50 border-b border-amber-100">
                <AlertTriangle className="w-3.5 h-3.5 shrink-0 mt-0.5" />
                <span>{errorMessage}</span>
              </div>
            )}

            {!isLoading && (!error || isSoftWarning) && fields.length === 0 && (
              <div className="px-3 py-2 text-sm text-gray-500">
                Kullanilabilir alan yok. (INMA tenant yapilandirmasi eksik)
              </div>
            )}

            {!isLoading && (!error || isSoftWarning) && fields.length > 0 && fields.map((f) => (
              <button
                key={f.FieldKey}
                type="button"
                onClick={() => handleSelectInma(f.FieldKey)}
                className="w-full text-left px-3 py-1.5 hover:bg-gray-50 flex items-center justify-between gap-2"
                title={`Imleç konumuna {{${f.FieldKey}}} eklenir`}
              >
                <span className="text-sm text-gray-900 truncate">{f.FieldName || f.FieldKey}</span>
                <span className="text-xs font-mono text-gray-500">{`{{${f.FieldKey}}}`}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * FEAT-DMP preview helper — returns a preview-safe rendered string where each
 * recognised INMA placeholder is replaced with a fixed demo default. Used by
 * template / campaign composer "preview" tiles so tenants see what the message
 * looks like without hitting INMA or exposing a real customer record.
 *
 * Interview Q3 decision (2026-04-20): hardcoded demo values, no INMA round-trip.
 *
 * FEAT-TFM-FLOW (P4): optional `mapping` parameter (semantic -> source cf). When
 * provided, semantic tokens are first resolved to their configured source slot
 * and then substituted with that slot's demo default. Unknown/unmapped semantic
 * tokens fall through to `match` (literal) so the preview never silently
 * replaces unconfigured placeholders with fake data.
 */
export function renderDynamicPreview(text: string, mapping?: Record<string, string>): string {
  if (!text) return '';
  const defaults: Record<string, string> = {
    name: 'Ornek Musteri',
    email: 'ornek@email.com',
    note: 'Demo not',
    pushname: 'Demo Push',
    datalistname: 'Demo Liste',
    cf1: 'Istanbul',
    cf2: 'ABC A.S.',
    cf3: 'Demo 3',
    cf4: 'Demo 4',
    cf5: 'Demo 5',
    cf6: 'Demo 6',
    cf7: 'Demo 7',
    cf8: 'Demo 8',
    cf9: 'Demo 9',
    cf10: 'Demo 10',
  };
  return text.replace(/\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}/g, (match, key: string) => {
    const lower = key.toLowerCase();
    if (defaults[lower] !== undefined) return defaults[lower];
    if (mapping) {
      const resolved = mapping[lower] ?? mapping[key];
      if (resolved && defaults[resolved.toLowerCase()] !== undefined) {
        return defaults[resolved.toLowerCase()];
      }
    }
    return match;
  });
}
