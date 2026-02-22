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

  const [wizardLoading, setWizardLoading] = useState(false);

  const handleStartWizard = async () => {
    if (!tenantId || wizardLoading) return;
    setWizardLoading(true);
    setError(null);
    try {
      const { startWizard } = await import('../lib/wizard-api');
      const result = await startWizard(tenantId);
      navigate(`/wizard/${result.flow_id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'AI Wizard baslatilamadi');
    } finally {
      setWizardLoading(false);
    }
  };

  const btnPrimary = 'px-4 py-2 bg-brand-500 hover:bg-brand-600 text-white text-sm font-medium rounded-lg transition-colors';
  const btnAI = 'px-4 py-2 bg-gradient-to-r from-purple-500 to-purple-600 hover:from-purple-600 hover:to-purple-700 text-white text-sm font-medium rounded-lg transition-all disabled:opacity-40';
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
            onClick={handleStartWizard}
            disabled={wizardLoading}
            className={btnAI}
          >
            {wizardLoading ? 'Hazirlaniyor...' : '\u2728 AI ile Olustur'}
          </button>
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
                onDoubleClick={() => navigate(`/editor/${flow.flow_id}`)}
                className="bg-white border border-navy-100 rounded-xl px-5 py-4 flex items-center justify-between hover:border-navy-200 hover:shadow-elevated transition-all cursor-pointer select-none"
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
                  {flow.assigned_instances && flow.assigned_instances.length > 0 && (
                    <div className="flex items-center gap-1.5 mt-1.5 flex-wrap">
                      {flow.assigned_instances.map((inst) => (
                        <span
                          key={inst.instanceId}
                          className="inline-flex items-center gap-1 px-2 py-0.5 text-2xs bg-emerald-50 text-emerald-700 border border-emerald-100 rounded-full"
                          title={inst.instanceId}
                        >
                          <svg viewBox="0 0 20 20" fill="currentColor" className="w-3 h-3">
                            <path d="M2 3.5A1.5 1.5 0 013.5 2h1.148a1.5 1.5 0 011.465 1.175l.716 3.223a1.5 1.5 0 01-1.052 1.767l-.933.267c-.694.198-.83 1.063-.373 1.574a7.028 7.028 0 004.633 2.368c.703.1 1.202-.466 1.128-1.176l-.11-1.056a1.5 1.5 0 011.21-1.632l2.378-.476A1.5 1.5 0 0115.5 9.5v1.264a3 3 0 01-2.286 2.909 11.054 11.054 0 01-7.863-1.867A11.023 11.023 0 012 5.732V3.5z" />
                          </svg>
                          {inst.instanceName}
                        </span>
                      ))}
                    </div>
                  )}
                </div>

                <div className="flex items-center gap-1 ml-4 flex-shrink-0">
                  {/* Edit */}
                  <button
                    onClick={(e) => { e.stopPropagation(); navigate(`/editor/${flow.flow_id}`); }}
                    className="p-2 rounded-lg text-brand-500 hover:bg-brand-50 transition-colors"
                    title="Duzenle"
                  >
                    <svg viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                      <path d="M2.695 14.763l-1.262 3.154a.5.5 0 00.65.65l3.155-1.262a4 4 0 001.343-.885L17.5 5.5a2.121 2.121 0 00-3-3L3.58 13.42a4 4 0 00-.885 1.343z" />
                    </svg>
                  </button>

                  {/* Activate / Deactivate */}
                  {flow.is_active ? (
                    <button
                      onClick={(e) => { e.stopPropagation(); handleDeactivate(flow.flow_id); }}
                      disabled={actionLoading === flow.flow_id}
                      className="p-2 rounded-lg text-amber-600 hover:bg-amber-50 transition-colors disabled:opacity-40"
                      title="Deaktif Et"
                    >
                      <svg viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                        <path d="M5.75 3a.75.75 0 00-.75.75v12.5c0 .414.336.75.75.75h1.5a.75.75 0 00.75-.75V3.75A.75.75 0 007.25 3h-1.5zM12.75 3a.75.75 0 00-.75.75v12.5c0 .414.336.75.75.75h1.5a.75.75 0 00.75-.75V3.75a.75.75 0 00-.75-.75h-1.5z" />
                      </svg>
                    </button>
                  ) : (
                    <button
                      onClick={(e) => { e.stopPropagation(); handleActivate(flow.flow_id); }}
                      disabled={actionLoading === flow.flow_id}
                      className="p-2 rounded-lg text-emerald-600 hover:bg-emerald-50 transition-colors disabled:opacity-40"
                      title="Aktif Et"
                    >
                      <svg viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                        <path d="M6.3 2.84A1.5 1.5 0 004 4.11v11.78a1.5 1.5 0 002.3 1.27l9.344-5.891a1.5 1.5 0 000-2.538L6.3 2.841z" />
                      </svg>
                    </button>
                  )}

                  {/* Duplicate */}
                  <button
                    onClick={(e) => { e.stopPropagation(); handleDuplicate(flow); }}
                    disabled={actionLoading === flow.flow_id}
                    className="p-2 rounded-lg text-brand-500 hover:bg-brand-50 transition-colors disabled:opacity-40"
                    title="Kopyala"
                  >
                    <svg viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                      <path d="M7 3.5A1.5 1.5 0 018.5 2h3.879a1.5 1.5 0 011.06.44l3.122 3.12A1.5 1.5 0 0117 6.622V12.5a1.5 1.5 0 01-1.5 1.5h-1v-3.379a3 3 0 00-.879-2.121L10.5 5.379A3 3 0 008.379 4.5H7v-1z" />
                      <path d="M4.5 6A1.5 1.5 0 003 7.5v9A1.5 1.5 0 004.5 18h7a1.5 1.5 0 001.5-1.5v-5.879a1.5 1.5 0 00-.44-1.06L9.44 6.44A1.5 1.5 0 008.378 6H4.5z" />
                    </svg>
                  </button>

                  {/* Delete */}
                  <button
                    onClick={(e) => { e.stopPropagation(); setDeleteTarget(flow); }}
                    disabled={actionLoading === flow.flow_id || flow.is_active}
                    className="p-2 rounded-lg text-red-400 hover:bg-red-50 transition-colors disabled:opacity-30 disabled:cursor-not-allowed"
                    title={flow.is_active ? 'Aktif flow silinemez' : 'Sil'}
                  >
                    <svg viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                      <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 006 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 10.23 1.482l.149-.022.841 10.518A2.75 2.75 0 007.596 19h4.807a2.75 2.75 0 002.742-2.53l.841-10.519.149.023a.75.75 0 00.23-1.482A41.03 41.03 0 0014 4.193V3.75A2.75 2.75 0 0011.25 1h-2.5zM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4zM8.58 7.72a.75.75 0 00-1.5.06l.3 7.5a.75.75 0 101.5-.06l-.3-7.5zm4.34.06a.75.75 0 10-1.5-.06l-.3 7.5a.75.75 0 101.5.06l.3-7.5z" clipRule="evenodd" />
                    </svg>
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
