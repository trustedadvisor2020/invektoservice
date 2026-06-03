import { useCallback, useEffect, useMemo, useState } from 'react';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import {
  Download, FileSpreadsheet, FileText, FileType2, Loader2, ListPlus,
  Filter as FilterIcon, Database, X, AlertTriangle, Info, RotateCw, History,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Select } from '../components/ui/Select';
import { Input } from '../components/ui/Input';
import {
  api, ApiClientError,
  type ExportFilter, type ExportFilterOptions, type FilteredCount,
  type ExportLogEntry, type SendReportData,
} from '../lib/api';

// =============================================================
// FEAT-OBI Phase 1B — Export Manager v2 (filter-driven recipients surface)
// One surface over ALL of the tenant's bulk-send recipients: filter by
// Şablon / Kampanya / Data Listesi (membership) / Teslim Durumu / Tarih →
// live count card (benzersiz numara + toplam kayıt) → CSV / Excel / Liste
// Oluştur (a new data_list source='export') → Export Geçmişi (export_logs).
// A separate section keeps Plan B's per-campaign PDF report (jsPDF in-browser).
// =============================================================

const FEATURE_DISABLED = 'INV-OB-056';

// jsPDF built-in fonts can't render Turkish glyphs; transliterate to ASCII so a
// forwarded report stays readable (documented v1 limitation, same as Plan B).
const TR_MAP: Record<string, string> = {
  'ş': 's', 'Ş': 'S', 'ğ': 'g', 'Ğ': 'G', 'ı': 'i', 'İ': 'I',
  'ç': 'c', 'Ç': 'C', 'ö': 'o', 'Ö': 'O', 'ü': 'u', 'Ü': 'U',
};
const tr = (s: string | null | undefined): string =>
  (s ?? '').replace(/[şŞğĞıİçÇöÖüÜ]/g, (c) => TR_MAP[c] ?? c);

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
};

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
  const [busy, setBusy] = useState<string | null>(null); // 'csv' | 'xlsx' | 'create' | 'pdf'

  // Liste Oluştur modal
  const [modalOpen, setModalOpen] = useState(false);
  const [listName, setListName] = useState('');
  const [modalError, setModalError] = useState<string | null>(null);

  // PDF section
  const [pdfJobId, setPdfJobId] = useState<string>('');

  function failMessage(e: unknown): string {
    if (e instanceof ApiClientError) {
      if (e.errorCode === FEATURE_DISABLED) return 'Dışa aktarma bu hesap için aktif değil.';
      if (e.errorCode === 'INV-OB-058') return 'Excel için veri çok büyük. CSV formatını kullanın (CSV tüm satırları içerir).';
      if (e.errorCode === 'INV-OB-059') return 'Bu isimde bir liste zaten var. Farklı bir ad girin.';
      if (e.errorCode === 'INV-OB-060') return 'Filtre geçersiz. Tarih aralığını kontrol edin.';
      if (e.errorCode === 'INV-OB-061') return 'Filtreye uyan gönderilebilir numara yok; liste oluşturulamadı.';
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

  async function exportPdf() {
    const jobId = Number(pdfJobId);
    if (!jobId) { setError('Önce bir kampanya seçin.'); return; }
    setBusy('pdf');
    setError(null);
    try {
      const data: SendReportData = await api.getSendReportData(jobId);
      const s = data.summary;
      const doc = new jsPDF();
      doc.setFontSize(16);
      doc.text(tr(`Kampanya Raporu: ${data.campaign_id}`), 14, 18);
      doc.setFontSize(10);
      const lines = [
        `Sablon: ${tr(s.template_name) || s.template_id}`,
        `Durum: ${tr(s.status)}`,
        `Toplam alici: ${s.total_recipients}`,
        `Gonderildi: ${s.sent}   Teslim: ${s.delivered}   Okundu: ${s.read}`,
        `Basarisiz: ${s.failed}   Engellendi: ${s.blocked}   Gonderilmedi: ${s.not_sent}`,
        `Olusturulma: ${fmtDate(s.created_at)}`,
      ];
      let y = 28;
      for (const line of lines) { doc.text(tr(line), 14, y); y += 6; }
      autoTable(doc, {
        startY: y + 2,
        head: [['Telefon', 'Durum', 'Gonderildi', 'Teslim', 'Okundu']],
        body: data.recipients.map((r) => [
          r.phone, tr(r.status_label), fmtDate(r.sent_at), fmtDate(r.delivered_at), fmtDate(r.read_at),
        ]),
        styles: { fontSize: 8 },
        headStyles: { fillColor: [30, 41, 59] },
      });
      if (data.recipient_table_truncated) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const finalY = (doc as any).lastAutoTable?.finalY ?? y;
        doc.setFontSize(8);
        doc.text(
          tr(`Not: ilk ${data.recipient_table_limit} / ${data.total_recipient_count} alici gosteriliyor — tam liste icin CSV/Excel kullanin.`),
          14, finalY + 8,
        );
      }
      const slug = data.campaign_id.replace(/[^a-zA-Z0-9]/g, '_').slice(0, 40) || 'kampanya';
      doc.save(`kampanya_${slug}_rapor.pdf`);
      await refreshHistory();
    } catch (e) {
      setError(failMessage(e));
    } finally {
      setBusy(null);
    }
  }

  // Active-filter chips (label resolution from the loaded options).
  const chips = useMemo(() => {
    const c: { key: string; label: string; clear: () => void }[] = [];
    if (filter.templateId != null) {
      const t = options?.templates.find((o) => o.id === filter.templateId);
      c.push({ key: 'tpl', label: `Şablon: ${t?.label ?? filter.templateId}`, clear: () => patchFilter({ templateId: null }) });
    }
    if (filter.jobId != null) {
      const j = options?.campaigns.find((o) => o.id === filter.jobId);
      c.push({ key: 'job', label: `Kampanya: ${j?.label ?? filter.jobId}`, clear: () => patchFilter({ jobId: null }) });
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
                label="Kampanya"
                value={filter.jobId != null ? String(filter.jobId) : ''}
                options={opt(options?.campaigns, 'Tüm kampanyalar')}
                onChange={(e) => patchFilter({ jobId: e.target.value ? Number(e.target.value) : null })}
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

          {/* ── Kampanya Raporu (PDF) ── */}
          <section className="bg-white border border-navy-100 rounded-xl shadow-soft p-4 space-y-3">
            <div className="flex items-center gap-2">
              <FileType2 className="w-4 h-4 text-brand-500" />
              <h2 className="text-sm font-medium text-navy-700">Kampanya Raporu (PDF)</h2>
            </div>
            <p className="text-xs text-navy-400">Tek kampanyanın özet + alıcı raporunu PDF olarak indirin (özet kart + ilk {2000} alıcı).</p>
            <div className="flex items-end gap-3">
              <div className="flex-1 max-w-sm">
                <Select
                  label="Kampanya"
                  value={pdfJobId}
                  options={opt(options?.campaigns, 'Kampanya seçin…')}
                  onChange={(e) => setPdfJobId(e.target.value)}
                />
              </div>
              <Button variant="primary" disabled={busy !== null || !pdfJobId} onClick={exportPdf}>
                {busy === 'pdf' ? <Loader2 className="w-4 h-4 animate-spin" /> : <FileType2 className="w-4 h-4" />} PDF İndir
              </Button>
            </div>
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
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
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
