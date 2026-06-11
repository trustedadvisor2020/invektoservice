import { useCallback, useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ReactFlowProvider } from '@xyflow/react';
import { FlowCanvas } from './components/FlowCanvas';
import { NodePalette } from './components/NodePalette';
import { Toolbar } from './components/Toolbar';
import { SimulationPanel } from './components/SimulationPanel';
import { AiChatPanel } from './components/AiChatPanel';
import { FlowLogPanel } from './components/FlowLogPanel';
import { NodePropertyPanel } from './panels/NodePropertyPanel';
import { useFlowStore } from '../../stores/flow-store';
import { useSimulationStore } from '../../stores/simulation-store';
import { useAiChatStore } from '../../stores/ai-chat-store';
import { useFlowLogStore } from '../../stores/flow-log-store';
import { useAuth } from '../../hooks/useAuth';
import { api, ApiClientError } from '../../lib/api';
import { computeFlowDiff } from '../../lib/flow-diff';
import type { FlowConfigV2 } from '../../types/flow';
import type { WizardMessage } from '../../types/wizard';

export function FlowEditorPage() {
  const { flowId: flowIdParam } = useParams<{ flowId: string }>();
  const navigate = useNavigate();
  const { session } = useAuth();
  const tenantId = session?.tenantId ?? 0;
  const flowId = parseInt(flowIdParam ?? '0', 10);

  const [isSaving, setIsSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [showExitDialog, setShowExitDialog] = useState(false);
  const [isActive, setIsActive] = useState(false);
  const [currentVersion, setCurrentVersion] = useState(0);
  const [isToggling, setIsToggling] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(() => {
    try { return localStorage.getItem('invekto_flow_palette_open') !== 'false'; }
    catch { return true; }
  });
  const loadFlow = useFlowStore((s) => s.loadFlow);

  // Load flow from API on mount
  useEffect(() => {
    if (!flowId || !tenantId) {
      navigate('/flow-builder', { replace: true });
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setLoadError(null);

    api.getFlow(tenantId, flowId)
      .then((detail) => {
        if (cancelled) return;
        const raw = (detail.flow_config ?? {}) as Partial<FlowConfigV2>;
        const config: FlowConfigV2 = {
          version: raw.version ?? 2,
          metadata: { name: detail.flow_name, ...raw.metadata },
          nodes: raw.nodes ?? [],
          edges: raw.edges ?? [],
          settings: raw.settings ?? {} as FlowConfigV2['settings'],
        };
        loadFlow(config, (detail.wizard_history as WizardMessage[] | null) ?? null);
        setIsActive(detail.is_active);
        setCurrentVersion(detail.current_version ?? 0);
        setIsLoading(false);

        // Auto-open AI wizard if loaded from template — show template as a banner,
        // send a HIDDEN TEMPLATE_SEED signal so the AI greets the user first (no visible user message).
        const params = new URLSearchParams(window.location.search);
        const templateId = params.get('template');
        if (templateId) {
          window.history.replaceState({}, '', window.location.pathname);
          import('../../data/flow-templates').then(({ FLOW_TEMPLATES }) => {
            const tpl = FLOW_TEMPLATES.find(t => t.id === templateId);
            if (!tpl) {
              // Stale/invalid template id (renamed or removed). Surface a diagnostic + fall back to a
              // standard non-template AI chat open so the user still gets the editor, just without a banner/seed.
              console.warn('[FlowEditor] Unknown template id in URL:', templateId, '— opening chat without template context.');
              useSimulationStore.getState().close();
              useAiChatStore.getState().open(flowId, tenantId);
              return;
            }
            useSimulationStore.getState().close();
            useAiChatStore.getState().openWithTemplate(flowId, tenantId, { title: tpl.title }, config);
          });
        }
      })
      .catch((err) => {
        if (cancelled) return;
        if (err instanceof ApiClientError && err.status === 404) {
          setLoadError('Flow bulunamadı.');
        } else {
          setLoadError(err instanceof Error ? err.message : 'Flow yüklenemedi');
        }
        setIsLoading(false);
      });

    return () => { cancelled = true; };
  }, [flowId, tenantId, loadFlow, navigate]);

  const handleTogglePalette = useCallback(() => {
    setPaletteOpen((v) => {
      const next = !v;
      try { localStorage.setItem('invekto_flow_palette_open', String(next)); } catch {}
      return next;
    });
  }, []);

  const handleSave = useCallback(async () => {
    setIsSaving(true);
    setSaveError(null);
    try {
      const config = useFlowStore.getState().toFlowConfig();
      const res = await api.updateFlow(tenantId, flowId, {
        flow_name: config.metadata.name,
        flow_config: config,
      });
      useFlowStore.getState().markClean();
      // Update version from response (Automation returns current_version)
      const ver = (res as unknown as { current_version?: number }).current_version;
      if (ver) setCurrentVersion(ver);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Kaydetme başarısız');
    } finally {
      setIsSaving(false);
    }
  }, [tenantId, flowId]);

  const handleBack = useCallback(() => {
    const isDirty = useFlowStore.getState().isDirty;
    if (isDirty) {
      setShowExitDialog(true);
      return;
    }
    useSimulationStore.getState().close();
    navigate('/flow-builder');
  }, [navigate]);

  const handleToggleActive = useCallback(async () => {
    setIsToggling(true);
    try {
      if (isActive) {
        await api.deactivateFlow(tenantId, flowId);
        setIsActive(false);
      } else {
        await api.activateFlow(tenantId, flowId);
        setIsActive(true);
      }
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'INV-AT-008: Flow aktivasyon hatası');
    } finally {
      setIsToggling(false);
    }
  }, [isActive, tenantId, flowId]);

  const handleExitSave = useCallback(async () => {
    setShowExitDialog(false);
    await handleSave();
    useSimulationStore.getState().close();
    navigate('/flow-builder');
  }, [handleSave, navigate]);

  const handleExitDiscard = useCallback(() => {
    setShowExitDialog(false);
    useSimulationStore.getState().close();
    navigate('/flow-builder');
  }, [navigate]);

  // AI Chat toggle — mutual exclusion with simulation & flow log
  const aiChatOpen = useAiChatStore((s) => s.isOpen);
  const pendingFlowConfig = useAiChatStore((s) => s.pendingFlowConfig);

  // Compute diff overlay when AI suggests changes
  useEffect(() => {
    const setPendingDiff = useFlowStore.getState().setPendingDiff;
    if (!pendingFlowConfig) {
      setPendingDiff(new Map());
      return;
    }
    const currentConfig = useFlowStore.getState().toFlowConfig();
    const diffMap = computeFlowDiff(currentConfig, pendingFlowConfig);
    const statusMap = new Map<string, 'added' | 'modified' | 'removed'>();
    for (const [id, diff] of diffMap) {
      if (diff.status !== 'unchanged') {
        statusMap.set(id, diff.status);
      }
    }
    setPendingDiff(statusMap);
  }, [pendingFlowConfig]);

  const handleToggleAiChat = useCallback(async () => {
    const aiChat = useAiChatStore.getState();
    if (aiChat.isOpen) {
      aiChat.close();
      return;
    }
    // Close simulation when opening AI chat (flow log stays independent)
    useSimulationStore.getState().close();
    await aiChat.open(flowId, tenantId);
  }, [flowId, tenantId]);

  const handleAiApply = useCallback((config: FlowConfigV2) => {
    const flowState = useFlowStore.getState();
    // Capture pre-apply snapshot so the panel can offer "Geri al"
    useAiChatStore.getState().captureSnapshot(flowState.toFlowConfig());
    loadFlow(config, flowState.wizardHistory);
  }, [loadFlow]);

  // Flow log toggle — independent panel, only controlled by its button
  const flowLogOpen = useFlowLogStore((s) => s.isOpen);

  const handleToggleFlowLog = useCallback(() => {
    const logStore = useFlowLogStore.getState();
    if (logStore.isOpen) {
      logStore.close();
    } else {
      logStore.open();
    }
  }, []);

  // AHA #4: Tek Tikla Test — save first if dirty, then start simulation
  const handleTest = useCallback(async () => {
    const store = useFlowStore.getState();
    const sim = useSimulationStore.getState();

    // If simulation is already open, just toggle it
    if (sim.isOpen) {
      sim.close();
      return;
    }

    // Close AI chat when opening simulation (flow log stays independent)
    useAiChatStore.getState().close();

    // If dirty, warn user to save first
    if (store.isDirty) {
      setSaveError('Önce flow\'u kaydedin, sonra test edin.');
      return;
    }

    // Start simulation
    await sim.start(tenantId, flowId);
  }, [tenantId, flowId]);

  // Rollback handler: restore a version and reload the flow
  const handleRollback = useCallback(async (versionNumber: number) => {
    try {
      const res = await api.rollbackFlowVersion(tenantId, flowId, versionNumber);
      setCurrentVersion(res.current_version);
      // Reload the flow to get the restored config
      const detail = await api.getFlow(tenantId, flowId);
      const raw = (detail.flow_config ?? {}) as Partial<FlowConfigV2>;
      const config: FlowConfigV2 = {
        version: raw.version ?? 2,
        metadata: { name: detail.flow_name, ...raw.metadata },
        nodes: raw.nodes ?? [],
        edges: raw.edges ?? [],
        settings: raw.settings ?? {} as FlowConfigV2['settings'],
      };
      loadFlow(config, (detail.wizard_history as WizardMessage[] | null) ?? null);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Geri alma başarısız');
    }
  }, [tenantId, flowId, loadFlow]);

  // Cleanup simulation and AI chat on unmount
  useEffect(() => {
    return () => {
      useSimulationStore.getState().close();
      useAiChatStore.getState().close();
      useFlowLogStore.getState().close();
    };
  }, []);

  // Keyboard shortcuts
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        handleSave();
      }
      if (e.ctrlKey && e.key === 'z') {
        e.preventDefault();
        useFlowStore.getState().undo();
      }
      if (e.ctrlKey && e.key === 'y') {
        e.preventDefault();
        useFlowStore.getState().redo();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [handleSave]);

  // Auto-save disabled — Q: "sürekli yeni sürüm yapıyor"
  // Save only via Ctrl+S or toolbar button

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-navy-50 text-navy-400">
        Flow yükleniyor...
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="h-screen flex flex-col items-center justify-center bg-navy-50 gap-4">
        <p className="text-red-600">{loadError}</p>
        <button
          onClick={() => navigate('/flow-builder')}
          className="px-4 py-2 bg-brand-500 hover:bg-brand-600 text-white text-sm rounded-lg"
        >
          Flow Listesine Dön
        </button>
      </div>
    );
  }

  return (
    <ReactFlowProvider>
      <div className="h-screen flex flex-col bg-navy-50">
        {/* Toolbar */}
        <Toolbar onSave={handleSave} isSaving={isSaving} onBack={handleBack} onTest={handleTest} paletteOpen={paletteOpen} onTogglePalette={handleTogglePalette} aiChatOpen={aiChatOpen} onToggleAiChat={handleToggleAiChat} flowLogOpen={flowLogOpen} onToggleFlowLog={handleToggleFlowLog} isActive={isActive} isToggling={isToggling} onToggleActive={handleToggleActive} currentVersion={currentVersion} tenantId={tenantId} flowId={flowId} onRollback={handleRollback} />

        {/* Save error banner */}
        {saveError && (
          <div className="bg-red-50 border-b border-red-200 px-4 py-2 text-xs text-red-600 flex items-center justify-between">
            <span>Kaydetme hatası: {saveError}</span>
            <button onClick={() => setSaveError(null)} className="text-red-400 hover:text-red-600">&times;</button>
          </div>
        )}

        {/* Main area */}
        <div className="flex-1 flex overflow-hidden">
          {/* Left: Node palette */}
          <NodePalette open={paletteOpen} />

          {/* Left: AI Chat panel (next to palette, mutual exclusion with flow log) */}
          <AiChatPanel onApply={handleAiApply} />

          {/* Left: Flow Log panel (next to palette, mutual exclusion with AI chat) */}
          <FlowLogPanel tenantId={tenantId} flowId={flowId} />

          {/* Center: Canvas */}
          <div className="flex-1 flex flex-col min-w-0">
            <FlowCanvas />
          </div>

          {/* Right: Node properties */}
          <NodePropertyPanel />

          {/* Simulation panel (flex shrink — canvas narrows when open) */}
          <SimulationPanel />
        </div>

        {/* Unsaved changes exit dialog */}
        {showExitDialog && (
          <div
            className="fixed inset-0 bg-navy-900/40 backdrop-blur-sm flex items-center justify-center z-50"
            onMouseDown={e => { if (e.target === e.currentTarget) setShowExitDialog(false); }}
          >
            <div className="bg-white border border-navy-100 rounded-2xl w-full max-w-sm p-6 shadow-elevated relative">
              <button
                onClick={() => setShowExitDialog(false)}
                className="absolute top-4 right-4 p-1 rounded hover:bg-navy-100 text-navy-300 hover:text-navy-600 transition-colors"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
                  <line x1="18" y1="6" x2="6" y2="18" />
                  <line x1="6" y1="6" x2="18" y2="18" />
                </svg>
              </button>
              <h2 className="text-lg font-semibold text-navy-900 mb-2">Kaydedilmemiş Değişiklikler</h2>
              <p className="text-sm text-navy-400 mb-5">
                Yaptığınız değişiklikler henüz kaydedilmedi. Ne yapmak istersiniz?
              </p>
              <div className="flex justify-end gap-2">
                <button
                  onClick={handleExitDiscard}
                  className="px-4 py-2 text-sm bg-navy-100 hover:bg-navy-200 text-navy-700 font-medium rounded-lg transition-colors"
                >
                  Kaydetme
                </button>
                <button
                  onClick={handleExitSave}
                  className="px-4 py-2 text-sm bg-brand-500 hover:bg-brand-600 text-white font-medium rounded-lg transition-colors"
                >
                  Kaydet ve Çık
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </ReactFlowProvider>
  );
}
