import { useState, useCallback, useEffect } from 'react';
import { RefreshCw, Plus, Search, Trash2, Save, X, ToggleLeft, ToggleRight } from 'lucide-react';
import { api, type RiTemplateItem } from '../lib/api';
import { useAuth } from '../hooks/useAuth';

type TabType = 'intent' | 'faq' | 'objection_handler' | 'followup_template';

const TABS: { id: TabType; label: string; keyField: string; descField: string }[] = [
  { id: 'intent', label: 'Intent', keyField: 'intentName', descField: 'description' },
  { id: 'faq', label: 'FAQ', keyField: 'question', descField: 'answer' },
  { id: 'objection_handler', label: 'Objection', keyField: 'objectionType', descField: 'objectionLabel' },
  { id: 'followup_template', label: 'Follow-up', keyField: 'templateType', descField: 'messageTemplate' },
];

export function RiTemplateManagementPage() {
  const { session } = useAuth();
  const tenantId = session?.tenantId ?? 0;
  const [sector, setSector] = useState('');
  const [sectorInput, setSectorInput] = useState('');
  const [activeTab, setActiveTab] = useState<TabType>('intent');
  const [items, setItems] = useState<RiTemplateItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editData, setEditData] = useState<Record<string, unknown>>({});
  const [showAdd, setShowAdd] = useState(false);
  const [newData, setNewData] = useState<Record<string, unknown>>({});
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);

  const tabConfig = TABS.find(t => t.id === activeTab)!;

  const loadTemplates = useCallback(async () => {
    if (!tenantId || !sector) return;
    setLoading(true);
    setError(null);
    try {
      const r = await api.getRiTemplates(tenantId, sector);
      const data = r as Record<string, unknown>;
      const key = activeTab === 'intent' ? 'intents'
        : activeTab === 'faq' ? 'faqs'
        : activeTab === 'objection_handler' ? 'objectionHandlers'
        : 'followupTemplates';
      setItems((data[key] as RiTemplateItem[]) ?? []);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Yuklenemedi');
    } finally {
      setLoading(false);
    }
  }, [tenantId, sector, activeTab]);

  useEffect(() => { loadTemplates(); }, [loadTemplates]);

  const handleLoadSector = () => {
    if (sectorInput.trim()) setSector(sectorInput.trim().toLowerCase());
  };

  const filtered = items.filter(i => {
    if (!search) return true;
    const s = search.toLowerCase();
    return Object.values(i).some(v => typeof v === 'string' && v.toLowerCase().includes(s));
  });

  const handleEdit = (item: RiTemplateItem) => {
    setEditingId(item.id);
    setEditData({ ...item });
  };

  const handleSave = async () => {
    if (!editingId) return;
    try {
      await api.updateRiTemplate(tenantId, activeTab, editingId, editData);
      setEditingId(null);
      await loadTemplates();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Guncelleme basarisiz');
    }
  };

  const handleCreate = async () => {
    try {
      await api.createRiTemplate(tenantId, activeTab, { ...newData, sector });
      setShowAdd(false);
      setNewData({});
      await loadTemplates();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Olusturma basarisiz');
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await api.deleteRiTemplate(tenantId, activeTab, id);
      setDeleteConfirm(null);
      await loadTemplates();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Silme basarisiz');
    }
  };

  const handleToggle = async (item: RiTemplateItem) => {
    try {
      await api.toggleRiTemplate(tenantId, activeTab, item.id, !(item.isActive as boolean));
      await loadTemplates();
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Toggle basarisiz');
    }
  };

  const getDisplayValue = (item: RiTemplateItem, field: string): string => {
    const val = item[field];
    if (val === null || val === undefined) return '-';
    if (typeof val === 'string') return val.length > 100 ? val.slice(0, 100) + '...' : val;
    return String(val);
  };

  return (
    <div style={{ padding: '2rem', maxWidth: 1200 }}>
      <h1 style={{ fontSize: '1.5rem', fontWeight: 700, marginBottom: '1rem' }}>
        RI Sablon Yonetimi
      </h1>

      {/* Sector selector */}
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.5rem', alignItems: 'center' }}>
        <input
          value={sectorInput}
          onChange={e => setSectorInput(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleLoadSector()}
          placeholder="Sektor girin (orn: moda, saglik)"
          style={{ padding: '0.5rem 0.75rem', border: '1px solid #d1d5db', borderRadius: 6, width: 260 }}
        />
        <button onClick={handleLoadSector} style={btnStyle}>
          Yukle
        </button>
        {sector && <span style={{ color: '#6b7280', fontSize: '0.875rem' }}>Aktif sektor: <b>{sector}</b></span>}
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: '0.25rem', borderBottom: '2px solid #e5e7eb', marginBottom: '1rem' }}>
        {TABS.map(tab => (
          <button
            key={tab.id}
            onClick={() => { setActiveTab(tab.id); setEditingId(null); setShowAdd(false); }}
            style={{
              padding: '0.5rem 1rem',
              border: 'none',
              borderBottom: activeTab === tab.id ? '2px solid #3b82f6' : '2px solid transparent',
              background: 'none',
              cursor: 'pointer',
              fontWeight: activeTab === tab.id ? 600 : 400,
              color: activeTab === tab.id ? '#3b82f6' : '#6b7280',
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {!sector ? (
        <p style={{ color: '#9ca3af', padding: '2rem', textAlign: 'center' }}>Sektor seciniz.</p>
      ) : (
        <>
          {/* Toolbar */}
          <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem', alignItems: 'center' }}>
            <div style={{ position: 'relative', flex: 1 }}>
              <Search size={16} style={{ position: 'absolute', left: 10, top: 10, color: '#9ca3af' }} />
              <input
                value={search}
                onChange={e => setSearch(e.target.value)}
                placeholder="Ara..."
                style={{ padding: '0.5rem 0.75rem 0.5rem 2rem', border: '1px solid #d1d5db', borderRadius: 6, width: '100%' }}
              />
            </div>
            <button onClick={loadTemplates} style={btnStyle} title="Yenile">
              <RefreshCw size={16} />
            </button>
            <button onClick={() => { setShowAdd(true); setNewData({}); }} style={{ ...btnStyle, background: '#3b82f6', color: '#fff' }}>
              <Plus size={16} /> Ekle
            </button>
            <span style={{ color: '#9ca3af', fontSize: '0.8rem' }}>{filtered.length} kayit</span>
          </div>

          {error && <div style={{ background: '#fef2f2', color: '#dc2626', padding: '0.75rem', borderRadius: 6, marginBottom: '1rem' }}>{error}</div>}
          {loading && <p style={{ color: '#9ca3af' }}>Yukleniyor...</p>}

          {/* Add row */}
          {showAdd && (
            <div style={{ background: '#f0f9ff', border: '1px solid #bfdbfe', borderRadius: 8, padding: '1rem', marginBottom: '1rem', display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <div>
                <label style={labelStyle}>{tabConfig.keyField}</label>
                <input
                  value={(newData[tabConfig.keyField] as string) ?? ''}
                  onChange={e => setNewData({ ...newData, [tabConfig.keyField]: e.target.value })}
                  style={inputStyle}
                />
              </div>
              <div style={{ flex: 1 }}>
                <label style={labelStyle}>{tabConfig.descField}</label>
                <input
                  value={(newData[tabConfig.descField] as string) ?? ''}
                  onChange={e => setNewData({ ...newData, [tabConfig.descField]: e.target.value })}
                  style={{ ...inputStyle, width: '100%' }}
                />
              </div>
              <button onClick={handleCreate} style={{ ...btnStyle, background: '#22c55e', color: '#fff' }}>
                <Save size={14} /> Kaydet
              </button>
              <button onClick={() => setShowAdd(false)} style={btnStyle}>
                <X size={14} />
              </button>
            </div>
          )}

          {/* Table */}
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid #e5e7eb', textAlign: 'left' }}>
                <th style={thStyle}>ID</th>
                <th style={thStyle}>{tabConfig.keyField}</th>
                <th style={thStyle}>{tabConfig.descField}</th>
                <th style={thStyle}>Aktif</th>
                <th style={{ ...thStyle, width: 120 }}>Islemler</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(item => (
                <tr key={item.id} style={{ borderBottom: '1px solid #f3f4f6' }}>
                  <td style={tdStyle}>{item.id}</td>
                  <td style={tdStyle}>
                    {editingId === item.id ? (
                      <input
                        value={(editData[tabConfig.keyField] as string) ?? ''}
                        onChange={e => setEditData({ ...editData, [tabConfig.keyField]: e.target.value })}
                        style={inputStyle}
                      />
                    ) : (
                      getDisplayValue(item, tabConfig.keyField)
                    )}
                  </td>
                  <td style={tdStyle}>
                    {editingId === item.id ? (
                      <input
                        value={(editData[tabConfig.descField] as string) ?? ''}
                        onChange={e => setEditData({ ...editData, [tabConfig.descField]: e.target.value })}
                        style={{ ...inputStyle, width: '100%' }}
                      />
                    ) : (
                      getDisplayValue(item, tabConfig.descField)
                    )}
                  </td>
                  <td style={tdStyle}>
                    <button onClick={() => handleToggle(item)} style={{ background: 'none', border: 'none', cursor: 'pointer' }}>
                      {item.isActive ? <ToggleRight size={20} color="#22c55e" /> : <ToggleLeft size={20} color="#d1d5db" />}
                    </button>
                  </td>
                  <td style={tdStyle}>
                    {editingId === item.id ? (
                      <div style={{ display: 'flex', gap: '0.25rem' }}>
                        <button onClick={handleSave} style={{ ...btnSmall, background: '#22c55e', color: '#fff' }}>
                          <Save size={12} />
                        </button>
                        <button onClick={() => setEditingId(null)} style={btnSmall}>
                          <X size={12} />
                        </button>
                      </div>
                    ) : deleteConfirm === item.id ? (
                      <div style={{ display: 'flex', gap: '0.25rem' }}>
                        <button onClick={() => handleDelete(item.id)} style={{ ...btnSmall, background: '#ef4444', color: '#fff' }}>
                          Sil
                        </button>
                        <button onClick={() => setDeleteConfirm(null)} style={btnSmall}>
                          <X size={12} />
                        </button>
                      </div>
                    ) : (
                      <div style={{ display: 'flex', gap: '0.25rem' }}>
                        <button onClick={() => handleEdit(item)} style={btnSmall} title="Duzenle">
                          Duzenle
                        </button>
                        <button onClick={() => setDeleteConfirm(item.id)} style={{ ...btnSmall, color: '#ef4444' }} title="Sil">
                          <Trash2 size={12} />
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {!loading && filtered.length === 0 && (
            <p style={{ color: '#9ca3af', textAlign: 'center', padding: '2rem' }}>
              Bu sektor icin {tabConfig.label} sablonu bulunamadi.
            </p>
          )}
        </>
      )}
    </div>
  );
}

const btnStyle: React.CSSProperties = {
  display: 'inline-flex', alignItems: 'center', gap: 4,
  padding: '0.5rem 0.75rem', border: '1px solid #d1d5db', borderRadius: 6,
  background: '#fff', cursor: 'pointer', fontSize: '0.875rem',
};
const btnSmall: React.CSSProperties = {
  display: 'inline-flex', alignItems: 'center', gap: 2,
  padding: '0.25rem 0.5rem', border: '1px solid #d1d5db', borderRadius: 4,
  background: '#fff', cursor: 'pointer', fontSize: '0.75rem',
};
const inputStyle: React.CSSProperties = {
  padding: '0.375rem 0.5rem', border: '1px solid #d1d5db', borderRadius: 4, fontSize: '0.875rem',
};
const labelStyle: React.CSSProperties = {
  display: 'block', fontSize: '0.75rem', color: '#6b7280', marginBottom: 2,
};
const thStyle: React.CSSProperties = {
  padding: '0.5rem 0.75rem', fontSize: '0.8rem', color: '#6b7280', fontWeight: 600,
};
const tdStyle: React.CSSProperties = {
  padding: '0.5rem 0.75rem', fontSize: '0.875rem',
};
