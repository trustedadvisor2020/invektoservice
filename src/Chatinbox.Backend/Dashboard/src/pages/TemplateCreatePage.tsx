import { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  api,
  TemplateCatalogDetail,
  TemplateCreateInput,
  TemplateBulkCreateResult,
} from '../lib/api';
import {
  ArrowLeft, Plus, Upload, Save, Check, AlertTriangle, FileJson, Tag,
} from 'lucide-react';
import { PlaceholderPicker } from '../components/PlaceholderPicker';

const TEMPLATE_TYPES = ['faq', 'message', 'intent', 'flow', 'scenario'];
const SCOPES = ['tenant', 'sector', 'platform'];
const LANGS = ['tr', 'en', 'de', 'fr', 'es', 'pt', 'nl', 'it', 'ru', 'ar'];
const GROUP_TAG_SUGGESTIONS = [
  'welcome_with_date', 'welcome_no_date', 'welcome_returning',
  'faq_pricing', 'faq_hours', 'faq_address', 'faq_booking',
];

type Tab = 'single' | 'bulk';

interface FormState {
  template_type: string;
  scope: string;
  sector: string;
  tenant_id: string;
  slug: string;
  name: string;
  description: string;
  lang: string;
  tags: string;
  group_tag: string;
  content_json: string;
}

const emptyForm: FormState = {
  template_type: 'message',
  scope: 'tenant',
  sector: '',
  tenant_id: '',
  slug: '',
  name: '',
  description: '',
  lang: 'tr',
  tags: '',
  group_tag: '',
  content_json: '{\n  "text": ""\n}',
};

const SLUG_RE = /^[a-z0-9-]+$/;

