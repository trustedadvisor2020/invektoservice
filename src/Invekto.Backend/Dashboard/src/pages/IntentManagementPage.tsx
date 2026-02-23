import { useState, useCallback, useEffect } from 'react';
import { Brain, RefreshCw, Plus, Search, Trash2, Save, X, Tag } from 'lucide-react';
import { api, type TenantEntry, type IntentPatternDto } from '../lib/api';

export function IntentManagementPage() {
  const [tenants, setTenants] = useState<TenantEntry[]>([]);
  const [selectedTenant, setSelectedTenant] = useState<number>(0);
  const [intents, setIntents] = useState<IntentPatternDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editName, setEditName] = useState('');
  const [editKeywords, setEditKeywords] = useState('');
  const [showAdd, setShowAdd] = useState(false);
  const [newName, setNewName] = useState('');
  const [newKeywords, setNewKeywords] = useState('');
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);

  // Load tenants on mount
  useEffect(() => {
    api.getOpsTenants()
      .then(r => setTenants(r.tenants))
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'Tenant listesi yuklenemedi'));
  }, []);

  const loadIntents = useCallback(async () => {
    if (!selectedTenant) return;
    setLoading(true);
    setError(null);
    try {
      const r = await api.getIntents(selectedTenant);
      setIntents(r.intents);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Yuklenemedi');
    } finally {
      setLoading(false);
    }
  }, [selectedTenant]);

  useEffect(() => { loadIntents(); }, [loadIntents]);

  const filtered = intents.filter(i =>
    !search || i.intent_name.toLowerCase().includes(search.toLowerCase()) ||
    i.keywords.some(k => k.toLowerCase().includes(search.toLowerCase()))
  );

  const handleEdit = (intent: IntentPatternDto) => {
    setEditingId(intent.id);
    setEditName(intent.intent_name);
    setEditKeywords(intent.keywords.join(', '));
  };

  const handleSave = async () => {
    if (!editingId || !editName.trim()) return;
    try {
      const keywords = editKeywords.split(',').map(k => k.trim()).filter(Boolean);
      await api.updateIntent(selectedTenant, editingId, { intent_name: editName.trim(), keywords });
      setEditingId(null);
      await loadIntents();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Guncelleme basarisiz');
    }
  };

  const handleAdd = async () => {
    if (!newName.trim()) return;
    try {
      const keywords = newKeywords.split(',').map(k => k.trim()).filter(Boolean);
      await api.createIntent(selectedTenant, { intent_name: newName.trim(), keywords });
      setShowAdd(false);
      setNewName('');
      setNewKeywords('');
      await loadIntents();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Ekleme basarisiz');
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await api.deleteIntent(selectedTenant, id);
      setDeleteConfirm(null);
      await loadIntents();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Silme basarisiz');
    }
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold text-navy-900 flex items-center gap-2">
            <Brain className="w-5 h-5" /> Intent Yonetimi
          </h1>
          <p className="text-xs text-navy-400">{intents.length} intent</p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={loadIntents} className="p-1.5 rounded hover:bg-navy-50" title="Yenile">
            <RefreshCw className={`w-4 h-4 text-navy-500 ${loading ? 'animate-spin' : ''}`} />
          </button>
          {selectedTenant > 0 && (
            <button
              onClick={() => setShowAdd(true)}
              className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700"
            >
              <Plus className="w-3.5 h-3.5" /> Yeni Intent
            </button>
          )}
        </div>
      </div>

      {/* Tenant selector + search */}
      <div className="flex items-center gap-3 bg-white rounded-lg border border-navy-100 p-3">
        <select
          value={selectedTenant}
          onChange={e => { setSelectedTenant(Number(e.target.value)); setSearch(''); }}
          className="text-xs border border-navy-200 rounded px-2 py-1.5 bg-white min-w-[200px]"
        >
          <option value={0}>Tenant sec...</option>
          {tenants.filter(t => t.isActive).map(t => (
            <option key={t.tenantId} value={t.tenantId}>{t.tenantName} ({t.tenantId})</option>
          ))}
        </select>
        <div className="flex items-center gap-1.5 flex-1">
          <Search className="w-3.5 h-3.5 text-navy-400" />
          <input
            type="text"
            placeholder="Intent veya keyword ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="text-xs border-none outline-none flex-1 bg-transparent"
          />
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700 flex items-center justify-between">
          {error}
          <button onClick={() => setError(null)} className="ml-2 text-red-400 hover:text-red-600">&times;</button>
        </div>
      )}

      {/* Add form */}
      {showAdd && (
        <div className="bg-brand-50 border border-brand-200 rounded-lg p-3 space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-navy-700">Yeni Intent</span>
            <button onClick={() => setShowAdd(false)} className="p-0.5 rounded hover:bg-brand-100">
              <X className="w-3.5 h-3.5 text-navy-500" />
            </button>
          </div>
          <input
            type="text"
            placeholder="Intent adi (ornegin: product_inquiry)"
            value={newName}
            onChange={e => setNewName(e.target.value)}
            className="w-full text-xs border border-navy-200 rounded px-2 py-1.5"
          />
          <input
            type="text"
            placeholder="Keywords (virgul ile ayir: fiyat, ucret, para)"
            value={newKeywords}
            onChange={e => setNewKeywords(e.target.value)}
            className="w-full text-xs border border-navy-200 rounded px-2 py-1.5"
          />
          <button
            onClick={handleAdd}
            disabled={!newName.trim()}
            className="px-3 py-1.5 text-xs font-medium rounded bg-brand-600 text-white hover:bg-brand-700 disabled:opacity-50"
          >
            Ekle
          </button>
        </div>
      )}

      {/* No tenant selected */}
      {selectedTenant === 0 && (
        <div className="bg-white rounded-lg border border-navy-100 px-3 py-8 text-center text-xs text-navy-400">
          Yukaridaki dropdown'dan bir tenant secin
        </div>
      )}

      {/* Table */}
      {selectedTenant > 0 && (
        <div className="bg-white rounded-lg border border-navy-100 overflow-hidden">
          <table className="w-full text-xs">
            <thead className="bg-navy-50 text-navy-500">
              <tr>
                <th className="text-left px-3 py-2 font-medium w-8">#</th>
                <th className="text-left px-3 py-2 font-medium">Intent</th>
                <th className="text-left px-3 py-2 font-medium">Keywords</th>
                <th className="text-center px-3 py-2 font-medium w-16">Ornek</th>
                <th className="text-center px-3 py-2 font-medium w-20">Guven</th>
                <th className="text-right px-3 py-2 font-medium w-20">Islem</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-navy-50">
              {filtered.map(intent => (
                <tr key={intent.id} className="hover:bg-navy-25">
                  {editingId === intent.id ? (
                    <>
                      <td className="px-3 py-2 text-navy-400">{intent.id}</td>
                      <td className="px-3 py-2">
                        <input
                          type="text"
                          value={editName}
                          onChange={e => setEditName(e.target.value)}
                          className="w-full text-xs border border-brand-300 rounded px-1.5 py-1"
                        />
                      </td>
                      <td className="px-3 py-2">
                        <input
                          type="text"
                          value={editKeywords}
                          onChange={e => setEditKeywords(e.target.value)}
                          className="w-full text-xs border border-brand-300 rounded px-1.5 py-1"
                          placeholder="virgul ile ayir"
                        />
                      </td>
                      <td className="px-3 py-2 text-center text-navy-400">{intent.sample_count}</td>
                      <td className="px-3 py-2 text-center text-navy-400">
                        {intent.confidence_avg != null ? Number(intent.confidence_avg).toFixed(2) : '-'}
                      </td>
                      <td className="px-3 py-2 text-right">
                        <div className="flex items-center justify-end gap-1">
                          <button onClick={handleSave} className="p-1 rounded hover:bg-emerald-50" title="Kaydet">
                            <Save className="w-3.5 h-3.5 text-emerald-600" />
                          </button>
                          <button onClick={() => setEditingId(null)} className="p-1 rounded hover:bg-navy-50" title="Iptal">
                            <X className="w-3.5 h-3.5 text-navy-400" />
                          </button>
                        </div>
                      </td>
                    </>
                  ) : (
                    <>
                      <td className="px-3 py-2 text-navy-400">{intent.id}</td>
                      <td className="px-3 py-2 font-medium text-navy-800 cursor-pointer" onClick={() => handleEdit(intent)}>
                        {intent.intent_name}
                      </td>
                      <td className="px-3 py-2">
                        <div className="flex flex-wrap gap-1">
                          {intent.keywords.slice(0, 5).map((kw, i) => (
                            <span key={i} className="inline-flex items-center gap-0.5 px-1.5 py-0.5 bg-navy-50 text-navy-600 rounded text-[10px]">
                              <Tag className="w-2.5 h-2.5" />{kw}
                            </span>
                          ))}
                          {intent.keywords.length > 5 && (
                            <span className="text-[10px] text-navy-400">+{intent.keywords.length - 5}</span>
                          )}
                        </div>
                      </td>
                      <td className="px-3 py-2 text-center text-navy-500">{intent.sample_count}</td>
                      <td className="px-3 py-2 text-center text-navy-500">
                        {intent.confidence_avg != null ? Number(intent.confidence_avg).toFixed(2) : '-'}
                      </td>
                      <td className="px-3 py-2 text-right" onClick={e => e.stopPropagation()}>
                        {deleteConfirm === intent.id ? (
                          <div className="flex items-center justify-end gap-1">
                            <button
                              onClick={() => handleDelete(intent.id)}
                              className="px-2 py-0.5 text-[10px] rounded bg-red-600 text-white hover:bg-red-700"
                            >
                              Sil
                            </button>
                            <button
                              onClick={() => setDeleteConfirm(null)}
                              className="p-1 rounded hover:bg-navy-50"
                            >
                              <X className="w-3 h-3 text-navy-400" />
                            </button>
                          </div>
                        ) : (
                          <button
                            onClick={() => setDeleteConfirm(intent.id)}
                            className="p-1 rounded hover:bg-red-50"
                            title="Sil"
                          >
                            <Trash2 className="w-3.5 h-3.5 text-navy-400 hover:text-red-500" />
                          </button>
                        )}
                      </td>
                    </>
                  )}
                </tr>
              ))}
              {filtered.length === 0 && !loading && (
                <tr>
                  <td colSpan={6} className="px-3 py-8 text-center text-navy-400">
                    {intents.length === 0 ? 'Bu tenant icin intent bulunamadi' : 'Arama sonucu yok'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
