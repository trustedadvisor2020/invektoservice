import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { createPortal } from 'react-dom';
import {
  Briefcase, X, Plus, Pencil, Archive, Loader2, Info, ListChecks, FileText, Send, Users, CheckCircle2,
  ExternalLink, Phone, Reply, Image as ImageIcon, Pause, Play, Ban, AlertTriangle, RefreshCw,
  Search, Download, BarChart3, ArrowUp, ArrowDown, ChevronsUpDown,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { cn } from '../lib/utils';
import { WaErrorPopover } from '../components/WaErrorPopover';
import { WaTemplatePreview } from '../components/WaTemplatePreview';
import {
  api, ApiClientError,
  type ProjectSummary, type ProjectStatus, type DataListSummary,
  type InstanceDto, type WaTemplate, type ProjectTemplateKind, type ProjectTemplateParam,
  type ProjectContentMode, type OutboundTemplateDto,
  type BulkSendPreviewResponse, type BulkSendStatusResponse,
  type DataListPreviewSample, type ListRecord, type ProjectTestSendRequest,
  type ProjectRun, type ProjectRecipient, type ProjectRecipientsPage, type ProjectFailureBucket,
} from '../lib/api';
import { resolveWaError } from '../lib/wa-error-codes';

// =============================================================
// FEAT-PROJELER PKT-14 — ProjectsPage (Projeler)
// Operator-facing management of "projects": a named, reusable campaign unit that
// targets one or more saved data-lists. The project also carries its SEND CONFIG:
// a WhatsApp Cloud API channel (instance) + message kind (plain text or approved
// template) + the template's operator-filled parameters. Send EXECUTION that
// consumes this config is a later slice. DELETE = soft-delete-as-archive.
// =============================================================

function errText(e: unknown, fallback: string): string {
  if (e instanceof ApiClientError) return `${e.errorCode}: ${e.message}`;
  if (e instanceof Error) return e.message;
  return fallback;
}

const STATUS_META: Record<ProjectStatus, { label: string; cls: string }> = {
  draft:     { label: 'Taslak',       cls: 'bg-navy-50 text-navy-500' },
  running:   { label: 'Çalışıyor',    cls: 'bg-green-50 text-green-600' },
  paused:    { label: 'Duraklatıldı', cls: 'bg-amber-50 text-amber-600' },
  completed: { label: 'Tamamlandı',   cls: 'bg-blue-50 text-blue-600' },
  cancelled: { label: 'İptal',        cls: 'bg-navy-50 text-navy-400' },
  archived:  { label: 'Arşivlendi',   cls: 'bg-navy-100 text-navy-400' },
};

function StatusBadge({ status }: { status: ProjectStatus }) {
  const meta = STATUS_META[status] ?? STATUS_META.draft;
  return <span className={cn('inline-block px-2 py-0.5 rounded-full text-xs font-medium', meta.cls)}>{meta.label}</span>;
}

// Per-message (recipient) delivery status — the report drawer's row status. Mirrors
// chk_message_status (migrations 055/056/061): queued/sending/posting/sent/delivered/read/failed/
// ambiguous/cancelled/paused. 'sending'+'posting' collapse to one "Gönderiliyor" label for operators.
const MSG_STATUS_META: Record<string, { label: string; cls: string }> = {
  queued:    { label: 'Kuyrukta',     cls: 'bg-navy-50 text-navy-500' },
  sending:   { label: 'Gönderiliyor', cls: 'bg-navy-50 text-navy-500' },
  posting:   { label: 'Gönderiliyor', cls: 'bg-navy-50 text-navy-500' },
  sent:      { label: 'Gönderildi',   cls: 'bg-indigo-50 text-indigo-600' },
  delivered: { label: 'İletildi',     cls: 'bg-blue-50 text-blue-600' },
  read:      { label: 'Okundu',       cls: 'bg-green-50 text-green-600' },
  failed:    { label: 'Başarısız',    cls: 'bg-red-50 text-red-600' },
  ambiguous: { label: 'Belirsiz',     cls: 'bg-amber-50 text-amber-600' },
  cancelled: { label: 'İptal',        cls: 'bg-navy-50 text-navy-400' },
  paused:    { label: 'Duraklatıldı', cls: 'bg-amber-50 text-amber-600' },
};

function MsgStatusBadge({ status }: { status: string }) {
  const meta = MSG_STATUS_META[status] ?? { label: status, cls: 'bg-navy-50 text-navy-500' };
  return <span className={cn('inline-block px-2 py-0.5 rounded-full text-xs font-medium whitespace-nowrap', meta.cls)}>{meta.label}</span>;
}

// Report recipient-table sort. The key is sent to the server (whitelisted ORDER BY in ProjectsRepository);
// null sort = server default (newest message id first).
type ReportSortKey = 'phone' | 'status' | 'sent' | 'delivered' | 'read';
type ReportSortDir = 'asc' | 'desc';

// A clickable column header for the report table. Shows the active sort direction (▲/▼) and a faint
// hover affordance (⇅) on the inactive ones. aria-sort lives on the <th> for assistive tech.
function ReportSortHeader({
  label, sortKey, activeSort, dir, onSort, className,
}: {
  label: string; sortKey: ReportSortKey; activeSort: ReportSortKey | null; dir: ReportSortDir;
  onSort: (key: ReportSortKey) => void; className?: string;
}) {
  const active = activeSort === sortKey;
  return (
    <th
      className={cn('px-4 py-2 text-left font-medium', className)}
      aria-sort={active ? (dir === 'asc' ? 'ascending' : 'descending') : 'none'}
    >
      <button
        type="button"
        onClick={() => onSort(sortKey)}
        className={cn('group/sort inline-flex items-center gap-1 hover:text-navy-700', active && 'text-navy-700')}
        title={`${label} sütununa göre sırala`}
      >
        {label}
        {active
          ? (dir === 'asc' ? <ArrowUp className="w-3 h-3" /> : <ArrowDown className="w-3 h-3" />)
          : <ChevronsUpDown className="w-3 h-3 opacity-0 group-hover/sort:opacity-40 transition-opacity" />}
      </button>
    </th>
  );
}

// Rapor 'Durum' dropdown filter (server-side). Each value is a comma-separated set of message statuses so a
// collapsed label (Gönderiliyor = sending+posting) maps to one option; '' = all statuses.
const REPORT_STATUS_OPTIONS: { v: string; label: string }[] = [
  { v: '', label: 'Tüm durumlar' },
  { v: 'queued', label: 'Kuyrukta' },
  { v: 'paused', label: 'Duraklatıldı' },
  { v: 'sending,posting', label: 'Gönderiliyor' },
  { v: 'sent', label: 'Gönderildi' },
  { v: 'delivered', label: 'İletildi' },
  { v: 'read', label: 'Okundu' },
  { v: 'failed', label: 'Başarısız' },
  { v: 'ambiguous', label: 'Belirsiz' },
  { v: 'cancelled', label: 'İptal' },
];

function fmtDateTime(iso: string | null): string {
  if (!iso) return '—';
  // 2-digit year (Q 2026-06-17): "17.06.26 16:30" — explicit parts instead of dateStyle:'short' (4-digit year).
  try { return new Date(iso).toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' }); }
  catch { return iso; }
}

// One run dropdown label: "12.06.26 09:47 · 8 alıcı".
function runLabel(r: ProjectRun): string {
  return `${fmtDateTime(r.created_at)} · ${r.total.toLocaleString('tr-TR')} alıcı`;
}

// CSV cell escape (RFC 4180): wrap in quotes + double any embedded quote. Always quote so commas /
// newlines / leading +90 phones survive Excel. A leading '=','+','-','@' is prefixed with a tab to
// neutralize CSV formula injection while staying readable in Excel.
function csvCell(v: string | number | null): string {
  let s = v == null ? '' : String(v);
  if (/^[=+\-@]/.test(s)) s = '\t' + s;
  return '"' + s.replace(/"/g, '""') + '"';
}

function recipientsToCsv(rows: ProjectRecipient[]): string {
  const header = ['Numara', 'Durum', 'Hata', 'Gönderim', 'İletildi', 'Okundu', 'Son Deneme', 'Mesaj ID'];
  const lines = rows.map((r) => [
    csvCell(r.phone),
    csvCell(MSG_STATUS_META[r.status]?.label ?? r.status),
    csvCell(r.error),
    csvCell(fmtDateTime(r.sent_at)),
    csvCell(fmtDateTime(r.delivered_at)),
    csvCell(fmtDateTime(r.read_at)),
    csvCell(fmtDateTime(r.last_attempt_at)),
    csvCell(r.message_id),
  ].join(','));
  return [header.map(csvCell).join(','), ...lines].join('\r\n');
}

const REPORT_PAGE_SIZE = 50;

const MAX_NAME = 120;
const MAX_DESC = 500;

// Selectable list columns for per-recipient template params (mirrors list_records' fixed schema:
// migration 052 — name/surname/email/field1..5/tags/note). Same set across every data-list.
const LIST_COLUMNS: { v: string; label: string }[] = [
  { v: 'name', label: 'Ad' },
  { v: 'surname', label: 'Soyad' },
  { v: 'email', label: 'E-posta' },
  { v: 'field1', label: 'Alan 1' },
  { v: 'field2', label: 'Alan 2' },
  { v: 'field3', label: 'Alan 3' },
  { v: 'field4', label: 'Alan 4' },
  { v: 'field5', label: 'Alan 5' },
  { v: 'tags', label: 'Etiketler' },
  { v: 'note', label: 'Not' },
];

type Placeholder = { key: string; location: string };

// Pull every {{key}} placeholder out of a template's preview (header text + body + footer), first-seen
// order, de-duplicated. cxapi's requiredInputs can omit header placeholders, so we read the rendered
// preview directly — the operator must be able to map a column to EVERY visible {{...}} (e.g. hname + name).
function extractPlaceholders(t: WaTemplate | null): Placeholder[] {
  if (!t?.preview) return [];
  const re = /\{\{\s*([^}\s]+)\s*\}\}/g;
  const seen = new Set<string>();
  const out: Placeholder[] = [];
  const scan = (text: string | null | undefined, location: string) => {
    if (!text) return;
    for (const m of text.matchAll(re)) {
      const key = m[1];
      if (!seen.has(key)) { seen.add(key); out.push({ key, location }); }
    }
  };
  scan(t.preview.header?.text, 'HEADER');
  scan(t.preview.body, 'BODY');
  scan(t.preview.footer, 'FOOTER');
  return out;
}

// aha #1 — placeholder key -> column auto-match aliases (case-insensitive). Template authors
// commonly name params after the data they expect; matching ones pre-fill the column dropdown.
const COLUMN_ALIASES: Record<string, string[]> = {
  name: ['name', 'ad', 'adi', 'isim', 'firstname', 'first_name'],
  surname: ['surname', 'soyad', 'soyadi', 'lastname', 'last_name'],
  email: ['email', 'eposta', 'e-posta', 'e_posta', 'mail'],
  field1: ['field1', 'alan1'],
  field2: ['field2', 'alan2'],
  field3: ['field3', 'alan3'],
  field4: ['field4', 'alan4'],
  field5: ['field5', 'alan5'],
  tags: ['tags', 'tag', 'etiket', 'etiketler'],
  note: ['note', 'not', 'notlar'],
};
function autoMatchColumn(placeholderKey: string): string | null {
  const k = placeholderKey.trim().toLowerCase();
  for (const [col, aliases] of Object.entries(COLUMN_ALIASES))
    if (aliases.includes(k)) return col;
  return null;
}

// Typed column access on a sample record (no index-signature casts).
function recordValue(rec: ListRecord, col: string): string | null {
  switch (col) {
    case 'name': return rec.name;
    case 'surname': return rec.surname;
    case 'email': return rec.email;
    case 'field1': return rec.field1;
    case 'field2': return rec.field2;
    case 'field3': return rec.field3;
    case 'field4': return rec.field4;
    case 'field5': return rec.field5;
    case 'tags': return rec.tags;
    case 'note': return rec.note;
    default: return null;
  }
}

// Render template text with {{...}} placeholders highlighted. When a resolver returns a sample
// value for a placeholder (aha #2: sample-recipient preview), the REAL value renders (green);
// unresolved placeholders keep the raw {{key}} highlight (brand).
function renderTemplateText(text: string, resolve?: (key: string) => string | null) {
  return text.split(/(\{\{\s*[^}\s]+\s*\}\})/g).map((part, i) => {
    const m = /^\{\{\s*([^}\s]+)\s*\}\}$/.exec(part);
    if (!m) return <span key={i}>{part}</span>;
    const v = resolve?.(m[1]);
    return v
      ? <span key={i} className="bg-green-100 text-green-700 rounded px-1">{v}</span>
      : <span key={i} className="bg-brand-100 text-brand-700 rounded px-1">{part}</span>;
  });
}

// Icon for a template button by cxapi type.
function buttonIcon(type: string | null) {
  if (type === 'URL') return <ExternalLink className="w-3.5 h-3.5" />;
  if (type === 'PHONE_NUMBER') return <Phone className="w-3.5 h-3.5" />;
  return <Reply className="w-3.5 h-3.5" />; // QUICK_REPLY / default
}

// Icon + label for a non-text (media) template header.
function mediaHeaderIcon(type: string | null) {
  return type === 'DOCUMENT' ? <FileText className="w-4 h-4" /> : <ImageIcon className="w-4 h-4" />;
}
function mediaHeaderLabel(type: string | null) {
  if (type === 'IMAGE') return 'Görsel başlık';
  if (type === 'VIDEO') return 'Video başlık';
  if (type === 'DOCUMENT') return 'Belge başlık';
  return 'Medya başlık';
}

