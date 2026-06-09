import { useEffect, useMemo, useState } from 'react';
import {
  Briefcase, X, Plus, Pencil, Archive, Loader2, Info, ListChecks, Radio, FileText,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { cn } from '../lib/utils';
import {
  api, ApiClientError,
  type ProjectSummary, type ProjectStatus, type DataListSummary,
  type InstanceDto, type WaTemplate, type ProjectTemplateKind, type ProjectTemplateParam,
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
  const [paramValues, setParamValues] = useState<string[]>([]); // index-aligned with the selected template's requiredInputs

  const [archivingId, setArchivingId] = useState<number | null>(null);

  // Only WhatsApp Cloud API lines are valid send channels (instance_type === 1; excludes SMS/web/channel).
  const whatsappInstances = useMemo(() => instances.filter(i => i.instanceType === 1), [instances]);
  const selectedTemplate = useMemo(
    () => templates.find(t => t.templateId === waTemplateId) ?? null, [templates, waTemplateId]);
  const requiredInputs = selectedTemplate?.requiredInputs ?? [];

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
    setParamValues([]);
    setTemplatesError(null);
    if (kind === 'wapcrm_template' && instanceId) void fetchTemplatesFor(Number(instanceId));
  }

  function onChannelChange(value: string) {
    setInstanceId(value);
    // Templates are per-channel; reset the template selection and reload for the new channel.
    setWaTemplateId('');
    setTemplates([]);
    setParamValues([]);
    setTemplatesError(null);
    if (templateKind === 'wapcrm_template' && value) void fetchTemplatesFor(Number(value));
  }

  function onTemplateChange(value: string) {
    setWaTemplateId(value);
    // Reset the operator-filled params to one empty slot per requiredInput of the newly chosen template.
    const tmpl = templates.find(t => t.templateId === value);
    setParamValues(new Array(tmpl?.requiredInputs?.length ?? 0).fill(''));
  }

  function setParamAt(index: number, value: string) {
    setParamValues(prev => {
      const next = prev.slice();
      while (next.length <= index) next.push('');
      next[index] = value;
      return next;
    });
  }

  useEffect(() => {
    void loadProjects();
    void loadLists();
    void loadInstances();
  }, []);

  function resetSendConfig() {
    setTemplateKind('');
    setInstanceId('');
    setWaTemplateId('');
    setTemplates([]);
    setParamValues([]);
    setTemplatesError(null);
  }

  function openCreate() {
    setEditing(null);
    setName('');
    setDescription('');
    setSelectedListIds([]);
    resetSendConfig();
    setFormError(null);
    setModalOpen(true);
  }

  async function openEdit(p: ProjectSummary) {
    setEditing(p);
    setName(p.name);
    setDescription(p.description ?? '');
    setSelectedListIds([]);
    setFormError(null);
    setModalOpen(true);

    // Re-populate the send config from the project (the summary now carries it).
    setTemplateKind(p.template_kind ?? '');
    setInstanceId(p.instance_id != null ? String(p.instance_id) : '');
    setWaTemplateId(p.wa_template_id ?? '');
    // param values are index-aligned with the stored param_mapping (same order the template requiredInputs had).
    setParamValues((p.param_mapping ?? []).map(x => x.value ?? ''));
    setTemplates([]);
    setTemplatesError(null);
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
    setModalOpen(false);
    setEditing(null);
  }

  function toggleList(id: number) {
    setSelectedListIds(prev => (prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]));
  }

  async function save() {
    const trimmed = name.trim();
    if (!trimmed) { setFormError('Proje adı gerekli.'); return; }

    // Validate the send config when a message kind is chosen (both kinds need a channel).
    let paramMapping: ProjectTemplateParam[] | undefined;
    if (templateKind !== '') {
      if (!instanceId || Number.isNaN(Number(instanceId))) {
        setFormError('Gönderim için bir WhatsApp kanalı seçin.'); return;
      }
      if (templateKind === 'wapcrm_template') {
        if (!waTemplateId) { setFormError('Bir onaylı şablon seçin.'); return; }
        // Guard: never rebuild param_mapping from an unloaded template (would wipe stored params).
        if (!selectedTemplate) { setFormError('Şablon bilgisi yüklenemedi. Lütfen tekrar deneyin.'); return; }
        for (let i = 0; i < requiredInputs.length; i++) {
          if (!(paramValues[i] ?? '').trim()) { setFormError('Tüm şablon parametrelerini doldurun.'); return; }
        }
        paramMapping = requiredInputs.map((ri, i) => ({
          kind: ri.kind, location: ri.location, paramKey: ri.paramKey, mediaType: ri.mediaType,
          value: (paramValues[i] ?? '').trim(),
        }));
      }
    }

    // template_kind is the driver: '' => omit the config block (leave unchanged); set => send the full block.
    const sendConfig = templateKind === ''
      ? {}
      : {
          template_kind: templateKind,
          instance_id: Number(instanceId),
          wa_template_id: templateKind === 'wapcrm_template' ? waTemplateId : null,
          param_mapping: templateKind === 'wapcrm_template' ? (paramMapping ?? []) : null,
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
                  <td className="px-4 py-2.5"><StatusBadge status={p.status} /></td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.target_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.sent_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.delivered_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{p.failed_count.toLocaleString('tr-TR')}</td>
                  <td className="px-4 py-2.5">
                    <div className="flex items-center justify-end gap-1.5">
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
          <div className="bg-white border border-navy-100 rounded-xl shadow-soft relative w-full max-w-lg max-h-[90vh] overflow-y-auto">
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

            <div className="px-5 py-3 space-y-4">
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
                  <span className="text-xs text-navy-400">({selectedListIds.length} seçili)</span>
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

              {/* ---- Send config: WhatsApp channel + message kind + template ---- */}
              <div className="w-full border-t border-navy-50 pt-3 space-y-3">
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
                        {i.instanceName}{i.account ? ` (${i.account})` : ''}
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
                      { v: '', label: 'Yok' },
                      { v: 'plain_text', label: 'Düz Metin' },
                      { v: 'wapcrm_template', label: 'Onaylı Şablon' },
                    ] as { v: ProjectTemplateKind | ''; label: string }[]).map(opt => (
                      <button
                        key={opt.v || 'none'}
                        type="button"
                        onClick={() => onKindChange(opt.v)}
                        className={cn(
                          'px-3 py-1.5 rounded-lg text-sm border transition-colors',
                          templateKind === opt.v
                            ? 'border-brand-500 bg-brand-50 text-brand-700 font-medium'
                            : 'border-navy-100 text-navy-600 hover:border-navy-200',
                        )}
                      >
                        {opt.label}
                      </button>
                    ))}
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
                              {t.templateId}{t.preview ? ` — ${t.preview.slice(0, 40)}` : ''}
                            </option>
                          ))}
                        </select>
                        {templates.length === 0 && (
                          <p className="text-xs text-navy-400 mt-1">Bu kanal için onaylı şablon bulunamadı.</p>
                        )}
                      </div>
                    )}

                    {selectedTemplate && (
                      <div className="space-y-2 border border-navy-50 rounded-lg p-3 bg-navy-50/30">
                        {selectedTemplate.preview && (
                          <p className="text-xs text-navy-500 whitespace-pre-wrap">{selectedTemplate.preview}</p>
                        )}
                        {requiredInputs.length === 0 ? (
                          <p className="text-xs text-navy-400">Bu şablon parametre gerektirmiyor.</p>
                        ) : (
                          requiredInputs.map((ri, i) => (
                            <div key={`${ri.paramKey ?? ri.location ?? 'p'}-${i}`}>
                              <label className="block text-xs font-medium text-navy-600 mb-1">
                                {ri.kind === 'media'
                                  ? `Medya (${ri.mediaType ?? 'dosya'})${ri.location ? ` — ${ri.location}` : ''}`
                                  : `Parametre ${ri.paramKey ?? i + 1}${ri.location ? ` — ${ri.location}` : ''}`}
                              </label>
                              <input
                                className="w-full px-3 py-2 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm placeholder:text-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus"
                                value={paramValues[i] ?? ''}
                                placeholder={ri.kind === 'media' ? 'Medya URL' : (ri.note ?? 'Değer')}
                                onChange={e => setParamAt(i, e.target.value)}
                              />
                              {ri.note && <p className="text-[11px] text-navy-400 mt-0.5">{ri.note}</p>}
                            </div>
                          ))
                        )}
                      </div>
                    )}
                  </div>
                )}

                {templateKind === 'plain_text' && (
                  <p className="text-xs text-navy-400">Düz metin gönderimi: mesaj metni gönderim adımında girilecek.</p>
                )}
              </div>

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
    </div>
  );
}
