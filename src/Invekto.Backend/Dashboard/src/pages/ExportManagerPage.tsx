import { useEffect, useState } from 'react';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import {
  Download, FileSpreadsheet, FileText, FileType2, Loader2,
  List as ListIcon, Send, AlertTriangle, Info,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import {
  api, ApiClientError,
  type DataListSummary, type SendJobSummary,
} from '../lib/api';

// =============================================================
// FEAT-OBI Phase 1A Plan B — ExportManagerPage
// One SPA surface to export (1) contact lists and (2) bulk-send campaign
// results as CSV / Excel / PDF. CSV + XLSX are produced + streamed by the
// Outbound service (with an export_logs audit row); PDF is rendered HERE in
// the browser (jsPDF) from a server report-data JSON endpoint that ALSO writes
// an export_logs row — so the KVKK audit trail is identical across all 3.
// =============================================================

const FEATURE_DISABLED = 'INV-OB-056';

// jsPDF's built-in fonts can't render Turkish glyphs; transliterate to ASCII so
// a forwarded report is readable rather than garbled (documented v1 limitation).
const TR_MAP: Record<string, string> = {
  'ş': 's', 'Ş': 'S', 'ğ': 'g', 'Ğ': 'G', 'ı': 'i', 'İ': 'I',
  'ç': 'c', 'Ç': 'C', 'ö': 'o', 'Ö': 'O', 'ü': 'u', 'Ü': 'U',
};
const tr = (s: string | null | undefined): string =>
  (s ?? '').replace(/[şŞğĞıİçÇöÖüÜ]/g, (c) => TR_MAP[c] ?? c);

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

export function ExportManagerPage() {
  const [lists, setLists] = useState<DataListSummary[]>([]);
  const [jobs, setJobs] = useState<SendJobSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [featureDisabled, setFeatureDisabled] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null); // "<kind>:<id>:<fmt>" while downloading

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const [listResult, jobResult] = await Promise.all([
          api.listDataLists().catch((e) => { throw e; }),
          api.listSendJobs(),
        ]);
        if (cancelled) return;
        setLists(listResult);
        setJobs(jobResult);
      } catch (e) {
        if (cancelled) return;
        if (e instanceof ApiClientError && e.errorCode === FEATURE_DISABLED) {
          setFeatureDisabled(true);
        } else {
          setError(e instanceof ApiClientError ? e.message : 'Veriler yüklenemedi.');
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  function failFromError(e: unknown): string {
    if (e instanceof ApiClientError) {
      if (e.errorCode === FEATURE_DISABLED) return 'Dışa aktarma bu hesap için aktif değil.';
      if (e.errorCode === 'INV-OB-058') return 'Excel için veri çok büyük. CSV formatını kullanın (CSV tüm satırları içerir).';
      if (e.errorCode === 'INV-OB-055') return 'Kaynak bulunamadı.';
      return e.message;
    }
    return 'Dışa aktarma başarısız oldu.';
  }

  async function exportList(list: DataListSummary, format: 'csv' | 'xlsx') {
    const key = `list:${list.id}:${format}`;
    setBusy(key);
    setError(null);
    try {
      const { blob, filename } = await api.downloadContactListExport(list.id, format);
      saveBlob(blob, filename);
    } catch (e) {
      setError(failFromError(e));
    } finally {
      setBusy(null);
    }
  }

  async function exportRecipients(job: SendJobSummary, format: 'csv' | 'xlsx') {
    const key = `job:${job.id}:${format}`;
    setBusy(key);
    setError(null);
    try {
      const { blob, filename } = await api.downloadSendRecipientsExport(job.id, format);
      saveBlob(blob, filename);
    } catch (e) {
      setError(failFromError(e));
    } finally {
      setBusy(null);
    }
  }

  async function exportReportPdf(job: SendJobSummary) {
    const key = `job:${job.id}:pdf`;
    setBusy(key);
    setError(null);
    try {
      const data = await api.getSendReportData(job.id);
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
        `Opt-out atlanan: ${s.skipped_optout}   Onay atlanan: ${s.skipped_consent}`,
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
    } catch (e) {
      setError(failFromError(e));
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="p-6 max-w-6xl mx-auto space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-navy-900">Veri Dışa Aktarma</h1>
        <p className="text-sm text-navy-500 mt-1">
          Kişi listelerinizi ve kampanya sonuçlarınızı CSV, Excel ya da PDF olarak indirin.
          Her dışa aktarma KVKK denetim kaydına işlenir.
        </p>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2.5 text-sm text-red-700 flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0" /> {error}
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
          {/* ── Contact lists ── */}
          <section className="bg-white border border-navy-100 rounded-xl shadow-soft overflow-hidden">
            <div className="px-4 py-3 border-b border-navy-50 flex items-center gap-2">
              <ListIcon className="w-4 h-4 text-brand-500" />
              <h2 className="text-sm font-medium text-navy-700">Kişi Listeleri</h2>
            </div>
            {lists.length === 0 ? (
              <div className="px-4 py-10 text-center text-navy-400 text-sm">Henüz kişi listesi yok.</div>
            ) : (
              <table className="w-full text-sm">
                <thead className="bg-navy-50/50 text-navy-500 text-xs">
                  <tr>
                    <th className="text-left font-medium px-4 py-2">Liste</th>
                    <th className="text-right font-medium px-4 py-2">Toplam</th>
                    <th className="text-right font-medium px-4 py-2">Dışa Aktar</th>
                  </tr>
                </thead>
                <tbody>
                  {lists.map((l) => (
                    <tr key={l.id} className="border-t border-navy-50">
                      <td className="px-4 py-2.5 text-navy-900">{l.name}</td>
                      <td className="px-4 py-2.5 text-right text-navy-600">{l.total_records.toLocaleString('tr-TR')}</td>
                      <td className="px-4 py-2.5">
                        <div className="flex items-center justify-end gap-2">
                          <Button size="sm" variant="secondary" disabled={busy !== null}
                            onClick={() => exportList(l, 'csv')}>
                            {busy === `list:${l.id}:csv`
                              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              : <FileText className="w-3.5 h-3.5" />} CSV
                          </Button>
                          <Button size="sm" variant="secondary" disabled={busy !== null}
                            onClick={() => exportList(l, 'xlsx')}>
                            {busy === `list:${l.id}:xlsx`
                              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              : <FileSpreadsheet className="w-3.5 h-3.5" />} Excel
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          {/* ── Bulk-send campaigns ── */}
          <section className="bg-white border border-navy-100 rounded-xl shadow-soft overflow-hidden">
            <div className="px-4 py-3 border-b border-navy-50 flex items-center gap-2">
              <Send className="w-4 h-4 text-brand-500" />
              <h2 className="text-sm font-medium text-navy-700">Kampanyalar</h2>
            </div>
            {jobs.length === 0 ? (
              <div className="px-4 py-10 text-center text-navy-400 text-sm">Henüz toplu gönderim kampanyası yok.</div>
            ) : (
              <table className="w-full text-sm">
                <thead className="bg-navy-50/50 text-navy-500 text-xs">
                  <tr>
                    <th className="text-left font-medium px-4 py-2">Kampanya</th>
                    <th className="text-left font-medium px-4 py-2">Durum</th>
                    <th className="text-right font-medium px-4 py-2">Alıcı</th>
                    <th className="text-left font-medium px-4 py-2">Tarih</th>
                    <th className="text-right font-medium px-4 py-2">Dışa Aktar</th>
                  </tr>
                </thead>
                <tbody>
                  {jobs.map((j) => (
                    <tr key={j.id} className="border-t border-navy-50">
                      <td className="px-4 py-2.5 text-navy-900">{j.campaign_id}</td>
                      <td className="px-4 py-2.5 text-navy-600">{j.status}</td>
                      <td className="px-4 py-2.5 text-right text-navy-600">{j.total_recipients.toLocaleString('tr-TR')}</td>
                      <td className="px-4 py-2.5 text-navy-500">{fmtDate(j.created_at)}</td>
                      <td className="px-4 py-2.5">
                        <div className="flex items-center justify-end gap-2">
                          <Button size="sm" variant="secondary" disabled={busy !== null}
                            onClick={() => exportRecipients(j, 'csv')}>
                            {busy === `job:${j.id}:csv`
                              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              : <FileText className="w-3.5 h-3.5" />} CSV
                          </Button>
                          <Button size="sm" variant="secondary" disabled={busy !== null}
                            onClick={() => exportRecipients(j, 'xlsx')}>
                            {busy === `job:${j.id}:xlsx`
                              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              : <FileSpreadsheet className="w-3.5 h-3.5" />} Excel
                          </Button>
                          <Button size="sm" variant="primary" disabled={busy !== null}
                            onClick={() => exportReportPdf(j)}>
                            {busy === `job:${j.id}:pdf`
                              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              : <FileType2 className="w-3.5 h-3.5" />} PDF
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          <p className="text-xs text-navy-400 flex items-center gap-1.5">
            <Download className="w-3.5 h-3.5" />
            PDF raporu özet kart + ilk 2.000 alıcıyı içerir; tam liste için CSV/Excel kullanın.
          </p>
        </>
      )}
    </div>
  );
}
