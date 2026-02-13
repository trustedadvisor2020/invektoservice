import { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';
import {
  listFlows,
  createFlow,
  deleteFlow,
  activateFlow,
  deactivateFlow,
  getFlow,
  type FlowSummary,
  ApiClientError,
} from '../lib/api';
import { createDefaultFlow, type FlowConfigV2 } from '../types/flow';

export function FlowListPage() {
  const { session, logout } = useAuth();
  const navigate = useNavigate();
  const tenantId = session?.tenant_id ?? 0;

  const [flows, setFlows] = useState<FlowSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<number | null>(null);

  // New flow dialog
  const [showNewDialog, setShowNewDialog] = useState(false);
  const [newFlowName, setNewFlowName] = useState('');
  const [newFlowError, setNewFlowError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  // Delete confirm dialog
  const [deleteTarget, setDeleteTarget] = useState<FlowSummary | null>(null);

  const fetchFlows = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await listFlows(tenantId);
      setFlows(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Flow listesi alinamadi');
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => {
    fetchFlows();
  }, [fetchFlows]);

  const handleCreate = async () => {
    if (!newFlowName.trim()) {
      setNewFlowError('Flow adi bos olamaz.');
      return;
    }
    setCreating(true);
    setNewFlowError(null);
    try {
      const defaultConfig = createDefaultFlow();
      defaultConfig.metadata.name = newFlowName.trim();
      const created = await createFlow(tenantId, {
        flow_name: newFlowName.trim(),
        flow_config: defaultConfig,
      });
      setShowNewDialog(false);
      setNewFlowName('');
      navigate(`/editor/${created.flow_id}`);
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 409) {
        setNewFlowError('Bu isimde bir flow zaten mevcut.');
      } else {
        setNewFlowError(err instanceof Error ? err.message : 'Olusturma basarisiz');
      }
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (flow: FlowSummary) => {
    setDeleteTarget(null);
    setActionLoading(flow.flow_id);
    try {
      await deleteFlow(tenantId, flow.flow_id);
      setFlows((prev) => prev.filter((f) => f.flow_id !== flow.flow_id));
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 409) {
        setError('Aktif flow silinemez. Once deaktif edin.');
      } else {
        setError(err instanceof Error ? err.message : 'Silme basarisiz');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleActivate = async (flowId: number) => {
    setActionLoading(flowId);
    setError(null);
    try {
      await activateFlow(tenantId, flowId);
      await fetchFlows();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Aktivasyon basarisiz');
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeactivate = async (flowId: number) => {
    setActionLoading(flowId);
    setError(null);
    try {
      await deactivateFlow(tenantId, flowId);
      await fetchFlows();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Deaktivasyon basarisiz');
    } finally {
      setActionLoading(null);
    }
  };

  const handleDuplicate = async (flow: FlowSummary) => {
    setActionLoading(flow.flow_id);
    setError(null);
    try {
      const detail = await getFlow(tenantId, flow.flow_id);
      const config = detail.flow_config as FlowConfigV2;

      // Generate duplicate name with numbered suffix
      const baseName = flow.flow_name;
      const existingNames = new Set(flows.map((f) => f.flow_name));

      let dupName = `${baseName} - Kopya`;
      if (existingNames.has(dupName)) {
        let counter = 2;
        while (existingNames.has(`${baseName} - Kopya (${counter})`)) {
          counter++;
        }
        dupName = `${baseName} - Kopya (${counter})`;
      }

      const dupConfig: FlowConfigV2 = {
        ...config,
        metadata: { ...config.metadata, name: dupName },
      };

      const created = await createFlow(tenantId, {
        flow_name: dupName,
        flow_config: dupConfig,
      });

      navigate(`/editor/${created.flow_id}`);
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 409) {
        setError('Bu isimde bir flow zaten mevcut. Lutfen tekrar deneyin.');
      } else {
        setError(err instanceof Error ? err.message : 'Kopyalama basarisiz');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleCopyConfig = async (flow: FlowSummary) => {
    // We only have summary here; for copy we fetch full detail to get flow_config
    setActionLoading(flow.flow_id);
    try {
      const detail = await getFlow(tenantId, flow.flow_id);
      await navigator.clipboard.writeText(
        JSON.stringify(detail.flow_config, null, 2)
      );
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Kopyalama basarisiz');
    } finally {
      setActionLoading(null);
    }
  };

  const formatDate = (iso: string) => {
    const d = new Date(iso);
    return d.toLocaleDateString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  return (
    <div className="min-h-screen bg-slate-50 text-slate-800">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 px-6 py-3 flex items-center justify-between shadow-sm">
        <div>
          <h1 className="text-xl font-bold text-slate-900">Flow Builder</h1>
          <span className="text-xs text-slate-500">Tenant #{tenantId}</span>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => {
              setNewFlowName('');
              setNewFlowError(null);
              setShowNewDialog(true);
            }}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-lg transition"
          >
            + Yeni Flow
          </button>
          <button
            onClick={logout}
            className="px-3 py-2 text-sm text-slate-500 hover:text-slate-900 transition"
          >
            Cikis
          </button>
        </div>
      </header>

      {/* Content */}
      <main className="max-w-5xl mx-auto px-6 py-8">
        {error && (
          <div className="mb-4 text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-4 py-3 flex items-center justify-between">
            <span>{error}</span>
            <button onClick={() => setError(null)} className="text-red-400 hover:text-red-600 ml-4">
              &times;
            </button>
          </div>
        )}

        {loading ? (
          <div className="text-center py-20 text-slate-500">Yukleniyor...</div>
        ) : flows.length === 0 ? (
          <div className="text-center py-20">
            <p className="text-slate-500 mb-4">Henuz bir flow olusturulmamis.</p>
            <button
              onClick={() => {
                setNewFlowName('');
                setNewFlowError(null);
                setShowNewDialog(true);
              }}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-lg transition"
            >
              Ilk Flow'u Olustur
            </button>
          </div>
        ) : (
          <div className="space-y-3">
            {flows.map((flow) => (
              <div
                key={flow.flow_id}
                className="bg-white border border-slate-200 rounded-xl px-5 py-4 flex items-center justify-between hover:border-slate-300 hover:shadow-sm transition"
              >
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="font-semibold text-slate-900 truncate">{flow.flow_name}</span>
                    {flow.is_active && (
                      <span className="px-2 py-0.5 text-xs bg-green-50 text-green-700 border border-green-200 rounded-full">
                        Aktif
                      </span>
                    )}
                    {flow.health_score != null && (
                      <HealthBadge score={flow.health_score} issues={flow.health_issues} />
                    )}
                  </div>
                  <div className="flex items-center gap-4 text-xs text-slate-500">
                    <span>v{flow.config_version}</span>
                    <span>{flow.node_count} node / {flow.edge_count} edge</span>
                    <span>Guncelleme: {formatDate(flow.updated_at)}</span>
                  </div>
                </div>

                <div className="flex items-center gap-2 ml-4 flex-shrink-0">
                  <button
                    onClick={() => navigate(`/editor/${flow.flow_id}`)}
                    className="px-3 py-1.5 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition"
                  >
                    Duzenle
                  </button>

                  {flow.is_active ? (
                    <button
                      onClick={() => handleDeactivate(flow.flow_id)}
                      disabled={actionLoading === flow.flow_id}
                      className="px-3 py-1.5 text-xs bg-amber-50 hover:bg-amber-100 text-amber-700 border border-amber-200 rounded-lg transition disabled:opacity-50"
                    >
                      Deaktif Et
                    </button>
                  ) : (
                    <button
                      onClick={() => handleActivate(flow.flow_id)}
                      disabled={actionLoading === flow.flow_id}
                      className="px-3 py-1.5 text-xs bg-green-50 hover:bg-green-100 text-green-700 border border-green-200 rounded-lg transition disabled:opacity-50"
                    >
                      Aktif Et
                    </button>
                  )}

                  <button
                    onClick={() => handleDuplicate(flow)}
                    disabled={actionLoading === flow.flow_id}
                    className="px-3 py-1.5 text-xs bg-indigo-50 hover:bg-indigo-100 text-indigo-600 border border-indigo-200 rounded-lg transition disabled:opacity-50"
                    title="Flow'un kopyasini olustur"
                  >
                    Kopyala
                  </button>

                  <button
                    onClick={() => handleCopyConfig(flow)}
                    disabled={actionLoading === flow.flow_id}
                    className="px-3 py-1.5 text-xs bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-lg transition disabled:opacity-50"
                    title="Flow JSON'u panoya kopyala"
                  >
                    JSON
                  </button>

                  <button
                    onClick={() => setDeleteTarget(flow)}
                    disabled={actionLoading === flow.flow_id || flow.is_active}
                    className="px-3 py-1.5 text-xs bg-red-50 hover:bg-red-100 text-red-600 border border-red-200 rounded-lg transition disabled:opacity-30 disabled:cursor-not-allowed"
                    title={flow.is_active ? 'Aktif flow silinemez' : 'Flow sil'}
                  >
                    Sil
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>

      {/* New Flow Dialog */}
      {showNewDialog && (
        <div className="fixed inset-0 bg-black/30 flex items-center justify-center z-50">
          <div className="bg-white border border-slate-200 rounded-xl w-full max-w-md p-6 shadow-xl">
            <h2 className="text-lg font-semibold text-slate-900 mb-4">Yeni Flow Olustur</h2>
            <input
              type="text"
              value={newFlowName}
              onChange={(e) => setNewFlowName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
              placeholder="Flow adi (ornek: Satis Chatbot)"
              className="w-full px-3 py-2 bg-white border border-slate-300 rounded-lg text-slate-900 placeholder-slate-400 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition mb-3"
              autoFocus
              disabled={creating}
            />
            {newFlowError && (
              <p className="text-sm text-red-600 mb-3">{newFlowError}</p>
            )}
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setShowNewDialog(false)}
                disabled={creating}
                className="px-4 py-2 text-sm text-slate-500 hover:text-slate-900 transition"
              >
                Iptal
              </button>
              <button
                onClick={handleCreate}
                disabled={creating}
                className="px-4 py-2 text-sm bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 disabled:opacity-50 text-white font-medium rounded-lg transition"
              >
                {creating ? 'Olusturuluyor...' : 'Olustur'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirm Dialog */}
      {deleteTarget && (
        <div className="fixed inset-0 bg-black/30 flex items-center justify-center z-50">
          <div className="bg-white border border-slate-200 rounded-xl w-full max-w-sm p-6 shadow-xl">
            <h2 className="text-lg font-semibold text-slate-900 mb-2">Flow'u Sil</h2>
            <p className="text-sm text-slate-500 mb-4">
              <strong className="text-slate-900">{deleteTarget.flow_name}</strong> flow'u kalici olarak silinecek. Bu islem geri alinamaz.
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setDeleteTarget(null)}
                className="px-4 py-2 text-sm text-slate-500 hover:text-slate-900 transition"
              >
                Iptal
              </button>
              <button
                onClick={() => handleDelete(deleteTarget)}
                className="px-4 py-2 text-sm bg-red-600 hover:bg-red-700 text-white font-medium rounded-lg transition"
              >
                Evet, Sil
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function HealthBadge({ score, issues }: { score: number; issues: string[] | null }) {
  let bg: string;
  let text: string;
  let border: string;
  let label: string;

  if (score >= 80) {
    bg = 'bg-green-50';
    text = 'text-green-700';
    border = 'border-green-200';
    label = 'Saglikli';
  } else if (score >= 50) {
    bg = 'bg-amber-50';
    text = 'text-amber-700';
    border = 'border-amber-200';
    label = 'Dikkat';
  } else {
    bg = 'bg-red-50';
    text = 'text-red-700';
    border = 'border-red-200';
    label = 'Sorunlu';
  }

  const tooltip = issues && issues.length > 0 ? issues.join(' | ') : `Skor: ${score}`;

  return (
    <span
      className={`px-2 py-0.5 text-xs ${bg} ${text} border ${border} rounded-full cursor-default`}
      title={tooltip}
    >
      {score} - {label}
    </span>
  );
}
