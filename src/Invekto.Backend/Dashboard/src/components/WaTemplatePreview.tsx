import { cn } from '../lib/utils';
import type { WaTemplate } from '../lib/api';

// Structured WhatsApp-style preview of an approved (HSM) template — header / body / footer / buttons.
// Pure (takes a WaTemplate). Shared by the Projeler Gönder modal + table hover card AND the
// Export Manager "Telefon Numarası Ara" Mesaj hover popup. Extracted from ProjectsPage so both
// surfaces render an identical card (single source of truth for the WA template preview look).
export function WaTemplatePreview({ t, className }: { t: WaTemplate; className?: string }) {
  const h = t.preview?.header;
  const btns = (t.preview?.buttons ?? []).map(b => b?.text?.trim()).filter((x): x is string => !!x);
  const empty = !h?.text && !h?.type && !t.preview?.body && !t.preview?.footer && btns.length === 0;
  return (
    <div className={cn('rounded-lg bg-[#f6faf2] border border-green-100 px-3 py-2 text-sm text-navy-800 space-y-1.5', className)}>
      {h?.text
        ? <div className="font-semibold whitespace-pre-wrap break-words">{h.text}</div>
        : h?.type ? <div className="text-xs italic text-navy-400">[{h.type} başlık]</div> : null}
      {t.preview?.body && <div className="whitespace-pre-wrap break-words leading-relaxed">{t.preview.body}</div>}
      {t.preview?.footer && <div className="text-xs text-navy-400 whitespace-pre-wrap break-words">{t.preview.footer}</div>}
      {btns.length > 0 && (
        <div className="flex flex-wrap gap-x-3 gap-y-1 pt-1.5 border-t border-green-100">
          {btns.map((b, i) => <span key={i} className="text-xs text-green-700 font-medium">▸ {b}</span>)}
        </div>
      )}
      {empty && <div className="text-xs italic text-navy-400">Şablon içeriği boş</div>}
    </div>
  );
}
