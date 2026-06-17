import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { createPortal } from 'react-dom';
import {
  Download, FileSpreadsheet, FileText, Loader2, ListPlus,
  Filter as FilterIcon, Database, X, AlertTriangle, Info, RotateCw, History,
  Search, Phone as PhoneIcon,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Select } from '../components/ui/Select';
import { Input } from '../components/ui/Input';
import { WaErrorPopover } from '../components/WaErrorPopover';
import { WaTemplatePreview } from '../components/WaTemplatePreview';
import {
  api, ApiClientError,
  type ExportFilter, type ExportFilterOptions, type FilteredCount,
  type ExportLogEntry, type PhoneHistoryResult, type PhoneHistoryMessageRow,
  type WaTemplate,
} from '../lib/api';

// =============================================================
// FEAT-OBI Phase 1B — Export Manager v2 (filter-driven recipients surface)
// One surface over ALL of the tenant's bulk-send recipients: filter by
// Şablon / Data Listesi (membership) / Teslim Durumu / Tarih → live count card
// (benzersiz numara + toplam kayıt) → CSV / Excel / Liste Oluştur (a new
// data_list source='export') → Export Geçmişi (export_logs).
// =============================================================

const FEATURE_DISABLED = 'INV-OB-056';

// Delivery-status options for the "Sonuç" → Teslim Durumu dropdown.
const STATUS_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: 'Tüm sonuçlar' },
  { value: 'read', label: 'Okundu' },
  { value: 'delivered', label: 'Teslim edildi' },
  { value: 'sent', label: 'Gönderildi' },
  { value: 'sending', label: 'Gönderiliyor' },
  { value: 'queued', label: 'Sırada' },
  { value: 'failed', label: 'Başarısız' },
  { value: 'blocked', label: 'Engellendi' },
  { value: 'not_sent', label: 'Gönderilmedi' },
];

const EXPORT_TYPE_LABELS: Record<string, string> = {
  contact_list: 'Kişi listesi',
  send_recipients: 'Kampanya alıcıları',
  send_summary: 'Kampanya raporu (PDF)',
  filtered_recipients: 'Filtreli dışa aktarma',
  list_from_export: 'Liste oluşturma',
  phone_history: 'Numara geçmişi',
};

// jsPDF core fonts are Latin-1 only — fold Turkish diacritics to ASCII so the PDF is legible
// (the on-screen table + CSV keep the original text; only the PDF render is transliterated).
const TR_ASCII: Record<string, string> = {
  ş: 's', Ş: 'S', ğ: 'g', Ğ: 'G', ı: 'i', İ: 'I', ç: 'c', Ç: 'C', ö: 'o', Ö: 'O', ü: 'u', Ü: 'U',
};
function tr(s: string | null | undefined): string {
  return (s ?? '').replace(/[şŞğĞıİçÇöÖüÜ]/g, (c) => TR_ASCII[c] ?? c);
}

function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

function fmtDate(s: string | null | undefined): string {
  if (!s) return '';
  const d = new Date(s);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleString('tr-TR');
}

const EMPTY_FILTER: ExportFilter = {};

