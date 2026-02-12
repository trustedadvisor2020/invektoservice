import { useCallback, useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ReactFlowProvider } from '@xyflow/react';
import { FlowCanvas } from '../components/FlowCanvas';
import { NodePalette } from '../components/NodePalette';
import { Toolbar } from '../components/Toolbar';
import { NodePropertyPanel } from '../panels/NodePropertyPanel';
import { FlowSettingsPanel } from '../panels/FlowSettingsPanel';
import { useFlowStore } from '../store/flow-store';
import { useAuth } from '../lib/auth';
import { getFlow, updateFlow, ApiClientError } from '../lib/api';
import type { FlowConfigV2 } from '../types/flow';

type RightPanel = 'properties' | 'settings';

export function FlowEditorPage() {
  const { flowId: flowIdParam } = useParams<{ flowId: string }>();
  const navigate = useNavigate();
  const { session } = useAuth();
  const tenantId = session?.tenant_id ?? 0;
  const flowId = parseInt(flowIdParam ?? '0', 10);

  const [rightPanel, setRightPanel] = useState<RightPanel>('properties');
  const [isSaving, setIsSaving] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
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
        loadFlow(config);
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

  // Auto-switch to properties panel when node is selected
  useEffect(() => {
    if (selectedNodeId) {
      setRightPanel('properties');
    }
  }, [selectedNodeId]);

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
    navigate('/');
  }, [navigate]);

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
        <Toolbar onSave={handleSave} isSaving={isSaving} onBack={handleBack} />

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
          <FlowCanvas />

          {/* Right: Panel switcher + panel */}
          <div className="flex flex-col">
            {/* Panel tabs */}
            <div className="flex border-b border-slate-200 bg-white">
              <button
                onClick={() => setRightPanel('properties')}
                className={`px-3 py-2 text-xs font-medium transition-colors ${
                  rightPanel === 'properties'
                    ? 'text-blue-600 border-b-2 border-blue-600'
                    : 'text-slate-400 hover:text-slate-700'
                }`}
              >
                Ozellikler
              </button>
              <button
                onClick={() => setRightPanel('settings')}
                className={`px-3 py-2 text-xs font-medium transition-colors ${
                  rightPanel === 'settings'
                    ? 'text-blue-600 border-b-2 border-blue-600'
                    : 'text-slate-400 hover:text-slate-700'
                }`}
              >
                Ayarlar
              </button>
            </div>

            {/* Panel content */}
            {rightPanel === 'properties' ? (
              <NodePropertyPanel />
            ) : (
              <FlowSettingsPanel />
            )}
          </div>
        </div>
      </div>
    </ReactFlowProvider>
  );
}
