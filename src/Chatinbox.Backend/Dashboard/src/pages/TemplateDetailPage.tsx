import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api, TemplateCatalogDetail, TemplateVersionItem } from '../lib/api';
import {
  ArrowLeft, Check, Clock, Users, FileQuestion, MessageCircle,
  Lightbulb, GitBranch, Layers, LayoutTemplate, Tag,
} from 'lucide-react';

const TYPE_ICONS: Record<string, React.ComponentType<{ className?: string }>> = {
  faq: FileQuestion, message: MessageCircle, intent: Lightbulb,
  flow: GitBranch, scenario: Layers,
};

// FEAT-WTP: sector-level suggested group_tag values surfaced in the datalist. Not a DB
// whitelist — tenants may enter any free-text label. See arch/features/welcome-template-pack.md.
const GROUP_TAG_SUGGESTIONS = [
  'welcome_with_date',
  'welcome_no_date',
  'welcome_returning',
  'faq_pricing',
  'faq_hours',
  'faq_address',
  'faq_booking',
];

export function TemplateDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [template, setTemplate] = useState<TemplateCatalogDetail | null>(null);
  const [versions, setVersions] = useState<TemplateVersionItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // FEAT-WTP: local draft for group_tag input (dirty when !== persisted value).
  const [groupTagDraft, setGroupTagDraft] = useState<string>('');
  const [savingGroupTag, setSavingGroupTag] = useState(false);

  useEffect(() => {
    if (!id) return;
    const numId = parseInt(id);
    if (isNaN(numId)) return;
    const fetchData = async () => {
      setLoading(true);
      setError(null);
      try {
        const [tmpl, vers] = await Promise.all([
          api.getTemplateCatalogItem(numId),
          api.getTemplateVersions(numId),
        ]);
        setTemplate(tmpl);
        setGroupTagDraft(tmpl.group_tag ?? '');
        setVersions(vers.versions);
      } catch (err) {
        console.error('Failed to fetch template:', err);
        setError('Şablon detayları yüklenirken hata oluştu.');
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [id]);

  const handlePublish = async () => {
    if (!template) return;
    setError(null);
    try {
      await api.publishTemplate(template.id);
      setTemplate(prev => prev ? { ...prev, is_published: true } : null);
    } catch (err) {
      console.error('Publish failed:', err);
      setError('Şablon yayınlanırken hata oluştu.');
    }
  };

  /**
   * FEAT-WTP: persist the group_tag draft.
   * Empty input clears the tag — backend TemplateRepository.UpdateAsync interprets
   * '' as "set column to NULL" and null as "leave untouched", so we send the trimmed
   * string verbatim (empty string propagates as the clear signal).
   */
  const handleSaveGroupTag = async () => {
    if (!template || savingGroupTag) return;
    const trimmed = groupTagDraft.trim();
    if (trimmed.length > 50) {
      setError('[INV-INT-FE-041] Grup etiketi en fazla 50 karakter olabilir.');
      return;
    }
    setSavingGroupTag(true);
    setError(null);
    try {
      // Empty string is intentional: it tells the backend to clear the column.
      // Passing null here would be a no-op (leave untouched) per the api.ts contract.
      const updated = await api.updateTemplateGroupTag(template.id, trimmed);
      setTemplate(updated);
      setGroupTagDraft(updated.group_tag ?? '');
    } catch (err) {
      console.error('Group tag save failed:', err);
      // INV-INT-FE-042 frontend fallback code: surface actionable next step (retry /
      // check connection) so support can distinguish this failure from generic 500s.
      const code = err instanceof Error && err.message ? err.message : 'unknown';
      setError(`[INV-INT-FE-042] Grup etiketi kaydedilemedi (${code}). Bağlantıyı kontrol edip tekrar deneyin.`);
    } finally {
      setSavingGroupTag(false);
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center py-12 text-navy-400 text-sm">Yükleniyor...</div>;
  }

  if (!template) {
    return (
      <div className="space-y-3 py-12">
        {error && (
          <div className="max-w-md mx-auto bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700">{error}</div>
        )}
        <div className="flex items-center justify-center text-navy-400 text-sm">Şablon bulunamadı</div>
      </div>
    );
  }

  const TypeIcon = TYPE_ICONS[template.template_type] || LayoutTemplate;

  return (
    <div className="space-y-4">
      {/* Error Banner */}
      {error && (
        <div className="flex items-center justify-between bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-400 hover:text-red-600 ml-2">&times;</button>
        </div>
      )}

      {/* Header */}
      <div className="flex items-center gap-3">
        <button onClick={() => navigate('/templates')} className="p-1.5 hover:bg-navy-50 rounded">
          <ArrowLeft className="w-4 h-4" />
        </button>
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <TypeIcon className="w-5 h-5 text-navy-600" />
            <h1 className="text-lg font-semibold text-navy-900">{template.name}</h1>
            {template.is_published ? (
              <span className="inline-flex items-center gap-0.5 px-2 py-0.5 rounded text-[10px] font-medium bg-emerald-50 text-emerald-700">
                <Check className="w-3 h-3" /> Yayında
              </span>
            ) : (
              <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-amber-50 text-amber-600">Taslak</span>
            )}
          </div>
          <p className="text-xs text-navy-400 mt-0.5">{template.slug} &middot; v{template.version} &middot; {template.scope}{template.sector ? ` / ${template.sector}` : ''}</p>
        </div>
        {!template.is_published && (
          <button onClick={handlePublish} className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded bg-emerald-600 text-white hover:bg-emerald-500">
            <Check className="w-3.5 h-3.5" /> Yayınla
          </button>
        )}
      </div>

      <div className="grid grid-cols-3 gap-4">
        {/* Content Editor */}
        <div className="col-span-2 space-y-4">
          {/* Description */}
          {template.description && (
            <div className="bg-white rounded-lg border border-navy-100 p-3">
              <h3 className="text-xs font-medium text-navy-500 mb-1">Açıklama</h3>
              <p className="text-sm text-navy-700">{template.description}</p>
            </div>
          )}

          {/* Content JSON */}
          <div className="bg-white rounded-lg border border-navy-100 p-3">
            <h3 className="text-xs font-medium text-navy-500 mb-2">İçerik (content_json)</h3>
            <pre className="text-[11px] bg-navy-25 rounded p-3 overflow-auto max-h-96 text-navy-700 font-mono">
              {JSON.stringify(template.content_json, null, 2)}
            </pre>
          </div>

          {/* Tags */}
          {template.tags.length > 0 && (
            <div className="bg-white rounded-lg border border-navy-100 p-3">
              <h3 className="text-xs font-medium text-navy-500 mb-2">Etiketler</h3>
              <div className="flex flex-wrap gap-1">
                {template.tags.map(tag => (
                  <span key={tag} className="px-2 py-0.5 rounded-full text-[10px] bg-navy-100 text-navy-600">{tag}</span>
                ))}
              </div>
            </div>
          )}

          {/* Version History */}
          {versions.length > 0 && (
            <div className="bg-white rounded-lg border border-navy-100 p-3">
              <h3 className="text-xs font-medium text-navy-500 mb-2">Versiyon Geçmişi</h3>
              <div className="space-y-2">
                {versions.map(v => (
                  <div key={v.id} className="flex items-start gap-2 text-xs border-l-2 border-navy-200 pl-3 py-1">
                    <Clock className="w-3 h-3 mt-0.5 text-navy-400 flex-shrink-0" />
                    <div>
                      <span className="font-medium text-navy-700">v{v.version}</span>
                      {v.change_summary && <span className="text-navy-500 ml-1">— {v.change_summary}</span>}
                      <div className="text-[10px] text-navy-400">{v.changed_by} &middot; {new Date(v.created_at).toLocaleDateString('tr-TR')}</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Sidebar */}
        <div className="space-y-4">
          {/* Meta */}
          <div className="bg-white rounded-lg border border-navy-100 p-3 space-y-2">
            <h3 className="text-xs font-medium text-navy-500">Bilgi</h3>
            <div className="grid grid-cols-2 gap-2 text-xs">
              <div><span className="text-navy-400">Tip:</span> <span className="font-medium">{template.template_type}</span></div>
              <div><span className="text-navy-400">Dil:</span> <span className="font-medium">{template.lang}</span></div>
              <div><span className="text-navy-400">Kullanım:</span> <span className="font-medium">{template.usage_count}</span></div>
              <div><span className="text-navy-400">Oluşturan:</span> <span className="font-medium">{template.created_by}</span></div>
            </div>
          </div>

          {/* FEAT-WTP: Group Tag editor */}
          <div className="bg-white rounded-lg border border-navy-100 p-3">
            <h3 className="text-xs font-medium text-navy-500 mb-2 flex items-center gap-1">
              <Tag className="w-3 h-3" />
              Grup Etiketi (Rotasyon)
            </h3>
            <div className="space-y-2">
              <input
                type="text"
                list="group-tag-suggestions"
                value={groupTagDraft}
                maxLength={50}
                onChange={e => setGroupTagDraft(e.target.value)}
                placeholder="orn. welcome_with_date"
                className="w-full text-xs border border-navy-200 rounded px-2 py-1.5 outline-none focus:border-navy-400"
                disabled={savingGroupTag}
              />
              <datalist id="group-tag-suggestions">
                {GROUP_TAG_SUGGESTIONS.map(g => <option key={g} value={g} />)}
              </datalist>
              <p className="text-[10px] text-navy-400">
                Aynı grup etiketine sahip şablonlar rotasyon havuzu oluştur. Boş bırakırsanız etiket silinir.
              </p>
              <button
                onClick={handleSaveGroupTag}
                disabled={savingGroupTag || groupTagDraft.trim() === (template.group_tag ?? '')}
                className="w-full px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700 disabled:opacity-40 disabled:cursor-not-allowed"
              >
                {savingGroupTag ? 'Kaydediliyor...' : 'Kaydet'}
              </button>
            </div>
          </div>

          {/* Confidence */}
          <div className="bg-white rounded-lg border border-navy-100 p-3">
            <h3 className="text-xs font-medium text-navy-500 mb-2">Güven Skoru</h3>
            <div className="flex items-center gap-2">
              <div className="flex-1 bg-navy-100 rounded-full h-2">
                <div className="bg-emerald-500 h-2 rounded-full" style={{ width: `${template.confidence_score * 100}%` }} />
              </div>
              <span className="text-xs font-medium text-navy-700">{(template.confidence_score * 100).toFixed(0)}%</span>
            </div>
          </div>

          {/* Sources */}
          <div className="bg-white rounded-lg border border-navy-100 p-3">
            <h3 className="text-xs font-medium text-navy-500 mb-2 flex items-center gap-1">
              <Users className="w-3 h-3" />
              Kaynaklar ({template.source_count} firma)
            </h3>
            {template.sources && template.sources.length > 0 ? (
              <div className="space-y-1.5">
                {template.sources.map(src => (
                  <div key={src.id} className="flex items-center justify-between text-xs">
                    <span className="text-navy-700">{src.tenant_name}</span>
                    <span className="text-navy-400">{src.sample_count} örnek</span>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-xs text-navy-400">Kaynak bilgisi yok</p>
            )}
          </div>

          {/* Timestamps */}
          <div className="bg-white rounded-lg border border-navy-100 p-3 space-y-1 text-xs text-navy-400">
            <div>Oluşturulma: {new Date(template.created_at).toLocaleDateString('tr-TR')}</div>
            <div>Güncelleme: {new Date(template.updated_at).toLocaleDateString('tr-TR')}</div>
          </div>
        </div>
      </div>
    </div>
  );
}
