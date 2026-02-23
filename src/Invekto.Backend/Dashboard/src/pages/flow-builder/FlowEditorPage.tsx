import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ReactFlowProvider } from '@xyflow/react';
import { FlowCanvas } from './components/FlowCanvas';
import { FlowPreviewPanel } from './components/FlowSummaryBar';
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
  const [previewOpen, setPreviewOpen] = useState(() => {
    try { return localStorage.getItem('invekto_flow_preview_open') !== 'false'; }
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
        setIsLoading(false);
      })
      .catch((err) => {
        if (cancelled) return;
        if (err instanceof ApiClientError && err.status === 404) {
          setLoadError('Flow bulunamadi.');
        } else {
          setLoadError(err instanceof Error ? err.message : 'Flow yuklenemedi');
        }
        setIsLoading(false);
      });

    return () => { cancelled = true; };
  }, [flowId, tenantId, loadFlow, navigate]);

  const handleTogglePreview = useCallback(() => {
    setPreviewOpen((v) => {
      const next = !v;
      try { localStorage.setItem('invekto_flow_preview_open', String(next)); } catch {}
      return next;
    });
  }, []);

  const handleSave = useCallback(async () => {
    setIsSaving(true);
    setSaveError(null);
    try {
      const config = useFlowStore.getState().toFlowConfig();
      await api.updateFlow(tenantId, flowId, {
        flow_name: config.metadata.name,
        flow_config: config,
      });
      useFlowStore.getState().markClean();
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Kaydetme basarisiz');
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

  const handleToggleAiChat = useCallback(async () => {
    const aiChat = useAiChatStore.getState();
    if (aiChat.isOpen) {
      aiChat.close();
      return;
    }
    // Close simulation & flow log when opening AI chat
    useSimulationStore.getState().close();
    useFlowLogStore.getState().close();
    await aiChat.open(flowId, tenantId);
  }, [flowId, tenantId]);

  const handleAiApply = useCallback((config: FlowConfigV2) => {
    const wizardHistory = useFlowStore.getState().wizardHistory;
    loadFlow(config, wizardHistory);
  }, [loadFlow]);

  // Flow log toggle — mutual exclusion with AI chat & simulation
  const flowLogOpen = useFlowLogStore((s) => s.isOpen);

  const handleToggleFlowLog = useCallback(() => {
    const logStore = useFlowLogStore.getState();
    if (logStore.isOpen) {
      logStore.close();
      return;
    }
    // Close AI chat and simulation when opening flow log
    useAiChatStore.getState().close();
    useSimulationStore.getState().close();
    logStore.open();
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

    // Close AI chat & flow log when opening simulation
    useAiChatStore.getState().close();
    useFlowLogStore.getState().close();

    // If dirty, warn user to save first
    if (store.isDirty) {
      setSaveError('Once flow\'u kaydedin, sonra test edin.');
      return;
    }

    // Start simulation
    await sim.start(tenantId, flowId);
  }, [tenantId, flowId]);

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

  // Auto-save: debounce 1.5s after any change
  const autoSaveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(() => {
    const unsub = useFlowStore.subscribe((state, prev) => {
      if (state.isDirty && !prev.isDirty) {
        // Fresh dirty → schedule save
        if (autoSaveTimer.current) clearTimeout(autoSaveTimer.current);
        autoSaveTimer.current = setTimeout(() => handleSave(), 1500);
      } else if (state.isDirty && prev.isDirty) {
        // Still dirty (ongoing edits) → reset timer
        if (autoSaveTimer.current) clearTimeout(autoSaveTimer.current);
        autoSaveTimer.current = setTimeout(() => handleSave(), 1500);
      }
    });
    return () => {
      unsub();
      if (autoSaveTimer.current) clearTimeout(autoSaveTimer.current);
    };
  }, [handleSave]);

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-navy-50 text-navy-400">
        Flow yukleniyor...
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
          Flow Listesine Don
        </button>
      </div>
    );
  }

  return (
    <ReactFlowProvider>
      <div className="h-screen flex flex-col bg-navy-50">
        {/* Toolbar */}
        <Toolbar onSave={handleSave} isSaving={isSaving} onBack={handleBack} onTest={handleTest} previewOpen={previewOpen} onTogglePreview={handleTogglePreview} aiChatOpen={aiChatOpen} onToggleAiChat={handleToggleAiChat} flowLogOpen={flowLogOpen} onToggleFlowLog={handleToggleFlowLog} />

        {/* Save error banner */}
        {saveError && (
          <div className="bg-red-50 border-b border-red-200 px-4 py-2 text-xs text-red-600 flex items-center justify-between">
            <span>Kaydetme hatasi: {saveError}</span>
            <button onClick={() => setSaveError(null)} className="text-red-400 hover:text-red-600">&times;</button>
          </div>
        )}

        {/* Main area */}
        <div className="flex-1 flex overflow-hidden">
          {/* Left: Node palette */}
          <NodePalette />

          {/* Left: AI Chat panel (next to palette, mutual exclusion with flow log) */}
          <AiChatPanel onApply={handleAiApply} />

          {/* Left: Flow Log panel (next to palette, mutual exclusion with AI chat) */}
          <FlowLogPanel tenantId={tenantId} flowId={flowId} />

          {/* Center: Canvas */}
          <div className="flex-1 flex flex-col min-w-0">
            <FlowCanvas />
          </div>

          {/* Right: Preview panel (toggleable) */}
          <FlowPreviewPanel open={previewOpen} />

          {/* Right: Node properties */}
          <NodePropertyPanel />

          {/* Simulation panel (flex shrink — canvas narrows when open) */}
          <SimulationPanel />
        </div>

        {/* Unsaved changes exit dialog */}
        {showExitDialog && (
          <div className="fixed inset-0 bg-navy-900/40 backdrop-blur-sm flex items-center justify-center z-50">
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
              <h2 className="text-lg font-semibold text-navy-900 mb-2">Kaydedilmemis Degisiklikler</h2>
              <p className="text-sm text-navy-400 mb-5">
                Yaptiginiz degisiklikler henuz kaydedilmedi. Ne yapmak istersiniz?
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
                  Kaydet ve Cik
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </ReactFlowProvider>
  );
}