// Faz 2.1 — Mesaj cell. For a resolved HSM (WAPCRM) template the cell shows a one-line body preview
// with a rich hover popup (the full WhatsApp-style card: header/body/footer/buttons). Plain-text and
// unresolved rows render the stored body as before. The popup is portalled to <body> so it escapes
// the table's horizontal overflow clipping (same pattern as WaErrorPopover).
function PhoneMsgCell({ tpl, text }: { tpl: WaTemplate | null; text: string }) {
  const [rect, setRect] = useState<DOMRect | null>(null);
  const ref = useRef<HTMLButtonElement>(null);

  if (!tpl) {
    return <span className="whitespace-pre-wrap break-words">{text}</span>;
  }

  const preview =
    tpl.preview?.body?.trim() ||
    (tpl.preview?.header?.type === 'TEXT' ? tpl.preview?.header?.text?.trim() : '') ||
    tpl.preview?.footer?.trim() ||
    '(önizleme yok)';

  const show = () => setRect(ref.current?.getBoundingClientRect() ?? null);
  const hide = () => setRect(null);

  // Height-aware placement: pin below the trigger, flip above when there's more room; clamp to viewport.
  const POPOVER_W = 360, GAP = 6, EDGE = 8;
  let style: CSSProperties | undefined;
  if (rect) {
    const vw = window.innerWidth, vh = window.innerHeight;
    const width = Math.min(POPOVER_W, vw - 2 * EDGE);
    const spaceBelow = vh - rect.bottom - GAP - EDGE;
    const spaceAbove = rect.top - GAP - EDGE;
    const below = spaceBelow >= spaceAbove;
    style = {
      position: 'fixed',
      width,
      left: Math.max(EDGE, Math.min(rect.left, vw - width - EDGE)),
      maxHeight: Math.max(0, Math.floor(below ? spaceBelow : spaceAbove)),
      ...(below ? { top: rect.bottom + GAP } : { bottom: vh - rect.top + GAP }),
    };
  }

  return (
    <>
      <button
        ref={ref}
        type="button"
        onMouseEnter={show}
        onMouseLeave={hide}
        onFocus={show}
        onBlur={hide}
        title="Gönderilen şablon önizlemesi"
        className="inline-block max-w-md truncate text-left align-bottom border-b border-dotted border-navy-300 text-navy-600 hover:text-navy-900 cursor-help"
      >
        {preview}
      </button>
      {rect && createPortal(
        <div
          role="tooltip"
          style={style}
          className="z-[70] overflow-auto rounded-xl bg-white border border-navy-100 shadow-soft p-2"
        >
          <div className="text-[11px] font-medium uppercase tracking-wide text-navy-400 mb-1 px-1">
            {tpl.name ?? 'Şablon'}{tpl.language ? ` · ${tpl.language}` : ''}
          </div>
          <WaTemplatePreview t={tpl} />
        </div>,
        document.body,
      )}
    </>
  );
}

