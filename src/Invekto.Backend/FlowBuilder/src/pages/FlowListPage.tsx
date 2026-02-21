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

  const btnPrimary = 'px-4 py-2 bg-brand-500 hover:bg-brand-600 text-white text-sm font-medium rounded-lg transition-colors';
  const btnGhost = 'px-3 py-2 text-sm text-navy-400 hover:text-navy-900 transition-colors';
  const inputClasses = 'w-full px-3 py-2.5 bg-white border border-navy-100 rounded-lg text-navy-900 placeholder-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus transition-all';

  return (
    <div className="min-h-screen bg-navy-50 text-navy-900">
      {/* Header */}
      <header className="bg-white border-b border-navy-100 px-6 py-3 flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold text-navy-900">Flow Builder</h1>
          <span className="text-2xs text-navy-300">Tenant #{tenantId}</span>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => {
              setNewFlowName('');
              setNewFlowError(null);
              setShowNewDialog(true);
            }}
            className={btnPrimary}
          >
            + Yeni Flow
          </button>
          <button onClick={logout} className={btnGhost}>
            Cikis
          </button>
        </div>
      </header>

      {/* Content */}
      <main className="max-w-5xl mx-auto px-6 py-8">
        {error && (
          <div className="mb-4 text-sm text-red-600 bg-red-50 border border-red-100 rounded-lg px-4 py-3 flex items-center justify-between">
            <span>{error}</span>
            <button onClick={() => setError(null)} className="text-red-400 hover:text-red-600 ml-4">
              &times;
            </button>
          </div>
        )}

        {loading ? (
          <div className="text-center py-20 text-navy-300">Yukleniyor...</div>
        ) : flows.length === 0 ? (
          <div className="text-center py-20">
            <p className="text-navy-400 mb-4">Henuz bir flow olusturulmamis.</p>
            <button
              onClick={() => {
                setNewFlowName('');
                setNewFlowError(null);
                setShowNewDialog(true);
              }}
              className={btnPrimary}
            >
              Ilk Flow'u Olustur
            </button>
          </div>
        ) : (
          <div className="space-y-3">
            {flows.map((flow) => (
              <div
                key={flow.flow_id}
                className="bg-white border border-navy-100 rounded-xl px-5 py-4 flex items-center justify-between hover:border-navy-200 hover:shadow-elevated transition-all"
              >
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="font-medium text-navy-900 truncate">{flow.flow_name}</span>
                    {flow.is_active && (
                      <span className="px-2 py-0.5 text-xs bg-emerald-50 text-emerald-700 border border-emerald-100 rounded-full">
                        Aktif
                      </span>
                    )}
                    {flow.health_score != null && (
                      <HealthBadge score={flow.health_score} issues={flow.health_issues} />
                    )}
                  </div>
                  <div className="flex items-center gap-4 text-xs text-navy-300">
                    <span>v{flow.config_version}</span>
                    <span>{flow.node_count} node / {flow.edge_count} edge</span>
                    <span>Guncelleme: {formatDate(flow.updated_at)}</span>
                  </div>
                </div>

                <div className="flex items-center gap-2 ml-4 flex-shrink-0">
                  <button
                    onClick={() => navigate(`/editor/${flow.flow_id}`)}
                    className="px-3 py-1.5 text-xs bg-brand-500 hover:bg-brand-600 text-white rounded-lg transition-colors font-medium"
                  >
                    Duzenle
                  </button>

                  {flow.is_active ? (
                    <button
                      onClick={() => handleDeactivate(flow.flow_id)}
                      disabled={actionLoading === flow.flow_id}
                      className="px-3 py-1.5 text-xs bg-amber-50 hover:bg-amber-100 text-amber-700 border border-amber-100 rounded-lg transition-colors disabled:opacity-40"
                    >
                      Deaktif Et
                    </button>
                  ) : (
                    <button
                      onClick={() => handleActivate(flow.flow_id)}
                      disabled={actionLoading === flow.flow_id}
                      className="px-3 py-1.5 text-xs bg-emerald-50 hover:bg-emerald-100 text-emerald-700 border border-emerald-100 rounded-lg transition-colors disabled:opacity-40"
                    >
                      Aktif Et
                    </button>
                  )}

                  <button
                    onClick={() => handleDuplicate(flow)}
                    disabled={actionLoading === flow.flow_id}
                    className="px-3 py-1.5 text-xs bg-brand-50 hover:bg-brand-100 text-brand-600 border border-brand-100 rounded-lg transition-colors disabled:opacity-40"
                    title="Flow'un kopyasini olustur"
                  >
                    Kopyala
                  </button>

                  <button
                    onClick={() => handleCopyConfig(flow)}
                    disabled={actionLoading === flow.flow_id}
                    className="px-3 py-1.5 text-xs bg-navy-50 hover:bg-navy-100 text-navy-500 rounded-lg transition-colors disabled:opacity-40"
                    title="Flow JSON'u panoya kopyala"
                  >
                    JSON
                  </button>

                  <button
                    onClick={() => setDeleteTarget(flow)}
                    disabled={actionLoading === flow.flow_id || flow.is_active}
                    className="px-3 py-1.5 text-xs bg-red-50 hover:bg-red-100 text-red-500 border border-red-100 rounded-lg transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
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
        <div className="fixed inset-0 bg-navy-900/40 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-white border border-navy-100 rounded-2xl w-full max-w-md p-6 shadow-elevated">
            <h2 className="text-lg font-semibold text-navy-900 mb-4">Yeni Flow Olustur</h2>
            <input
              type="text"
              value={newFlowName}
              onChange={(e) => setNewFlowName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
              placeholder="Flow adi (ornek: Satis Chatbot)"
              className={`${inputClasses} mb-3`}
              autoFocus
              disabled={creating}
            />
            {newFlowError && (
              <p className="text-sm text-red-500 mb-3">{newFlowError}</p>
            )}
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setShowNewDialog(false)}
                disabled={creating}
                className={btnGhost}
              >
                Iptal
              </button>
              <button
                onClick={handleCreate}
                disabled={creating}
                className={`${btnPrimary} disabled:opacity-40`}
              >
                {creating ? 'Olusturuluyor...' : 'Olustur'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirm Dialog */}
      {deleteTarget && (
        <div className="fixed inset-0 bg-navy-900/40 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-white border border-navy-100 rounded-2xl w-full max-w-sm p-6 shadow-elevated">
            <h2 className="text-lg font-semibold text-navy-900 mb-2">Flow'u Sil</h2>
            <p className="text-sm text-navy-400 mb-4">
              <strong className="text-navy-900">{deleteTarget.flow_name}</strong> flow'u kalici olarak silinecek. Bu islem geri alinamaz.
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setDeleteTarget(null)}
                className={btnGhost}
              >
                Iptal
              </button>
              <button
                onClick={() => handleDelete(deleteTarget)}
                className="px-4 py-2 text-sm bg-red-500 hover:bg-red-600 text-white font-medium rounded-lg transition-colors"
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
    bg = 'bg-emerald-50';
    text = 'text-emerald-700';
    border = 'border-emerald-100';
    label = 'Saglikli';
  } else if (score >= 50) {
    bg = 'bg-amber-50';
    text = 'text-amber-700';
    border = 'border-amber-100';
    label = 'Dikkat';
  } else {
    bg = 'bg-red-50';
    text = 'text-red-700';
    border = 'border-red-100';
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
