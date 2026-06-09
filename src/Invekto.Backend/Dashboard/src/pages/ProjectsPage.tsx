import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Briefcase, X, Plus, Pencil, Archive, Loader2, Info, ListChecks, Radio, FileText, Send, Users, CheckCircle2,
  ExternalLink, Phone, Reply, Image as ImageIcon, Pause, Play, Ban, AlertTriangle,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { cn } from '../lib/utils';
import {
  api, ApiClientError,
  type ProjectSummary, type ProjectStatus, type DataListSummary,
  type InstanceDto, type WaTemplate, type ProjectTemplateKind, type ProjectTemplateParam,
  type ProjectContentMode, type OutboundTemplateDto,
  type BulkSendPreviewResponse, type BulkSendStatusResponse,
  type DataListPreviewSample, type ListRecord,
} from '../lib/api';

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

  // Only WhatsApp Cloud API lines are valid send channels (instance_type === 1; excludes SMS/web/channel).
  const whatsappInstances = useMemo(() => instances.filter(i => i.instanceType === 1), [instances]);
  const selectedTemplate = useMemo(
    () => templates.find(t => t.templateId === waTemplateId) ?? null, [templates, waTemplateId]);
  const requiredInputs = selectedTemplate?.requiredInputs ?? [];
  // Text placeholders ({{...}}) read from the rendered preview (covers header params cxapi omits from requiredInputs).
  const placeholders = useMemo(() => extractPlaceholders(selectedTemplate), [selectedTemplate]);
  // Media inputs come from cxapi requiredInputs (preview text has no {{...}} for media).
  const mediaInputs = useMemo(() => requiredInputs.filter(ri => ri.kind === 'media'), [requiredInputs]);
  // Stable key (location+mediaType, not index) so edit re-populate doesn't depend on async template load order.
  const mediaKey = (ri: { location: string | null; mediaType: string | null }) =>
    `${ri.location ?? ''}:${ri.mediaType ?? ''}`;
  // Gallery templates are tenant-scoped (not per-channel); the picker resolves the selected one for a preview.
  const selectedGalleryTemplate = useMemo(
    () => galleryTemplates.find(t => String(t.id) === outboundTemplateId) ?? null, [galleryTemplates, outboundTemplateId]);

  const PLAIN_TEXT_BODY_MAX = 4096; // mirrors PlainTextBodyMaxLength in ProjectsService.cs

  async function loadProjects() {
    setLoading(true);
    setLoadError(null);
    setFeatureDisabled(false);
    try {
      setProjects(await api.listProjects());
    } catch (e) {
      setProjects([]);
      // 403 = the tenant isn't enabled for projects (ProjectsOptions gate / plan).
      if (e instanceof ApiClientError && e.status === 403) setFeatureDisabled(true);
      else setLoadError(errText(e, 'Projeler yüklenemedi'));
    } finally {
      setLoading(false);
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

  // aha #3: column option label enriched with the sample value + fill % from the insight.
  const columnOptionLabel = (c: { v: string; label: string }): string => {
    let label = c.label;
    const sampleVal = listInsight?.sample ? recordValue(listInsight.sample, c.v) : null;
    if (sampleVal) label += ` — "${sampleVal.length > 18 ? `${sampleVal.slice(0, 18)}…` : sampleVal}"`;
    const stat = listInsight?.column_stats?.[c.v];
    if (stat && stat.total > 0)
      label += stat.filled === 0 ? ' · boş' : ` · %${Math.round((stat.filled / stat.total) * 100)} dolu`;
    return label;
  };

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

  function openCreate() {
    setEditing(null);
    setName('');
    setDescription('');
    setSelectedListIds([]);
    resetSendConfig();
    setShowSample(true);
    setFormError(null);
    setModalOpen(true);
  }

  async function openEdit(p: ProjectSummary) {
    setEditing(p);
    setName(p.name);
    setDescription(p.description ?? '');
    setSelectedListIds([]);
    setShowSample(true);
    setFormError(null);
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
        <div className="px-4 py-3 border-b border-navy-50 flex items-center gap-2">
          <Briefcase className="w-4 h-4 text-navy-400" />
          <h2 className="text-sm font-medium text-navy-700">Projeler</h2>
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
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-navy-50/50 text-navy-500 text-xs">
              <tr>
                <th className="text-left font-medium px-4 py-2">Proje</th>
                <th className="text-left font-medium px-4 py-2">Durum</th>
                <th className="text-right font-medium px-4 py-2">Liste</th>
                <th className="text-right font-medium px-4 py-2">Gönderildi</th>
                <th className="text-right font-medium px-4 py-2">İletildi</th>
                <th className="text-right font-medium px-4 py-2">Başarısız</th>
                <th className="text-right font-medium px-4 py-2">İşlem</th>
              </tr>
            </thead>
            <tbody>
              {projects.map(p => (
                <tr key={p.id} className="border-t border-navy-50">
                  <td className="px-4 py-2.5">
                    <div className="text-navy-900">{p.name}</div>
                    {p.description && <div className="text-xs text-navy-400 truncate max-w-xs">{p.description}</div>}
                  </td>
                  <td className="px-4 py-2.5">
                    <StatusBadge status={p.status} />
                    {p.cancelled_count > 0 && (
                      <div className="text-[11px] text-navy-400 mt-0.5 tabular-nums">{p.cancelled_count.toLocaleString('tr-TR')} iptal</div>
                    )}
                  </td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.target_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.sent_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.delivered_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.failed_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5">
                    <div className="flex items-center justify-end gap-1.5">
                      {/* SS-D status-aware run controls: running -> Duraklat; paused -> Devam Et; either -> İptal */}
                      {p.status === 'running' && (
                        <Button
                          size="sm"
                          variant="secondary"
                          disabled={lifecycleBusyId === p.id}
                          title="Gönderimi duraklat"
                          onClick={() => runLifecycle(p, 'pause', 'Gönderim duraklatılamadı')}
                        >
                          {lifecycleBusyId === p.id
                            ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                            : <Pause className="w-3.5 h-3.5" />} Duraklat
                        </Button>
                      )}
                      {p.status === 'paused' && (
                        <Button
                          size="sm"
                          variant="primary"
                          disabled={lifecycleBusyId === p.id}
                          title="Gönderimi sürdür"
                          onClick={() => runLifecycle(p, 'resume', 'Gönderim sürdürülemedi')}
                        >
                          {lifecycleBusyId === p.id
                            ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                            : <Play className="w-3.5 h-3.5" />} Devam Et
                        </Button>
                      )}
                      {(p.status === 'running' || p.status === 'paused') && (
                        <Button
                          size="sm"
                          variant="ghost"
                          disabled={lifecycleBusyId === p.id}
                          title="Gönderimi iptal et"
                          onClick={() => setCancelTarget(p)}
                        >
                          <Ban className="w-3.5 h-3.5 text-red-500" /> İptal
                        </Button>
                      )}
                      {(() => {
                        const reason = sendDisabledReason(p);
                        return (
                          <Button
                            size="sm"
                            variant="primary"
                            disabled={reason !== null}
                            title={reason ?? 'Bu projeyi gönder'}
                            onClick={() => openSend(p)}
                          >
                            <Send className="w-3.5 h-3.5" /> Gönder
                          </Button>
                        );
                      })()}
                      <Button size="sm" variant="secondary" onClick={() => openEdit(p)}>
                        <Pencil className="w-3.5 h-3.5" /> Düzenle
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={archivingId === p.id}
                        onClick={() => archive(p)}
                        title="Arşivle"
                      >
                        {archivingId === p.id
                          ? <Loader2 className="w-3.5 h-3.5 animate-spin text-navy-400" />
                          : <Archive className="w-3.5 h-3.5 text-navy-400" />}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ))}
      </div>

      {/* ---- Create / Edit modal (X-close, no İptal text button) ---- */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 p-4">
          <div className="bg-white border border-navy-100 rounded-xl shadow-soft relative w-full max-w-3xl max-h-[90vh] overflow-y-auto">
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
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                {/* Left column: identity + target lists */}
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
                </div>{/* /left column */}

                {/* Right column: send config (WhatsApp channel + message kind + template) */}
                <div className="space-y-4 md:border-l md:border-navy-50 md:pl-5">
              <div className="w-full space-y-3">
                <div className="flex items-center gap-1.5">
                  <Radio className="w-4 h-4 text-navy-400" />
                  <label className="text-sm font-medium text-navy-700">Gönderim Ayarları</label>
                  <span className="text-xs text-navy-400">(opsiyonel)</span>
                </div>

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

                    {selectedTemplate && (
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
                                  {/* aha #3: option label enriched with the sample value + fill % */}
                                  {LIST_COLUMNS.map(c => (
                                    <option key={c.v} value={c.v}>{columnOptionLabel(c)}</option>
                                  ))}
                                </select>
                              </div>
                            ))}
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
                        {selectedGalleryTemplate && (
                          <p className="text-xs text-navy-500 whitespace-pre-wrap border border-navy-50 rounded-lg p-2 mt-2 bg-navy-50/30">
                            {selectedGalleryTemplate.message_template}
                          </p>
                        )}
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
                </div>{/* /right column */}
              </div>{/* /two-column grid */}

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

      {/* ---- Gönder (run dispatch) modal: preview -> confirm -> status (X-close) ---- */}
      {sendProject && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 p-4">
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
              {/* Content summary — what will be sent */}
              <div className="rounded-lg bg-navy-50/60 border border-navy-100 px-3 py-2">
                <div className="text-[11px] font-medium text-navy-400 mb-0.5">Gönderilecek içerik</div>
                <div className="text-sm text-navy-700 whitespace-pre-wrap break-words line-clamp-4">
                  {sendContentSummary(sendProject)}
                </div>
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

      {/* ---- İptal (cancel) confirm modal (X-close; irreversible — sent messages are not recalled) ---- */}
      {cancelTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 p-4">
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
    </div>
  );
}