export function ExportManagerPage() {
  const [options, setOptions] = useState<ExportFilterOptions | null>(null);
  const [filter, setFilter] = useState<ExportFilter>(EMPTY_FILTER);
  const [count, setCount] = useState<FilteredCount | null>(null);
  const [countLoading, setCountLoading] = useState(false);

  const [history, setHistory] = useState<ExportLogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [featureDisabled, setFeatureDisabled] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null); // 'csv' | 'xlsx' | 'create'

  // Liste Oluştur modal
  const [modalOpen, setModalOpen] = useState(false);
  const [listName, setListName] = useState('');
  const [modalError, setModalError] = useState<string | null>(null);

  // FEAT-OBI Phase 2 — Telefon Numarası Ara (single-number history)
  const [phoneInput, setPhoneInput] = useState('');
  const [phoneResult, setPhoneResult] = useState<PhoneHistoryResult | null>(null);
  const [phoneLoading, setPhoneLoading] = useState(false);
  const [phoneError, setPhoneError] = useState<string | null>(null);
  const [phoneBusy, setPhoneBusy] = useState<string | null>(null); // 'csv' | 'pdf'

  // Faz 2.1 — approved-template (HSM/WAPCRM) name+preview resolution, cached per cxapi channel
  // (instance_id). 'error' = that channel's template fetch failed → the row degrades to the raw
  // template id. Best-effort enrichment: it never blocks or errors the phone lookup itself.
  const [tplByInstance, setTplByInstance] = useState<Record<number, WaTemplate[] | 'error'>>({});
  const tplFetchedRef = useRef<Set<number>>(new Set());

  function failMessage(e: unknown): string {
    if (e instanceof ApiClientError) {
      if (e.errorCode === FEATURE_DISABLED) return 'Dışa aktarma bu hesap için aktif değil.';
      if (e.errorCode === 'INV-OB-058') return 'Excel için veri çok büyük. CSV formatını kullanın (CSV tüm satırları içerir).';
      if (e.errorCode === 'INV-OB-059') return 'Bu isimde bir liste zaten var. Farklı bir ad girin.';
      if (e.errorCode === 'INV-OB-060') return 'Filtre geçersiz. Tarih aralığını kontrol edin.';
      if (e.errorCode === 'INV-OB-061') return 'Filtreye uyan gönderilebilir numara yok; liste oluşturulamadı.';
      if (e.errorCode === 'INV-OB-097') return 'Geçerli bir telefon numarası girin.';
      return e.message;
    }
    return 'İşlem başarısız oldu.';
  }

  // Initial load: filter options + history.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const [opts, hist] = await Promise.all([
          api.getExportFilterOptions(),
          api.listExportHistory(),
        ]);
        if (cancelled) return;
        setOptions(opts);
        setHistory(hist);
      } catch (e) {
        if (cancelled) return;
        if (e instanceof ApiClientError && e.errorCode === FEATURE_DISABLED) {
          setFeatureDisabled(true);
        } else {
          setError(failMessage(e));
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  // Debounced count whenever the filter changes (and the feature is usable).
  const filterKey = JSON.stringify(filter);
  useEffect(() => {
    if (loading || featureDisabled) return;
    let cancelled = false;
    setCountLoading(true);
    const handle = setTimeout(async () => {
      try {
        const c = await api.getExportRecipientCount(filter);
        if (!cancelled) { setCount(c); setError(null); }
      } catch (e) {
        if (!cancelled) { setCount(null); setError(failMessage(e)); }
      } finally {
        if (!cancelled) setCountLoading(false);
      }
    }, 400);
    return () => { cancelled = true; clearTimeout(handle); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey, loading, featureDisabled]);

  // Faz 2.1 — when a result lands, fetch the approved-template list for each cxapi channel present in
  // the HSM rows (once per instance_id). Per-channel failures are isolated as 'error' (row keeps the
  // raw id). Non-blocking: the table renders immediately and names/previews fill in when fetched.
  useEffect(() => {
    if (!phoneResult) return;
    const instances = Array.from(new Set(
      phoneResult.rows
        .filter((r) => r.wa_template_id && r.instance_id != null)
        .map((r) => r.instance_id as number),
    )).filter((id) => !tplFetchedRef.current.has(id));
    if (instances.length === 0) return;
    instances.forEach((id) => tplFetchedRef.current.add(id));
    let cancelled = false;
    instances.forEach((id) => {
      api.getWaTemplates(id)
        .then((res) => { if (!cancelled) setTplByInstance((m) => ({ ...m, [id]: res.templates })); })
        .catch((e) => {
          // Best-effort enrichment: the row degrades to the raw template id, so this must not surface as a
          // page error — but it isn't fully silent either (same console.warn contract as refreshHistory).
          console.warn(`Şablon listesi yüklenemedi (kanal ${id}) — ham şablon id'sine düşülüyor:`, e);
          if (!cancelled) setTplByInstance((m) => ({ ...m, [id]: 'error' }));
        });
    });
    return () => { cancelled = true; };
  }, [phoneResult]);

  const refreshHistory = useCallback(async () => {
    // Non-blocking: the export/create already succeeded; a stale history list must not surface as a
    // page error. We still log so a refresh failure isn't fully silent (Codex CQ2).
    try {
      setHistory(await api.listExportHistory());
    } catch (e) {
      console.warn('Export geçmişi yenilenemedi:', e);
    }
  }, []);

  function patchFilter(p: Partial<ExportFilter>) {
    setSuccess(null);
    setFilter((prev) => ({ ...prev, ...p }));
  }
  function clearFilters() {
    setSuccess(null);
    setFilter(EMPTY_FILTER);
  }

  async function downloadExport(format: 'csv' | 'xlsx') {
    setBusy(format);
    setError(null);
    setSuccess(null);
    try {
      const { blob, filename } = await api.downloadFilteredExport(filter, format);
      saveBlob(blob, filename);
      await refreshHistory();
    } catch (e) {
      setError(failMessage(e));
    } finally {
      setBusy(null);
    }
  }

  async function submitCreateList() {
    const name = listName.trim();
    if (!name) { setModalError('Liste adı gerekli.'); return; }
    setBusy('create');
    setModalError(null);
    try {
      const result = await api.createListFromExport(name, filter);
      setModalOpen(false);
      setListName('');
      setSuccess(`"${result.name}" listesi oluşturuldu — ${result.record_count.toLocaleString('tr-TR')} numara.`);
      await refreshHistory();
    } catch (e) {
      setModalError(failMessage(e));
    } finally {
      setBusy(null);
    }
  }

  // FEAT-OBI Phase 2 — Telefon Numarası Ara handlers.
  async function doPhoneSearch() {
    const phone = phoneInput.trim();
    if (!phone) { setPhoneError('Telefon numarası girin.'); setPhoneResult(null); return; }
    setPhoneLoading(true);
    setPhoneError(null);
    try {
      const result = await api.getPhoneHistory(phone);
      setPhoneResult(result);
    } catch (e) {
      setPhoneResult(null);
      setPhoneError(failMessage(e));
    } finally {
      setPhoneLoading(false);
    }
  }

  async function downloadPhoneCsv() {
    const phone = phoneInput.trim();
    if (!phone) return;
    setPhoneBusy('csv');
    setPhoneError(null);
    try {
      const { blob, filename } = await api.downloadPhoneHistoryCsv(phone);
      saveBlob(blob, filename);
      await refreshHistory();
    } catch (e) {
      setPhoneError(failMessage(e));
    } finally {
      setPhoneBusy(null);
    }
  }

  async function downloadPhonePdf() {
    const phone = phoneInput.trim();
    if (!phone) return;
    setPhoneBusy('pdf');
    setPhoneError(null);
    try {
      // Server returns the FULL rows as JSON (and writes the KVKK audit row); render the PDF client-side.
      const data = await api.getPhoneHistoryPdfData(phone);
      const { default: JsPdf } = await import('jspdf');
      const { default: autoTable } = await import('jspdf-autotable');
      const doc = new JsPdf();
      doc.setFontSize(13);
      doc.text(tr(`Numara Gecmisi — ${data.normalized_phone}`), 14, 16);
      doc.setFontSize(9);
      doc.text(tr(`${data.rows.length} mesaj · ${new Date().toLocaleString('tr-TR')}`), 14, 22);
      autoTable(doc, {
        startY: 28,
        head: [['Tarih', 'Durum', 'Sablon', 'Kampanya', 'Hata', 'Mesaj']],
        body: data.rows.map((r) => [
          tr(fmtDate(r.created_at)),
          tr(r.status_label),
          tr(r.template_name ?? '—'),
          // Kampanya: project name when present; else campaign_id so the PDF evidence keeps the identity.
          tr(r.campaign_name ?? r.campaign_id ?? '—'),
          tr(r.error ?? ''),
          tr(r.message_text),
        ]),
        styles: { fontSize: 7, cellWidth: 'wrap', overflow: 'linebreak' },
        columnStyles: { 5: { cellWidth: 60 } },
        headStyles: { fillColor: [30, 41, 59] },
      });
      doc.save(`numara_gecmis_${data.normalized_phone.replace(/[^0-9]/g, '')}.pdf`);
      await refreshHistory();
    } catch (e) {
      setPhoneError(failMessage(e));
    } finally {
      setPhoneBusy(null);
    }
  }

  // Active-filter chips (label resolution from the loaded options).
  const chips = useMemo(() => {
    const c: { key: string; label: string; clear: () => void }[] = [];
    if (filter.templateId != null) {
      const t = options?.templates.find((o) => o.id === filter.templateId);
      c.push({ key: 'tpl', label: `Şablon: ${t?.label ?? filter.templateId}`, clear: () => patchFilter({ templateId: null }) });
    }
    if (filter.listId != null) {
      const l = options?.lists.find((o) => o.id === filter.listId);
      c.push({ key: 'list', label: `Liste: ${l?.label ?? filter.listId}`, clear: () => patchFilter({ listId: null }) });
    }
    if (filter.status) {
      const st = STATUS_OPTIONS.find((o) => o.value === filter.status);
      c.push({ key: 'status', label: `Sonuç: ${st?.label ?? filter.status}`, clear: () => patchFilter({ status: null }) });
    }
    if (filter.from || filter.to) {
      c.push({ key: 'date', label: `Tarih: ${filter.from ?? '…'} – ${filter.to ?? '…'}`, clear: () => patchFilter({ from: null, to: null }) });
    }
    return c;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey, options]);

  const opt = (items: { id: number; label: string }[] | undefined, allLabel: string) =>
    [{ value: '', label: allLabel }, ...(items ?? []).map((i) => ({ value: String(i.id), label: i.label || `#${i.id}` }))];

  const actionsDisabled = busy !== null || countLoading || (count?.total_count ?? 0) === 0;

  // Resolve a row's HSM template (id + channel) to the fetched WaTemplate; null for plain/gallery/unresolved.
  const resolveTpl = (r: PhoneHistoryMessageRow): WaTemplate | null => {
    if (!r.wa_template_id || r.instance_id == null) return null;
    const list = tplByInstance[r.instance_id];
    if (!list || list === 'error') return null;
    return list.find((t) => t.templateId === r.wa_template_id) ?? null;
  };

  // Şablon column label: resolved HSM name → gallery template_name → raw HSM id → '—'.
  const templateLabel = (r: PhoneHistoryMessageRow): string =>
    resolveTpl(r)?.name || r.template_name || r.wa_template_id || '—';

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-navy-900">Export Manager</h1>
        <p className="text-sm text-navy-500 mt-1">Hızlı filtreleme ve dışa aktarma — her işlem KVKK denetim kaydına işlenir.</p>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2.5 text-sm text-red-700 flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0" /> {error}
        </div>
      )}
      {success && (
        <div className="bg-green-50 border border-green-200 rounded-lg px-4 py-2.5 text-sm text-green-700 flex items-center gap-2">
          <ListPlus className="w-4 h-4 shrink-0" /> {success}
        </div>
      )}

      {featureDisabled && (
        <div className="px-4 py-10 text-center text-navy-500 text-sm flex flex-col items-center gap-2 bg-white border border-navy-100 rounded-xl">
          <Info className="w-6 h-6 text-navy-300" />
          <div className="font-medium">Dışa aktarma bu hesapta henüz etkin değil.</div>
          <div className="text-navy-400">Etkinleştirmek için yöneticinizle iletişime geçin.</div>
        </div>
      )}

      {loading && (
        <div className="px-4 py-10 text-center text-navy-400 text-sm flex items-center justify-center gap-2">
          <Loader2 className="w-4 h-4 animate-spin" /> Yükleniyor…
        </div>
      )}

      {!loading && !featureDisabled && (
        <>
          {/* ── Telefon Numarası Ara (tek numara geçmişi) ── */}
          <section className="bg-white border border-navy-100 rounded-xl shadow-soft p-4 space-y-3">
            <div className="flex items-center gap-2">
              <PhoneIcon className="w-4 h-4 text-brand-500" />
              <h2 className="text-sm font-medium text-navy-700">Telefon Numarası Ara</h2>
            </div>
            <p className="text-xs text-navy-400">
              Bir numaranın bu hesaptan gönderilen tüm mesaj geçmişini (şablon, kampanya, durum, hata, içerik) görün.
            </p>
            <div className="flex flex-col sm:flex-row gap-2 sm:items-end">
              <div className="flex-1">
                <Input
                  label="Telefon numarası"
                  placeholder="Örn: 0532 123 45 67 veya +905321234567"
                  value={phoneInput}
                  onChange={(e) => { setPhoneInput(e.target.value); setPhoneError(null); }}
                  onKeyDown={(e) => { if (e.key === 'Enter') doPhoneSearch(); }}
                />
              </div>
              <Button variant="primary" disabled={phoneLoading || !phoneInput.trim()} onClick={doPhoneSearch}>
                {phoneLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Search className="w-4 h-4" />} Ara
              </Button>
            </div>

            {phoneError && (
              <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-sm text-red-700 flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" /> {phoneError}
              </div>
            )}

            {phoneResult && (
              <div className="space-y-2">
                <div className="flex items-center justify-between flex-wrap gap-2">
                  <div className="text-xs text-navy-500">
                    <span className="font-medium text-navy-700">{phoneResult.normalized_phone}</span>
                    {' · '}{phoneResult.rows.length.toLocaleString('tr-TR')} mesaj
                    {phoneResult.truncated && (
                      <span className="text-amber-600"> · en yeni {phoneResult.limit.toLocaleString('tr-TR')} gösteriliyor — tümü için indirin</span>
                    )}
                  </div>
                  {phoneResult.rows.length > 0 && (
                    <div className="flex items-center gap-2">
                      <Button variant="secondary" disabled={phoneBusy !== null} onClick={downloadPhoneCsv}>
                        {phoneBusy === 'csv' ? <Loader2 className="w-4 h-4 animate-spin" /> : <FileText className="w-4 h-4" />} CSV
                      </Button>
                      <Button variant="secondary" disabled={phoneBusy !== null} onClick={downloadPhonePdf}>
                        {phoneBusy === 'pdf' ? <Loader2 className="w-4 h-4 animate-spin" /> : <FileText className="w-4 h-4" />} PDF
                      </Button>
                    </div>
                  )}
                </div>

                {phoneResult.rows.length === 0 ? (
                  <div className="px-4 py-8 text-center text-navy-400 text-sm border border-navy-50 rounded-lg">
                    Bu numaraya gönderim bulunamadı.
                  </div>
                ) : (
                  <div className="border border-navy-50 rounded-lg overflow-x-auto">
                    <table className="w-full text-sm">
                      <thead className="bg-navy-50/50 text-navy-500 text-xs">
                        <tr>
                          <th className="text-left font-medium px-3 py-2 whitespace-nowrap">Tarih</th>
                          <th className="text-left font-medium px-3 py-2">Durum</th>
                          <th className="text-left font-medium px-3 py-2">Şablon</th>
                          <th className="text-left font-medium px-3 py-2">Kampanya</th>
                          <th className="text-left font-medium px-3 py-2">Hata</th>
                          <th className="text-left font-medium px-3 py-2">Mesaj</th>
                        </tr>
                      </thead>
                      <tbody>
                        {phoneResult.rows.map((r) => (
                          <tr key={r.id} className="border-t border-navy-50 align-top">
                            <td className="px-3 py-2 text-navy-500 whitespace-nowrap">{fmtDate(r.created_at)}</td>
                            <td className="px-3 py-2 text-navy-700 whitespace-nowrap">{r.status_label}</td>
                            <td className="px-3 py-2 text-navy-500">{templateLabel(r)}</td>
                            <td className="px-3 py-2 text-navy-500">{r.campaign_name ?? '—'}</td>
                            <td className="px-3 py-2">{r.error ? <WaErrorPopover raw={r.error} /> : <span className="text-navy-300">—</span>}</td>
                            <td className="px-3 py-2 text-navy-600 max-w-md"><PhoneMsgCell tpl={resolveTpl(r)} text={r.message_text} /></td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </section>

          {/* ── Filtreler ── */}
          <section className="bg-white border border-navy-100 rounded-xl shadow-soft p-4 space-y-4">
            <div className="flex items-center gap-2">
              <FilterIcon className="w-4 h-4 text-brand-500" />
              <h2 className="text-sm font-medium text-navy-700">Filtreler</h2>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
              <Select
                label="Şablon"
                value={filter.templateId != null ? String(filter.templateId) : ''}
                options={opt(options?.templates, 'Tüm şablonlar')}
                onChange={(e) => patchFilter({ templateId: e.target.value ? Number(e.target.value) : null })}
              />
              <Select
                label="Data Listesi"
                value={filter.listId != null ? String(filter.listId) : ''}
                options={opt(options?.lists, 'Tüm listeler')}
                onChange={(e) => patchFilter({ listId: e.target.value ? Number(e.target.value) : null })}
              />
              <Select
                label="Sonuç (teslim durumu)"
                value={filter.status ?? ''}
                options={STATUS_OPTIONS}
                onChange={(e) => patchFilter({ status: e.target.value || null })}
              />
              <Input
                label="Başlangıç tarihi"
                type="date"
                value={filter.from ?? ''}
                onChange={(e) => patchFilter({ from: e.target.value || null })}
              />
              <Input
                label="Bitiş tarihi"
                type="date"
                value={filter.to ?? ''}
                onChange={(e) => patchFilter({ to: e.target.value || null })}
              />
            </div>

            {chips.length > 0 && (
              <div className="flex items-center flex-wrap gap-2 pt-1">
                <span className="text-xs text-navy-400">Aktif filtreler:</span>
                {chips.map((chip) => (
                  <span key={chip.key} className="inline-flex items-center gap-1 bg-navy-50 text-navy-600 text-xs rounded-full px-2.5 py-1">
                    {chip.label}
                    <button onClick={chip.clear} className="hover:text-navy-900" aria-label="Filtreyi kaldır">
                      <X className="w-3 h-3" />
                    </button>
                  </span>
                ))}
                <button onClick={clearFilters} className="inline-flex items-center gap-1 text-xs text-red-500 hover:text-red-700">
                  <X className="w-3 h-3" /> Tümünü Temizle
                </button>
              </div>
            )}
          </section>

          {/* ── Sayım kartı ── */}
          <section className="bg-gradient-to-b from-brand-50/60 to-white border border-navy-100 rounded-xl shadow-soft p-6 text-center">
            <div className="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-white border border-navy-100 mb-3">
              <Database className="w-5 h-5 text-brand-500" />
            </div>
            <div className="text-4xl font-bold text-navy-900 tabular-nums flex items-center justify-center gap-2">
              {countLoading
                ? <Loader2 className="w-7 h-7 animate-spin text-navy-300" />
                : (count?.unique_count ?? 0).toLocaleString('tr-TR')}
            </div>
            <div className="text-sm text-navy-500 mt-1">benzersiz numara</div>
            <div className="text-xs text-navy-400 mt-1">
              Export edilecek toplam kayıt: <span className="font-medium text-navy-600">{(count?.total_count ?? 0).toLocaleString('tr-TR')}</span> (tekrarlar dahil)
            </div>
          </section>

          {/* ── Export Seçenekleri ── */}
          <section className="bg-white border border-navy-100 rounded-xl shadow-soft p-4 space-y-3">
            <h2 className="text-sm font-medium text-navy-700">Export Seçenekleri</h2>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <button
                disabled={actionsDisabled}
                onClick={() => downloadExport('csv')}
                className="flex items-center gap-3 text-left border border-navy-100 rounded-lg p-4 hover:border-brand-300 hover:shadow-soft transition disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <span className="w-10 h-10 rounded-lg bg-brand-50 flex items-center justify-center shrink-0">
                  {busy === 'csv' ? <Loader2 className="w-5 h-5 animate-spin text-brand-500" /> : <FileText className="w-5 h-5 text-brand-500" />}
                </span>
                <span>
                  <span className="block text-sm font-medium text-navy-800">CSV İndir</span>
                  <span className="block text-xs text-navy-400">Tüm satırlar (UTF-8)</span>
                </span>
              </button>

              <button
                disabled={actionsDisabled}
                onClick={() => downloadExport('xlsx')}
                className="flex items-center gap-3 text-left border border-navy-100 rounded-lg p-4 hover:border-brand-300 hover:shadow-soft transition disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <span className="w-10 h-10 rounded-lg bg-green-50 flex items-center justify-center shrink-0">
                  {busy === 'xlsx' ? <Loader2 className="w-5 h-5 animate-spin text-green-600" /> : <FileSpreadsheet className="w-5 h-5 text-green-600" />}
                </span>
                <span>
                  <span className="block text-sm font-medium text-navy-800">Excel İndir</span>
                  <span className="block text-xs text-navy-400">Detaylı analiz (.xlsx)</span>
                </span>
              </button>

              <button
                disabled={actionsDisabled}
                onClick={() => { setModalError(null); setListName(''); setModalOpen(true); }}
                className="flex items-center gap-3 text-left border border-navy-100 rounded-lg p-4 hover:border-brand-300 hover:shadow-soft transition disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <span className="w-10 h-10 rounded-lg bg-purple-50 flex items-center justify-center shrink-0">
                  <ListPlus className="w-5 h-5 text-purple-600" />
                </span>
                <span>
                  <span className="block text-sm font-medium text-navy-800">Liste Oluştur</span>
                  <span className="block text-xs text-navy-400">Benzersiz numaralardan yeni liste</span>
                </span>
              </button>
            </div>
            {(count?.total_count ?? 0) === 0 && !countLoading && (
              <p className="text-xs text-navy-400">Filtreye uyan kayıt yok — export için filtreleri gevşetin.</p>
            )}
          </section>

          {/* ── Export Geçmişi ── */}
          <section className="bg-white border border-navy-100 rounded-xl shadow-soft overflow-hidden">
            <div className="px-4 py-3 border-b border-navy-50 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <History className="w-4 h-4 text-brand-500" />
                <h2 className="text-sm font-medium text-navy-700">Export Geçmişi</h2>
              </div>
              <button onClick={refreshHistory} className="text-xs text-navy-400 hover:text-navy-700 inline-flex items-center gap-1">
                <RotateCw className="w-3.5 h-3.5" /> Yenile
              </button>
            </div>
            {history.length === 0 ? (
              <div className="px-4 py-10 text-center text-navy-400 text-sm">Henüz export yapılmamış.</div>
            ) : (
              <table className="w-full text-sm">
                <thead className="bg-navy-50/50 text-navy-500 text-xs">
                  <tr>
                    <th className="text-left font-medium px-4 py-2">Tür</th>
                    <th className="text-left font-medium px-4 py-2">Kaynak</th>
                    <th className="text-left font-medium px-4 py-2">Format</th>
                    <th className="text-right font-medium px-4 py-2">Kayıt</th>
                    <th className="text-left font-medium px-4 py-2">Durum</th>
                    <th className="text-left font-medium px-4 py-2">Tarih</th>
                  </tr>
                </thead>
                <tbody>
                  {history.map((h) => (
                    <tr key={h.id} className="border-t border-navy-50">
                      <td className="px-4 py-2.5 text-navy-700">{EXPORT_TYPE_LABELS[h.export_type] ?? h.export_type}</td>
                      <td className="px-4 py-2.5 text-navy-500">{h.source_name ?? '—'}</td>
                      <td className="px-4 py-2.5 text-navy-600 uppercase">{h.format}</td>
                      <td className="px-4 py-2.5 text-right text-navy-600 tabular-nums">{h.row_count.toLocaleString('tr-TR')}</td>
                      <td className="px-4 py-2.5">
                        {h.status === 'completed'
                          ? <span className="text-green-600">Tamamlandı</span>
                          : <span className="text-red-600">Başarısız{h.error_code ? ` (${h.error_code})` : ''}</span>}
                      </td>
                      <td className="px-4 py-2.5 text-navy-500">{fmtDate(h.created_at)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          <p className="text-xs text-navy-400 flex items-center gap-1.5">
            <Download className="w-3.5 h-3.5" />
            Filtreler kampanya alıcılarına (gönderim sonuçlarına) uygulanır. "Liste Oluştur" benzersiz gönderilebilir numaralardan yeni bir data listesi yaratır.
          </p>
        </>
      )}

      {/* ── Liste Oluştur modal ── */}
      {modalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
          onMouseDown={e => { if (e.target === e.currentTarget) setModalOpen(false); }}
        >
          <div className="bg-white rounded-xl shadow-lg w-full max-w-md p-5 relative">
            <button
              onClick={() => setModalOpen(false)}
              className="absolute top-3 right-3 text-navy-400 hover:text-navy-700"
              aria-label="Kapat"
            >
              <X className="w-5 h-5" />
            </button>
            <h3 className="text-base font-semibold text-navy-900 mb-1">Liste Oluştur</h3>
            <p className="text-xs text-navy-500 mb-4">
              Mevcut filtreye uyan <span className="font-medium text-navy-700">{(count?.unique_count ?? 0).toLocaleString('tr-TR')}</span> benzersiz numara yeni bir data listesine yazılacak.
            </p>
            <Input
              label="Liste adı"
              placeholder="Örn: Haziran ulaşılamayanlar"
              value={listName}
              error={modalError ?? undefined}
              onChange={(e) => { setListName(e.target.value); setModalError(null); }}
              onKeyDown={(e) => { if (e.key === 'Enter') submitCreateList(); }}
              autoFocus
            />
            <div className="flex justify-end mt-4">
              <Button variant="primary" disabled={busy === 'create' || !listName.trim()} onClick={submitCreateList}>
                {busy === 'create' ? <Loader2 className="w-4 h-4 animate-spin" /> : <ListPlus className="w-4 h-4" />} Oluştur
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