export function TemplateCreatePage() {
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>('single');

  // ── Single template state ──────────────────────────────────────────────
  const [form, setForm] = useState<FormState>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [singleError, setSingleError] = useState<string | null>(null);
  const [created, setCreated] = useState<TemplateCatalogDetail | null>(null);

  // FEAT-DMP: cursor-aware INMA placeholder insertion into content_json.
  const contentTextareaRef = useRef<HTMLTextAreaElement | null>(null);
  const insertTokenIntoContentJson = (token: string) => {
    const el = contentTextareaRef.current;
    const current = form.content_json;
    if (!el) {
      setForm({ ...form, content_json: current + token });
      return;
    }
    const start = el.selectionStart ?? current.length;
    const end = el.selectionEnd ?? current.length;
    const next = current.slice(0, start) + token + current.slice(end);
    setForm({ ...form, content_json: next });
    requestAnimationFrame(() => {
      if (contentTextareaRef.current) {
        const caret = start + token.length;
        contentTextareaRef.current.focus();
        contentTextareaRef.current.setSelectionRange(caret, caret);
      }
    });
  };

  const handleSingleSave = async () => {
    setSingleError(null);

    // Validation
    if (!SLUG_RE.test(form.slug)) {
      setSingleError('[INV-INT-FE-043] Slug formatı geçersiz — sadece lowercase harf, rakam, tire.');
      return;
    }
    if (!form.name.trim()) {
      setSingleError('[INV-INT-FE-043] İsim boş bırakılamaz.');
      return;
    }
    if (form.scope === 'tenant' && !form.tenant_id.trim()) {
      setSingleError('[INV-INT-FE-043] Tenant scope için tenant_id zorunlu.');
      return;
    }
    if (form.scope === 'sector' && !form.sector.trim()) {
      setSingleError('[INV-INT-FE-043] Sector scope için sector zorunlu.');
      return;
    }
    if (form.group_tag.length > 50) {
      setSingleError('[INV-INT-FE-043] Grup etiketi en fazla 50 karakter.');
      return;
    }

    let content: Record<string, unknown>;
    try {
      content = JSON.parse(form.content_json);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'parse error';
      setSingleError(`[INV-INT-FE-043] content_json geçersiz JSON: ${msg}`);
      return;
    }

    const input: TemplateCreateInput = {
      template_type: form.template_type,
      scope: form.scope,
      sector: form.sector.trim() === '' ? null : form.sector.trim(),
      tenant_id: form.tenant_id.trim() === '' ? null : Number(form.tenant_id),
      slug: form.slug,
      name: form.name.trim(),
      description: form.description.trim() === '' ? null : form.description.trim(),
      lang: form.lang,
      tags: form.tags.trim() === '' ? [] : form.tags.split(',').map(t => t.trim()).filter(Boolean),
      group_tag: form.group_tag.trim() === '' ? null : form.group_tag.trim(),
      content_json: content,
    };

    setSaving(true);
    try {
      const result = await api.createTemplate(input);
      setCreated(result);
      setForm(emptyForm);
    } catch (err) {
      const code = err instanceof Error && err.message ? err.message : 'unknown';
      setSingleError(`[INV-INT-FE-044] Template kaydedilemedi (${code}). Slug çatışması varsa farklı slug deneyin.`);
    } finally {
      setSaving(false);
    }
  };

  // ── Bulk state ─────────────────────────────────────────────────────────
  const [bulkJson, setBulkJson] = useState<string>('');
  const [parsed, setParsed] = useState<TemplateCreateInput[] | null>(null);
  const [parseError, setParseError] = useState<string | null>(null);
  const [bulkSaving, setBulkSaving] = useState(false);
  const [bulkResult, setBulkResult] = useState<TemplateBulkCreateResult | null>(null);

  const handleBulkParse = () => {
    setParseError(null);
    setParsed(null);
    setBulkResult(null);

    let arr: unknown;
    try {
      arr = JSON.parse(bulkJson);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'parse error';
      setParseError(`[INV-INT-FE-045] JSON parse hatası: ${msg}`);
      return;
    }

    if (!Array.isArray(arr)) {
      setParseError('[INV-INT-FE-045] JSON kök dizi (array) olmalı.');
      return;
    }
    if (arr.length === 0) {
      setParseError('[INV-INT-FE-045] Boş dizi — en az 1 template gerekli.');
      return;
    }
    if (arr.length > 100) {
      setParseError(`[INV-INT-FE-045] ${arr.length} template gönderildi, maksimum 100.`);
      return;
    }

    // Per-item shape check (required fields)
    const normalized: TemplateCreateInput[] = [];
    for (let i = 0; i < arr.length; i++) {
      const item = arr[i] as Record<string, unknown> | null;
      if (!item || typeof item !== 'object') {
        setParseError(`[INV-INT-FE-045] Item ${i}: obje bekleniyor.`);
        return;
      }
      const slug = typeof item.slug === 'string' ? item.slug : '';
      const name = typeof item.name === 'string' ? item.name : '';
      const template_type = typeof item.template_type === 'string' ? item.template_type : '';
      const scope = typeof item.scope === 'string' ? item.scope : '';
      const lang = typeof item.lang === 'string' ? item.lang : '';
      if (!slug || !name || !template_type || !scope || !lang) {
        setParseError(`[INV-INT-FE-045] Item ${i} (slug="${slug}"): slug/name/template_type/scope/lang zorunlu.`);
        return;
      }
      if (!SLUG_RE.test(slug)) {
        setParseError(`[INV-INT-FE-045] Item ${i} slug="${slug}" format geçersiz.`);
        return;
      }
      if (!item.content_json || typeof item.content_json !== 'object') {
        setParseError(`[INV-INT-FE-045] Item ${i} (slug="${slug}"): content_json obje olmalı.`);
        return;
      }
      normalized.push(item as unknown as TemplateCreateInput);
    }

    setParsed(normalized);
  };

  const handleBulkSubmit = async () => {
    if (!parsed) return;
    setBulkSaving(true);
    setBulkResult(null);
    try {
      const result = await api.bulkImportTemplates(parsed);
      setBulkResult(result);
    } catch (err) {
      const code = err instanceof Error && err.message ? err.message : 'unknown';
      setParseError(`[INV-INT-FE-046] Topluca yükleme başarısız (${code}).`);
    } finally {
      setBulkSaving(false);
    }
  };

  const handleRetryFailed = () => {
    if (!bulkResult || bulkResult.failed.length === 0 || !parsed) return;
    const failedIndices = new Set(bulkResult.failed.map(f => f.index));
    const retryList = parsed.filter((_, i) => failedIndices.has(i));
    setParsed(retryList);
    setBulkJson(JSON.stringify(retryList, null, 2));
    setBulkResult(null);
  };

  // ── Render ─────────────────────────────────────────────────────────────
  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center gap-3">
        <button onClick={() => navigate('/templates')} className="p-1.5 hover:bg-navy-50 rounded">
          <ArrowLeft className="w-4 h-4" />
        </button>
        <div className="flex-1">
          <h1 className="text-lg font-semibold text-navy-900 flex items-center gap-2">
            <Plus className="w-5 h-5" />
            Yeni Template Oluştur
          </h1>
          <p className="text-xs text-navy-400">Tek template formu veya JSON topluca import.</p>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-2 border-b border-navy-100">
        <button
          onClick={() => setTab('single')}
          className={`px-3 py-2 text-xs font-medium flex items-center gap-1 border-b-2 transition-colors ${
            tab === 'single' ? 'border-navy-800 text-navy-900' : 'border-transparent text-navy-400 hover:text-navy-600'
          }`}
        >
          <Save className="w-3.5 h-3.5" />
          Tek Template
        </button>
        <button
          onClick={() => setTab('bulk')}
          className={`px-3 py-2 text-xs font-medium flex items-center gap-1 border-b-2 transition-colors ${
            tab === 'bulk' ? 'border-navy-800 text-navy-900' : 'border-transparent text-navy-400 hover:text-navy-600'
          }`}
        >
          <Upload className="w-3.5 h-3.5" />
          Topluca JSON
        </button>
      </div>

      {/* Single template tab */}
      {tab === 'single' && (
        <div className="space-y-3">
          {singleError && (
            <div className="flex items-center justify-between bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700">
              <span>{singleError}</span>
              <button onClick={() => setSingleError(null)} className="text-red-400 hover:text-red-600 ml-2">&times;</button>
            </div>
          )}
          {created && (
            <div className="flex items-center gap-2 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-2 text-xs text-emerald-700">
              <Check className="w-4 h-4" />
              <span>Oluşturuldu: <strong>{created.name}</strong> (id={created.id}, slug={created.slug})</span>
              <button onClick={() => navigate(`/templates/${created.id}`)} className="ml-auto text-xs underline hover:text-emerald-900">
                Görüntüle
              </button>
            </div>
          )}

          <div className="bg-white rounded-lg border border-navy-100 p-4 space-y-3">
            <div className="grid grid-cols-3 gap-3">
              <div>
                <label className="text-[10px] font-medium text-navy-500">Tip</label>
                <select
                  value={form.template_type}
                  onChange={e => setForm({ ...form, template_type: e.target.value })}
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
                >
                  {TEMPLATE_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>
              <div>
                <label className="text-[10px] font-medium text-navy-500">Kapsam</label>
                <select
                  value={form.scope}
                  onChange={e => setForm({ ...form, scope: e.target.value })}
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
                >
                  {SCOPES.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              </div>
              <div>
                <label className="text-[10px] font-medium text-navy-500">Dil</label>
                <select
                  value={form.lang}
                  onChange={e => setForm({ ...form, lang: e.target.value })}
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
                >
                  {LANGS.map(l => <option key={l} value={l}>{l}</option>)}
                </select>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-[10px] font-medium text-navy-500">Tenant ID {form.scope === 'tenant' && <span className="text-red-500">*</span>}</label>
                <input
                  type="number"
                  value={form.tenant_id}
                  onChange={e => setForm({ ...form, tenant_id: e.target.value })}
                  placeholder={form.scope === 'tenant' ? 'orn. 5050' : 'scope sector/platform için boş'}
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
                />
              </div>
              <div>
                <label className="text-[10px] font-medium text-navy-500">Sektör {form.scope === 'sector' && <span className="text-red-500">*</span>}</label>
                <input
                  type="text"
                  value={form.sector}
                  onChange={e => setForm({ ...form, sector: e.target.value })}
                  placeholder="orn. dental"
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-[10px] font-medium text-navy-500">Slug <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={form.slug}
                  onChange={e => setForm({ ...form, slug: e.target.value.toLowerCase() })}
                  placeholder="ornek-slug"
                  maxLength={200}
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none font-mono"
                />
                <p className="text-[10px] text-navy-400 mt-0.5">lowercase, rakam, tire</p>
              </div>
              <div>
                <label className="text-[10px] font-medium text-navy-500">İsim <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={form.name}
                  onChange={e => setForm({ ...form, name: e.target.value })}
                  maxLength={300}
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
                />
              </div>
            </div>

            <div>
              <label className="text-[10px] font-medium text-navy-500">Açıklama</label>
              <input
                type="text"
                value={form.description}
                onChange={e => setForm({ ...form, description: e.target.value })}
                className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-[10px] font-medium text-navy-500">Tags (virgül ayrılır)</label>
                <input
                  type="text"
                  value={form.tags}
                  onChange={e => setForm({ ...form, tags: e.target.value })}
                  placeholder="welcome,greeting"
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
                />
              </div>
              <div>
                <label className="text-[10px] font-medium text-navy-500 flex items-center gap-1">
                  <Tag className="w-3 h-3" /> Grup Etiketi (Rotasyon)
                </label>
                <input
                  type="text"
                  list="group-tag-suggestions-create"
                  value={form.group_tag}
                  onChange={e => setForm({ ...form, group_tag: e.target.value })}
                  placeholder="orn. welcome_with_date"
                  maxLength={50}
                  className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none"
                />
                <datalist id="group-tag-suggestions-create">
                  {GROUP_TAG_SUGGESTIONS.map(g => <option key={g} value={g} />)}
                </datalist>
              </div>
            </div>

            <div>
              <label className="text-[10px] font-medium text-navy-500 flex items-center gap-1">
                <FileJson className="w-3 h-3" /> content_json (JSON)
              </label>
              <textarea
                ref={contentTextareaRef}
                value={form.content_json}
                onChange={e => setForm({ ...form, content_json: e.target.value })}
                rows={6}
                className="w-full text-[11px] border border-navy-200 rounded px-2 py-1.5 outline-none font-mono"
              />
              <div className="flex items-center justify-between mt-0.5">
                <p className="text-[10px] text-navy-400">
                  Message tipi: <code>{`{"text": "..."}`}</code> — FAQ tipi: <code>{`{"text": "..."}`}</code> veya <code>{`{"answer": "..."}`}</code>.
                </p>
                <PlaceholderPicker
                  onInsert={(token) => insertTokenIntoContentJson(token)}
                  triggerLabel="Dinamik alan"
                  position="above"
                  tfmAware
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-2 pt-2 border-t border-navy-50">
              <button
                onClick={() => navigate('/templates')}
                className="px-3 py-1.5 text-xs font-medium rounded border border-navy-200 hover:bg-navy-50"
                disabled={saving}
              >
                Vazgeç
              </button>
              <button
                onClick={handleSingleSave}
                disabled={saving}
                className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700 disabled:opacity-40"
              >
                <Save className="w-3.5 h-3.5" />
                {saving ? 'Kaydediliyor...' : 'Kaydet'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Bulk JSON tab */}
      {tab === 'bulk' && (
        <div className="space-y-3">
          {parseError && (
            <div className="flex items-center justify-between bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700">
              <span>{parseError}</span>
              <button onClick={() => setParseError(null)} className="text-red-400 hover:text-red-600 ml-2">&times;</button>
            </div>
          )}

          <div className="bg-white rounded-lg border border-navy-100 p-4 space-y-3">
            <div>
              <label className="text-[10px] font-medium text-navy-500 flex items-center gap-1">
                <FileJson className="w-3 h-3" /> JSON Array (maksimum 100 template)
              </label>
              <textarea
                value={bulkJson}
                onChange={e => { setBulkJson(e.target.value); setParsed(null); setBulkResult(null); }}
                rows={14}
                placeholder='[&#10;  {&#10;    "template_type": "message",&#10;    "scope": "tenant",&#10;    "tenant_id": 5050,&#10;    "slug": "welcome-1",&#10;    "name": "Welcome variant 1",&#10;    "lang": "tr",&#10;    "group_tag": "welcome_with_date",&#10;    "content_json": {"text": "..."}&#10;  }&#10;]'
                className="w-full text-[11px] border border-navy-200 rounded px-2 py-1.5 outline-none font-mono"
              />
            </div>
            <div className="flex items-center justify-between">
              <p className="text-[10px] text-navy-400">
                Zorunlu alanlar: <code>template_type, scope, slug, name, lang, content_json</code>. tenant_id scope=tenant için zorunlu.
              </p>
              <button
                onClick={handleBulkParse}
                disabled={!bulkJson.trim() || bulkSaving}
                className="px-3 py-1.5 text-xs font-medium rounded border border-navy-200 hover:bg-navy-50 disabled:opacity-40"
              >
                Doğrula ve Önizle
              </button>
            </div>
          </div>

          {/* Preview table */}
          {parsed && parsed.length > 0 && !bulkResult && (
            <div className="bg-white rounded-lg border border-navy-100 overflow-hidden">
              <div className="px-3 py-2 bg-navy-50 flex items-center justify-between">
                <span className="text-xs font-medium text-navy-700">
                  Önizleme: {parsed.length} template hazır
                </span>
                <button
                  onClick={handleBulkSubmit}
                  disabled={bulkSaving}
                  className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700 disabled:opacity-40"
                >
                  <Upload className="w-3.5 h-3.5" />
                  {bulkSaving ? 'Yükleniyor...' : `Topluca Yükle (${parsed.length})`}
                </button>
              </div>
              <table className="w-full text-xs">
                <thead className="bg-navy-25 text-navy-500">
                  <tr>
                    <th className="text-left px-3 py-1.5 font-medium">#</th>
                    <th className="text-left px-3 py-1.5 font-medium">Slug</th>
                    <th className="text-left px-3 py-1.5 font-medium">İsim</th>
                    <th className="text-left px-3 py-1.5 font-medium">Tip</th>
                    <th className="text-left px-3 py-1.5 font-medium">Kapsam</th>
                    <th className="text-left px-3 py-1.5 font-medium">Grup Etiketi</th>
                    <th className="text-left px-3 py-1.5 font-medium">Dil</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-navy-50">
                  {parsed.slice(0, 20).map((t, i) => (
                    <tr key={i}>
                      <td className="px-3 py-1.5 text-navy-400">{i}</td>
                      <td className="px-3 py-1.5 font-mono text-navy-700">{t.slug}</td>
                      <td className="px-3 py-1.5 text-navy-700">{t.name}</td>
                      <td className="px-3 py-1.5 text-navy-500">{t.template_type}</td>
                      <td className="px-3 py-1.5 text-navy-500">{t.scope}{t.tenant_id ? ` (${t.tenant_id})` : ''}</td>
                      <td className="px-3 py-1.5 text-navy-500">{t.group_tag ?? '-'}</td>
                      <td className="px-3 py-1.5 text-navy-500">{t.lang}</td>
                    </tr>
                  ))}
                  {parsed.length > 20 && (
                    <tr>
                      <td colSpan={7} className="px-3 py-1.5 text-center text-navy-400">
                        ... ve {parsed.length - 20} template daha
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          {/* Result summary */}
          {bulkResult && (
            <div className="space-y-3">
              <div className="bg-white rounded-lg border border-navy-100 p-3">
                <div className="flex items-center gap-3">
                  <div className="flex-1">
                    <h3 className="text-sm font-medium text-navy-900">Yükleme Sonucu</h3>
                    <p className="text-xs text-navy-500">
                      Toplam {bulkResult.total} — {' '}
                      <span className="text-emerald-600 font-medium">{bulkResult.succeeded_count} başarılı</span>
                      {bulkResult.failed_count > 0 && (
                        <> / <span className="text-red-600 font-medium">{bulkResult.failed_count} başarısız</span></>
                      )}
                    </p>
                  </div>
                  {bulkResult.failed_count > 0 && (
                    <button
                      onClick={handleRetryFailed}
                      className="px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700"
                    >
                      Başarısızları Tekrar Dene
                    </button>
                  )}
                  <button
                    onClick={() => { setBulkJson(''); setParsed(null); setBulkResult(null); }}
                    className="px-3 py-1.5 text-xs font-medium rounded border border-navy-200 hover:bg-navy-50"
                  >
                    Yeni Batch
                  </button>
                </div>
              </div>

              {bulkResult.failed_count > 0 && (
                <div className="bg-white rounded-lg border border-red-200 overflow-hidden">
                  <div className="px-3 py-2 bg-red-50 flex items-center gap-2">
                    <AlertTriangle className="w-4 h-4 text-red-600" />
                    <span className="text-xs font-medium text-red-700">Başarısız Olan Template'ler</span>
                  </div>
                  <table className="w-full text-xs">
                    <thead className="bg-red-25 text-navy-500">
                      <tr>
                        <th className="text-left px-3 py-1.5 font-medium">#</th>
                        <th className="text-left px-3 py-1.5 font-medium">Slug</th>
                        <th className="text-left px-3 py-1.5 font-medium">Hata Kodu</th>
                        <th className="text-left px-3 py-1.5 font-medium">Detay</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-navy-50">
                      {bulkResult.failed.map((f, i) => (
                        <tr key={i}>
                          <td className="px-3 py-1.5 text-navy-400">{f.index}</td>
                          <td className="px-3 py-1.5 font-mono text-navy-700">{f.slug ?? '-'}</td>
                          <td className="px-3 py-1.5 font-mono text-red-600">{f.error_code}</td>
                          <td className="px-3 py-1.5 text-navy-700">{f.error_message}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
