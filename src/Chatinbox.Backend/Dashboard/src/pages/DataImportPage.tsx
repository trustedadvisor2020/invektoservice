import { useEffect, useMemo, useRef, useState } from 'react';
import * as XLSX from 'xlsx';
import {
  Upload, FileSpreadsheet, X, AlertTriangle, CheckCircle2, Loader2,
  Trash2, Download, ChevronDown, Info, List as ListIcon,
  Eye, Search, ChevronLeft, ChevronRight, Plus,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import { cn } from '../lib/utils';
import {
  api, ApiClientError,
  type DataListSummary, type ImportScenario, type ImportRowDto,
  type ImportBatchRequest, type ListRecord, type ListRecordsPage,
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

type WizardStep = 1 | 2 | 3;

type ParsedRow = Record<string, string>;

// CSV bytes -> text. Strict UTF-8 first (fatal:true REJECTS invalid byte sequences instead of
// silently emitting U+FFFD), then Windows-1254 (Turkish ANSI — Excel'in varsayılan "CSV" kaydı)
// for files the strict pass rejects: "Yılmaz" stays "Yılmaz" in both encodings. Valid UTF-8
// (BOM'lu/BOM'suz) decodes byte-identical to before. The 1254 decoder is feature-detected; a
// runtime without it falls back to today's non-fatal UTF-8 — never worse than the status quo.
function decodeCsvBytes(buf: ArrayBuffer): string {
  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(buf);
  } catch {
    try {
      return new TextDecoder('windows-1254').decode(buf);
    } catch {
      return new TextDecoder('utf-8').decode(buf);
    }
  }
}

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

// ---- Phone normalizer (PREVIEW ONLY) ----
// Mirrors Chatinbox.Outbound PhoneNormalizer (intl-safe). The server re-normalizes
// authoritatively on import; this only drives the operator-facing preview counts.

// Scientific-notation artifact ("9.05332E+11"): Excel DISPLAY text whose digits
// are already lost — never a real phone. Without this reject, digit-stripping
// would turn it into a wrong-but-plausible 8-digit number that passes the E.164
// guard. Mirrors the server-side PhoneNormalizer reject; both test the trimmed
// input so preview invalid-counts match the import result.
const SCI_NOTATION_RE = /\d[eE][+-]?\d/;

function normalizePhonePreview(raw: string | null | undefined): string | null {
  if (!raw) return null;
  const trimmed = String(raw).trim();
  if (!trimmed) return null;
  if (SCI_NOTATION_RE.test(trimmed)) return null;
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

// Viewer single-record mutations: map the known INV-OB codes to actionable Turkish
// messages (AC3); anything else falls back to errText's code+message form.
function recordErrText(e: unknown, fallback: string): string {
  if (e instanceof ApiClientError) {
    switch (e.errorCode) {
      case 'INV-OB-088': return 'Bu numara listede zaten var.';
      case 'INV-OB-047': return 'Geçersiz telefon numarası. Örnek format: 905xxxxxxxxx';
      case 'INV-OB-048': return 'Liste kayıt limitine ulaştı.';
      case 'INV-OB-051': return 'Liste şu anda içe aktarım işleminde — bitince tekrar deneyin.';
      case 'INV-OB-089': return 'Kayıt bulunamadı (başka bir kullanıcı silmiş olabilir). Liste yenilendi.';
    }
  }
  return errText(e, fallback);
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

// ---- List viewer popup (FEAT-PROJELER PKT-14) ----
// All string-valued record columns; phone is always shown, the rest hide when the loaded
// page has no value in them (fixed list_records schema, so this is the full universe).
const VIEWER_COLUMNS: { key: Exclude<keyof ListRecord, 'sendable' | 'id'>; label: string }[] = [
  { key: 'phone', label: 'Telefon' },
  { key: 'name', label: 'Ad' },
  { key: 'surname', label: 'Soyad' },
  { key: 'email', label: 'E-posta' },
  { key: 'field1', label: 'Alan 1' },
  { key: 'field2', label: 'Alan 2' },
  { key: 'field3', label: 'Alan 3' },
  { key: 'field4', label: 'Alan 4' },
  { key: 'field5', label: 'Alan 5' },
  { key: 'tags', label: 'Etiketler' },
  { key: 'note', label: 'Not' },
];
const VIEWER_PAGE_SIZE = 50;

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
  // True when ANY row's phone looks like a scientific-notation artifact — tracked over
  // the full file during summary computation (not inferred from the capped invalid
  // samples), so the operator hint cannot be missed on large files.
  const [sciNotationInFile, setSciNotationInFile] = useState(false);

  const fileInputRef = useRef<HTMLInputElement>(null);

  // ---- List viewer popup (double-click / eye icon on a list row) ----
  const [viewerList, setViewerList] = useState<DataListSummary | null>(null);
  const [viewerPage, setViewerPage] = useState<ListRecordsPage | null>(null);
  const [viewerLoading, setViewerLoading] = useState(false);
  const [viewerError, setViewerError] = useState<string | null>(null);
  const [viewerSearch, setViewerSearch] = useState('');
  const [viewerPageNum, setViewerPageNum] = useState(1);
  const viewerSeqRef = useRef(0); // drops stale responses (rapid typing/paging)

  // ---- Viewer single-record mutations (manual add + 2-stage permanent delete) ----
  const [addOpen, setAddOpen] = useState(false);
  const [addForm, setAddForm] = useState({ phone: '', name: '', surname: '', email: '' });
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);
  const [addedOk, setAddedOk] = useState(false);
  const [armedDeleteId, setArmedDeleteId] = useState<number | null>(null); // first trash click arms this row
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [rowError, setRowError] = useState<string | null>(null);
  const disarmTimerRef = useRef<number | null>(null);

  function openViewer(l: DataListSummary) {
    setViewerList(l);
    setViewerSearch('');
    setViewerPageNum(1);
    setViewerPage(null);
    setViewerError(null);
    setAddOpen(false);
    setAddForm({ phone: '', name: '', surname: '', email: '' });
    setAddError(null);
    setAddedOk(false);
    setArmedDeleteId(null);
    setRowError(null);
  }

  // The armed "Emin misin?" state self-disarms after 3s; clear the timer on unmount.
  useEffect(() => () => {
    if (disarmTimerRef.current) window.clearTimeout(disarmTimerRef.current);
  }, []);

  // Fresh counters from a mutation response -> popup header + main list table, no extra fetch.
  // setViewerList's new reference also retriggers the records effect (single batched refetch).
  function applyRecordCounters(listId: number, totalRecords: number, sendableCount: number) {
    setLists(ls => ls.map(l => l.id === listId ? { ...l, total_records: totalRecords, sendable_count: sendableCount } : l));
    setViewerList(v => v && v.id === listId ? { ...v, total_records: totalRecords, sendable_count: sendableCount } : v);
  }

  async function submitAddRecord() {
    if (!viewerList || adding) return;
    const phone = addForm.phone.trim();
    if (!phone) { setAddError('Telefon zorunlu.'); return; }
    setAdding(true);
    setAddError(null);
    setAddedOk(false);
    try {
      const res = await api.addDataListRecord(viewerList.id, {
        phone,
        name: addForm.name.trim() || undefined,
        surname: addForm.surname.trim() || undefined,
        email: addForm.email.trim() || undefined,
      });
      setAddForm({ phone: '', name: '', surname: '', email: '' });
      setAddedOk(true);
      // ORDER BY id puts the new record on the LAST page — jump there (search cleared)
      // so the operator actually SEES the row that was just added.
      setViewerSearch('');
      setViewerPageNum(Math.max(1, Math.ceil(res.total_records / VIEWER_PAGE_SIZE)));
      applyRecordCounters(viewerList.id, res.total_records, res.sendable_count);
    } catch (e) {
      setAddError(recordErrText(e, 'Kayıt eklenemedi'));
    } finally {
      setAdding(false);
    }
  }

  function armDelete(recordId: number) {
    setArmedDeleteId(recordId);
    setRowError(null);
    if (disarmTimerRef.current) window.clearTimeout(disarmTimerRef.current);
    disarmTimerRef.current = window.setTimeout(() => setArmedDeleteId(null), 3000);
  }

  async function confirmDeleteRecord(record: ListRecord) {
    if (!viewerList || deletingId !== null) return;
    if (disarmTimerRef.current) window.clearTimeout(disarmTimerRef.current);
    setArmedDeleteId(null);
    setDeletingId(record.id);
    setRowError(null);
    try {
      const res = await api.deleteDataListRecord(viewerList.id, record.id);
      // Deleting the only row of page>1 -> step back; batched with the counter patch below
      // so the effect refetches once. Math.max guards the stale-closure race (user paged
      // while the DELETE was in flight) from pushing the page number to 0.
      if ((viewerPage?.records.length ?? 0) === 1 && viewerPageNum > 1) setViewerPageNum(p => Math.max(1, p - 1));
      applyRecordCounters(viewerList.id, res.total_records, res.sendable_count);
    } catch (e) {
      setRowError(recordErrText(e, 'Kayıt silinemedi'));
      if (e instanceof ApiClientError && e.errorCode === 'INV-OB-089') {
        // Another user/tab already removed it — pull fresh summaries so the popup header
        // counters match the refetched rows (a bare refetch would keep stale header counts).
        try {
          const fresh = await api.listDataLists();
          setLists(fresh);
          const cur = fresh.find(l => l.id === viewerList.id);
          setViewerList(v => (v ? (cur ? { ...v, total_records: cur.total_records, sendable_count: cur.sendable_count } : { ...v }) : v));
        } catch (refreshErr) {
          // Refresh itself failed: say so — the 089 message above claims "Liste yenilendi",
          // which would be false; tell the operator the counts may be stale instead.
          console.warn('list summary refresh after INV-OB-089 failed', refreshErr);
          setRowError('Kayıt zaten silinmiş; liste sayıları yenilenemedi — listeyi kapatıp yeniden açın.');
          setViewerList(v => (v ? { ...v } : v)); // rows still refetch
        }
      }
    } finally {
      setDeletingId(null);
    }
  }

  // Debounced server-paged fetch; search resets to page 1 at the input's onChange.
  useEffect(() => {
    if (!viewerList) return;
    setViewerLoading(true);
    setViewerError(null);
    const seq = ++viewerSeqRef.current;
    const listId = viewerList.id;
    const search = viewerSearch.trim();
    const page = viewerPageNum;
    const t = window.setTimeout(async () => {
      try {
        const result = await api.getDataListRecords(listId, {
          search: search || undefined, page, pageSize: VIEWER_PAGE_SIZE,
        });
        if (seq === viewerSeqRef.current) setViewerPage(result);
      } catch (e) {
        if (seq === viewerSeqRef.current) {
          setViewerPage(null);
          setViewerError(errText(e, 'Kayıtlar yüklenemedi'));
        }
      } finally {
        if (seq === viewerSeqRef.current) setViewerLoading(false);
      }
    }, 300);
    return () => window.clearTimeout(t);
  }, [viewerList, viewerSearch, viewerPageNum]);

  // Hide columns that are entirely empty in the loaded page (phone always visible).
  const viewerVisibleCols = useMemo(() => {
    const recs = viewerPage?.records ?? [];
    return VIEWER_COLUMNS.filter(c => c.key === 'phone' || recs.some(r => r[c.key]));
  }, [viewerPage]);

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
    setSciNotationInFile(false);
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
    setSciNotationInFile(false);
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
      // CSV: decode bytes ourselves via decodeCsvBytes (strict UTF-8 -> Windows-1254 fallback;
      // TextDecoder strips a BOM); SheetJS otherwise guesses Windows-1252 for a BOM-less CSV and
      // mojibakes "Yılmaz" -> "YÄ±lmaz". Excel (.xlsx/.xls) stores text as UTF-8 internally, so
      // the binary path is correct for those.
      const isCsv = lower.endsWith('.csv');
      const wb = isCsv
        ? XLSX.read(decodeCsvBytes(buf), { type: 'string', cellFormula: false, cellHTML: false, cellDates: false })
        : XLSX.read(buf, { type: 'array', cellFormula: false, cellHTML: false, cellDates: false });
      if (!wb.SheetNames.length) { setUploadErr('Dosyada sayfa bulunamadı.'); return; }
      if (wb.SheetNames.length > 1) setMultiSheetNote(true); // single-sheet only: use the first.
      const sheet = wb.Sheets[wb.SheetNames[0]];
      // Excel "General" renders 12+ digit NUMBERS as scientific ("9.05332E+11") and
      // raw:false below returns that display text — a phone typed as a number would be
      // silently truncated. The raw cell value keeps full precision inside .xlsx/.xls,
      // so re-render the display text from it. Repaired ONLY when the value is a
      // positive integer in the 8..15-digit E.164 range; everything else stays
      // scientific and falls through to the invalid-phone reject on purpose.
      // NOT applied to CSV: there the scientific TEXT is all the file has (digits were
      // lost when Excel saved it) and SheetJS still parses it into a numeric cell —
      // "repairing" it would fabricate a wrong-but-plausible number.
      if (!isCsv) {
        for (const addr of Object.keys(sheet)) {
          if (addr[0] === '!') continue;
          const cell = sheet[addr] as XLSX.CellObject;
          if (cell.t !== 'n' || typeof cell.v !== 'number' || !Number.isFinite(cell.v)) continue;
          if (typeof cell.w !== 'string' || !SCI_NOTATION_RE.test(cell.w)) continue;
          if (!Number.isInteger(cell.v) || cell.v <= 0) continue;
          const full = cell.v.toFixed(0);
          if (/^\d{8,15}$/.test(full)) cell.w = full;
        }
      }
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
      let sciDetected = false;
      const uniquePhones: string[] = [];
      for (const row of rows) {
        const rawPhone = row[phoneCol];
        const n = normalizePhonePreview(rawPhone);
        if (!n) {
          invalid++;
          if (!sciDetected && rawPhone && SCI_NOTATION_RE.test(rawPhone.trim())) sciDetected = true;
          continue;
        }
        if (seen.has(n)) { fileDup++; continue; }
        seen.add(n);
        uniquePhones.push(n);
      }
      setSciNotationInFile(sciDetected);

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

  // GR-2: a source column mapped to one field disappears from every OTHER field's dropdown
  // (the field's own selection stays listed so its <select> keeps a valid value).
  const usedColumns = useMemo(
    () => new Set(Object.values(mapping).filter((v): v is string => v !== null)),
    [mapping]);
  const headersFor = (field: keyof FieldMapping) =>
    headers.filter(h => h === mapping[field] || !usedColumns.has(h));
  // Defense for stale state (e.g. a mapping captured before this rule existed): block İlerle
  // visibly instead of importing the same column into two fields.
  const mappedCols = Object.values(mapping).filter((v): v is string => v !== null);
  const hasDuplicateMapping = new Set(mappedCols).size !== mappedCols.length;

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
              <div className="text-xs text-navy-400">Etkinleştirmek için Chatinbox ekibiyle iletişime geçin.</div>
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
                  <tr
                    key={l.id}
                    className="border-t border-navy-50 cursor-pointer hover:bg-navy-50/40"
                    onDoubleClick={() => openViewer(l)}
                    title="İçeriği görüntülemek için çift tıklayın"
                  >
                    <td className="px-4 py-2.5 text-navy-900">{l.name}</td>
                    <td className="px-4 py-2.5"><StatusBadge status={l.status} /></td>
                    <td className="px-4 py-2.5 text-right tabular-nums">{l.total_records.toLocaleString('tr-TR')}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums">{l.sendable_count.toLocaleString('tr-TR')}</td>
                    <td className="px-4 py-2.5">
                      <div className="flex items-center justify-end gap-1.5">
                        {/* Bulk send moved into Projeler (a project sends from its channel); lists are data-only now. */}
                        <Button size="sm" variant="ghost" onClick={() => openViewer(l)} title="İçeriği görüntüle">
                          <Eye className="w-3.5 h-3.5 text-navy-500" />
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
                <MapSelect label="Telefon" value={mapping.phone} headers={headersFor('phone')} onChange={setMap('phone')} required />
                {!mapping.phone && <div className="text-xs text-red-500 -mt-1">Telefon alanı zorunlu.</div>}
                <div className="grid grid-cols-2 gap-3">
                  {optionalFields.map(f => (
                    <MapSelect key={f.field} label={f.label} value={mapping[f.field]} headers={headersFor(f.field)} onChange={setMap(f.field)} />
                  ))}
                </div>
                {hasDuplicateMapping && (
                  <div className="text-xs text-red-500">Aynı kaynak kolon birden fazla alana eşlenemez. Eşlemeleri düzeltin.</div>
                )}
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
                <Button disabled={!mapping.phone || !targetValid || hasDuplicateMapping} onClick={() => setStep(3)}>İlerle</Button>
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
              {(sciNotationInFile || invalidSamples.some(s => SCI_NOTATION_RE.test(s))) && (
                <div className="flex items-start gap-2 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
                  <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                  <span>Telefon değerleri bilimsel gösterimle gelmiş görünüyor (Excel’de sayı formatı). Kaynak Excel’de telefon kolonunu “Metin” formatına çevirip dosyayı yeniden oluşturun.</span>
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

      {/* ---- List viewer popup (X-close + backdrop click): records table + search + server paging ---- */}
      {viewerList && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 p-4"
          onMouseDown={e => { if (e.target === e.currentTarget) setViewerList(null); }}
        >
          <div className="bg-white border border-navy-100 rounded-xl shadow-soft relative w-full max-w-4xl max-h-[88vh] flex flex-col">
            <button
              onClick={() => setViewerList(null)}
              className="absolute right-3 top-3 text-navy-300 hover:text-navy-600"
              title="Kapat"
              aria-label="Kapat"
            >
              <X className="w-5 h-5" />
            </button>

            <div className="px-5 pt-5 pb-3">
              <h2 className="text-base font-semibold text-navy-900 flex items-center gap-2">
                <ListIcon className="w-4 h-4 text-navy-400" /> {viewerList.name}
              </h2>
              <p className="text-xs text-navy-400 mt-0.5">
                {viewerList.total_records.toLocaleString('tr-TR')} kayıt · {viewerList.sendable_count.toLocaleString('tr-TR')} gönderilebilir
              </p>
            </div>

            <div className="px-5 pb-3 flex items-center gap-2">
              <div className="relative flex-1">
                <Search className="w-4 h-4 text-navy-300 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  className="w-full pl-9 pr-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus"
                  placeholder="Telefon, ad, e-posta veya herhangi bir alanda ara…"
                  value={viewerSearch}
                  onChange={e => { setViewerSearch(e.target.value); setViewerPageNum(1); }}
                />
              </div>
              {viewerList.status === 'ready' && (
                <Button
                  size="sm"
                  variant={addOpen ? 'secondary' : 'primary'}
                  onClick={() => { setAddOpen(o => !o); setAddError(null); setAddedOk(false); }}
                >
                  <Plus className="w-4 h-4" /> Yeni Kayıt
                </Button>
              )}
            </div>

            {/* Manual single-record add panel (phone required; invalid + duplicate rejected server-side) */}
            {addOpen && viewerList.status === 'ready' && (
              <div className="px-5 pb-3">
                <div className="border border-navy-100 rounded-lg p-3 bg-navy-50/40">
                  <div className="grid grid-cols-2 lg:grid-cols-4 gap-2">
                    <input
                      className="px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus"
                      placeholder="Telefon *"
                      autoFocus
                      value={addForm.phone}
                      onChange={e => { setAddForm(f => ({ ...f, phone: e.target.value })); setAddError(null); setAddedOk(false); }}
                      onKeyDown={e => { if (e.key === 'Enter') void submitAddRecord(); }}
                    />
                    <input
                      className="px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus"
                      placeholder="Ad"
                      value={addForm.name}
                      onChange={e => { setAddForm(f => ({ ...f, name: e.target.value })); setAddedOk(false); }}
                      onKeyDown={e => { if (e.key === 'Enter') void submitAddRecord(); }}
                    />
                    <input
                      className="px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus"
                      placeholder="Soyad"
                      value={addForm.surname}
                      onChange={e => { setAddForm(f => ({ ...f, surname: e.target.value })); setAddedOk(false); }}
                      onKeyDown={e => { if (e.key === 'Enter') void submitAddRecord(); }}
                    />
                    <input
                      className="px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus"
                      placeholder="E-posta"
                      value={addForm.email}
                      onChange={e => { setAddForm(f => ({ ...f, email: e.target.value })); setAddedOk(false); }}
                      onKeyDown={e => { if (e.key === 'Enter') void submitAddRecord(); }}
                    />
                  </div>
                  <div className="mt-2 flex items-center justify-between gap-3">
                    <div className="text-xs min-h-[1rem]">
                      {addError ? (
                        <span className="text-red-600">{addError}</span>
                      ) : addedOk ? (
                        <span className="text-emerald-600 inline-flex items-center gap-1">
                          <CheckCircle2 className="w-3.5 h-3.5" /> Kayıt eklendi
                        </span>
                      ) : null}
                    </div>
                    <Button size="sm" onClick={() => void submitAddRecord()} disabled={adding || !addForm.phone.trim()}>
                      {adding && <Loader2 className="w-4 h-4 animate-spin" />} Ekle
                    </Button>
                  </div>
                </div>
              </div>
            )}

            {rowError && (
              <div className="px-5 pb-2 text-xs text-red-600">{rowError}</div>
            )}

            <div className="flex-1 overflow-auto border-t border-navy-50">
              {viewerError ? (
                <div className="px-5 py-8 text-center text-sm text-red-600">{viewerError}</div>
              ) : viewerLoading && !viewerPage ? (
                <div className="px-5 py-10 text-center text-navy-400 text-sm flex items-center justify-center gap-2">
                  <Loader2 className="w-4 h-4 animate-spin" /> Kayıtlar yükleniyor…
                </div>
              ) : !viewerPage || viewerPage.records.length === 0 ? (
                <div className="px-5 py-10 text-center text-navy-400 text-sm">
                  {viewerSearch.trim() ? 'Aramayla eşleşen kayıt yok.' : 'Bu listede kayıt yok.'}
                </div>
              ) : (
                <table className="w-full text-sm">
                  <thead className="bg-navy-50/50 text-navy-500 text-xs sticky top-0">
                    <tr>
                      {viewerVisibleCols.map(c => (
                        <th key={c.key} className="text-left font-medium px-4 py-2 whitespace-nowrap">{c.label}</th>
                      ))}
                      {viewerList.status === 'ready' && <th className="px-2 py-2 w-12" aria-label="İşlem"></th>}
                    </tr>
                  </thead>
                  <tbody className={cn(viewerLoading && 'opacity-50')}>
                    {viewerPage.records.map(r => (
                      <tr key={r.id} className={cn('border-t border-navy-50', !r.sendable && 'opacity-60')}>
                        {viewerVisibleCols.map(c => (
                          <td key={c.key} className="px-4 py-2 text-navy-800 max-w-[220px] truncate" title={r[c.key] ?? ''}>
                            {c.key === 'phone'
                              ? (
                                <span className="tabular-nums flex items-center gap-1.5">
                                  {r.phone ?? '—'}
                                  {!r.sendable && (
                                    <span className="px-1.5 py-0.5 rounded bg-navy-50 text-navy-400 text-[10px]">gönderilemez</span>
                                  )}
                                </span>
                              )
                              : (r[c.key] ?? '')}
                          </td>
                        ))}
                        {viewerList.status === 'ready' && (
                          <td className="px-2 py-2 text-right whitespace-nowrap">
                            {armedDeleteId === r.id ? (
                              <button
                                onClick={() => void confirmDeleteRecord(r)}
                                disabled={deletingId !== null}
                                className="text-red-600 hover:text-red-700 text-[11px] font-semibold disabled:opacity-50"
                                title="Silmeyi onayla"
                              >
                                Emin misin?
                              </button>
                            ) : deletingId === r.id ? (
                              <Loader2 className="w-4 h-4 animate-spin text-navy-300 inline" />
                            ) : (
                              <button
                                onClick={() => armDelete(r.id)}
                                disabled={deletingId !== null}
                                className="text-navy-300 hover:text-red-600 disabled:opacity-50"
                                title="Kaydı listeden sil"
                                aria-label="Kaydı listeden sil"
                              >
                                <Trash2 className="w-4 h-4" />
                              </button>
                            )}
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            <div className="px-5 py-3 border-t border-navy-50 flex items-center justify-between text-xs text-navy-500">
              <span className="tabular-nums">
                {viewerPage && viewerPage.total > 0
                  ? `${((viewerPage.page - 1) * viewerPage.page_size + 1).toLocaleString('tr-TR')}–${Math.min(viewerPage.page * viewerPage.page_size, viewerPage.total).toLocaleString('tr-TR')} / ${viewerPage.total.toLocaleString('tr-TR')} kayıt`
                  : '—'}
              </span>
              <div className="flex items-center gap-1.5">
                <Button
                  size="sm" variant="ghost"
                  disabled={viewerLoading || deletingId !== null || viewerPageNum <= 1}
                  onClick={() => setViewerPageNum(p => Math.max(1, p - 1))}
                >
                  <ChevronLeft className="w-4 h-4" /> Önceki
                </Button>
                <Button
                  size="sm" variant="ghost"
                  disabled={viewerLoading || deletingId !== null || !viewerPage || viewerPage.page * viewerPage.page_size >= viewerPage.total}
                  onClick={() => setViewerPageNum(p => p + 1)}
                >
                  Sonraki <ChevronRight className="w-4 h-4" />
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}

// NOTE: the per-list "Listeye Gönder" bulk-send modal was removed — sending now lives
// inside a Proje (a project sends from its configured WhatsApp channel + template).
// Data lists are data-only here. The Outbound bulk-send API (previewFromList /
// confirmBulkSend) is retained server-side for the project send slice.