export default function ProjectsPage() {
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [featureDisabled, setFeatureDisabled] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [lists, setLists] = useState<DataListSummary[]>([]);

  // Modal state. editing === null => create; editing is the project being edited.
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<ProjectSummary | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedListIds, setSelectedListIds] = useState<number[]>([]);
  const [loadingTargets, setLoadingTargets] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // ---- Send config (channel + template) ----
  const [instances, setInstances] = useState<InstanceDto[]>([]);
  const [templateKind, setTemplateKind] = useState<ProjectTemplateKind | ''>(''); // '' = no send config
  const [instanceId, setInstanceId] = useState<string>('');   // cxapi instanceID (string form of the int); '' = none
  const [waTemplateId, setWaTemplateId] = useState<string>('');
  const [templates, setTemplates] = useState<WaTemplate[]>([]);
  const [loadingTemplates, setLoadingTemplates] = useState(false);
  const [templatesError, setTemplatesError] = useState<string | null>(null);
  // Text placeholders ({{key}}) map to a data-list COLUMN (per-recipient personalization at send time, PR-4):
  // placeholder key -> list column key (name/surname/email/field1..5/tags/note).
  const [paramColumns, setParamColumns] = useState<Record<string, string>>({});
  // Media inputs (cxapi requiredInput kind='media') take a public URL literal: media key -> URL.
  const [mediaValues, setMediaValues] = useState<Record<string, string>>({});

  // ---- List insight (aha #2/#3/#4): ONE debounced round-trip per target-list change ----
  // sample = first sendable record of the first selected list; reach = deduplicated recipient
  // count; column_stats = per-column fill. FAIL-SILENT: the modal works fully without it.
  const [listInsight, setListInsight] = useState<DataListPreviewSample | null>(null);
  const [insightLoading, setInsightLoading] = useState(false);
  const [showSample, setShowSample] = useState(true); // aha #2 toggle: sample values vs raw {{...}}
  const insightSeqRef = useRef(0);                    // drops stale responses on rapid list toggling
  // aha #5 dirty guard: pristine form fingerprint, captured AFTER the async edit re-populate.
  const baselineRef = useRef<string | null>(null);

  // ---- plain_text content (migration 059): the content a plain_text run sends, chosen in settings ----
  const [contentMode, setContentMode] = useState<ProjectContentMode | ''>(''); // '' until a content source is chosen
  const [outboundTemplateId, setOutboundTemplateId] = useState<string>('');    // gallery template id (string form); '' = none
  const [plainTextBody, setPlainTextBody] = useState<string>('');              // free text
  const [galleryTemplates, setGalleryTemplates] = useState<OutboundTemplateDto[]>([]);
  const [galleryError, setGalleryError] = useState<string | null>(null);

  const [archivingId, setArchivingId] = useState<number | null>(null);

  // ---- Run dispatch (Gönder): preview -> confirm -> status ----
  const [sendProject, setSendProject] = useState<ProjectSummary | null>(null);
  const [sendCampaignId, setSendCampaignId] = useState<string>('');
  const [sendPhase, setSendPhase] = useState<'previewing' | 'preview' | 'confirming' | 'sent' | 'error'>('previewing');
  const [sendPreview, setSendPreview] = useState<BulkSendPreviewResponse | null>(null);
  const [sendStatus, setSendStatus] = useState<BulkSendStatusResponse | null>(null);
  const [sendError, setSendError] = useState<string | null>(null);

  // ---- Run lifecycle (SS-D): pause / resume / cancel ----
  const [lifecycleBusyId, setLifecycleBusyId] = useState<number | null>(null);
  const [cancelTarget, setCancelTarget] = useState<ProjectSummary | null>(null);

  // ---- Rapor (delivery report) drawer ----
  const [reportProject, setReportProject] = useState<ProjectSummary | null>(null);
  const [reportRuns, setReportRuns] = useState<ProjectRun[]>([]);
  const [reportCampaign, setReportCampaign] = useState<string>(''); // '' = Tümü (all runs)
  const [reportSearch, setReportSearch] = useState('');
  const [reportSearchDraft, setReportSearchDraft] = useState('');
  const [reportStatus, setReportStatus] = useState(''); // '' = all; otherwise a REPORT_STATUS_OPTIONS value (CSV of statuses)
  // #4 failure-reason breakdown: the active bucket filter (a Meta code or 'none' for no-code rows; '' = off)
  // and the buckets themselves (failed/ambiguous grouped by code for the open project/run).
  const [reportFailCode, setReportFailCode] = useState('');
  const [failureBuckets, setFailureBuckets] = useState<ProjectFailureBucket[]>([]);
  const [reportSort, setReportSort] = useState<ReportSortKey | null>(null); // null = server default (id DESC)
  const [reportDir, setReportDir] = useState<ReportSortDir>('desc');
  const [reportPage, setReportPage] = useState(1);
  const [reportData, setReportData] = useState<ProjectRecipientsPage | null>(null);
  const [reportLoading, setReportLoading] = useState(false);
  const [reportError, setReportError] = useState<string | null>(null);
  const [resendingId, setResendingId] = useState<number | null>(null);
  const [reportExporting, setReportExporting] = useState(false);
  // Bulk resend (ALL project errors): confirm gate + in-flight flag. The count shown in the confirm is the
  // sum of failed+ambiguous across all runs (the bulk scope is the whole project, not the selected run).
  const [bulkResendConfirm, setBulkResendConfirm] = useState(false);
  const [bulkResending, setBulkResending] = useState(false);
  // Bumped on resend to force a recipients re-fetch (status changed server-side).
  const [reportReloadTick, setReportReloadTick] = useState(0);

  // ---- Status filter (GR-9): single dropdown over the full set (single includeArchived fetch).
  // 'all' = everything EXCEPT archived; 'archived' is its own explicit option.
  const [statusFilter, setStatusFilter] = useState<ProjectStatus | 'all'>('all');

  // ---- Row template info: approved-template catalogs keyed by instance so rows can show
  // the template NAME (not the raw id) + full content on hover. Fetched once per distinct
  // channel (ref-set guard). 'error' = terminal fetch failure for that channel: the row keeps
  // the id fallback, the tooltip says so, and the next projects reload (Yenile) retries.
  const [rowTemplates, setRowTemplates] = useState<Record<number, WaTemplate[] | 'error'>>({});
  const rowTplFetchedRef = useRef<Set<number>>(new Set());
  // Rich hover preview (portal-positioned so it escapes the table's overflow-hidden). Anchored to the
  // hovered content line's bounding rect; cleared on mouseleave.
  const [rowHover, setRowHover] = useState<{ project: ProjectSummary; rect: DOMRect } | null>(null);

  // ---- FEATURE B (status-pull): "Durumu Yenile" pulls live cxapi message-status for pending recipients.
  const [reportStatusPulling, setReportStatusPulling] = useState(false);
  const [reportPullNote, setReportPullNote] = useState<string | null>(null);

  // ---- Test send (GR-8): one plain-text message to one number, from the CURRENT form state.
  // testResult.ok: true=delivered (green), false=failed (red), null=pending/inconclusive (neutral).
  const [testPhone, setTestPhone] = useState('');
  const [testSending, setTestSending] = useState(false);
  const [testResult, setTestResult] = useState<{ ok: boolean | null; text: string; reason?: string | null } | null>(null);

  // Only real WABA lines are valid send channels. cxapi gives instanceType=1 to BOTH WABA and
  // QR-Code lines, so connectionType is the discriminator. null connectionType (pre-063 cache row)
  // falls back to the old type filter for DISPLAY only — the server-side guard is strict WABA, so a
  // stale non-WABA row shown here is rejected at save/test (fail-closed).
  const whatsappInstances = useMemo(
    () => instances.filter(i => i.connectionType != null
      ? i.connectionType.trim().toUpperCase() === 'WABA'
      : i.instanceType === 1),
    [instances]);
  const selectedTemplate = useMemo(
    () => templates.find(t => t.templateId === waTemplateId) ?? null, [templates, waTemplateId]);
  const requiredInputs = selectedTemplate?.requiredInputs ?? [];
  // Text placeholders ({{...}}) read from the rendered preview (covers header params cxapi omits from requiredInputs).
  const placeholders = useMemo(() => extractPlaceholders(selectedTemplate), [selectedTemplate]);
  // Media inputs come from cxapi requiredInputs (preview text has no {{...}} for media).
  const mediaInputs = useMemo(() => requiredInputs.filter(ri => ri.kind === 'media'), [requiredInputs]);
  // cxapi gap (canlı kanıt 2026-06-10, instance 6570): requiredInputs HEADER text değişkenlerini
  // listelemiyor VE chatoperation bizim gönderdiğimiz bu paramKey'leri uygulamıyor — alıcıya şablonun
  // Meta-onaylı varsayılan değeri gidiyor (header "Test {{hname}}" → "Test Müşterimiz"). Eşleme + wire
  // bilinçli aynen kalıyor (INMA düzeltince otomatik çalışır); requiredInputs'ta görünmeyen anahtarlar
  // için operatöre aşağıda amber uyarı gösterilir. INMA fix'i requiredInputs'a anahtarı ekleyince bu
  // küme boşalır ve uyarı kendiliğinden kaybolur.
  const vendorUnappliedKeys = useMemo(() => {
    const declared = new Set(
      (selectedTemplate?.requiredInputs ?? []).flatMap(ri => ri.kind === 'text' && ri.paramKey ? [ri.paramKey] : []));
    return placeholders.filter(ph => !declared.has(ph.key)).map(ph => ph.key);
  }, [selectedTemplate, placeholders]);
  // Stable key (location+mediaType, not index) so edit re-populate doesn't depend on async template load order.
  const mediaKey = (ri: { location: string | null; mediaType: string | null }) =>
    `${ri.location ?? ''}:${ri.mediaType ?? ''}`;
  // Gallery templates are tenant-scoped (not per-channel); the picker resolves the selected one for a preview.
  const selectedGalleryTemplate = useMemo(
    () => galleryTemplates.find(t => String(t.id) === outboundTemplateId) ?? null, [galleryTemplates, outboundTemplateId]);

  const PLAIN_TEXT_BODY_MAX = 4096; // mirrors PlainTextBodyMaxLength in ProjectsService.cs

  // silent: the #1 auto-refresh poll path — skip the full-table spinner and KEEP the last good rows on a
  // transient failure (a flicker-free background refresh while a run is in flight). The manual/initial path
  // (silent=false) keeps the original spinner + error-surfacing behavior.
  async function loadProjects(silent = false) {
    if (!silent) { setLoading(true); setLoadError(null); }
    setFeatureDisabled(false);
    try {
      // includeArchived: the table fetches the FULL set once; the status chips filter client-side
      // (an archived project is the tenant's own data — the Arşivli chip is how it is reached).
      const next = await api.listProjects(true);
      setProjects(next);
      if (silent) setLoadError(null); // a recovered poll clears a stale error banner
    } catch (e) {
      if (silent) {
        // Background poll failure: keep the last good table, don't clobber it or block with an error.
        console.warn('[projects] otomatik yenileme başarısız:', errText(e, 'reload failed'));
        return;
      }
      setProjects([]);
      // 403 = the tenant isn't enabled for projects (ProjectsOptions gate / plan).
      if (e instanceof ApiClientError && e.status === 403) setFeatureDisabled(true);
      else setLoadError(errText(e, 'Projeler yüklenemedi'));
    } finally {
      if (!silent) setLoading(false);
    }
  }

  async function loadLists() {
    try {
      setLists(await api.listDataLists());
    } catch {
      setLists([]); // a list-load failure must not block the projects view; the multi-select just shows empty
    }
  }

  async function loadInstances() {
    try {
      const res = await api.getInstances();
      setInstances(res.instances ?? []);
    } catch {
      setInstances([]); // channel dropdown just shows empty; never blocks the projects view
    }
  }

  // Şablon Galerisi (outbound_templates) for the plain_text gallery-template content picker.
  // Tenant-scoped + channel-independent, so it loads once with the page; only active templates are selectable.
  async function loadGalleryTemplates() {
    setGalleryError(null);
    try {
      const res = await api.listOutboundTemplates();
      setGalleryTemplates((res.templates ?? []).filter(t => t.is_active));
    } catch (e) {
      setGalleryTemplates([]);
      setGalleryError(errText(e, 'Şablon galerisi yüklenemedi'));
    }
  }

  // Fetch the approved templates for a channel. Used on channel change and on edit re-populate.
  async function fetchTemplatesFor(instId: number) {
    setLoadingTemplates(true);
    setTemplatesError(null);
    try {
      const res = await api.getWaTemplates(instId);
      setTemplates(res.templates ?? []);
    } catch (e) {
      setTemplates([]);
      setTemplatesError(errText(e, 'Şablonlar alınamadı'));
    } finally {
      setLoadingTemplates(false);
    }
  }

  function onKindChange(kind: ProjectTemplateKind | '') {
    setTemplateKind(kind);
    // Switching away from template clears the template selection; switching to it (re)loads templates.
    setWaTemplateId('');
    setTemplates([]);
    setParamColumns({});
    setMediaValues({});
    setTemplatesError(null);
    // plain_text needs a content choice; default to gallery template (operator can switch to free text).
    // Any other kind clears the plain_text content carriers.
    if (kind === 'plain_text') {
      setContentMode('gallery_template');
    } else {
      setContentMode('');
      setOutboundTemplateId('');
      setPlainTextBody('');
    }
    if (kind === 'wapcrm_template' && instanceId) void fetchTemplatesFor(Number(instanceId));
  }

  function onChannelChange(value: string) {
    setInstanceId(value);
    // Templates are per-channel; reset the template selection and reload for the new channel.
    setWaTemplateId('');
    setTemplates([]);
    setParamColumns({});
    setMediaValues({});
    setTemplatesError(null);
    if (templateKind === 'wapcrm_template' && value) void fetchTemplatesFor(Number(value));
  }

  function onTemplateChange(value: string) {
    setWaTemplateId(value);
    // aha #1: a new template has a different placeholder set — pre-fill the column for every
    // placeholder whose key matches a column alias (ad→name, email→email...); rest stay manual.
    const tmpl = templates.find(t => t.templateId === value) ?? null;
    const auto: Record<string, string> = {};
    for (const ph of extractPlaceholders(tmpl)) {
      const col = autoMatchColumn(ph.key);
      if (col) auto[ph.key] = col;
    }
    setParamColumns(auto);
    setMediaValues({});
  }

  useEffect(() => {
    void loadProjects();
    void loadLists();
    void loadInstances();
    void loadGalleryTemplates();
  }, []);

  // Row template-name enrichment: fetch the approved-template catalog once per distinct
  // channel used by a wapcrm_template project. The ref-set guard prevents duplicate fetches
  // across project reloads. SUCCESSFUL catalogs stay cached for the page's lifetime (the
  // catalog rarely changes; the edit modal always fetches fresh); FAILED channels are
  // un-marked in the catch below, so the next projects reload (Yenile) retries them.
  useEffect(() => {
    const instanceIds = Array.from(new Set(
      projects
        .filter(p => p.template_kind === 'wapcrm_template' && p.instance_id != null && p.instance_id > 0)
        .map(p => p.instance_id as number),
    ));
    for (const instId of instanceIds) {
      if (rowTplFetchedRef.current.has(instId)) continue;
      rowTplFetchedRef.current.add(instId);
      void api.getWaTemplates(instId)
        .then(res => setRowTemplates(prev => ({ ...prev, [instId]: res.templates ?? [] })))
        .catch(e => {
          // Enrichment-only failure: log it, mark the channel as terminal-error (tooltip tells
          // the operator), and un-mark the ref so the next projects reload (Yenile) retries.
          // The page itself never blocks on this — rows keep the template-id fallback.
          console.warn(`[projects] şablon kataloğu alınamadı (instance ${instId}):`, e);
          rowTplFetchedRef.current.delete(instId);
          setRowTemplates(prev => ({ ...prev, [instId]: 'error' }));
        });
    }
  }, [projects]);

  // ---- aha #5: dirty-form fingerprint (stable key order so map insertion order can't fake a diff) ----
  const stableMap = (o: Record<string, string>) => Object.keys(o).sort().map(k => `${k}=${o[k]}`).join('|');
  const formFingerprint = () => JSON.stringify({
    name, description,
    lists: [...selectedListIds].sort((a, b) => a - b),
    templateKind, instanceId, waTemplateId,
    cols: stableMap(paramColumns), media: stableMap(mediaValues),
    contentMode, outboundTemplateId, plainTextBody,
  });

  // Capture the pristine snapshot once the modal is open AND the async edit target-load settled
  // (loadingTargets only flips during openEdit; create captures on first committed render).
  useEffect(() => {
    if (modalOpen && !loadingTargets) {
      if (baselineRef.current === null) baselineRef.current = formFingerprint();
    }
    if (!modalOpen) baselineRef.current = null;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [modalOpen, loadingTargets]);

  // ---- aha #2/#3/#4: debounced list-insight fetch on target change. Stale responses dropped. ----
  useEffect(() => {
    if (!modalOpen || selectedListIds.length === 0) {
      setListInsight(null);
      setInsightLoading(false);
      return;
    }
    setInsightLoading(true);
    const seq = ++insightSeqRef.current;
    const ids = selectedListIds.slice();
    const t = window.setTimeout(async () => {
      try {
        const insight = await api.getDataListPreviewSample(ids);
        if (seq === insightSeqRef.current) setListInsight(insight);
      } catch {
        // Fail-silent by design: the aha extras must never block the core create/edit flow.
        if (seq === insightSeqRef.current) setListInsight(null);
      } finally {
        if (seq === insightSeqRef.current) setInsightLoading(false);
      }
    }, 400);
    return () => window.clearTimeout(t);
  }, [modalOpen, selectedListIds]);

  // aha #2: resolves a placeholder to the sample recipient's mapped-column value (preview only).
  const sampleResolver = (key: string): string | null => {
    if (!showSample || !listInsight?.sample) return null;
    const col = paramColumns[key];
    return col ? recordValue(listInsight.sample, col) : null;
  };
  const sampleDisplayName = listInsight?.sample
    ? [listInsight.sample.name, listInsight.sample.surname].filter(Boolean).join(' ')
    : '';

  // aha #3 (revised, Q 2026-06-10): column option label shows ONLY the sample value — the
  // "%X dolu / boş" fill-rate suffix was dropped as noise.
  const columnOptionLabel = (c: { v: string; label: string }): string => {
    let label = c.label;
    const sampleVal = listInsight?.sample ? recordValue(listInsight.sample, c.v) : null;
    if (sampleVal) label += ` — "${sampleVal.length > 18 ? `${sampleVal.slice(0, 18)}…` : sampleVal}"`;
    return label;
  };

  // ---- GR-8: the plain text a test message would carry, from the CURRENT (unsaved) form state.
  // HSM template: rendered preview text — header/body/footer joined, mapped placeholders replaced
  // with the sample recipient's values (independent of the showSample display toggle); unresolved
  // placeholders stay literal {{key}} so the operator SEES what could not be resolved.
  // plain_text: the free-text body, or the selected gallery template's text verbatim.
  const testMessageText = useMemo(() => {
    if (templateKind === 'wapcrm_template' && selectedTemplate?.preview) {
      const resolve = (key: string): string | null => {
        const col = paramColumns[key];
        return col && listInsight?.sample ? recordValue(listInsight.sample, col) : null;
      };
      const render = (text: string | null | undefined) =>
        (text ?? '').replace(/\{\{\s*([^}\s]+)\s*\}\}/g, (m, key: string) => resolve(key) ?? m);
      const headerText = selectedTemplate.preview.header?.type === 'TEXT'
        ? render(selectedTemplate.preview.header.text) : '';
      const parts = [headerText, render(selectedTemplate.preview.body), render(selectedTemplate.preview.footer)]
        .map(p => p.trim()).filter(p => p.length > 0);
      return parts.join('\n\n');
    }
    if (templateKind === 'plain_text' && contentMode === 'free_text') return plainTextBody.trim();
    if (templateKind === 'plain_text' && contentMode === 'gallery_template')
      return (selectedGalleryTemplate?.message_template ?? '').trim();
    return '';
  }, [templateKind, selectedTemplate, paramColumns, listInsight, contentMode, plainTextBody, selectedGalleryTemplate]);

  async function sendTest() {
    const phone = testPhone.trim();
    if (!phone || !testMessageText || testSending) return;
    setTestSending(true);
    setTestResult(null);
    try {
      // HSM template: the REAL template wire model goes out (templateId + resolved parameters +
      // headerMedia — wapcrm-api-integration-guide §3.2; Q 2026-06-10). Param values resolve from
      // the sample recipient via the current column mapping; with no sample value the paramKey
      // itself ships as a visible dummy (a 622 reject would teach the operator less than seeing
      // the template render with placeholder names). plain_text keeps the literal body.
      const isHsmTest = templateKind === 'wapcrm_template' && !!selectedTemplate && !!waTemplateId;
      let req: ProjectTestSendRequest;
      if (isHsmTest) {
        const params: Record<string, string> = {};
        for (const ph of placeholders) {
          const col = paramColumns[ph.key];
          const sampleVal = col && listInsight?.sample ? recordValue(listInsight.sample, col) : null;
          params[ph.key] = sampleVal ?? ph.key;
        }
        const mediaUrl = mediaInputs.length > 0 ? (mediaValues[mediaKey(mediaInputs[0])] ?? '').trim() : '';
        req = {
          phone,
          instance_id: Number(instanceId),
          wa_template_id: waTemplateId,
          template_params: params,
          ...(mediaUrl ? { template_header_media_url: mediaUrl } : {}),
        };
      } else {
        req = { phone, message_text: testMessageText };
      }
      const res = await api.projectTestSend(req);
      if (res.queued === 0) {
        setTestResult({ ok: false, text: 'Mesaj gönderilmedi: numara filtrelendi (opt-out / izin).' });
        return;
      }
      // 202 = sadece "kuyruğa girdi". Worker saniyeler içinde teslim eder/düşürür — NİHAİ sonucu
      // yokla ki operatör hatayı (failed_reason) gerçekten GÖRSÜN (Q 2026-06-10: "hata varsa göstersin").
      setTestResult({ ok: null, text: 'Kuyruğa alındı — gönderim sonucu bekleniyor…' });
      for (let i = 0; i < 10; i++) {
        await new Promise<void>(resolve => { window.setTimeout(resolve, 1500); });
        let st;
        try {
          st = await api.getBroadcastStatus(res.broadcast_id);
        } catch {
          continue; // tek yoklama hatası akışı bozmasın — sonraki tura geç
        }
        if (st.failed > 0) {
          // Keep the headline short; the provider reason (with its Meta code) gets a
          // rich Turkish explanation via WaErrorPopover in the banner below.
          setTestResult({
            ok: false,
            text: 'Test mesajı GÖNDERİLEMEDİ.',
            reason: st.failed_reason_sample ?? null,
          });
          return;
        }
        if (st.sent + st.delivered + st.read > 0) {
          setTestResult({ ok: true, text: 'Test mesajı gönderildi ✓ — WhatsApp\'ınızı kontrol edin.' });
          return;
        }
        if (st.status === 'completed') {
          // Tamamlandı ama ne sent ne failed: ör. sağlayıcı tarafında engellendi (906/907).
          setTestResult({ ok: false, text: 'Gönderim tamamlandı ama mesaj iletilmedi (alıcı tarafında engellenmiş olabilir).' });
          return;
        }
      }
      setTestResult({ ok: null, text: 'Sonuç henüz netleşmedi — mesaj hâlâ kuyrukta görünüyor.' });
    } catch (e) {
      setTestResult({ ok: false, text: errText(e, 'Test mesajı gönderilemedi') });
    } finally {
      setTestSending(false);
    }
  }

  // ---- GR-9: status chip filtering over the single includeArchived fetch.
  const statusCounts = useMemo(() => {
    const counts: Partial<Record<ProjectStatus, number>> = {};
    for (const p of projects) counts[p.status] = (counts[p.status] ?? 0) + 1;
    return counts;
  }, [projects]);
  const visibleProjects = useMemo(
    () => projects.filter(p => (statusFilter === 'all' ? p.status !== 'archived' : p.status === statusFilter)),
    [projects, statusFilter]);

  function resetSendConfig() {
    // New projects default to the approved-template (HSM) flow (Q 2026-06-10); plain_text is disabled in the UI.
    // A template is optional to save — leaving it unselected stores no send config (metadata-only project).
    setTemplateKind('wapcrm_template');
    setInstanceId('');
    setWaTemplateId('');
    setTemplates([]);
    setParamColumns({});
    setMediaValues({});
    setTemplatesError(null);
    setContentMode('');
    setOutboundTemplateId('');
    setPlainTextBody('');
  }

  function resetTestSend() {
    setTestPhone('');
    setTestSending(false);
    setTestResult(null);
  }

  function openCreate() {
    setEditing(null);
    setName('');
    setDescription('');
    setSelectedListIds([]);
    resetSendConfig();
    setShowSample(true);
    setFormError(null);
    resetTestSend();
    setModalOpen(true);
  }

  async function openEdit(p: ProjectSummary) {
    setEditing(p);
    setName(p.name);
    setDescription(p.description ?? '');
    setSelectedListIds([]);
    setShowSample(true);
    setFormError(null);
    resetTestSend();
    setModalOpen(true);

    // Re-populate the send config from the project (the summary now carries it).
    // Default an unconfigured project to the HSM flow (matches create); an existing plain_text project keeps its kind.
    setTemplateKind(p.template_kind ?? 'wapcrm_template');
    setInstanceId(p.instance_id != null ? String(p.instance_id) : '');
    setWaTemplateId(p.wa_template_id ?? '');
    // Re-populate column mapping (text placeholders) + media URLs from the stored param_mapping.
    const cols: Record<string, string> = {};
    const media: Record<string, string> = {};
    for (const x of p.param_mapping ?? []) {
      if (x.kind === 'media') {
        if (x.value) media[`${x.location ?? ''}:${x.mediaType ?? ''}`] = x.value;
      } else if (x.paramKey) {
        // column-mapped (new) or legacy literal value — prefer the column reference.
        if (x.column) cols[x.paramKey] = x.column;
      }
    }
    setParamColumns(cols);
    setMediaValues(media);
    setTemplates([]);
    setTemplatesError(null);
    // plain_text content (migration 059): re-populate the operator's content choice.
    setContentMode(p.content_mode ?? '');
    setOutboundTemplateId(p.outbound_template_id != null ? String(p.outbound_template_id) : '');
    setPlainTextBody(p.plain_text_body ?? '');
    // Load the template list so the picker + param labels render (values are already set above).
    if (p.template_kind === 'wapcrm_template' && p.instance_id != null) void fetchTemplatesFor(p.instance_id);

    // Pull the project's current target list ids (ProjectSummary carries only the count).
    setLoadingTargets(true);
    try {
      const detail = await api.getProject(p.id);
      setSelectedListIds(detail.targets.map(t => t.data_list_id));
    } catch (e) {
      setFormError(errText(e, 'Proje hedefleri yüklenemedi'));
    } finally {
      setLoadingTargets(false);
    }
  }

  function closeModal() {
    if (saving) return; // don't drop the modal mid-save
    // aha #5: a dirty form must not be lost by an accidental X-click. The successful-save path
    // closes via setModalOpen(false) directly and never hits this guard.
    const baseline = baselineRef.current;
    if (baseline !== null && baseline !== formFingerprint()
        && !window.confirm('Kaydedilmemiş değişiklikler var. Kapatılsın mı?'))
      return;
    setModalOpen(false);
    setEditing(null);
  }

  function toggleList(id: number) {
    setSelectedListIds(prev => (prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]));
  }

  async function save() {
    const trimmed = name.trim();
    if (!trimmed) { setFormError('Proje adı gerekli.'); return; }

    // Validate the send config. New projects default to the HSM (wapcrm_template) flow, but a template is
    // OPTIONAL (Q 2026-06-10): leaving it unpicked stores no send config (metadata-only project). plain_text
    // stays supported for EDITING existing projects (the UI disables choosing it for new ones).
    let paramMapping: ProjectTemplateParam[] | undefined;
    let buildConfig = false;

    if (templateKind === 'plain_text') {
      if (!instanceId || Number.isNaN(Number(instanceId))) {
        setFormError('Gönderim için bir WhatsApp kanalı seçin.'); return;
      }
      // Content is chosen in settings: gallery template OR free text (Q decision 2026-06-09).
      if (contentMode === '') { setFormError('Düz metin için içerik türü seçin: galeri şablonu veya serbest metin.'); return; }
      if (contentMode === 'gallery_template' && (!outboundTemplateId || Number.isNaN(Number(outboundTemplateId)))) {
        setFormError('Galeri şablonu için bir şablon seçin.'); return;
      }
      if (contentMode === 'free_text') {
        if (!plainTextBody.trim()) { setFormError('Serbest metin için bir mesaj yazın.'); return; }
        if (plainTextBody.trim().length > PLAIN_TEXT_BODY_MAX) {
          setFormError(`Mesaj metni ${PLAIN_TEXT_BODY_MAX} karakteri aşıyor.`); return;
        }
      }
      buildConfig = true;
    } else if (templateKind === 'wapcrm_template' && waTemplateId) {
      // A template was picked → full HSM config (no template = metadata-only, omitted below).
      if (!instanceId || Number.isNaN(Number(instanceId))) {
        setFormError('Gönderim için bir WhatsApp kanalı seçin.'); return;
      }
      // Guard: never rebuild param_mapping from an unloaded template (would wipe stored params).
      if (!selectedTemplate) { setFormError('Şablon bilgisi yüklenemedi. Lütfen tekrar deneyin.'); return; }
      // Each {{...}} placeholder must be mapped to a list column; each media input needs a public URL.
      for (const ph of placeholders) {
        if (!paramColumns[ph.key]) { setFormError('Her şablon parametresi için bir liste kolonu seçin.'); return; }
      }
      for (const ri of mediaInputs) {
        if (!(mediaValues[mediaKey(ri)] ?? '').trim()) { setFormError('Şablon medyası için bir URL girin.'); return; }
      }
      paramMapping = [
        ...placeholders.map(ph => ({
          kind: 'text', location: ph.location, paramKey: ph.key, mediaType: null,
          source: 'column' as const, column: paramColumns[ph.key],
        })),
        ...mediaInputs.map(ri => ({
          kind: 'media', location: ri.location, paramKey: ri.paramKey, mediaType: ri.mediaType,
          source: 'literal' as const, value: (mediaValues[mediaKey(ri)] ?? '').trim(),
        })),
      ];
      buildConfig = true;
    }
    // else: templateKind === '' OR wapcrm_template with no template picked → metadata-only (omit config block).

    // When no config is built the block is omitted (server leaves the project's existing config unchanged).
    // For plain_text the content carriers are mutually exclusive per content_mode; the server (BuildSendConfig)
    // re-validates + enforces the same exclusivity authoritatively.
    const sendConfig = !buildConfig
      ? {}
      : {
          // buildConfig is only true when templateKind is 'plain_text' | 'wapcrm_template' (never ''), but the
          // boolean flag doesn't narrow the union — coalesce the impossible '' to null to satisfy the type.
          template_kind: templateKind || null,
          instance_id: Number(instanceId),
          wa_template_id: templateKind === 'wapcrm_template' ? waTemplateId : null,
          param_mapping: templateKind === 'wapcrm_template' ? (paramMapping ?? []) : null,
          content_mode: templateKind === 'plain_text' ? (contentMode as ProjectContentMode) : null,
          outbound_template_id:
            templateKind === 'plain_text' && contentMode === 'gallery_template' ? Number(outboundTemplateId) : null,
          plain_text_body:
            templateKind === 'plain_text' && contentMode === 'free_text' ? plainTextBody.trim() : null,
        };

    setSaving(true);
    setFormError(null);
    try {
      const descVal = description.trim() === '' ? null : description.trim();
      if (editing) {
        await api.updateProject(editing.id, {
          name: trimmed, description: descVal, target_list_ids: selectedListIds, ...sendConfig,
        });
      } else {
        await api.createProject({
          name: trimmed, description: descVal, target_list_ids: selectedListIds, ...sendConfig,
        });
      }
      setModalOpen(false);
      setEditing(null);
      await loadProjects();
    } catch (e) {
      setFormError(errText(e, 'Proje kaydedilemedi'));
    } finally {
      setSaving(false);
    }
  }

  async function archive(p: ProjectSummary) {
    if (!window.confirm(`"${p.name}" projesi arşivlensin mi? Çalıştırma geçmişi korunur; ad yeniden kullanılabilir.`)) return;
    setArchivingId(p.id);
    try {
      await api.archiveProject(p.id);
      await loadProjects();
    } catch (e) {
      setLoadError(errText(e, 'Proje arşivlenemedi'));
    } finally {
      setArchivingId(null);
    }
  }

  // ---- Run lifecycle (SS-D): pause / resume / cancel ----
  // Each is a project-level POST that returns the fresh detail; we just reload the table after.
  async function runLifecycle(p: ProjectSummary, action: 'pause' | 'resume' | 'cancel', failMsg: string) {
    setLifecycleBusyId(p.id);
    try {
      if (action === 'pause') await api.projectSendPause(p.id);
      else if (action === 'resume') await api.projectSendResume(p.id);
      else await api.projectSendCancel(p.id);
      await loadProjects();
    } catch (e) {
      setLoadError(errText(e, failMsg));
    } finally {
      setLifecycleBusyId(null);
    }
  }

  // İptal is the one irreversible action (sent messages are not recalled) -> confirm modal first.
  // Keep the modal open (busy spinner) during the call, then close it.
  async function confirmCancelRun() {
    if (!cancelTarget) return;
    await runLifecycle(cancelTarget, 'cancel', 'Gönderim iptal edilemedi');
    setCancelTarget(null);
  }

  // ---- Rapor (delivery report) drawer ----
  function openReport(p: ProjectSummary) {
    setReportProject(p);
    setReportRuns([]);
    setReportCampaign('');
    setReportSearch('');
    setReportSearchDraft('');
    setReportStatus('');
    setReportFailCode('');
    setFailureBuckets([]);
    setReportSort(null);
    setReportDir('desc');
    setReportPage(1);
    setReportData(null);
    setReportError(null);
    setReportPullNote(null);
    void api.getProjectReportRuns(p.id)
      .then((runs) => {
        setReportRuns(runs);
        // On first open default to the LATEST run (runs are created_at DESC), not "Tüm gönderimler".
        // The operator can still pick "Tüm gönderimler" from the dropdown.
        if (runs.length > 0) setReportCampaign(runs[0].campaign_id);
      })
      .catch((e) => setReportError(errText(e, 'Gönderimler yüklenemedi')));
  }

  function closeReport() {
    setReportProject(null);
    setReportData(null);
    setReportError(null);
    setReportPullNote(null);
    setReportStatus('');
    setReportFailCode('');
    setFailureBuckets([]);
    setBulkResendConfirm(false);
  }

  // Single "Yenile" action (merged from the old DB-reload + "Durumu Yenile" pair): re-read the recipient
  // page from the DB AND best-effort pull live cxapi message-status for pending recipients, then refresh
  // runs + the project table. The DB re-fetch runs UNCONDITIONALLY (works for every tenant); the live pull
  // is gated by the CxapiSend allowlist, so a non-allowlisted tenant (INV-OB-094) silently skips it instead
  // of seeing an error — the table still refreshed.
  async function refreshReport() {
    if (!reportProject) return;
    setReportStatusPulling(true);
    setReportPullNote(null);
    setReportError(null);
    let pullHardFailed = false;
    try {
      // 1) Best-effort live status pull FIRST, so the DB carries the latest statuses before we re-read.
      try {
        const res = await api.projectRefreshReportStatus(reportProject.id, reportCampaign || undefined);
        setReportPullNote(`${res.updated.toLocaleString('tr-TR')} / ${res.checked.toLocaleString('tr-TR')} kayıt güncellendi.`);
      } catch (e) {
        if (e instanceof ApiClientError && e.errorCode === 'INV-OB-094') {
          // Not allowlisted for the live pull — expected, not an error: fall through to the plain DB
          // re-fetch below so this tenant still gets a refresh.
        } else {
          // A real live-pull failure (network / server). Log it AND show a DURABLE error: skip the
          // recipient reload below so the recipients effect (which clears reportError on each load) does
          // not immediately erase this message. The DB is unchanged by a failed pull, so nothing is lost.
          console.warn('[rapor] canlı durum sorgulanamadı:', errText(e, 'status pull failed'));
          setReportError(errText(e, 'Canlı durum sorgulanamadı'));
          pullHardFailed = true;
        }
      }
      // 2) THEN re-fetch the recipient page so the table reflects the pulled statuses (success) or simply
      //    refreshes (403 not-allowlisted). Skipped on a hard pull failure to keep the error visible.
      if (!pullHardFailed) {
        setReportReloadTick((t) => t + 1);
        // Secondary refreshes — a failure here must not mask the result above.
        try { setReportRuns(await api.getProjectReportRuns(reportProject.id)); }
        catch (e) { console.warn('[rapor] yenileme sonrası gönderimler yenilenemedi:', errText(e, 'runs refresh failed')); }
        void loadProjects();
      }
    } finally {
      setReportStatusPulling(false);
    }
  }

  // Debounce the search box (300ms) into the applied filter; reset to page 1 on any new term.
  useEffect(() => {
    const t = setTimeout(() => { setReportSearch(reportSearchDraft.trim()); setReportPage(1); }, 300);
    return () => clearTimeout(t);
  }, [reportSearchDraft]);

  // Load the recipient page whenever the open project / run filter / search / page / reload tick change.
  useEffect(() => {
    if (!reportProject) return;
    let cancelled = false;
    setReportLoading(true);
    setReportError(null);
    api.getProjectReportRecipients(reportProject.id, {
      campaignId: reportCampaign || undefined,
      search: reportSearch || undefined,
      status: reportStatus || undefined,
      failCode: reportFailCode || undefined,
      sort: reportSort ?? undefined,
      dir: reportSort ? reportDir : undefined,
      page: reportPage,
      pageSize: REPORT_PAGE_SIZE,
    })
      .then((res) => { if (!cancelled) setReportData(res); })
      .catch((e) => { if (!cancelled) setReportError(errText(e, 'Rapor yüklenemedi')); })
      .finally(() => { if (!cancelled) setReportLoading(false); });
    return () => { cancelled = true; };
  }, [reportProject, reportCampaign, reportSearch, reportStatus, reportFailCode, reportSort, reportDir, reportPage, reportReloadTick]);

  // #4 Failure-reason breakdown fetch: failed/ambiguous grouped by Meta code for the open project/run. Re-runs
  // on run change + refresh tick (tracks live sends + resends). Fail-silent — an extra over the recipient table.
  useEffect(() => {
    if (!reportProject) { setFailureBuckets([]); return; }
    let cancelled = false;
    api.getProjectFailureBreakdown(reportProject.id, reportCampaign || undefined)
      .then((b) => { if (!cancelled) setFailureBuckets(b); })
      .catch((e) => {
        if (!cancelled) {
          // Fail-silent (an extra over the table) but observable — log so a broken endpoint isn't invisible.
          console.warn('[rapor] başarısızlık kırılımı alınamadı:', errText(e, 'breakdown failed'));
          setFailureBuckets([]);
        }
      });
    return () => { cancelled = true; };
  }, [reportProject, reportCampaign, reportReloadTick]);

  // Column-header click: toggle direction on the active column, else switch to the new column (newest-first
  // default). Resets to page 1 so the operator sees the top of the re-sorted set.
  function onReportSort(key: ReportSortKey) {
    setReportPage(1);
    if (reportSort === key) setReportDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setReportSort(key); setReportDir('desc'); }
  }

  // Resend ONE undelivered (failed/ambiguous) recipient; refresh runs + recipients + the project table.
  async function doResend(messageId: number) {
    if (!reportProject) return;
    setResendingId(messageId);
    try {
      await api.projectReportResend(reportProject.id, messageId);
      // Refresh the run counters (secondary to the resend itself). A failure here must NOT fail the
      // resend or be swallowed silently: log it and keep the prior counters; the recipient table still
      // reloads below so the operator sees the row's new status either way.
      try {
        setReportRuns(await api.getProjectReportRuns(reportProject.id));
      } catch (e) {
        console.warn('[rapor] resend sonrası gönderim sayaçları yenilenemedi:', errText(e, 'runs refresh failed'));
      }
      setReportReloadTick((t) => t + 1); // re-fetch the current recipient page (status changed server-side)
      void loadProjects();               // the project row counters/status changed too
    } catch (e) {
      setReportError(errText(e, 'Yeniden gönderilemedi'));
    } finally {
      setResendingId(null);
    }
  }

  // Resend EVERY undelivered (failed/ambiguous) recipient of the WHOLE project in one server-side transaction,
  // then refresh runs + recipients + the project table (same trio as the single resend). A 0-requeued result is
  // a successful no-op (nothing was eligible) — surfaced as an info note, not an error.
  async function doResendAll() {
    if (!reportProject) return;
    setBulkResending(true);
    setReportError(null);
    setReportPullNote(null);
    try {
      const { requeued } = await api.projectReportResendBulk(reportProject.id);
      setBulkResendConfirm(false);
      setReportPullNote(
        requeued > 0
          ? `${requeued.toLocaleString('tr-TR')} hatalı kayıt yeniden gönderim için kuyruğa alındı.`
          : 'Yeniden gönderilecek hatalı kayıt bulunamadı.');
      try {
        setReportRuns(await api.getProjectReportRuns(reportProject.id));
      } catch (e) {
        console.warn('[rapor] toplu resend sonrası gönderim sayaçları yenilenemedi:', errText(e, 'runs refresh failed'));
      }
      setReportReloadTick((t) => t + 1);
      void loadProjects();
    } catch (e) {
      setBulkResendConfirm(false);
      setReportError(errText(e, 'Hatalı kayıtlar yeniden gönderilemedi'));
    } finally {
      setBulkResending(false);
    }
  }

  // Export ALL rows matching the active filter (run + search), not just the visible page: re-fetch with a
  // large page size, build the CSV client-side, and download. The server caps pageSize at 5000.
  async function exportReportCsv() {
    if (!reportProject) return;
    setReportExporting(true);
    try {
      const all = await api.getProjectReportRecipients(reportProject.id, {
        campaignId: reportCampaign || undefined,
        search: reportSearch || undefined,
        status: reportStatus || undefined,
        failCode: reportFailCode || undefined,
        page: 1,
        pageSize: 5000,
      });
      const csv = recipientsToCsv(all.items);
      const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      const safeName = (reportProject.name || 'proje').replace(/[^a-zA-Z0-9_-]+/g, '_').slice(0, 40);
      a.href = url;
      a.download = `rapor_${safeName}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (e) {
      setReportError(errText(e, 'CSV dışa aktarılamadı'));
    } finally {
      setReportExporting(false);
    }
  }

  // ---- Run dispatch (Gönder) ----
  // Client mirror of the server eligibility (ProjectsService.ResolveSendContent/ResolveHsmContent +
  // target check). PR-4: an HSM (onaylı şablon) project is dispatchable once it carries a template +
  // instance — the server still live-validates against the cxapi catalog (and rejects with INV-OB-08x
  // when the tenant is not enabled). Returns a disable reason, or null when the project can be sent.
  function sendDisabledReason(p: ProjectSummary): string | null {
    // One active run per project (mirrors INV-OB-080): no new send while a run is in flight or paused.
    if (p.status === 'running' || p.status === 'paused')
      return 'Bu projede aktif bir gönderim var. Önce duraklatın, sürdürün veya iptal edin.';
    if (p.template_kind === 'wapcrm_template') {
      if (!(p.wa_template_id && p.wa_template_id.trim()))
        return 'Onaylı şablon seçilmemiş. Proje ayarlarından bir şablon seçin.';
      if (!(p.instance_id && p.instance_id > 0))
        return 'Gönderim hattı (instance) seçilmemiş. Proje ayarlarını tamamlayın.';
    } else {
      if (p.template_kind !== 'plain_text')
        return 'Önce proje ayarlarından gönderim içeriği (galeri şablonu veya serbest metin) ekleyin.';
      if (p.content_mode === 'gallery_template' && !(p.outbound_template_id && p.outbound_template_id > 0))
        return 'Galeri şablonu seçilmemiş. Proje ayarlarından bir şablon seçin.';
      if (p.content_mode === 'free_text' && !(p.plain_text_body && p.plain_text_body.trim()))
        return 'Serbest metin boş. Proje ayarlarından bir mesaj yazın.';
      if (p.content_mode !== 'gallery_template' && p.content_mode !== 'free_text')
        return 'Önce proje ayarlarından gönderim içeriği (galeri şablonu veya serbest metin) ekleyin.';
    }
    if (p.target_count <= 0)
      return 'Projenin hedef listesi yok. Düzenle’den en az bir liste ekleyin.';
    return null;
  }

  // Approved-template hover body: header/body/footer/buttons joined as plain text
  // (native title tooltip — the table container is overflow-hidden, so an absolutely
  // positioned tooltip would clip; title cannot).
  function waPreviewText(t: WaTemplate): string {
    const parts: string[] = [];
    const h = t.preview?.header;
    if (h?.text) parts.push(h.text);
    else if (h?.type) parts.push(`[${h.type} başlık]`);
    if (t.preview?.body) parts.push(t.preview.body);
    if (t.preview?.footer) parts.push(t.preview.footer);
    const btns = (t.preview?.buttons ?? []).map(b => b?.text).filter((x): x is string => !!x);
    if (btns.length) parts.push(`[${btns.join(' | ')}]`);
    return parts.join('\n\n') || 'Şablon içeriği boş';
  }

  // Resolve a project's approved (HSM) template from the loaded per-instance catalog, or null
  // (not a wapcrm_template project, no template id, or the catalog isn't loaded/failed). Used for
  // the structured preview in the Gönder modal + the table hover card.
  function resolveWaTemplate(p: ProjectSummary): WaTemplate | null {
    if (p.template_kind !== 'wapcrm_template' || !p.wa_template_id?.trim()) return null;
    const catalog = p.instance_id != null ? rowTemplates[p.instance_id] : undefined;
    return Array.isArray(catalog) ? (catalog.find(t => t.templateId === p.wa_template_id) ?? null) : null;
  }

  // Row content line: the assigned content's NAME for the cell + full content for the tooltip.
  // Returns null when the project has no content yet (no extra row line).
  function rowContentInfo(p: ProjectSummary): { label: string; hover: string } | null {
    if (p.template_kind === 'wapcrm_template' && p.wa_template_id?.trim()) {
      const catalog = p.instance_id != null ? rowTemplates[p.instance_id] : undefined;
      const tpl = Array.isArray(catalog)
        ? catalog.find(t => t.templateId === p.wa_template_id)
        : undefined;
      return {
        label: `Şablon: ${tpl?.name?.trim() || p.wa_template_id}`,
        hover: tpl
          ? waPreviewText(tpl)
          : catalog === 'error'
            ? 'Şablon içeriği yüklenemedi — Yenile ile tekrar deneyin'
            : Array.isArray(catalog)
              ? 'Şablon katalogda bulunamadı'
              : 'Şablon içeriği yükleniyor…',
      };
    }
    if (p.template_kind === 'plain_text' && p.content_mode === 'gallery_template' && p.outbound_template_id != null) {
      const g = galleryTemplates.find(t => t.id === p.outbound_template_id);
      return {
        label: `Galeri: ${g?.name ?? `#${p.outbound_template_id}`}`,
        hover: g?.message_template?.trim() || 'Şablon içeriği yüklenemedi',
      };
    }
    if (p.template_kind === 'plain_text' && p.content_mode === 'free_text' && p.plain_text_body?.trim()) {
      return { label: 'Serbest metin', hover: p.plain_text_body };
    }
    return null;
  }

  function sendContentSummary(p: ProjectSummary): string {
    if (p.template_kind === 'wapcrm_template')
      return p.wa_template_id?.trim() ? `Onaylı şablon: ${p.wa_template_id}` : '—';
    if (p.template_kind === 'plain_text' && p.content_mode === 'free_text')
      return p.plain_text_body?.trim() || '(boş)';
    if (p.template_kind === 'plain_text' && p.content_mode === 'gallery_template') {
      const t = galleryTemplates.find(g => g.id === p.outbound_template_id);
      return t ? `Galeri şablonu: ${t.name}` : `Galeri şablonu #${p.outbound_template_id ?? '?'}`;
    }
    return '—';
  }

  async function openSend(p: ProjectSummary) {
    const cid = crypto.randomUUID?.() ?? `proj-${p.id}-${Date.now()}`;
    setSendProject(p);
    setSendCampaignId(cid);
    setSendPreview(null);
    setSendStatus(null);
    setSendError(null);
    setSendPhase('previewing');
    // Load gallery names lazily so the content summary can show a name (not just an id).
    if (p.template_kind === 'plain_text' && p.content_mode === 'gallery_template') void loadGalleryTemplates();
    try {
      const preview = await api.projectSendPreview(p.id, cid);
      setSendPreview(preview);
      setSendPhase('preview');
    } catch (e) {
      setSendError(errText(e, 'Önizleme oluşturulamadı'));
      setSendPhase('error');
    }
  }

  async function confirmSend() {
    if (!sendProject) return;
    setSendPhase('confirming');
    setSendError(null);
    try {
      const status = await api.projectSendConfirm(sendProject.id, sendCampaignId);
      setSendStatus(status);
      setSendPhase('sent');
      await loadProjects(); // refresh table counters/status after dispatch
    } catch (e) {
      setSendError(errText(e, 'Gönderim başlatılamadı'));
      setSendPhase('error');
    }
  }

  function closeSend() {
    setSendProject(null);
    setSendError(null);
    setSendPreview(null);
    setSendStatus(null);
  }

  // ---- #1 Auto-refresh (table): silently reload while a run is active. Paused when the tab is hidden or the
  // full-screen report drawer covers the table (its own poll takes over); stops when no run is active. ----
  const anyRunActive = useMemo(
    () => projects.some(p => p.status === 'running' || p.status === 'paused'),
    [projects]);
  useEffect(() => {
    if (!anyRunActive || reportProject) return;
    const id = window.setInterval(() => {
      if (document.visibilityState === 'visible') void loadProjects(true);
    }, 8000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [anyRunActive, reportProject]);

  // ---- #1 Auto-refresh (report drawer): silent recipient + counter refresh while the open run still has queued
  // work. The recipients effect won't flash a spinner (reportData already exists); the tick also re-runs breakdown. ----
  const reportRunActive = useMemo(() => {
    if (!reportProject || reportRuns.length === 0) return false;
    return reportCampaign
      ? reportRuns.some(r => r.campaign_id === reportCampaign && r.queued > 0)
      : reportRuns.some(r => r.queued > 0);
  }, [reportProject, reportRuns, reportCampaign]);
  useEffect(() => {
    if (!reportRunActive) return;
    const id = window.setInterval(() => {
      if (document.visibilityState !== 'visible' || !reportProject) return;
      setReportReloadTick(t => t + 1);
      void api.getProjectReportRuns(reportProject.id).then(setReportRuns)
        .catch((e) => console.warn('[rapor] otomatik gönderim sayaçları yenilenemedi:', errText(e, 'runs refresh failed')));
    }, 8000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reportRunActive, reportProject]);

  // ---- #5 Esc-to-close: dismiss the TOP-most open layer. A latest-ref keeps the handler's closures (closeModal's
  // dirty-guard, the busy flags) fresh without re-subscribing per render. A pinned WaErrorPopover is portalled with
  // focus inside it and runs its OWN Esc handler — bail so Esc dismisses the card, not the whole drawer. ----
  const escRef = useRef<() => void>(() => {});
  escRef.current = () => {
    const ae = document.activeElement;
    if (ae instanceof HTMLElement && ae.closest('[aria-label^="WhatsApp hata"]')) return;
    if (bulkResendConfirm) { if (!bulkResending) setBulkResendConfirm(false); return; }
    if (cancelTarget) { if (lifecycleBusyId !== cancelTarget.id) setCancelTarget(null); return; }
    if (sendProject) { closeSend(); return; }
    if (reportProject) { closeReport(); return; }
    if (modalOpen) { closeModal(); return; }
  };
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') escRef.current(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);

  return (
    <div className="p-6 max-w-6xl mx-auto space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-navy-900">Projeler</h1>
          <p className="text-sm text-navy-500 mt-0.5">Kayıtlı listeleri hedefleyen yeniden kullanılabilir kampanya projeleri oluşturun.</p>
        </div>
        {!featureDisabled && (
          <Button onClick={openCreate}>
            <Plus className="w-4 h-4" /> Yeni Proje
          </Button>
        )}
      </div>

      <div className="bg-white border border-navy-100 rounded-xl shadow-soft overflow-hidden">
        <div className="px-4 py-3 border-b border-navy-50 flex items-center gap-2 flex-wrap">
          <Briefcase className="w-4 h-4 text-navy-400" />
          <h2 className="text-sm font-medium text-navy-700">Projeler</h2>
          {/* GR-9: status filter ('Tümü' = everything except archived) — dropdown + Yenile (Q 2026-06-10) */}
          {!featureDisabled && (
            <div className="ml-auto flex items-center gap-2">
              {!loading && projects.length > 0 && (
                <select
                  value={statusFilter}
                  onChange={e => setStatusFilter(e.target.value as ProjectStatus | 'all')}
                  aria-label="Durum filtresi"
                  className="text-xs border border-navy-100 rounded-lg px-2 py-1.5 text-navy-600 bg-white tabular-nums focus:outline-none focus:border-brand-400"
                >
                  {([
                    { v: 'all' as const, label: 'Tümü', count: projects.filter(p => p.status !== 'archived').length },
                    ...((Object.keys(STATUS_META) as ProjectStatus[]).map(s => ({
                      v: s, label: STATUS_META[s].label, count: statusCounts[s] ?? 0,
                    }))),
                  ]).map(f => (
                    <option key={f.v} value={f.v}>
                      {f.label} ({f.count.toLocaleString('tr-TR')})
                    </option>
                  ))}
                </select>
              )}
              <Button
                size="sm"
                variant="secondary"
                disabled={loading}
                title="Proje listesini yenile"
                onClick={() => void loadProjects()}
              >
                <RefreshCw className={cn('w-3.5 h-3.5', loading && 'animate-spin')} /> Yenile
              </Button>
            </div>
          )}
        </div>

        {featureDisabled && (
          <div className="px-4 py-10 text-center text-navy-500 text-sm flex flex-col items-center gap-2">
            <Info className="w-5 h-5 text-navy-400" />
            <div className="font-medium">Bu özellik hesabınızda henüz etkin değil.</div>
            <div className="text-xs text-navy-400">Etkinleştirmek için Invekto ekibiyle iletişime geçin.</div>
          </div>
        )}

        {!featureDisabled && loadError && <div className="px-4 py-3 text-sm text-red-600">{loadError}</div>}

        {!featureDisabled && (loading ? (
          <div className="px-4 py-8 text-center text-navy-400 text-sm flex items-center justify-center gap-2">
            <Loader2 className="w-4 h-4 animate-spin" /> Yükleniyor…
          </div>
        ) : projects.length === 0 ? (
          <div className="px-4 py-10 text-center text-navy-400 text-sm">
            Henüz proje yok. “Yeni Proje” ile ilk projenizi oluşturun.
          </div>
        ) : visibleProjects.length === 0 ? (
          <div className="px-4 py-10 text-center text-navy-400 text-sm">
            Bu filtrede proje yok.
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-navy-50/50 text-navy-500 text-xs">
              <tr>
                <th className="text-left font-medium px-4 py-2">Proje</th>
                <th className="text-left font-medium px-4 py-2">Durum</th>
                <th className="text-right font-medium px-4 py-2" title="Hedef liste(ler)deki toplam gönderilebilir telefon adedi (opt-out / geçersiz numaralar hariç)">Alıcı</th>
                <th className="text-right font-medium px-4 py-2" title="Şu an 'gönderildi' durumunda bekleyen mesaj sayısı (iletilen/okunan ayrı kolonlarda)">Gönderildi</th>
                <th className="text-right font-medium px-4 py-2" title="Şu an 'iletildi' durumundaki mesaj sayısı (henüz okunmamış)">İletildi</th>
                <th className="text-right font-medium px-4 py-2" title="Okundu olarak işaretlenen mesaj sayısı">Okundu</th>
                <th className="text-right font-medium px-4 py-2">Başarısız</th>
                <th className="text-right font-medium px-4 py-2">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {visibleProjects.map(p => {
                const contentInfo = rowContentInfo(p);
                return (
                <tr
                  key={p.id}
                  className={cn('border-t border-navy-50', p.status !== 'archived' && 'cursor-pointer hover:bg-navy-50/40')}
                  onDoubleClick={e => {
                    if (p.status === 'archived') return;
                    // Action buttons live inside the row — a double-click on (or bubbling from)
                    // any interactive control must not ALSO open the edit modal.
                    if ((e.target as HTMLElement).closest('button, a, input, select')) return;
                    void openEdit(p);
                  }}
                  title={p.status !== 'archived' ? 'Düzenlemek için çift tıklayın' : undefined}
                >
                  <td className="px-4 py-2.5">
                    <div className="text-navy-900">{p.name}</div>
                    {p.description && <div className="text-xs text-navy-400 truncate max-w-xs">{p.description}</div>}
                    {contentInfo && (
                      <div
                        className="text-[11px] text-navy-400 truncate max-w-xs cursor-help inline-flex items-center gap-1"
                        onMouseEnter={e => setRowHover({ project: p, rect: e.currentTarget.getBoundingClientRect() })}
                        onMouseLeave={() => setRowHover(prev => (prev?.project.id === p.id ? null : prev))}
                      >
                        <Info className="w-3 h-3 shrink-0 text-navy-300" />
                        <span className="truncate">{contentInfo.label}</span>
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-2.5">
                    <StatusBadge status={p.status} />
                    {/* #2 progress bar — only for an active run. processed = terminal-status counts; total =
                        processed + still-queued, so the bar reaches 100% exactly when the run drains. */}
                    {(p.status === 'running' || p.status === 'paused') && (() => {
                      const processed = p.sent_count + p.delivered_count + p.read_count + p.failed_count + p.ambiguous_count + p.cancelled_count;
                      const totalRun = processed + p.queued_count;
                      if (totalRun <= 0) return null;
                      const pct = Math.min(100, Math.round((processed / totalRun) * 100));
                      return (
                        <div className="mt-1 w-28" title={`${processed.toLocaleString('tr-TR')} / ${totalRun.toLocaleString('tr-TR')} işlendi (%${pct})`}>
                          <div className="h-1.5 rounded-full bg-navy-100 overflow-hidden">
                            <div
                              className={cn('h-full rounded-full transition-all duration-500', p.status === 'paused' ? 'bg-amber-400' : 'bg-brand-500')}
                              style={{ width: `${pct}%` }}
                            />
                          </div>
                          <div className="text-[10px] text-navy-400 mt-0.5 tabular-nums">{processed.toLocaleString('tr-TR')} / {totalRun.toLocaleString('tr-TR')}</div>
                        </div>
                      );
                    })()}
                    {p.cancelled_count > 0 && (
                      <div className="text-[11px] text-navy-400 mt-0.5 tabular-nums">{p.cancelled_count.toLocaleString('tr-TR')} iptal</div>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-right tabular-nums" title={`${p.target_count.toLocaleString('tr-TR')} liste`}>{p.recipients_total.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.sent_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.delivered_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums text-green-600">{p.read_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.failed_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5">
                    {p.status === 'archived' ? (
                      <div className="text-right text-xs text-navy-300">arşivli</div>
                    ) : (
                    // Borderless icon actions (Q 2026-06-17): ghost + icon-only + tooltip — the labelled
                    // buttons overflowed the row. SS-D status-aware run controls: running -> Duraklat;
                    // paused -> Devam Et; either -> İptal.
                    <div className="flex items-center justify-end gap-0.5">
                      {p.status === 'running' && (
                        <Button
                          size="sm"
                          variant="ghost"
                          className="px-2"
                          disabled={lifecycleBusyId === p.id}
                          title="Gönderimi duraklat"
                          aria-label="Gönderimi duraklat"
                          onClick={() => runLifecycle(p, 'pause', 'Gönderim duraklatılamadı')}
                        >
                          {lifecycleBusyId === p.id
                            ? <Loader2 className="w-4 h-4 animate-spin" />
                            : <Pause className="w-4 h-4 text-amber-600" />}
                        </Button>
                      )}
                      {p.status === 'paused' && (
                        <Button
                          size="sm"
                          variant="ghost"
                          className="px-2"
                          disabled={lifecycleBusyId === p.id}
                          title="Gönderimi sürdür"
                          aria-label="Gönderimi sürdür"
                          onClick={() => runLifecycle(p, 'resume', 'Gönderim sürdürülemedi')}
                        >
                          {lifecycleBusyId === p.id
                            ? <Loader2 className="w-4 h-4 animate-spin" />
                            : <Play className="w-4 h-4 text-brand-600" />}
                        </Button>
                      )}
                      {(p.status === 'running' || p.status === 'paused') && (
                        <Button
                          size="sm"
                          variant="ghost"
                          className="px-2"
                          disabled={lifecycleBusyId === p.id}
                          title="Gönderimi iptal et"
                          aria-label="Gönderimi iptal et"
                          onClick={() => setCancelTarget(p)}
                        >
                          <Ban className="w-4 h-4 text-red-500" />
                        </Button>
                      )}
                      {(() => {
                        const reason = sendDisabledReason(p);
                        return (
                          <Button
                            size="sm"
                            variant="ghost"
                            className="px-2"
                            disabled={reason !== null}
                            title={reason ?? 'Bu projeyi gönder'}
                            aria-label="Bu projeyi gönder"
                            onClick={() => openSend(p)}
                          >
                            <Send className="w-4 h-4 text-brand-600" />
                          </Button>
                        );
                      })()}
                      <Button size="sm" variant="ghost" className="px-2" onClick={() => openReport(p)} title="Gönderim raporu (kişi bazlı durum)" aria-label="Gönderim raporu">
                        <BarChart3 className="w-4 h-4 text-navy-500" />
                      </Button>
                      <Button size="sm" variant="ghost" className="px-2" onClick={() => openEdit(p)} title="Projeyi düzenle" aria-label="Projeyi düzenle">
                        <Pencil className="w-4 h-4 text-navy-500" />
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="px-2"
                        disabled={archivingId === p.id}
                        onClick={() => archive(p)}
                        title="Arşivle"
                        aria-label="Arşivle"
                      >
                        {archivingId === p.id
                          ? <Loader2 className="w-4 h-4 animate-spin text-navy-400" />
                          : <Archive className="w-4 h-4 text-navy-400" />}
                      </Button>
                    </div>
                    )}
                  </td>
                </tr>
                );
              })}
            </tbody>
          </table>
        ))}
      </div>

      {/* ---- Create / Edit modal (X-close + backdrop click, no İptal text button) ---- */}
      {modalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 p-4"
          onMouseDown={e => { if (e.target === e.currentTarget) closeModal(); }}
        >
          <div className="bg-white border border-navy-100 rounded-xl shadow-soft relative w-full max-w-6xl max-h-[90vh] overflow-y-auto">
            <button
              onClick={closeModal}
              className="absolute right-3 top-3 text-navy-300 hover:text-navy-600"
              title="Kapat"
              aria-label="Kapat"
            >
              <X className="w-5 h-5" />
            </button>

            <div className="px-5 pt-5 pb-2">
              <h2 className="text-base font-semibold text-navy-900">{editing ? 'Projeyi Düzenle' : 'Yeni Proje'}</h2>
              <p className="text-xs text-navy-400 mt-0.5">Ad, hedef listeler, gönderim kanalı ve şablonu seçin. Gönderim bu projeden yapılır.</p>
            </div>

            <div className="px-5 py-3">
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
                {/* Column 1: identity + target lists */}
                <div className="space-y-4">
              <Input
                label="Proje Adı"
                value={name}
                maxLength={MAX_NAME}
                placeholder="Örn. Yaz Kampanyası"
                onChange={e => setName(e.target.value)}
              />

              <div className="w-full">
                <label className="block text-sm font-medium text-navy-700 mb-1.5">Açıklama (opsiyonel)</label>
                <textarea
                  className="w-full px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus hover:border-navy-200 transition-all duration-150 resize-none"
                  rows={2}
                  maxLength={MAX_DESC}
                  placeholder="Projenin amacı / notlar"
                  value={description}
                  onChange={e => setDescription(e.target.value)}
                />
              </div>

              <div className="w-full">
                <div className="flex items-center gap-1.5 mb-1.5">
                  <ListChecks className="w-4 h-4 text-navy-400" />
                  <label className="text-sm font-medium text-navy-700">Hedef Listeler</label>
                  <span className="text-xs text-navy-400">
                    ({selectedListIds.length} seçili
                    {selectedListIds.length > 0 && (insightLoading
                      ? ' · sayılıyor…'
                      : listInsight ? ` · ${listInsight.reach.toLocaleString('tr-TR')} benzersiz alıcı` : '')})
                  </span>
                </div>
                {loadingTargets ? (
                  <div className="px-3 py-6 text-center text-navy-400 text-sm flex items-center justify-center gap-2 border border-navy-50 rounded-lg">
                    <Loader2 className="w-4 h-4 animate-spin" /> Hedefler yükleniyor…
                  </div>
                ) : lists.length === 0 ? (
                  <div className="px-3 py-6 text-center text-navy-400 text-xs border border-navy-50 rounded-lg">
                    Henüz liste yok. Önce “Veri Yönetimi”nden bir liste oluşturun.
                  </div>
                ) : (
                  <div className="max-h-48 overflow-y-auto border border-navy-50 rounded-lg divide-y divide-navy-50">
                    {lists.map(l => {
                      const checked = selectedListIds.includes(l.id);
                      return (
                        <label key={l.id} className="flex items-center gap-2.5 px-3 py-2 cursor-pointer hover:bg-navy-50/50">
                          <input
                            type="checkbox"
                            className="rounded border-navy-200 text-brand-500 focus:ring-brand-500/20"
                            checked={checked}
                            onChange={() => toggleList(l.id)}
                          />
                          <span className="flex-1 text-sm text-navy-800 truncate">{l.name}</span>
                          <span className="text-xs text-navy-400 tabular-nums">{l.sendable_count.toLocaleString('tr-TR')} gönderilebilir</span>
                        </label>
                      );
                    })}
                  </div>
                )}
              </div>
                </div>{/* /column 1 */}

                {/* Column 2: send config (channel + message kind + template/content pickers).
                    "Gönderim Ayarları" heading dropped (Q 2026-06-10). */}
                <div className="space-y-4 md:border-l md:border-navy-50 md:pl-5">
              <div className="w-full space-y-3">
                {/* WhatsApp channel — Cloud API lines only */}
                <div>
                  <label className="block text-xs font-medium text-navy-600 mb-1">WhatsApp Kanalı (hat)</label>
                  <select
                    className="w-full px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm focus:outline-none focus:border-brand-500 focus:shadow-focus"
                    value={instanceId}
                    onChange={e => onChannelChange(e.target.value)}
                  >
                    <option value="">— Kanal seçilmedi —</option>
                    {whatsappInstances.map(i => (
                      <option key={i.id} value={i.instanceId}>
                        {i.instanceName}
                      </option>
                    ))}
                  </select>
                  {whatsappInstances.length === 0 && (
                    <p className="text-xs text-navy-400 mt-1">WhatsApp Cloud API hattı bulunamadı. Ayarlar &gt; Instance listesini yükleyin.</p>
                  )}
                </div>

                {/* Message kind */}
                <div>
                  <label className="block text-xs font-medium text-navy-600 mb-1">Mesaj Türü</label>
                  <div className="flex gap-2">
                    {([
                      { v: 'wapcrm_template', label: 'Onaylı Şablon', disabled: false },
                      { v: 'plain_text', label: 'Düz Metin', disabled: true },
                    ] as { v: ProjectTemplateKind; label: string; disabled: boolean }[]).map(opt => {
                      const selected = templateKind === opt.v;
                      return (
                        <button
                          key={opt.v}
                          type="button"
                          disabled={opt.disabled && !selected}
                          onClick={() => { if (!opt.disabled) onKindChange(opt.v); }}
                          title={opt.disabled ? 'Düz metin gönderimi şu an kapalı — Onaylı Şablon kullanın.' : undefined}
                          className={cn(
                            'px-3 py-1.5 rounded-lg text-sm border transition-colors',
                            selected
                              ? 'border-brand-500 bg-brand-50 text-brand-700 font-medium'
                              : opt.disabled
                                ? 'border-navy-50 bg-navy-50/40 text-navy-300 cursor-not-allowed'
                                : 'border-navy-100 text-navy-600 hover:border-navy-200',
                          )}
                        >
                          {opt.label}
                        </button>
                      );
                    })}
                  </div>
                </div>

                {/* Template picker + operator-filled params (wapcrm_template only) */}
                {templateKind === 'wapcrm_template' && (
                  <div className="space-y-3">
                    {!instanceId ? (
                      <p className="text-xs text-navy-400">Şablonları görmek için önce bir WhatsApp kanalı seçin.</p>
                    ) : loadingTemplates ? (
                      <div className="px-3 py-4 text-center text-navy-400 text-sm flex items-center justify-center gap-2 border border-navy-50 rounded-lg">
                        <Loader2 className="w-4 h-4 animate-spin" /> Şablonlar yükleniyor…
                      </div>
                    ) : templatesError ? (
                      <div className="text-xs text-red-600">{templatesError}</div>
                    ) : (
                      <div>
                        <label className="text-xs font-medium text-navy-600 mb-1 flex items-center gap-1">
                          <FileText className="w-3.5 h-3.5 text-navy-400" /> Onaylı Şablon
                        </label>
                        <select
                          className="w-full px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm focus:outline-none focus:border-brand-500 focus:shadow-focus"
                          value={waTemplateId}
                          onChange={e => onTemplateChange(e.target.value)}
                        >
                          <option value="">— Şablon seçilmedi —</option>
                          {templates.map(t => (
                            <option key={t.templateId ?? ''} value={t.templateId ?? ''}>
                              {t.name ?? t.templateId}{t.language ? ` (${t.language})` : ''}
                            </option>
                          ))}
                        </select>
                        {templates.length === 0 && (
                          <p className="text-xs text-navy-400 mt-1">Bu kanal için onaylı şablon bulunamadı.</p>
                        )}
                      </div>
                    )}

                    {/* (Önizleme + parametre eşleme 3. kolona taşındı — GR-7) */}
                  </div>
                )}

                {/* Plain-text content: gallery template OR free text, chosen in settings */}
                {templateKind === 'plain_text' && (
                  <div className="space-y-3">
                    <div>
                      <label className="block text-xs font-medium text-navy-600 mb-1">İçerik</label>
                      <div className="flex gap-2">
                        {([
                          { v: 'gallery_template', label: 'Galeri Şablonu' },
                          { v: 'free_text', label: 'Serbest Metin' },
                        ] as { v: ProjectContentMode; label: string }[]).map(opt => (
                          <button
                            key={opt.v}
                            type="button"
                            onClick={() => setContentMode(opt.v)}
                            className={cn(
                              'px-3 py-1.5 rounded-lg text-sm border transition-colors',
                              contentMode === opt.v
                                ? 'border-brand-500 bg-brand-50 text-brand-700 font-medium'
                                : 'border-navy-100 text-navy-600 hover:border-navy-200',
                            )}
                          >
                            {opt.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    {contentMode === 'gallery_template' && (
                      <div>
                        <label className="text-xs font-medium text-navy-600 mb-1 flex items-center gap-1">
                          <FileText className="w-3.5 h-3.5 text-navy-400" /> Galeri Şablonu
                        </label>
                        <select
                          className="w-full px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm focus:outline-none focus:border-brand-500 focus:shadow-focus"
                          value={outboundTemplateId}
                          onChange={e => setOutboundTemplateId(e.target.value)}
                        >
                          <option value="">— Şablon seçilmedi —</option>
                          {galleryTemplates.map(t => (
                            <option key={t.id} value={t.id}>{t.name}</option>
                          ))}
                        </select>
                        {galleryError ? (
                          <p className="text-xs text-red-600 mt-1">{galleryError}</p>
                        ) : galleryTemplates.length === 0 ? (
                          <p className="text-xs text-navy-400 mt-1">Etkin şablon yok. “Şablon Galerisi”nden bir şablon oluşturun veya serbest metin kullanın.</p>
                        ) : null}
                        {/* (Şablon metni önizlemesi 3. kolondaki baloncukta — GR-7) */}
                      </div>
                    )}

                    {contentMode === 'free_text' && (
                      <div>
                        <label className="block text-xs font-medium text-navy-600 mb-1">Mesaj Metni</label>
                        <textarea
                          className="w-full px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus resize-none"
                          rows={4}
                          maxLength={PLAIN_TEXT_BODY_MAX}
                          placeholder="Gönderilecek düz metin mesajı"
                          value={plainTextBody}
                          onChange={e => setPlainTextBody(e.target.value)}
                        />
                        <p className="text-[11px] text-navy-400 mt-0.5">{plainTextBody.length}/{PLAIN_TEXT_BODY_MAX}</p>
                      </div>
                    )}
                  </div>
                )}
              </div>
                </div>{/* /column 2 */}

                {/* Column 3: Önizleme & Test (GR-7/GR-8) — WhatsApp önizleme, değişken eşlemeleri, test gönderim */}
                <div className="space-y-4 md:border-l md:border-navy-50 md:pl-5 md:col-span-2 lg:col-span-1">
                  {templateKind === 'wapcrm_template' && selectedTemplate && (
                    <div className="space-y-3">
                      {/* WhatsApp-style preview */}
                      {selectedTemplate.preview && (
                        <div>
                          <div className="flex items-center justify-between mb-1">
                            <label className="block text-xs font-medium text-navy-600">Önizleme</label>
                            {/* aha #2: flip between raw {{...}} and the sample recipient's real values */}
                            {listInsight?.sample && (
                              <button
                                type="button"
                                onClick={() => setShowSample(s => !s)}
                                className="text-[11px] text-brand-600 hover:text-brand-700 underline decoration-dotted"
                              >
                                {showSample
                                  ? 'Değişkenleri göster'
                                  : `Örnekle göster${sampleDisplayName ? ` (${sampleDisplayName})` : ''}`}
                              </button>
                            )}
                          </div>
                          <div className="rounded-lg p-3 bg-[#e5ddd5]">
                            <div className="bg-white rounded-lg rounded-tl-none shadow-sm px-3 py-2 max-w-[90%] text-sm">
                              {selectedTemplate.preview.header && (
                                selectedTemplate.preview.header.type === 'TEXT'
                                  ? selectedTemplate.preview.header.text && (
                                      <div className="font-semibold text-navy-900 mb-1 whitespace-pre-wrap break-words">
                                        {renderTemplateText(selectedTemplate.preview.header.text, sampleResolver)}
                                      </div>
                                    )
                                  : (
                                    <div className="mb-2 flex items-center justify-center gap-1.5 rounded-md bg-navy-50 text-navy-400 text-xs py-5">
                                      {mediaHeaderIcon(selectedTemplate.preview.header.type)}
                                      {mediaHeaderLabel(selectedTemplate.preview.header.type)}
                                    </div>
                                  )
                              )}
                              {selectedTemplate.preview.body && (
                                <div className="text-navy-800 whitespace-pre-wrap break-words">{renderTemplateText(selectedTemplate.preview.body, sampleResolver)}</div>
                              )}
                              {selectedTemplate.preview.footer && (
                                <div className="text-navy-400 text-xs mt-1 whitespace-pre-wrap break-words">{selectedTemplate.preview.footer}</div>
                              )}
                              <div className="text-[10px] text-navy-300 text-right mt-1">şimdi</div>
                            </div>
                            {(selectedTemplate.preview.buttons?.length ?? 0) > 0 && (
                              <div className="mt-1 space-y-1">
                                {selectedTemplate.preview.buttons?.map((b, bi) => (
                                  <div
                                    key={`${b.text ?? b.type ?? 'btn'}-${bi}`}
                                    className="bg-white rounded-lg shadow-sm py-1.5 flex items-center justify-center gap-1.5 text-[#00a5f4] text-sm font-medium"
                                  >
                                    {buttonIcon(b.type)}{b.text ?? b.type}
                                  </div>
                                ))}
                              </div>
                            )}
                          </div>
                        </div>
                      )}

                      {/* Per-parameter mapping: each {{...}} placeholder -> a list column (per-recipient at send time) */}
                      {placeholders.length === 0 && mediaInputs.length === 0 ? (
                        <p className="text-xs text-navy-400">Bu şablon parametre gerektirmiyor.</p>
                      ) : (
                        <div className="space-y-2">
                          {placeholders.length > 0 && (
                            <p className="text-[11px] text-navy-400">
                              Her parametreyi seçili listedeki bir kolona eşleyin — gönderimde her kişiye kendi değeri yazılır.
                            </p>
                          )}
                          {placeholders.map(ph => (
                            <div key={ph.key} className="flex items-center gap-2">
                              <span
                                className="shrink-0 w-2/5 text-xs font-medium text-navy-600 truncate"
                                title={`{{${ph.key}}} — ${ph.location}`}
                              >
                                {`{{${ph.key}}}`}
                                <span className="text-navy-300 font-normal"> — {ph.location}</span>
                              </span>
                              <select
                                className="flex-1 px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm focus:outline-none focus:border-brand-500 focus:shadow-focus"
                                value={paramColumns[ph.key] ?? ''}
                                onChange={e => setParamColumns(prev => ({ ...prev, [ph.key]: e.target.value }))}
                              >
                                <option value="">— Kolon seç —</option>
                                {/* aha #3 (revised): option label shows the sample value only */}
                                {LIST_COLUMNS.map(c => (
                                  <option key={c.v} value={c.v}>{columnOptionLabel(c)}</option>
                                ))}
                              </select>
                            </div>
                          ))}
                          {vendorUnappliedKeys.length > 0 && (
                            <div className="flex items-start gap-2 px-3 py-2 rounded-lg bg-amber-50 text-amber-700 text-xs">
                              <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
                              <span>
                                WapCRM şu değişken(ler)i gönderimde henüz uygulamıyor:{' '}
                                <span className="font-medium">{vendorUnappliedKeys.map(k => `{{${k}}}`).join(', ')}</span>
                                {' '}— alıcıya şablonun onaylı varsayılan değeri gider. Eşlemeniz yine de kaydedilir
                                ve WapCRM desteği eklediğinde otomatik geçerli olur.
                              </span>
                            </div>
                          )}
                          {mediaInputs.map(ri => (
                            <div key={mediaKey(ri)}>
                              <label className="block text-xs font-medium text-navy-600 mb-1">
                                Medya ({ri.mediaType ?? 'dosya'}){ri.location ? ` — ${ri.location}` : ''}
                              </label>
                              <input
                                className="w-full px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus"
                                value={mediaValues[mediaKey(ri)] ?? ''}
                                placeholder="Herkese açık medya URL'i"
                                onChange={e => setMediaValues(prev => ({ ...prev, [mediaKey(ri)]: e.target.value }))}
                              />
                              {ri.note && <p className="text-[11px] text-navy-400 mt-0.5">{ri.note}</p>}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  )}

                  {/* plain_text içerik baloncuğu: serbest metin veya seçili galeri şablonunun metni */}
                  {templateKind === 'plain_text' && testMessageText !== '' && (
                    <div>
                      <label className="block text-xs font-medium text-navy-600 mb-1">Önizleme</label>
                      <div className="rounded-lg p-3 bg-[#e5ddd5]">
                        <div className="bg-white rounded-lg rounded-tl-none shadow-sm px-3 py-2 max-w-[90%] text-sm">
                          <div className="text-navy-800 whitespace-pre-wrap break-words">{renderTemplateText(testMessageText)}</div>
                          <div className="text-[10px] text-navy-300 text-right mt-1">şimdi</div>
                        </div>
                      </div>
                    </div>
                  )}

                  {!(templateKind === 'wapcrm_template' && selectedTemplate) && !(templateKind === 'plain_text' && testMessageText !== '') && (
                    <p className="text-xs text-navy-400">Önizleme ve test için bir şablon veya içerik seçin.</p>
                  )}

                  {/* GR-8: test send — kendi numarana düz metin bağlantı testi */}
                  {testMessageText !== '' && (
                    <div className="border-t border-navy-50 pt-3 space-y-2">
                      <label className="text-xs font-medium text-navy-600 flex items-center gap-1">
                        <Send className="w-3.5 h-3.5 text-navy-400" /> Test Mesajı
                      </label>
                      <p className="text-[11px] text-navy-400">
                        {templateKind === 'wapcrm_template'
                          ? 'Kendi numaranıza GERÇEK onaylı şablon formatında test gönderin (parametreler örnek alıcıdan doldurulur).'
                          : 'Kendi numaranıza düz metin bağlantı testi gönderin.'}
                      </p>
                      <div className="flex gap-2">
                        <input
                          className="flex-1 min-w-0 px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus"
                          placeholder="+905xxxxxxxxx"
                          value={testPhone}
                          onChange={e => setTestPhone(e.target.value)}
                          onKeyDown={e => { if (e.key === 'Enter') void sendTest(); }}
                        />
                        <Button
                          size="sm"
                          variant="secondary"
                          disabled={testSending || !testPhone.trim() || !testMessageText}
                          onClick={() => void sendTest()}
                          title="Bu numaraya test mesajı gönder"
                        >
                          {testSending
                            ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                            : <Send className="w-3.5 h-3.5" />} Test Gönder
                        </Button>
                      </div>
                      {testResult && (
                        <div className={cn(
                          'text-xs flex flex-wrap items-center gap-x-1.5 gap-y-1',
                          testResult.ok === true ? 'text-green-600' : testResult.ok === false ? 'text-red-600' : 'text-navy-500',
                        )}>
                          <span>{testResult.text}</span>
                          {testResult.reason && <WaErrorPopover raw={testResult.reason} />}
                        </div>
                      )}
                    </div>
                  )}
                </div>{/* /column 3 */}
              </div>{/* /three-column grid */}

              {formError && <div className="text-sm text-red-600">{formError}</div>}
            </div>

            <div className="px-5 py-4 border-t border-navy-50 flex justify-end">
              <Button onClick={save} disabled={saving || loadingTargets || (templateKind === 'wapcrm_template' && loadingTemplates)}>
                {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
                {editing ? 'Kaydet' : 'Oluştur'}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* ---- Gönder (run dispatch) modal: preview -> confirm -> status (X-close + backdrop click) ---- */}
      {sendProject && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 p-4"
          onMouseDown={e => { if (e.target === e.currentTarget) closeSend(); }}
        >
          <div className="bg-white border border-navy-100 rounded-xl shadow-soft relative w-full max-w-md">
            <button
              onClick={closeSend}
              className="absolute right-3 top-3 text-navy-300 hover:text-navy-600"
              aria-label="Kapat"
            >
              <X className="w-5 h-5" />
            </button>

            <div className="px-5 py-4 border-b border-navy-50">
              <h3 className="text-base font-semibold text-navy-900 flex items-center gap-2">
                <Send className="w-4 h-4 text-brand-500" /> Gönder
              </h3>
              <p className="text-sm text-navy-500 mt-0.5 truncate">{sendProject.name}</p>
            </div>

            <div className="px-5 py-4 space-y-4">
              {/* Content summary — what will be sent: approved-template NAME + structured full preview,
                  else the plain text summary (gallery / free-text). */}
              <div className="rounded-lg bg-navy-50/60 border border-navy-100 px-3 py-2">
                <div className="text-[11px] font-medium text-navy-400 mb-0.5">Gönderilecek içerik</div>
                {(() => {
                  const tpl = resolveWaTemplate(sendProject);
                  if (tpl) return (
                    <>
                      <div className="text-sm font-medium text-navy-800 mb-1.5">
                        Şablon: {tpl.name?.trim() || sendProject.wa_template_id}
                      </div>
                      <WaTemplatePreview t={tpl} />
                    </>
                  );
                  return (
                    <div className="text-sm text-navy-700 whitespace-pre-wrap break-words line-clamp-4">
                      {sendContentSummary(sendProject)}
                    </div>
                  );
                })()}
              </div>

              {sendPhase === 'previewing' && (
                <div className="py-4 text-center text-navy-400 text-sm flex items-center justify-center gap-2">
                  <Loader2 className="w-4 h-4 animate-spin" /> Önizleme hazırlanıyor…
                </div>
              )}

              {(sendPhase === 'preview' || sendPhase === 'confirming') && sendPreview && (
                <div className="space-y-3">
                  <div className="flex items-center gap-2 text-sm text-navy-700">
                    <Users className="w-4 h-4 text-navy-400" />
                    <span><span className="font-semibold tabular-nums">{sendPreview.total_valid.toLocaleString('tr-TR')}</span> alıcıya gönderilecek</span>
                  </div>
                  {sendPreview.sample.length > 0 && (
                    <div>
                      <div className="text-[11px] font-medium text-navy-400 mb-1">Örnek numaralar</div>
                      <div className="flex flex-wrap gap-1.5">
                        {sendPreview.sample.map(s => (
                          <span key={s} className="px-2 py-0.5 rounded bg-navy-50 text-navy-600 text-xs tabular-nums">{s}</span>
                        ))}
                      </div>
                    </div>
                  )}
                  {(sendPreview.skipped_params?.count ?? 0) > 0 && (
                    <div className="flex items-start gap-2 px-3 py-2 rounded-lg bg-amber-50 text-amber-700 text-xs">
                      <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
                      <span>
                        <span className="font-semibold tabular-nums">{(sendPreview.skipped_params?.count ?? 0).toLocaleString('tr-TR')}</span>{' '}
                        alıcıda zorunlu şablon parametresi eksik — bu alıcılar atlanacak
                        {sendPreview.skipped_params?.by_param && Object.keys(sendPreview.skipped_params.by_param).length > 0 && (
                          <> ({Object.entries(sendPreview.skipped_params.by_param).map(([k, n]) => `${k}: ${n}`).join(', ')})</>
                        )}.
                      </span>
                    </div>
                  )}
                  <p className="text-[11px] text-navy-400">
                    Aynı numara birden fazla listede olsa bile tek mesaj alır. Onayladıktan sonra gönderim başlar.
                  </p>
                </div>
              )}

              {sendPhase === 'sent' && (
                <div className="space-y-2">
                  <div className="flex items-center gap-2 text-sm text-green-600">
                    <CheckCircle2 className="w-4 h-4" /> Gönderim başlatıldı.
                  </div>
                  {sendStatus && (
                    <div className="text-sm text-navy-600">
                      <span className="font-semibold tabular-nums">{sendStatus.total_queued.toLocaleString('tr-TR')}</span> mesaj kuyruğa alındı.
                    </div>
                  )}
                  <p className="text-[11px] text-navy-400">İletim/okundu durumu zaman içinde güncellenir; durumu proje listesinden takip edebilirsiniz.</p>
                </div>
              )}

              {sendError && <div className="text-sm text-red-600">{sendError}</div>}
            </div>

            <div className="px-5 py-4 border-t border-navy-50 flex justify-end gap-2">
              {sendPhase === 'preview' && (
                <Button onClick={confirmSend} disabled={(sendPreview?.total_valid ?? 0) <= 0}>
                  <Send className="w-4 h-4" /> Onayla ve Gönder
                </Button>
              )}
              {sendPhase === 'confirming' && (
                <Button disabled><Loader2 className="w-4 h-4 animate-spin" /> Gönderiliyor…</Button>
              )}
              {(sendPhase === 'sent' || sendPhase === 'error') && (
                <Button variant="secondary" onClick={closeSend}>Kapat</Button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ---- İptal (cancel) confirm modal (X-close + backdrop click; irreversible — sent messages are not recalled) ---- */}
      {cancelTarget && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 p-4"
          onMouseDown={e => { if (e.target === e.currentTarget) setCancelTarget(null); }}
        >
          <div className="bg-white border border-navy-100 rounded-xl shadow-soft relative w-full max-w-md">
            <button
              onClick={() => setCancelTarget(null)}
              className="absolute right-3 top-3 text-navy-300 hover:text-navy-600"
              aria-label="Kapat"
            >
              <X className="w-5 h-5" />
            </button>
            <div className="px-5 py-4 border-b border-navy-50">
              <h3 className="text-base font-semibold text-navy-900 flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 text-red-500" /> Gönderimi iptal et
              </h3>
              <p className="text-sm text-navy-500 mt-0.5 truncate">{cancelTarget.name}</p>
            </div>
            <div className="px-5 py-4 space-y-2 text-sm text-navy-700">
              <p>Kuyruktaki ve duraklatılmış mesajlar iptal edilecek.</p>
              <p className="text-[12px] text-red-600 font-medium">Zaten gönderilmiş mesajlar geri alınamaz.</p>
            </div>
            <div className="px-5 py-4 border-t border-navy-50 flex justify-end gap-2">
              <Button
                variant="ghost"
                disabled={lifecycleBusyId === cancelTarget.id}
                onClick={confirmCancelRun}
              >
                {lifecycleBusyId === cancelTarget.id
                  ? <><Loader2 className="w-4 h-4 animate-spin" /> İptal ediliyor…</>
                  : <><Ban className="w-4 h-4 text-red-500" /> Gönderimi iptal et</>}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* ---- Rapor (delivery report) right drawer: run dropdown + searchable status table + resend + CSV ---- */}
      {reportProject && (
        <div className="fixed inset-0 z-50 flex" role="dialog" aria-modal="true">
          <div className="relative w-full h-full bg-white shadow-2xl flex flex-col">
            {/* header */}
            <div className="px-5 py-4 border-b border-navy-50 flex items-center justify-between">
              <div className="min-w-0">
                <div className="text-xs text-navy-400">Gönderim Raporu</div>
                <div className="text-lg font-semibold text-navy-900 truncate">{reportProject.name}</div>
              </div>
              <button onClick={closeReport} className="p-1.5 rounded hover:bg-navy-50 text-navy-400" title="Kapat" aria-label="Kapat">
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* filters: run dropdown + phone search + refresh + CSV */}
            <div className="px-5 py-3 border-b border-navy-50 flex flex-wrap items-center gap-2">
              <select
                className="border border-navy-100 rounded-lg px-2.5 py-1.5 text-sm text-navy-700 bg-white max-w-[260px]"
                value={reportCampaign}
                onChange={(e) => { setReportCampaign(e.target.value); setReportFailCode(''); setReportPage(1); setReportPullNote(null); }}
                title="Gönderim seç"
              >
                <option value="">Tüm gönderimler ({reportRuns.length})</option>
                {reportRuns.map((r) => (
                  <option key={r.campaign_id} value={r.campaign_id}>{runLabel(r)}</option>
                ))}
              </select>
              <select
                className="border border-navy-100 rounded-lg px-2.5 py-1.5 text-sm text-navy-700 bg-white"
                value={reportStatus}
                onChange={(e) => { setReportStatus(e.target.value); setReportFailCode(''); setReportPage(1); }}
                title="Duruma göre filtrele"
              >
                {REPORT_STATUS_OPTIONS.map((o) => (
                  <option key={o.v} value={o.v}>{o.label}</option>
                ))}
              </select>
              <div className="relative flex-1 min-w-[160px]">
                <Search className="w-4 h-4 text-navy-300 absolute left-2.5 top-1/2 -translate-y-1/2 pointer-events-none" />
                <Input
                  className="pl-8"
                  placeholder="Numara ara…"
                  value={reportSearchDraft}
                  onChange={(e) => setReportSearchDraft(e.target.value)}
                />
              </div>
              {/* Tek "Yenile" ikon butonu: tabloyu DB'den tazeler + (yetkiliyse) sağlayıcıdan canlı durum çeker */}
              <Button
                variant="secondary"
                size="sm"
                className="px-2"
                onClick={refreshReport}
                disabled={reportStatusPulling || reportLoading}
                title="Yenile — tabloyu veritabanından tazele ve bekleyen kayıtların durumunu sağlayıcıdan canlı sorgula"
                aria-label="Yenile"
              >
                <RefreshCw className={cn('w-3.5 h-3.5', (reportStatusPulling || reportLoading) && 'animate-spin')} />
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={exportReportCsv}
                disabled={reportExporting || !reportData || reportData.total === 0}
                title="Filtrelenmiş tüm satırları CSV olarak indir"
              >
                {reportExporting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Download className="w-3.5 h-3.5" />} CSV
              </Button>
              {(() => {
                // Bulk resend scope is the WHOLE project (Q kararı), so the eligible count is failed+ambiguous
                // summed across ALL runs — independent of the selected run / phone filter. Icon-only button with
                // the eligible count as a red badge (Q 2026-06-17: ikon + rozet, metin yok).
                const bulkEligible = reportRuns.reduce((a, r) => a + r.failed + r.ambiguous, 0);
                return (
                  <div className="ml-auto relative">
                    <Button
                      variant="secondary"
                      size="sm"
                      className="px-2 text-red-600 border-red-200 hover:bg-red-50"
                      onClick={() => { setReportError(null); setBulkResendConfirm(true); }}
                      disabled={bulkResending || reportLoading || bulkEligible === 0}
                      title={bulkEligible === 0
                        ? 'Yeniden gönderilecek hatalı kayıt yok'
                        : `Projedeki ${bulkEligible.toLocaleString('tr-TR')} başarısız/belirsiz kaydı tek seferde yeniden gönder`}
                      aria-label={bulkEligible > 0 ? `Hataları tekrar gönder (${bulkEligible})` : 'Hataları tekrar gönder'}
                    >
                      {bulkResending ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Reply className="w-3.5 h-3.5" />}
                    </Button>
                    {bulkEligible > 0 && !bulkResending && (
                      <span className="absolute -top-1.5 -right-1.5 min-w-[18px] h-[18px] px-1 rounded-full bg-red-600 text-white text-[10px] font-semibold leading-[18px] text-center tabular-nums pointer-events-none">
                        {bulkEligible > 99 ? '99+' : bulkEligible}
                      </span>
                    )}
                  </div>
                );
              })()}
            </div>

            {/* summary chips for the selected run (or all runs) — clickable status filters (Q 2026-06-17).
                Each chip writes reportStatus (same state the dropdown drives, so they stay in sync); the
                active chip is highlighted and clicking it again clears the filter. "Toplam" = all (''). */}
            {(() => {
              const sel = reportCampaign ? reportRuns.find((r) => r.campaign_id === reportCampaign) : null;
              const agg = sel ?? reportRuns.reduce(
                (a, r) => ({
                  total: a.total + r.total, sent: a.sent + r.sent, delivered: a.delivered + r.delivered,
                  read: a.read + r.read, failed: a.failed + r.failed, ambiguous: a.ambiguous + r.ambiguous,
                }),
                { total: 0, sent: 0, delivered: 0, read: 0, failed: 0, ambiguous: 0 },
              );
              const chips: { v: string; label: string; value: number; cls: string }[] = [
                { v: '',          label: 'Toplam',     value: agg.total,     cls: 'text-navy-800' },
                { v: 'sent',      label: 'Gönderildi', value: agg.sent,      cls: 'text-indigo-600' },
                { v: 'delivered', label: 'İletildi',   value: agg.delivered, cls: 'text-blue-600' },
                { v: 'read',      label: 'Okundu',     value: agg.read,      cls: 'text-green-600' },
                { v: 'failed',    label: 'Başarısız',  value: agg.failed,    cls: 'text-red-600' },
                { v: 'ambiguous', label: 'Belirsiz',   value: agg.ambiguous, cls: 'text-amber-600' },
              ];
              return (
                <div className="px-5 py-2 border-b border-navy-50 flex flex-wrap gap-1.5 text-xs tabular-nums">
                  {chips.map((c) => {
                    const active = reportStatus === c.v;
                    return (
                      <button
                        key={c.v || 'all'}
                        type="button"
                        onClick={() => { setReportStatus(active && c.v !== '' ? '' : c.v); setReportFailCode(''); setReportPage(1); }}
                        className={cn(
                          'inline-flex items-center gap-1 px-2 py-0.5 rounded-md border transition-colors',
                          active ? 'border-brand-300 bg-brand-50 text-navy-700' : 'border-transparent text-navy-500 hover:bg-navy-50',
                        )}
                        title={c.v === '' ? 'Tüm durumları göster' : `Sadece "${c.label}" olanları göster`}
                        aria-pressed={active}
                      >
                        {c.label} <b className={c.cls}>{c.value.toLocaleString('tr-TR')}</b>
                      </button>
                    );
                  })}
                </div>
              );
            })()}

            {/* #4 failure-reason breakdown — failed/ambiguous grouped by Meta error code. Click a bucket to filter
                the table to exactly that reason (clears the status filter); click again to clear. The label comes
                from resolveWaError on a sampled raw reason, so codeless reasons show their own text. */}
            {failureBuckets.length > 0 && (
              <div className="px-5 py-2 border-b border-navy-50 bg-red-50/30">
                <div className="text-[11px] font-medium text-navy-400 mb-1">Başarısızlık nedenleri — filtrelemek için tıklayın</div>
                <div className="flex flex-wrap gap-1.5 text-xs">
                  {failureBuckets.map((b) => {
                    const key = b.code ?? 'none';
                    const active = reportFailCode === key;
                    const resolved = resolveWaError(b.sample_error);
                    const label = resolved.info?.title
                      ?? (b.sample_error.length > 44 ? `${b.sample_error.slice(0, 44)}…` : b.sample_error)
                      ?? 'Bilinmeyen neden';
                    return (
                      <button
                        key={key}
                        type="button"
                        onClick={() => {
                          setReportFailCode(active ? '' : key);
                          if (!active) setReportStatus('');
                          setReportPage(1);
                        }}
                        className={cn(
                          'inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md border transition-colors max-w-full',
                          active ? 'border-red-300 bg-red-100 text-red-700' : 'border-red-100 bg-white text-navy-600 hover:bg-red-50',
                        )}
                        title={b.code ? `Meta kodu ${b.code}${resolved.info?.title ? ` — ${resolved.info.title}` : ''}` : b.sample_error}
                        aria-pressed={active}
                      >
                        {b.code && <span className="font-semibold tabular-nums text-red-600">{b.code}</span>}
                        <span className="truncate max-w-[220px]">{label}</span>
                        <b className="tabular-nums text-red-700">{b.count.toLocaleString('tr-TR')}</b>
                      </button>
                    );
                  })}
                </div>
              </div>
            )}

            {reportPullNote && (
              <div className="px-5 py-1.5 bg-green-50/70 text-green-700 text-xs flex items-center gap-1.5">
                <CheckCircle2 className="w-3.5 h-3.5 shrink-0" /> {reportPullNote}
              </div>
            )}

            {reportError && (
              <div className="px-5 py-2 bg-red-50 text-red-700 text-sm flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" /> {reportError}
              </div>
            )}

            {/* recipient table */}
            <div className="flex-1 overflow-auto">
              {reportLoading && !reportData ? (
                <div className="flex items-center justify-center py-16 text-navy-400"><Loader2 className="w-5 h-5 animate-spin" /></div>
              ) : !reportData || reportData.items.length === 0 ? (
                <div className="py-16 text-center text-navy-400 text-sm">Bu filtreye uygun kayıt yok.</div>
              ) : (
                <table className="w-full text-sm">
                  <thead className="sticky top-0 bg-navy-50/90 backdrop-blur text-navy-500 text-xs">
                    <tr>
                      <ReportSortHeader label="Numara" sortKey="phone" activeSort={reportSort} dir={reportDir} onSort={onReportSort} className="sticky left-0 z-20 bg-navy-50" />
                      <ReportSortHeader label="Durum" sortKey="status" activeSort={reportSort} dir={reportDir} onSort={onReportSort} />
                      <ReportSortHeader label="Gönderim" sortKey="sent" activeSort={reportSort} dir={reportDir} onSort={onReportSort} className="whitespace-nowrap" />
                      <ReportSortHeader label="İletildi" sortKey="delivered" activeSort={reportSort} dir={reportDir} onSort={onReportSort} className="whitespace-nowrap" />
                      <ReportSortHeader label="Okundu" sortKey="read" activeSort={reportSort} dir={reportDir} onSort={onReportSort} className="whitespace-nowrap" />
                      <th className="px-4 py-2 text-left font-medium">Hata</th>
                      <th className="px-4 py-2 text-right font-medium">İşlem</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-navy-50">
                    {reportData.items.map((r) => (
                      <tr key={r.message_id} className="group hover:bg-navy-50/40">
                        <td className="px-4 py-2 text-navy-800 tabular-nums whitespace-nowrap sticky left-0 z-10 bg-white group-hover:bg-navy-50">{r.phone}</td>
                        <td className="px-4 py-2"><MsgStatusBadge status={r.status} /></td>
                        <td className="px-4 py-2 text-xs text-navy-500 tabular-nums whitespace-nowrap">{fmtDateTime(r.sent_at)}</td>
                        <td className="px-4 py-2 text-xs text-navy-500 tabular-nums whitespace-nowrap">{fmtDateTime(r.delivered_at)}</td>
                        <td className="px-4 py-2 text-xs text-green-600 tabular-nums whitespace-nowrap">{fmtDateTime(r.read_at)}</td>
                        <td className="px-4 py-2 text-xs text-navy-500 max-w-xs">
                          <WaErrorPopover raw={r.error} />
                        </td>
                        <td className="px-4 py-2 text-right">
                          {r.can_resend && (
                            <Button
                              size="sm"
                              variant="secondary"
                              disabled={resendingId === r.message_id}
                              onClick={() => doResend(r.message_id)}
                              title="Bu numaraya projenin içeriğiyle yeniden gönder"
                            >
                              {resendingId === r.message_id
                                ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                                : <Reply className="w-3.5 h-3.5" />} Tekrar Gönder
                            </Button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            {/* pagination footer */}
            {reportData && reportData.total > 0 && (
              <div className="px-5 py-3 border-t border-navy-50 flex items-center justify-between text-sm text-navy-500">
                <span className="tabular-nums">
                  Toplam {reportData.total.toLocaleString('tr-TR')} · sayfa {reportData.page}
                </span>
                <div className="flex items-center gap-2">
                  <Button size="sm" variant="secondary" disabled={reportPage <= 1 || reportLoading} onClick={() => setReportPage((p) => Math.max(1, p - 1))}>Önceki</Button>
                  <Button size="sm" variant="secondary" disabled={reportLoading || reportPage * REPORT_PAGE_SIZE >= reportData.total} onClick={() => setReportPage((p) => p + 1)}>Sonraki</Button>
                </div>
              </div>
            )}
          </div>

          {/* Bulk resend confirm — sits ABOVE the drawer (z-60). Real WhatsApp sends, so it is gated. */}
          {bulkResendConfirm && (() => {
            const bulkEligible = reportRuns.reduce((a, r) => a + r.failed + r.ambiguous, 0);
            return (
              <div
                className="fixed inset-0 z-[60] flex items-center justify-center bg-navy-900/40 p-4"
                onMouseDown={e => { if (e.target === e.currentTarget && !bulkResending) setBulkResendConfirm(false); }}
              >
                <div className="bg-white border border-navy-100 rounded-xl shadow-soft relative w-full max-w-md">
                  <button
                    onClick={() => setBulkResendConfirm(false)}
                    disabled={bulkResending}
                    className="absolute right-3 top-3 text-navy-300 hover:text-navy-600 disabled:opacity-40"
                    aria-label="Kapat"
                  >
                    <X className="w-5 h-5" />
                  </button>
                  <div className="px-5 py-4 border-b border-navy-50">
                    <h3 className="text-base font-semibold text-navy-900 flex items-center gap-2">
                      <Reply className="w-4 h-4 text-red-500" /> Hataları tekrar gönder
                    </h3>
                    <p className="text-sm text-navy-500 mt-0.5 truncate">{reportProject.name}</p>
                  </div>
                  <div className="px-5 py-4 space-y-2 text-sm text-navy-700">
                    <p>
                      Bu projedeki <b className="text-red-600 tabular-nums">{bulkEligible.toLocaleString('tr-TR')}</b> başarısız/belirsiz
                      kayıt, projenin içeriğiyle yeniden gönderim için kuyruğa alınacak.
                    </p>
                    <p className="text-[12px] text-navy-400">Tüm gönderimler kapsanır — seçili gönderim veya numara filtresi dikkate alınmaz.</p>
                  </div>
                  <div className="px-5 py-4 border-t border-navy-50 flex justify-end gap-2">
                    <Button variant="ghost" disabled={bulkResending} onClick={doResendAll}>
                      {bulkResending
                        ? <><Loader2 className="w-4 h-4 animate-spin" /> Kuyruğa alınıyor…</>
                        : <><Reply className="w-4 h-4 text-red-500" /> Tümünü tekrar gönder</>}
                    </Button>
                  </div>
                </div>
              </div>
            );
          })()}
        </div>
      )}

      {/* ---- Table content hover: rich preview card (portal → escapes the table overflow-hidden) ---- */}
      {rowHover && createPortal((() => {
        const tpl = resolveWaTemplate(rowHover.project);
        const info = rowContentInfo(rowHover.project);
        const r = rowHover.rect;
        const below = r.bottom < window.innerHeight / 2;
        const style: CSSProperties = {
          position: 'fixed',
          left: Math.max(8, Math.min(r.left, window.innerWidth - 360)),
          ...(below ? { top: r.bottom + 6 } : { bottom: window.innerHeight - r.top + 6 }),
        };
        return (
          <div style={style} className="z-[60] w-[340px] rounded-xl bg-white border border-navy-100 shadow-soft p-3 pointer-events-none">
            <div className="text-[11px] font-medium text-navy-400 mb-1.5 truncate">{info?.label ?? 'İçerik önizleme'}</div>
            {tpl
              ? <WaTemplatePreview t={tpl} />
              : <div className="text-sm text-navy-700 whitespace-pre-wrap break-words max-h-72 overflow-auto">{info?.hover ?? '—'}</div>}
          </div>
        );
      })(), document.body)}
    </div>
  );
}
