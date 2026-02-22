import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ReactFlowProvider } from '@xyflow/react';
import { FlowCanvas } from '../components/FlowCanvas';
import { FlowPreviewPanel } from '../components/FlowSummaryBar';
import { NodePalette } from '../components/NodePalette';
import { Toolbar } from '../components/Toolbar';
import { SimulationPanel } from '../components/SimulationPanel';
import { AiChatPanel } from '../components/AiChatPanel';
import { NodePropertyPanel } from '../panels/NodePropertyPanel';
import { useFlowStore } from '../store/flow-store';
import { useSimulationStore } from '../store/simulation-store';
import { useAiChatStore } from '../store/ai-chat-store';
import { useAuth } from '../lib/auth';
import { getFlow, updateFlow, ApiClientError } from '../lib/api';
import type { FlowConfigV2 } from '../types/flow';

export function FlowEditorPage() {
  const { flowId: flowIdParam } = useParams<{ flowId: string }>();
  const navigate = useNavigate();
  const { session } = useAuth();
  const tenantId = session?.tenant_id ?? 0;
  const flowId = parseInt(flowIdParam ?? '0', 10);

  const [isSaving, setIsSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [previewOpen, setPreviewOpen] = useState(() => {
    try { return localStorage.getItem('invekto_flow_preview_open') !== 'false'; }
    catch { return true; }
  });
  const loadFlow = useFlowStore((s) => s.loadFlow);

  // Load flow from API on mount
  useEffect(() => {
    if (!flowId || !tenantId) {
      navigate('/', { replace: true });
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setLoadError(null);

    getFlow(tenantId, flowId)
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
        loadFlow(config, detail.wizard_history ?? null);
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
      await updateFlow(tenantId, flowId, {
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
      if (!window.confirm('Kaydedilmemis degisiklikler var. Yine de cikis yapilsin mi?')) {
        return;
      }
    }
    // Close simulation if open
    useSimulationStore.getState().close();
    navigate('/');
  }, [navigate]);

  // AI Chat toggle — mutual exclusion with simulation
  const aiChatOpen = useAiChatStore((s) => s.isOpen);

  const handleToggleAiChat = useCallback(async () => {
    const aiChat = useAiChatStore.getState();
    if (aiChat.isOpen) {
      aiChat.close();
      return;
    }
    // Close simulation when opening AI chat
    useSimulationStore.getState().close();
    await aiChat.open(flowId, tenantId);
  }, [flowId, tenantId]);

  const handleAiApply = useCallback((config: FlowConfigV2) => {
    const wizardHistory = useFlowStore.getState().wizardHistory;
    loadFlow(config, wizardHistory);
  }, [loadFlow]);

  // AHA #4: Tek Tikla Test — save first if dirty, then start simulation
  const handleTest = useCallback(async () => {
    const store = useFlowStore.getState();
    const sim = useSimulationStore.getState();

    // If simulation is already open, just toggle it
    if (sim.isOpen) {
      sim.close();
      return;
    }

    // Close AI chat when opening simulation
    useAiChatStore.getState().close();

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
      <div className="h-screen flex items-center justify-center bg-slate-50 text-slate-500">
        Flow yukleniyor...
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="h-screen flex flex-col items-center justify-center bg-slate-50 gap-4">
        <p className="text-red-600">{loadError}</p>
        <button
          onClick={() => navigate('/')}
          className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm rounded-lg"
        >
          Flow Listesine Don
        </button>
      </div>
    );
  }

  return (
    <ReactFlowProvider>
      <div className="h-screen flex flex-col bg-slate-50">
        {/* Toolbar */}
        <Toolbar onSave={handleSave} isSaving={isSaving} onBack={handleBack} onTest={handleTest} previewOpen={previewOpen} onTogglePreview={handleTogglePreview} aiChatOpen={aiChatOpen} onToggleAiChat={handleToggleAiChat} />

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

          {/* AI Chat panel (mutual exclusive with simulation) */}
          <AiChatPanel onApply={handleAiApply} />
        </div>
      </div>
    </ReactFlowProvider>
  );
}
