import { useState, useRef, useEffect } from 'react';
import { RefreshCw, Variable, Loader2, AlertTriangle } from 'lucide-react';
import { useDynamicFields } from '../hooks/useDynamicFields';
import { Button } from './ui/Button';

// FEAT-DMP: dropdown that lists INMA placeholders (name/email/note/pushname/datalistname
// + active cf1..cf10) and calls onInsert with the raw token wrapper '{{cf1}}'.
// FieldName (tenant label) is shown as the primary text; FieldKey as subdued monospace.
// TFM semantic names are out of scope for DMP — a future FEAT-TFM pass can swap the
// display layer to show 'roadshow_city' while still emitting the INMA-resolved '{{cf1}}'.

export interface PlaceholderPickerProps {
  onInsert: (token: string) => void;
  /** Optional label for the trigger button (default: "Dinamik alan ekle"). */
  triggerLabel?: string;
  /** Hide trigger button and render inline (when consumer provides its own trigger). */
  inline?: boolean;
  /** Position above/below; defaults to below. */
  position?: 'above' | 'below';
}

export function PlaceholderPicker({
  onInsert,
  triggerLabel = 'Dinamik alan ekle',
  inline = false,
  position = 'below',
}: PlaceholderPickerProps) {
  const [open, setOpen] = useState(false);
  const { fields, isLoading, error, errorKind, refresh } = useDynamicFields();
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

  const handleSelect = (fieldKey: string) => {
    const token = `{{${fieldKey.toLowerCase()}}}`;
    onInsert(token);
    setOpen(false);
  };

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
            <span className="text-xs font-medium text-gray-600">INMA Dinamik Alanlar</span>
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
                onClick={() => handleSelect(f.FieldKey)}
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
 */
export function renderDynamicPreview(text: string): string {
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
    return defaults[lower] ?? match;
  });
}
