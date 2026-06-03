import { useEffect, useMemo, useRef, useState } from 'react';
import * as XLSX from 'xlsx';
import {
  Upload, FileSpreadsheet, X, AlertTriangle, CheckCircle2, Loader2,
  Trash2, Send, Download, ChevronDown, Info, List as ListIcon,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import { cn } from '../lib/utils';
import {
  api, ApiClientError,
  type DataListSummary, type ImportScenario, type ImportRowDto,
  type ImportBatchRequest, type OutboundTemplateDto, type BulkSendStatusResponse,
} from '../lib/api';

// =============================================================
// FEAT-OBI Phase 1A — DataImportPage
// 3-step wizard (upload -> map + scenario -> summary + execute) feeding the
// tenant-scoped contact-list layer in the Outbound service, plus a list ->
// bulk-send "Gönder" flow (preview-from-list -> confirm -> status).
//
// CSV + Excel are parsed CLIENT-SIDE via hardened SheetJS; the server is the
// authority for phone normalization, dedup and the import scenario. Counts shown
// before execute are a best-effort preview so the operator commits with eyes open.
// =============================================================

// ---- Hardened SheetJS bounds (AC6) ----
const MAX_FILE_BYTES = 15 * 1024 * 1024; // 15 MB — reject oversized uploads before parse.
const MAX_INPUT_ROWS = 50_000;           // mirrors ContactListOptions.MaxImportRows (NO auto-partition).
const EXISTS_PROBE_BATCH = 20_000;       // <= ContactListOptions.MaxExistsProbe per request.
const PREVIEW_ROWS = 10;
const IMPORT_JOBS_KEY = 'invekto_import_jobs';

type WizardStep = 1 | 2 | 3;

type ParsedRow = Record<string, string>;

interface FieldMapping {
  phone: string | null;
  name: string | null;
  surname: string | null;
  email: string | null;
  tags: string | null;
  note: string | null;
  field1: string | null;
  field2: string | null;
  field3: string | null;
  field4: string | null;
  field5: string | null;
}

const EMPTY_MAPPING: FieldMapping = {
  phone: null, name: null, surname: null, email: null, tags: null, note: null,
  field1: null, field2: null, field3: null, field4: null, field5: null,
};

interface CustomFlags {
  setDuplicatesSendable: boolean;
  updateDuplicateFields: boolean;
}

interface ImportSummary {
  total: number;
  invalid: number;
  fileDup: number;
  dbExists: number;
  toInsert: number;
  toUpdate: number;
}

interface ImportJob {
  id: number;
  fileName: string;
  listName: string;
  inserted: number;
  updated: number;
  skipped: number;
  invalid: number;
  finishedAt: string;
}

// ---- Phone normalizer (PREVIEW ONLY) ----
// Mirrors Invekto.Outbound PhoneNormalizer (intl-safe). The server re-normalizes
// authoritatively on import; this only drives the operator-facing preview counts.
function normalizePhonePreview(raw: string | null | undefined): string | null {
  if (!raw) return null;
  const trimmed = String(raw).trim();
  if (!trimmed) return null;
  const hadPlus = trimmed.startsWith('+');
  let digits = trimmed.replace(/\D+/g, '');
  if (!digits) return null;
  if (!hadPlus && digits.startsWith('00')) digits = digits.slice(2);
  if (!digits) return null;
  const guard = (d: string): string | null => (d.length < 8 || d.length > 15 ? null : `+${d}`);
  if (hadPlus) return guard(digits);
  if (digits.length === 12 && digits.startsWith('90')) return guard(digits);
  if (digits.length === 11 && digits.startsWith('0')) return guard('90' + digits.slice(1));
  if (digits.length === 10 && digits.startsWith('5')) return guard('90' + digits);
  return guard(digits);
}

function normalizeHeader(h: string): string {
  return String(h || '').toLowerCase().replace(/[^a-z0-9]+/g, '');
}

function guessPhoneColumn(headers: string[]): string | null {
  const candidates = ['phone', 'phonenumber', 'telefon', 'telefonno', 'tel', 'gsm', 'mobile', 'mobil', 'cep', 'msisdn'];
  const normalized = headers.map(h => ({ raw: h, norm: normalizeHeader(h) }));
  for (const c of candidates) {
    const hit = normalized.find(x => x.norm === c || x.norm.includes(c));
    if (hit) return hit.raw;
  }
  return null;
}

// CSV formula-injection escape — applied when we re-emit values to a downloadable
// CSV (the invalid-rows report). A leading = + - @ (or tab/CR) is neutralized with
// a leading apostrophe so spreadsheet apps never treat the cell as a formula.
function csvCell(value: string): string {
  let v = value ?? '';
  if (/^[=+\-@\t\r]/.test(v)) v = `'${v}`;
  if (/[",\n]/.test(v)) v = `"${v.replace(/"/g, '""')}"`;
  return v;
}

function errText(e: unknown, fallback: string): string {
  if (e instanceof ApiClientError) return `${e.errorCode}: ${e.message}`;
  if (e instanceof Error) return e.message;
  return fallback;
}

// Module-level so it keeps a stable component identity across parent re-renders
// (a nested component would remount the <select> on every keystroke).
function MapSelect({
  label, value, headers, onChange, required,
}: {
  label: string;
  value: string | null;
  headers: string[];
  onChange: (v: string | null) => void;
  required?: boolean;
}) {
  return (
    <label className="block text-sm">
      <span className="block text-xs text-navy-500 mb-1">
        {label}{required && <span className="text-red-500"> *</span>}
      </span>
      <select
        value={value ?? ''}
        onChange={e => onChange(e.target.value || null)}
        className="w-full h-9 px-2 bg-white border border-navy-100 rounded-lg text-sm text-navy-900 focus:outline-none focus:border-brand-500"
      >
        <option value="">— Seç —</option>
        {headers.map(h => <option key={h} value={h}>{h}</option>)}
      </select>
    </label>
  );
}

function StatusBadge({ status }: { status: DataListSummary['status'] }) {
  const map: Record<DataListSummary['status'], string> = {
    ready: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    importing: 'bg-amber-50 text-amber-700 border-amber-200',
    failed: 'bg-red-50 text-red-700 border-red-200',
  };
  const label: Record<DataListSummary['status'], string> = {
    ready: 'Hazır', importing: 'İçe aktarılıyor', failed: 'Başarısız',
  };
  return (
    <span className={cn('inline-flex items-center px-2 py-0.5 rounded-full text-xs border', map[status])}>
      {label[status]}
    </span>
  );
}

export function DataImportPage() {
  // ---- Lists overview ----
  const [lists, setLists] = useState<DataListSummary[]>([]);
  const [loadingLists, setLoadingLists] = useState(false);
  const [listsError, setListsError] = useState<string | null>(null);
  const [featureDisabled, setFeatureDisabled] = useState(false);

  // ---- Wizard ----
  const [wizardOpen, setWizardOpen] = useState(false);
  const [step, setStep] = useState<WizardStep>(1);
  const [fileName, setFileName] = useState('');
  const [headers, setHeaders] = useState<string[]>([]);
  const [rows, setRows] = useState<ParsedRow[]>([]);
  const [uploadErr, setUploadErr] = useState<string | null>(null);
  const [multiSheetNote, setMultiSheetNote] = useState(false);
  const [dragActive, setDragActive] = useState(false);

  const [mapping, setMapping] = useState<FieldMapping>(EMPTY_MAPPING);
  const [targetListId, setTargetListId] = useState<number | ''>('');
  const [newListName, setNewListName] = useState('');
  const [scenario, setScenario] = useState<ImportScenario>('new_only');
  const [custom, setCustom] = useState<CustomFlags>({ setDuplicatesSendable: false, updateDuplicateFields: false });

  const [summary, setSummary] = useState<ImportSummary | null>(null);
  const [dbProbeWarn, setDbProbeWarn] = useState(false);
  const [calculating, setCalculating] = useState(false);
  const [processing, setProcessing] = useState(false);
  const [resultMsg, setResultMsg] = useState<string | null>(null);
  const [importErr, setImportErr] = useState<string | null>(null);
  const [invalidSamples, setInvalidSamples] = useState<string[]>([]);

  const [jobs, setJobs] = useState<ImportJob[]>(() => {
    try { return JSON.parse(localStorage.getItem(IMPORT_JOBS_KEY) || '[]'); }
    catch (err) {
      console.warn('[DataImport] import job history could not be parsed; starting empty:', err);
      return [];
    }
  });

  // ---- Send flow ----
  const [sendList, setSendList] = useState<DataListSummary | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadLists = async () => {
    setLoadingLists(true);
    setListsError(null);
    setFeatureDisabled(false);
    try {
      setLists(await api.listDataLists());
    } catch (e) {
      setLists([]);
      // 403 = the tenant isn't enabled for contact lists (feature gate / plan), not a transient
      // error — show a calm "not enabled" state instead of a red retryable error.
      if (e instanceof ApiClientError && e.status === 403) {
        setFeatureDisabled(true);
      } else {
        setListsError(errText(e, 'Listeler yüklenemedi'));
      }
    } finally {
      setLoadingLists(false);
    }
  };

  useEffect(() => { void loadLists(); }, []);

  const preview = useMemo(() => rows.slice(0, PREVIEW_ROWS), [rows]);

  function resetWizard() {
    setStep(1);
    setFileName('');
    setHeaders([]);
    setRows([]);
    setUploadErr(null);
    setMultiSheetNote(false);
    setMapping(EMPTY_MAPPING);
    setTargetListId('');
    setNewListName('');
    setScenario('new_only');
    setCustom({ setDuplicatesSendable: false, updateDuplicateFields: false });
    setSummary(null);
    setResultMsg(null);
    setImportErr(null);
    setInvalidSamples([]);
  }

  function openWizard() {
    resetWizard();
    setWizardOpen(true);
  }
  function closeWizard() {
    setWizardOpen(false);
    resetWizard();
  }

  async function handleFile(file: File) {
    setUploadErr(null);
    setMultiSheetNote(false);
    const lower = file.name.toLowerCase();
    if (!/\.(xlsx|xls|csv)$/.test(lower)) {
      setUploadErr('Yalnızca .xlsx, .xls veya .csv dosyaları desteklenir.');
      return;
    }
    if (file.size > MAX_FILE_BYTES) {
      setUploadErr(`Dosya çok büyük (${(file.size / 1048576).toFixed(1)} MB). En fazla ${MAX_FILE_BYTES / 1048576} MB.`);
      return;
    }
    try {
      const buf = await file.arrayBuffer();
      // Hardened parse: never read/eval formulas or embedded HTML; values as text.
      // CSV: decode bytes as UTF-8 ourselves (TextDecoder strips a BOM and handles Turkish
      // characters); SheetJS otherwise guesses Windows-1252 for a BOM-less CSV and mojibakes
      // "Yılmaz" -> "YÄ±lmaz". Excel (.xlsx/.xls) stores text as UTF-8 internally, so the binary
      // path is correct for those.
      const isCsv = lower.endsWith('.csv');
      const wb = isCsv
        ? XLSX.read(new TextDecoder('utf-8').decode(buf), { type: 'string', cellFormula: false, cellHTML: false, cellDates: false })
        : XLSX.read(buf, { type: 'array', cellFormula: false, cellHTML: false, cellDates: false });
      if (!wb.SheetNames.length) { setUploadErr('Dosyada sayfa bulunamadı.'); return; }
      if (wb.SheetNames.length > 1) setMultiSheetNote(true); // single-sheet only: use the first.
      const sheet = wb.Sheets[wb.SheetNames[0]];
      const aoa = XLSX.utils.sheet_to_json<string[]>(sheet, { header: 1, blankrows: false, raw: false, defval: '' });
      if (!aoa.length) { setUploadErr('Dosya boş görünüyor.'); return; }

      const head = (aoa[0] as unknown[]).map((h, i) => String(h ?? '').trim() || `col_${i + 1}`);
      const body = aoa.slice(1).filter(r => Array.isArray(r) && r.some(c => String(c ?? '').trim() !== ''));

      if (body.length > MAX_INPUT_ROWS) {
        setUploadErr(
          `Dosyada ${body.length.toLocaleString('tr-TR')} satır var. Tek seferde en fazla ` +
          `${MAX_INPUT_ROWS.toLocaleString('tr-TR')} satır içe aktarılabilir (otomatik bölme yok). ` +
          `Lütfen dosyayı küçültüp tekrar deneyin.`
        );
        return;
      }

      const parsed: ParsedRow[] = body.map(r => {
        const obj: ParsedRow = {};
        for (let i = 0; i < head.length; i++) obj[head[i]] = String((r as unknown[])[i] ?? '').trim();
        return obj;
      });

      setHeaders(head);
      setRows(parsed);
      setFileName(file.name);
      const guessed = guessPhoneColumn(head);
      setMapping({ ...EMPTY_MAPPING, phone: guessed });
    } catch (e) {
      setUploadErr(errText(e, 'Dosya okunamadı. Geçerli bir CSV/Excel dosyası mı?'));
    }
  }

  function onDrop(e: React.DragEvent) {
    e.preventDefault();
    setDragActive(false);
    const f = e.dataTransfer?.files?.[0];
    if (f) void handleFile(f);
  }

  // Build the field-mapped rows the server expects (phone is normalized server-side).
  function buildPayloadRows(): ImportRowDto[] {
    if (!mapping.phone) return [];
    const phoneCol = mapping.phone;
    const pick = (row: ParsedRow, col: string | null): string | undefined => {
      if (!col) return undefined;
      const v = row[col];
      return v ? v : undefined;
    };
    return rows.map(row => ({
      phone: row[phoneCol] || undefined,
      name: pick(row, mapping.name),
      surname: pick(row, mapping.surname),
      email: pick(row, mapping.email),
      tags: pick(row, mapping.tags),
      note: pick(row, mapping.note),
      field1: pick(row, mapping.field1),
      field2: pick(row, mapping.field2),
      field3: pick(row, mapping.field3),
      field4: pick(row, mapping.field4),
      field5: pick(row, mapping.field5),
    }));
  }

  // Best-effort preview: normalize client-side, dedup within file, probe the target
  // list for already-existing phones, then project insert/update per scenario.
  async function computeSummary() {
    if (!mapping.phone) return;
    setCalculating(true);
    setSummary(null);
    setDbProbeWarn(false);
    setImportErr(null);
    try {
      const phoneCol = mapping.phone;
      const seen = new Set<string>();
      let invalid = 0;
      let fileDup = 0;
      const uniquePhones: string[] = [];
      for (const row of rows) {
        const n = normalizePhonePreview(row[phoneCol]);
        if (!n) { invalid++; continue; }
        if (seen.has(n)) { fileDup++; continue; }
        seen.add(n);
        uniquePhones.push(n);
      }

      // dedup against the TARGET list only (server dedup key is (list_id, phone)).
      let dbExists = 0;
      if (typeof targetListId === 'number') {
        try {
          for (let i = 0; i < uniquePhones.length; i += EXISTS_PROBE_BATCH) {
            const batch = uniquePhones.slice(i, i + EXISTS_PROBE_BATCH);
            const res = await api.dataListRecordsExist(batch, targetListId);
            dbExists += res.exists.length;
          }
        } catch (e) {
          // Non-fatal: dedup preview may be incomplete; server still enforces uniqueness on import.
          // Surface it so the operator knows the "Listede Var" count is unreliable, not silently zero.
          console.warn('[DataImport] records/exists probe failed:', e);
          setDbProbeWarn(true);
        }
      }

      const valid = rows.length - invalid - fileDup;
      const fresh = Math.max(0, valid - dbExists);
      let toInsert = 0;
      let toUpdate = 0;
      switch (scenario) {
        case 'new_only': toInsert = fresh; break;
        case 'recall_all': toInsert = fresh; toUpdate = dbExists; break;
        case 'update_info': toInsert = 0; toUpdate = dbExists; break;
        case 'custom':
          toInsert = fresh;
          toUpdate = (custom.setDuplicatesSendable || custom.updateDuplicateFields) ? dbExists : 0;
          break;
      }
      setSummary({ total: rows.length, invalid, fileDup, dbExists, toInsert, toUpdate });
    } finally {
      setCalculating(false);
    }
  }

  // Recompute the preview whenever inputs change on step 3.
  useEffect(() => {
    if (step !== 3 || !mapping.phone || rows.length === 0) return;
    void computeSummary();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [step, mapping.phone, targetListId, scenario, custom.setDuplicatesSendable, custom.updateDuplicateFields]);

  const targetValid = typeof targetListId === 'number' || newListName.trim().length > 0;

  async function submitImport() {
    if (!mapping.phone || !targetValid) return;
    setProcessing(true);
    setResultMsg(null);
    setImportErr(null);
    setInvalidSamples([]);
    try {
      const req: ImportBatchRequest = {
        list_id: typeof targetListId === 'number' ? targetListId : null,
        new_list_name: typeof targetListId === 'number' ? null : newListName.trim(),
        scenario,
        custom: scenario === 'custom'
          ? { set_duplicates_sendable: custom.setDuplicatesSendable, update_duplicate_fields: custom.updateDuplicateFields }
          : undefined,
        rows: buildPayloadRows(),
      };
      const res = await api.importDataList(req);
      setInvalidSamples(res.invalid_samples ?? []);
      setResultMsg(
        `Tamamlandı — ${res.inserted.toLocaleString('tr-TR')} eklendi, ` +
        `${res.updated.toLocaleString('tr-TR')} güncellendi, ` +
        `${res.skipped_existing.toLocaleString('tr-TR')} atlandı, ` +
        `${res.invalid.toLocaleString('tr-TR')} geçersiz.`
      );
      const job: ImportJob = {
        id: Date.now(),
        fileName,
        listName: res.list_name,
        inserted: res.inserted,
        updated: res.updated,
        skipped: res.skipped_existing,
        invalid: res.invalid,
        finishedAt: new Date().toISOString(),
      };
      setJobs(prev => {
        const next = [job, ...prev].slice(0, 20);
        localStorage.setItem(IMPORT_JOBS_KEY, JSON.stringify(next));
        return next;
      });
      await loadLists();
    } catch (e) {
      setImportErr(errText(e, 'İçe aktarma başarısız oldu.'));
    } finally {
      setProcessing(false);
    }
  }

  function downloadInvalidRows() {
    if (!mapping.phone || invalidSamples.length === 0) return;
    const lines = ['phone (gecersiz)', ...invalidSamples.map(csvCell)];
    const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'gecersiz-numaralar.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  async function deleteList(list: DataListSummary) {
    if (!window.confirm(`"${list.name}" listesi silinsin mi? Bu işlem geri alınamaz.`)) return;
    try {
      await api.deleteDataList(list.id);
      await loadLists();
    } catch (e) {
      setListsError(errText(e, 'Liste silinemedi'));
    }
  }

  const setMap = (field: keyof FieldMapping) => (v: string | null) => setMapping(p => ({ ...p, [field]: v }));

  const optionalFields: { label: string; field: keyof FieldMapping }[] = [
    { label: 'Ad', field: 'name' },
    { label: 'Soyad', field: 'surname' },
    { label: 'E-posta', field: 'email' },
    { label: 'Etiketler (virgüllü)', field: 'tags' },
    { label: 'Not', field: 'note' },
    { label: 'Alan-1', field: 'field1' },
    { label: 'Alan-2', field: 'field2' },
    { label: 'Alan-3', field: 'field3' },
    { label: 'Alan-4', field: 'field4' },
    { label: 'Alan-5', field: 'field5' },
  ];

  const scenarioCards: { key: ImportScenario; title: string; desc: string }[] = [
    { key: 'new_only', title: 'Sadece yeni numaraları ekle', desc: 'Listede olan kayıtlara dokunma; yalnızca yeni numaraları ekle.' },
    { key: 'recall_all', title: 'Tümünü yeniden gönderilebilir yap', desc: 'Mevcut kayıtları yeniden gönderilebilir yap ve bilgilerini güncelle.' },
    { key: 'update_info', title: 'Sadece bilgileri güncelle', desc: 'Ad, e-posta gibi alanları güncelle; gönderilebilir durumunu değiştirme, yeni ekleme.' },
    { key: 'custom', title: 'Özel ayarlar', desc: 'Mevcut kayıtlar için işlemleri kendin seç.' },
  ];

  return (
    <div className="p-6 max-w-6xl mx-auto space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-navy-900">Veri İçe Aktarma</h1>
          <p className="text-sm text-navy-500 mt-0.5">CSV/Excel yükleyin, kişi listeleri oluşturun ve toplu gönderime besleyin.</p>
        </div>
        {!wizardOpen && (
          <Button onClick={openWizard}>
            <Upload className="w-4 h-4" /> Veri İçe Aktar
          </Button>
        )}
      </div>

      {/* ---- Lists overview ---- */}
      {!wizardOpen && (
        <div className="bg-white border border-navy-100 rounded-xl shadow-soft overflow-hidden">
          <div className="px-4 py-3 border-b border-navy-50 flex items-center gap-2">
            <ListIcon className="w-4 h-4 text-navy-400" />
            <h2 className="text-sm font-medium text-navy-700">Listeler</h2>
          </div>
          {featureDisabled && (
            <div className="px-4 py-10 text-center text-navy-500 text-sm flex flex-col items-center gap-2">
              <Info className="w-5 h-5 text-navy-400" />
              <div className="font-medium">Bu özellik hesabınızda henüz etkin değil.</div>
              <div className="text-xs text-navy-400">Etkinleştirmek için Invekto ekibiyle iletişime geçin.</div>
            </div>
          )}
          {!featureDisabled && listsError && <div className="px-4 py-3 text-sm text-red-600">{listsError}</div>}
          {!featureDisabled && (loadingLists ? (
            <div className="px-4 py-8 text-center text-navy-400 text-sm flex items-center justify-center gap-2">
              <Loader2 className="w-4 h-4 animate-spin" /> Yükleniyor…
            </div>
          ) : lists.length === 0 ? (
            <div className="px-4 py-10 text-center text-navy-400 text-sm">
              Henüz liste yok. “Veri İçe Aktar” ile ilk listenizi oluşturun.
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-navy-50/50 text-navy-500 text-xs">
                <tr>
                  <th className="text-left font-medium px-4 py-2">Liste</th>
                  <th className="text-left font-medium px-4 py-2">Durum</th>
                  <th className="text-right font-medium px-4 py-2">Toplam</th>
                  <th className="text-right font-medium px-4 py-2">Gönderilebilir</th>
                  <th className="text-right font-medium px-4 py-2">İşlem</th>
                </tr>
              </thead>
              <tbody>
                {lists.map(l => (
                  <tr key={l.id} className="border-t border-navy-50">
                    <td className="px-4 py-2.5 text-navy-900">{l.name}</td>
                    <td className="px-4 py-2.5"><StatusBadge status={l.status} /></td>
                    <td className="px-4 py-2.5 text-right tabular-nums">{l.total_records.toLocaleString('tr-TR')}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums">{l.sendable_count.toLocaleString('tr-TR')}</td>
                    <td className="px-4 py-2.5">
                      <div className="flex items-center justify-end gap-1.5">
                        <Button
                          size="sm"
                          variant="secondary"
                          disabled={l.status !== 'ready' || l.sendable_count === 0}
                          onClick={() => setSendList(l)}
                        >
                          <Send className="w-3.5 h-3.5" /> Gönder
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => deleteList(l)} title="Sil">
                          <Trash2 className="w-3.5 h-3.5 text-red-500" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ))}
        </div>
      )}

      {/* ---- Recent jobs ---- */}
      {!wizardOpen && jobs.length > 0 && (
        <div className="bg-white border border-navy-100 rounded-xl shadow-soft p-4">
          <h2 className="text-sm font-medium text-navy-700 mb-2">Son İçe Aktarmalar</h2>
          <ul className="space-y-1.5 text-xs text-navy-600">
            {jobs.slice(0, 5).map(j => (
              <li key={j.id} className="flex items-center justify-between">
                <span className="truncate">{j.listName} · {j.fileName}</span>
                <span className="text-navy-400 tabular-nums">
                  +{j.inserted} / ~{j.updated} / {j.invalid} geçersiz
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* ---- Wizard ---- */}
      {wizardOpen && (
        <div className="bg-white border border-navy-100 rounded-xl shadow-soft relative">
          <button
            onClick={closeWizard}
            className="absolute right-3 top-3 text-navy-300 hover:text-navy-600"
            title="Kapat"
            aria-label="Kapat"
          >
            <X className="w-5 h-5" />
          </button>

          <div className="px-5 pt-5 flex items-center gap-2 text-xs">
            {([1, 2, 3] as WizardStep[]).map((s, i) => (
              <span key={s} className="flex items-center gap-2">
                <span className={cn('px-2.5 py-1 rounded-full', step === s ? 'bg-brand-500 text-white' : 'bg-navy-50 text-navy-500')}>
                  {s}. {s === 1 ? 'Dosya' : s === 2 ? 'Eşleme & Seçenekler' : 'Özet'}
                </span>
                {i < 2 && <ChevronDown className="w-3 h-3 -rotate-90 text-navy-300" />}
              </span>
            ))}
          </div>

          {/* Step 1 — upload */}
          {step === 1 && (
            <div className="p-5 space-y-4">
              {rows.length === 0 ? (
                <div
                  onDragEnter={e => { e.preventDefault(); setDragActive(true); }}
                  onDragOver={e => e.preventDefault()}
                  onDragLeave={e => { e.preventDefault(); setDragActive(false); }}
                  onDrop={onDrop}
                  onClick={() => fileInputRef.current?.click()}
                  className={cn(
                    'border-2 border-dashed rounded-xl p-10 text-center cursor-pointer transition-colors',
                    dragActive ? 'border-brand-500 bg-brand-50' : 'border-navy-200 bg-navy-50/40 hover:border-navy-300'
                  )}
                >
                  <FileSpreadsheet className="w-8 h-8 mx-auto text-navy-300 mb-2" />
                  <div className="text-sm text-navy-700">Dosyayı sürükleyip bırakın veya tıklayın</div>
                  <div className="text-xs text-navy-400 mt-1">.xlsx, .xls veya .csv · en fazla {MAX_FILE_BYTES / 1048576} MB · {MAX_INPUT_ROWS.toLocaleString('tr-TR')} satır</div>
                </div>
              ) : (
                <div className="flex items-center justify-between bg-navy-50/50 rounded-lg px-4 py-3">
                  <div className="flex items-center gap-2 text-sm text-navy-700">
                    <FileSpreadsheet className="w-4 h-4 text-brand-500" />
                    <span className="font-medium">{fileName}</span>
                    <span className="text-navy-400">· {rows.length.toLocaleString('tr-TR')} satır · {headers.length} kolon</span>
                  </div>
                  <Button size="sm" variant="ghost" onClick={() => { setRows([]); setHeaders([]); setFileName(''); }}>
                    Değiştir
                  </Button>
                </div>
              )}
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx,.xls,.csv"
                className="hidden"
                onChange={e => { const f = e.target.files?.[0]; if (f) void handleFile(f); e.target.value = ''; }}
              />

              {uploadErr && (
                <div className="flex items-start gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                  <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" /> <span>{uploadErr}</span>
                </div>
              )}
              {multiSheetNote && rows.length > 0 && (
                <div className="flex items-start gap-2 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                  <Info className="w-4 h-4 mt-0.5 flex-shrink-0" /> Dosyada birden çok sayfa var; yalnızca ilk sayfa içe aktarılır.
                </div>
              )}

              {preview.length > 0 && (
                <div className="overflow-auto border border-navy-100 rounded-lg">
                  <table className="min-w-full text-xs">
                    <thead className="bg-navy-50/50">
                      <tr>{headers.map(h => <th key={h} className="border-b border-navy-100 px-2 py-1.5 text-left text-navy-500 font-medium">{h}</th>)}</tr>
                    </thead>
                    <tbody>
                      {preview.map((r, i) => (
                        <tr key={i} className="odd:bg-white even:bg-navy-50/30">
                          {headers.map(h => <td key={h} className="border-b border-navy-50 px-2 py-1 text-navy-700">{r[h] ?? ''}</td>)}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              <div className="flex justify-end gap-2 pt-1">
                <Button variant="secondary" onClick={closeWizard}>İptal</Button>
                <Button disabled={rows.length === 0} onClick={() => setStep(2)}>İlerle</Button>
              </div>
            </div>
          )}

          {/* Step 2 — mapping + options */}
          {step === 2 && (
            <div className="p-5 grid grid-cols-1 md:grid-cols-2 gap-5">
              <div className="space-y-3">
                <h3 className="text-sm font-medium text-navy-700">Alan Eşleme</h3>
                <MapSelect label="Telefon" value={mapping.phone} headers={headers} onChange={setMap('phone')} required />
                {!mapping.phone && <div className="text-xs text-red-500 -mt-1">Telefon alanı zorunlu.</div>}
                <div className="grid grid-cols-2 gap-3">
                  {optionalFields.map(f => (
                    <MapSelect key={f.field} label={f.label} value={mapping[f.field]} headers={headers} onChange={setMap(f.field)} />
                  ))}
                </div>
              </div>

              <div className="space-y-4">
                <div>
                  <h3 className="text-sm font-medium text-navy-700 mb-2">Hedef Liste</h3>
                  <select
                    value={targetListId === '' ? '' : String(targetListId)}
                    onChange={e => { setTargetListId(e.target.value ? Number(e.target.value) : ''); if (e.target.value) setNewListName(''); }}
                    className="w-full h-9 px-2 bg-white border border-navy-100 rounded-lg text-sm text-navy-900 focus:outline-none focus:border-brand-500"
                  >
                    <option value="">— Yeni liste oluştur —</option>
                    {lists.map(l => <option key={l.id} value={l.id}>{l.name} ({l.total_records.toLocaleString('tr-TR')})</option>)}
                  </select>
                  {targetListId === '' && (
                    <input
                      value={newListName}
                      onChange={e => setNewListName(e.target.value)}
                      placeholder="Yeni liste adı"
                      className="mt-2 w-full h-9 px-3 bg-white border border-navy-100 rounded-lg text-sm text-navy-900 placeholder:text-navy-300 focus:outline-none focus:border-brand-500"
                    />
                  )}
                  {!targetValid && <div className="text-xs text-red-500 mt-1">Bir liste seçin veya yeni liste adı girin.</div>}
                </div>

                <div>
                  <h3 className="text-sm font-medium text-navy-700 mb-2">Mevcut Kayıtlar İçin İşlem</h3>
                  <div className="space-y-2" role="radiogroup" aria-label="İçe aktarma senaryosu">
                    {scenarioCards.map(sc => (
                      <div
                        key={sc.key}
                        role="radio"
                        aria-checked={scenario === sc.key}
                        tabIndex={scenario === sc.key ? 0 : -1}
                        onClick={() => setScenario(sc.key)}
                        onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setScenario(sc.key); } }}
                        className={cn(
                          'p-3 border rounded-lg cursor-pointer transition-all',
                          scenario === sc.key ? 'border-brand-500 bg-brand-50 ring-1 ring-brand-500' : 'border-navy-100 hover:border-navy-200'
                        )}
                      >
                        <div className="flex items-center gap-2">
                          <span className={cn('w-4 h-4 rounded-full border-2 flex items-center justify-center', scenario === sc.key ? 'border-brand-500' : 'border-navy-300')}>
                            {scenario === sc.key && <span className="w-2 h-2 rounded-full bg-brand-500" />}
                          </span>
                          <span className="text-sm font-medium text-navy-800">{sc.title}</span>
                        </div>
                        <p className="text-xs text-navy-500 mt-1 ml-6">{sc.desc}</p>
                        {sc.key === 'custom' && scenario === 'custom' && (
                          <div className="mt-3 ml-6 space-y-2 border-t border-navy-100 pt-3">
                            <label className="flex items-center gap-2 text-xs text-navy-700 cursor-pointer">
                              <input type="checkbox" checked={custom.setDuplicatesSendable}
                                onChange={e => setCustom(p => ({ ...p, setDuplicatesSendable: e.target.checked }))} />
                              Mevcut numaraları yeniden gönderilebilir yap
                            </label>
                            <label className="flex items-center gap-2 text-xs text-navy-700 cursor-pointer">
                              <input type="checkbox" checked={custom.updateDuplicateFields}
                                onChange={e => setCustom(p => ({ ...p, updateDuplicateFields: e.target.checked }))} />
                              Mevcut kayıtların bilgilerini güncelle
                            </label>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              </div>

              <div className="md:col-span-2 flex justify-end gap-2">
                <Button variant="secondary" onClick={() => setStep(1)}>Geri</Button>
                <Button disabled={!mapping.phone || !targetValid} onClick={() => setStep(3)}>İlerle</Button>
              </div>
            </div>
          )}

          {/* Step 3 — summary + execute */}
          {step === 3 && (
            <div className="p-5 space-y-4">
              <h3 className="text-sm font-medium text-navy-700">Özet</h3>
              {calculating ? (
                <div className="flex items-center gap-2 text-sm text-navy-500 py-6">
                  <Loader2 className="w-4 h-4 animate-spin" /> Önizleme hesaplanıyor…
                </div>
              ) : summary && (
                <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                  {[
                    ['Toplam', summary.total],
                    ['Geçersiz', summary.invalid],
                    ['Dosyada Mükerrer', summary.fileDup],
                    ['Listede Var', summary.dbExists],
                    ['Eklenecek', summary.toInsert],
                    ['Güncellenecek', summary.toUpdate],
                  ].map(([label, val]) => (
                    <div key={label as string} className="border border-navy-100 rounded-lg px-3 py-2">
                      <div className="text-xs text-navy-500">{label}</div>
                      <div className="text-lg font-semibold text-navy-900 tabular-nums">{(val as number).toLocaleString('tr-TR')}</div>
                    </div>
                  ))}
                </div>
              )}
              <p className="text-xs text-navy-400">
                Telefon normalizasyonu ve mükerrer ayıklaması sunucu tarafında kesinleşir; yukarıdaki sayılar ön izlemedir.
              </p>
              {dbProbeWarn && (
                <div className="flex items-start gap-2 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                  <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                  <span>“Listede Var” kontrolü yapılamadı; bu sayı eksik olabilir. Sunucu içe aktarmada mükerrerleri yine de ayıklar.</span>
                </div>
              )}

              {resultMsg && (
                <div className="flex items-start gap-2 text-sm text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-2">
                  <CheckCircle2 className="w-4 h-4 mt-0.5 flex-shrink-0" /> <span>{resultMsg}</span>
                </div>
              )}
              {invalidSamples.length > 0 && (
                <div className="text-xs text-navy-600">
                  <div className="flex items-center justify-between mb-1">
                    <span>{invalidSamples.length} geçersiz numara örneği</span>
                    <button onClick={downloadInvalidRows} className="inline-flex items-center gap-1 text-brand-600 hover:text-brand-700">
                      <Download className="w-3.5 h-3.5" /> CSV indir
                    </button>
                  </div>
                  <div className="bg-navy-50 border border-navy-100 rounded-lg p-2 max-h-28 overflow-auto font-mono">
                    {invalidSamples.slice(0, 20).join(', ')}
                  </div>
                </div>
              )}
              {importErr && (
                <div className="flex items-start gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                  <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" /> <span>{importErr}</span>
                </div>
              )}

              <div className="flex justify-end gap-2">
                {resultMsg ? (
                  <Button onClick={closeWizard}>Bitti</Button>
                ) : (
                  <>
                    <Button variant="secondary" disabled={processing} onClick={() => setStep(2)}>Geri</Button>
                    <Button disabled={processing || calculating} onClick={submitImport}>
                      {processing && <Loader2 className="w-4 h-4 animate-spin" />}
                      Onayla ve İçe Aktar
                    </Button>
                  </>
                )}
              </div>
            </div>
          )}
        </div>
      )}

      {sendList && <SendModal list={sendList} onClose={() => setSendList(null)} />}
    </div>
  );
}

// ---------------------------------------------------------------
// Send modal: list -> preview-from-list -> confirm -> status.
// campaign_id is generated once per open (idempotency key for the backend).
// ---------------------------------------------------------------
function SendModal({ list, onClose }: { list: DataListSummary; onClose: () => void }) {
  const [templates, setTemplates] = useState<OutboundTemplateDto[]>([]);
  const [templateId, setTemplateId] = useState<number | ''>('');
  const [loading, setLoading] = useState(true);
  const [campaignId] = useState(() => `list-${list.id}-${Date.now()}`);
  const [phase, setPhase] = useState<'pick' | 'preview' | 'sent'>('pick');
  const [previewing, setPreviewing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [preview, setPreview] = useState<{ total: number; cap: number; sample: string[] } | null>(null);
  const [status, setStatus] = useState<BulkSendStatusResponse | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const res = await api.listOutboundTemplates();
        const active = res.templates.filter(t => t.is_active);
        setTemplates(active);
        if (active.length === 1) setTemplateId(active[0].id);
      } catch (e) {
        setErr(errText(e, 'Şablonlar yüklenemedi'));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  async function runPreview() {
    if (typeof templateId !== 'number') return;
    setPreviewing(true);
    setErr(null);
    try {
      const res = await api.previewFromList({ campaign_id: campaignId, list_id: list.id, template_id: templateId });
      setPreview({ total: res.total_valid, cap: res.hard_cap, sample: res.sample });
      setPhase('preview');
    } catch (e) {
      setErr(errText(e, 'Önizleme oluşturulamadı'));
    } finally {
      setPreviewing(false);
    }
  }

  async function confirmSend() {
    setConfirming(true);
    setErr(null);
    try {
      const res = await api.confirmBulkSend(campaignId);
      setStatus(res);
      setPhase('sent');
    } catch (e) {
      setErr(errText(e, 'Gönderim başlatılamadı'));
    } finally {
      setConfirming(false);
    }
  }

  return (
    <div className="fixed inset-0 bg-black/40 z-50 grid place-items-center p-4">
      <div className="bg-white rounded-xl shadow-soft border border-navy-100 w-[460px] max-w-full overflow-hidden">
        <div className="px-5 pt-5 pb-3 flex items-start justify-between">
          <div>
            <h2 className="text-base font-semibold text-navy-900">Listeye Gönder</h2>
            <p className="text-sm text-navy-500 mt-0.5">{list.name} · {list.sendable_count.toLocaleString('tr-TR')} gönderilebilir</p>
          </div>
          <button onClick={onClose} className="text-navy-300 hover:text-navy-600" aria-label="Kapat"><X className="w-5 h-5" /></button>
        </div>

        <div className="px-5 pb-5 space-y-4">
          {err && (
            <div className="flex items-start gap-2 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
              <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" /> <span>{err}</span>
            </div>
          )}

          {phase === 'pick' && (
            <>
              {loading ? (
                <div className="flex items-center gap-2 text-sm text-navy-500 py-4"><Loader2 className="w-4 h-4 animate-spin" /> Şablonlar yükleniyor…</div>
              ) : templates.length === 0 ? (
                <p className="text-sm text-navy-500">Aktif şablon yok. Önce bir mesaj şablonu oluşturun.</p>
              ) : (
                <label className="block text-sm">
                  <span className="block text-xs text-navy-500 mb-1">Mesaj Şablonu</span>
                  <select
                    value={templateId === '' ? '' : String(templateId)}
                    onChange={e => setTemplateId(e.target.value ? Number(e.target.value) : '')}
                    className="w-full h-9 px-2 bg-white border border-navy-100 rounded-lg text-sm text-navy-900 focus:outline-none focus:border-brand-500"
                  >
                    <option value="">— Şablon seç —</option>
                    {templates.map(t => <option key={t.id} value={t.id}>{t.name} ({t.lang})</option>)}
                  </select>
                </label>
              )}
              <div className="flex justify-end gap-2">
                <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
                <Button disabled={typeof templateId !== 'number' || previewing} onClick={runPreview}>
                  {previewing && <Loader2 className="w-4 h-4 animate-spin" />} Önizle
                </Button>
              </div>
            </>
          )}

          {phase === 'preview' && preview && (
            <>
              <div className="bg-navy-50/60 border border-navy-100 rounded-lg p-3 space-y-2 text-sm">
                <div className="flex justify-between"><span className="text-navy-500">Gönderilecek</span><span className="font-semibold tabular-nums">{preview.total.toLocaleString('tr-TR')}</span></div>
                <div className="flex justify-between"><span className="text-navy-500">Üst sınır</span><span className="tabular-nums">{preview.cap.toLocaleString('tr-TR')}</span></div>
              </div>
              {preview.sample.length > 0 && (
                <div className="text-xs text-navy-500">
                  <div className="mb-1">Örnek alıcılar</div>
                  <div className="bg-navy-50 border border-navy-100 rounded-lg p-2 font-mono">{preview.sample.join(', ')}</div>
                </div>
              )}
              <p className="text-xs text-navy-400">
                Anlık görüntü alındı. Liste sonradan değişse de yalnızca burada görünen alıcılara gönderilir. Onay/şikayet ve KVKK kontrolleri gönderim anında uygulanır.
              </p>
              <div className="flex justify-end gap-2">
                <Button variant="secondary" disabled={confirming} onClick={() => setPhase('pick')}>Geri</Button>
                <Button disabled={confirming} onClick={confirmSend}>
                  {confirming && <Loader2 className="w-4 h-4 animate-spin" />} <Send className="w-4 h-4" /> Gönder
                </Button>
              </div>
            </>
          )}

          {phase === 'sent' && status && (
            <>
              <div className="flex items-center gap-2 text-emerald-700">
                <CheckCircle2 className="w-5 h-5" />
                <span className="text-sm font-medium">Gönderim başlatıldı (durum: {status.status})</span>
              </div>
              <div className="bg-navy-50/60 border border-navy-100 rounded-lg p-3 space-y-1.5 text-sm">
                <div className="flex justify-between"><span className="text-navy-500">Kuyruğa alınan</span><span className="tabular-nums">{status.total_queued.toLocaleString('tr-TR')}</span></div>
                <div className="flex justify-between"><span className="text-navy-500">Opt-out atlanan</span><span className="tabular-nums">{status.total_skipped_optout.toLocaleString('tr-TR')}</span></div>
                <div className="flex justify-between"><span className="text-navy-500">İzin yok atlanan</span><span className="tabular-nums">{status.total_skipped_consent.toLocaleString('tr-TR')}</span></div>
              </div>
              <div className="flex justify-end">
                <Button onClick={onClose}>Tamam</Button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
