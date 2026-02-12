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
import { createDefaultFlow } from '../types/flow';

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
    <div className="min-h-screen bg-[#0f0f23] text-gray-200">
      {/* Header */}
      <header className="bg-[#1e1e3a] border-b border-[#2d2d4a] px-6 py-3 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Flow Builder</h1>
          <span className="text-xs text-gray-400">Tenant #{tenantId}</span>
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
            className="px-3 py-2 text-sm text-gray-400 hover:text-white transition"
          >
            Cikis
          </button>
        </div>
      </header>

      {/* Content */}
      <main className="max-w-5xl mx-auto px-6 py-8">
        {error && (
          <div className="mb-4 text-sm text-red-400 bg-red-900/20 border border-red-800/30 rounded-lg px-4 py-3 flex items-center justify-between">
            <span>{error}</span>
            <button onClick={() => setError(null)} className="text-red-300 hover:text-white ml-4">
              &times;
            </button>
          </div>
        )}

        {loading ? (
          <div className="text-center py-20 text-gray-400">Yukleniyor...</div>
        ) : flows.length === 0 ? (
          <div className="text-center py-20">
            <p className="text-gray-400 mb-4">Henuz bir flow olusturulmamis.</p>
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
                className="bg-[#1e1e3a] border border-[#2d2d4a] rounded-xl px-5 py-4 flex items-center justify-between hover:border-[#3d3d5a] transition"
              >
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="font-semibold text-white truncate">{flow.flow_name}</span>
                    {flow.is_active && (
                      <span className="px-2 py-0.5 text-xs bg-green-900/40 text-green-400 border border-green-800/30 rounded-full">
                        Aktif
                      </span>
                    )}
                  </div>
                  <div className="flex items-center gap-4 text-xs text-gray-400">
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
                      className="px-3 py-1.5 text-xs bg-yellow-700/50 hover:bg-yellow-700 text-yellow-200 rounded-lg transition disabled:opacity-50"
                    >
                      Deaktif Et
                    </button>
                  ) : (
                    <button
                      onClick={() => handleActivate(flow.flow_id)}
                      disabled={actionLoading === flow.flow_id}
                      className="px-3 py-1.5 text-xs bg-green-700/50 hover:bg-green-700 text-green-200 rounded-lg transition disabled:opacity-50"
                    >
                      Aktif Et
                    </button>
                  )}

                  <button
                    onClick={() => handleCopyConfig(flow)}
                    disabled={actionLoading === flow.flow_id}
                    className="px-3 py-1.5 text-xs bg-[#2d2d4a] hover:bg-[#3d3d5a] text-gray-300 rounded-lg transition disabled:opacity-50"
                    title="Flow JSON'u panoya kopyala"
                  >
                    Kopyala
                  </button>

                  <button
                    onClick={() => setDeleteTarget(flow)}
                    disabled={actionLoading === flow.flow_id || flow.is_active}
                    className="px-3 py-1.5 text-xs bg-red-900/40 hover:bg-red-800 text-red-300 rounded-lg transition disabled:opacity-30 disabled:cursor-not-allowed"
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
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50">
          <div className="bg-[#1e1e3a] border border-[#2d2d4a] rounded-xl w-full max-w-md p-6 shadow-2xl">
            <h2 className="text-lg font-semibold text-white mb-4">Yeni Flow Olustur</h2>
            <input
              type="text"
              value={newFlowName}
              onChange={(e) => setNewFlowName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
              placeholder="Flow adi (ornek: Satis Chatbot)"
              className="w-full px-3 py-2 bg-[#0f0f23] border border-[#3d3d5a] rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-blue-500 transition mb-3"
              autoFocus
              disabled={creating}
            />
            {newFlowError && (
              <p className="text-sm text-red-400 mb-3">{newFlowError}</p>
            )}
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setShowNewDialog(false)}
                disabled={creating}
                className="px-4 py-2 text-sm text-gray-400 hover:text-white transition"
              >
                Iptal
              </button>
              <button
                onClick={handleCreate}
                disabled={creating}
                className="px-4 py-2 text-sm bg-blue-600 hover:bg-blue-700 disabled:bg-blue-800 disabled:opacity-50 text-white font-medium rounded-lg transition"
              >
                {creating ? 'Olusturuluyor...' : 'Olustur'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirm Dialog */}
      {deleteTarget && (
        <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50">
          <div className="bg-[#1e1e3a] border border-[#2d2d4a] rounded-xl w-full max-w-sm p-6 shadow-2xl">
            <h2 className="text-lg font-semibold text-white mb-2">Flow'u Sil</h2>
            <p className="text-sm text-gray-400 mb-4">
              <strong className="text-white">{deleteTarget.flow_name}</strong> flow'u kalici olarak silinecek. Bu islem geri alinamaz.
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setDeleteTarget(null)}
                className="px-4 py-2 text-sm text-gray-400 hover:text-white transition"
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
