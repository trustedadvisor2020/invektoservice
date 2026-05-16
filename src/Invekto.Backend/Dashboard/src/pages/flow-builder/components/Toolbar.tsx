import { useCallback, useEffect, useRef, useState } from 'react';
import { useFlowStore } from '../../../stores/flow-store';
import { useSimulationStore } from '../../../stores/simulation-store';
import { FlowSettingsModal } from '../panels/FlowSettingsPanel';
import { cn } from '../../../lib/utils';
import { api } from '../../../lib/api';
import type { FlowVersionSummary } from '../../../lib/api';

interface ToolbarProps {
  onSave?: () => void;
  isSaving?: boolean;
  onBack?: () => void;
  onTest?: () => void;
  paletteOpen?: boolean;
  onTogglePalette?: () => void;
  aiChatOpen?: boolean;
  onToggleAiChat?: () => void;
  flowLogOpen?: boolean;
  onToggleFlowLog?: () => void;
  isActive?: boolean;
  isToggling?: boolean;
  onToggleActive?: () => void;
  currentVersion?: number;
  tenantId?: number;
  flowId?: number;
  onRollback?: (versionNumber: number) => void;
}

export function Toolbar({ onSave, isSaving, onBack, onTest, paletteOpen, onTogglePalette, aiChatOpen, onToggleAiChat, flowLogOpen, onToggleFlowLog, isActive, isToggling, onToggleActive, currentVersion, tenantId, flowId, onRollback }: ToolbarProps) {
  const isDirty = useFlowStore((s) => s.isDirty);
  const simIsOpen = useSimulationStore((s) => s.isOpen);
  const simIsLoading = useSimulationStore((s) => s.isLoading);
  const flowMetadata = useFlowStore((s) => s.flowMetadata);
  const setMetadata = useFlowStore((s) => s.setMetadata);
  const undo = useFlowStore((s) => s.undo);
  const redo = useFlowStore((s) => s.redo);
  const historyIndex = useFlowStore((s) => s.historyIndex);
  const historyLength = useFlowStore((s) => s.history.length);

  const [settingsOpen, setSettingsOpen] = useState(false);
  const [versionDropdownOpen, setVersionDropdownOpen] = useState(false);
  const [versions, setVersions] = useState<FlowVersionSummary[]>([]);
  const [versionsLoading, setVersionsLoading] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const [versionsError, setVersionsError] = useState<string | null>(null);

  const loadVersions = useCallback(async () => {
    if (!tenantId || !flowId) return;
    setVersionsLoading(true);
    setVersionsError(null);
    try {
      const res = await api.getFlowVersions(tenantId, flowId);
      setVersions(res.versions);
    } catch (err) {
      setVersionsError(err instanceof Error ? err.message : 'Surum listesi yuklenemedi');
    }
    setVersionsLoading(false);
  }, [tenantId, flowId]);

  const handleToggleVersionDropdown = useCallback(() => {
    const next = !versionDropdownOpen;
    setVersionDropdownOpen(next);
    if (next) loadVersions();
  }, [versionDropdownOpen, loadVersions]);

  // Close dropdown on outside click
  useEffect(() => {
    if (!versionDropdownOpen) return;
    const handler = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node))
        setVersionDropdownOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [versionDropdownOpen]);

  const canUndo = historyIndex >= 0;
  const canRedo = historyIndex < historyLength - 1;

  return (
    <>
      <div className="h-12 bg-white border-b border-navy-100 flex items-center px-4 gap-3 flex-shrink-0 shadow-sm">
        {/* Back button */}
        {onBack && (
          <button
            onClick={onBack}
            className="p-1.5 rounded hover:bg-navy-50 transition-colors text-navy-500"
            title="Flow Listesine Don"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
              <polyline points="15 18 9 12 15 6" />
            </svg>
          </button>
        )}

        {/* Node Palette Toggle */}
        {onTogglePalette && (
          <button
            onClick={onTogglePalette}
            className={cn(
              'p-1.5 rounded transition-colors',
              paletteOpen
                ? 'bg-brand-50 text-brand-600 hover:bg-brand-100'
                : 'text-navy-500 hover:bg-navy-50'
            )}
            title={paletteOpen ? 'Node panelini gizle' : 'Node panelini goster'}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
              <rect x="3" y="3" width="18" height="18" rx="2" />
              <line x1="9" y1="3" x2="9" y2="21" />
            </svg>
          </button>
        )}

        {/* Flow name */}
        <input
          type="text"
          value={flowMetadata.name}
          onChange={(e) => setMetadata({ name: e.target.value })}
          className="bg-transparent text-sm font-medium text-navy-900 border-none outline-none w-40 focus:ring-1 focus:ring-brand-500/30 rounded px-2 py-1 min-w-0"
          placeholder="Flow Adi"
        />

        {/* Dirty indicator */}
        {isDirty && (
          <span className="w-2 h-2 rounded-full bg-amber-400 flex-shrink-0" title="Kaydedilmemis degisiklikler" />
        )}

        {/* Activate / Deactivate toggle */}
        {onToggleActive && (
          <button
            onClick={onToggleActive}
            disabled={isToggling}
            className={cn(
              'flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium transition-colors',
              isToggling
                ? 'bg-navy-50 text-navy-300 cursor-wait'
                : isActive
                  ? 'bg-emerald-50 text-emerald-700 hover:bg-emerald-100 border border-emerald-200'
                  : 'bg-navy-50 text-navy-400 hover:bg-navy-100 border border-navy-200'
            )}
            title={isActive ? 'Flow\'u durdur' : 'Flow\'u baslat'}
          >
            {isActive ? (
              <svg viewBox="0 0 24 24" fill="currentColor" className="w-3.5 h-3.5">
                <rect x="6" y="4" width="4" height="16" rx="1" />
                <rect x="14" y="4" width="4" height="16" rx="1" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" fill="currentColor" className="w-3.5 h-3.5">
                <polygon points="5 3 19 12 5 21 5 3" />
              </svg>
            )}
            {isActive ? 'Aktif' : 'Pasif'}
          </button>
        )}

        {/* Version badge */}
        {currentVersion != null && currentVersion > 0 && (
          <div className="relative" ref={dropdownRef}>
            <button
              onClick={handleToggleVersionDropdown}
              className={cn(
                'flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium transition-colors border',
                versionDropdownOpen
                  ? 'bg-indigo-50 text-indigo-700 border-indigo-200'
                  : 'bg-navy-50 text-navy-500 hover:bg-navy-100 border-navy-200'
              )}
              title="Surum gecmisi"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
                <circle cx="12" cy="12" r="10" />
                <polyline points="12 6 12 12 16 14" />
              </svg>
              v{currentVersion}
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3 h-3">
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </button>
            {versionDropdownOpen && (
              <div className="absolute top-full left-0 mt-1 w-64 bg-white border border-navy-200 rounded-lg shadow-elevated z-50 max-h-60 overflow-y-auto">
                {versionsLoading ? (
                  <div className="px-3 py-2 text-xs text-navy-400">Yukleniyor...</div>
                ) : versionsError ? (
                  <div className="px-3 py-2 text-xs text-red-500">{versionsError}</div>
                ) : versions.length === 0 ? (
                  <div className="px-3 py-2 text-xs text-navy-400">Surum bulunamadi</div>
                ) : (
                  versions.map((v) => (
                    <div
                      key={v.id}
                      className={cn(
                        'px-3 py-2 flex items-center justify-between text-xs border-b border-navy-50 last:border-0',
                        v.versionNumber === currentVersion ? 'bg-indigo-50' : 'hover:bg-navy-50'
                      )}
                    >
                      <div>
                        <span className="font-medium text-navy-900">v{v.versionNumber}</span>
                        <span className="text-navy-400 ml-1.5">
                          {new Date(v.createdAt).toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' })}
                        </span>
                        {v.createdBy && (
                          <span className={cn(
                            'ml-1.5 px-1 py-0.5 rounded text-[10px]',
                            v.createdBy === 'rollback' ? 'bg-amber-100 text-amber-700'
                              : v.createdBy === 'ai' ? 'bg-purple-100 text-purple-700'
                              : 'bg-navy-100 text-navy-500'
                          )}>
                            {v.createdBy}
                          </span>
                        )}
                      </div>
                      {v.versionNumber !== currentVersion && onRollback && (
                        <button
                          onClick={() => { onRollback(v.versionNumber); setVersionDropdownOpen(false); }}
                          className="text-indigo-600 hover:text-indigo-800 font-medium"
                        >
                          Geri Al
                        </button>
                      )}
                    </div>
                  ))
                )}
              </div>
            )}
          </div>
        )}

        <div className="flex-1" />

        {/* Undo/Redo */}
        <div className="flex items-center gap-1">
          <button
            onClick={undo}
            disabled={!canUndo}
            className={cn(
              'p-1.5 rounded hover:bg-navy-50 transition-colors',
              canUndo ? 'text-navy-500' : 'text-navy-200 cursor-not-allowed'
            )}
            title="Geri Al (Ctrl+Z)"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
              <polyline points="1 4 1 10 7 10" />
              <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
            </svg>
          </button>
          <button
            onClick={redo}
            disabled={!canRedo}
            className={cn(
              'p-1.5 rounded hover:bg-navy-50 transition-colors',
              canRedo ? 'text-navy-500' : 'text-navy-200 cursor-not-allowed'
            )}
            title="Ileri Al (Ctrl+Y)"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
              <polyline points="23 4 23 10 17 10" />
              <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10" />
            </svg>
          </button>
        </div>

        {/* Divider */}
        <div className="w-px h-6 bg-navy-100" />

        {/* AI ile Gelistir */}
        {onToggleAiChat && (
          <button
            onClick={onToggleAiChat}
            className={cn(
              'p-2 rounded-md transition-colors',
              aiChatOpen
                ? 'bg-purple-500 text-white'
                : 'bg-navy-50 hover:bg-navy-100 text-navy-500'
            )}
            title="AI"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 00-2.455 2.456z" />
            </svg>
          </button>
        )}

        {/* Akis Loglari */}
        {onToggleFlowLog && (
          <button
            onClick={onToggleFlowLog}
            className={cn(
              'p-2 rounded-md transition-colors',
              flowLogOpen
                ? 'bg-sky-500 text-white'
                : 'bg-navy-50 hover:bg-navy-100 text-navy-500'
            )}
            title="Akis Loglari"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
              <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
              <rect x="8" y="2" width="8" height="4" rx="1" ry="1" />
              <line x1="9" y1="12" x2="15" y2="12" />
              <line x1="9" y1="16" x2="15" y2="16" />
            </svg>
          </button>
        )}

        {/* Settings button */}
        <button
          onClick={() => setSettingsOpen(true)}
          className={cn(
            'p-2 rounded-md transition-colors',
            settingsOpen
              ? 'bg-navy-100 text-navy-700'
              : 'bg-navy-50 hover:bg-navy-100 text-navy-500'
          )}
          title="Ayarlar"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
            <circle cx="12" cy="12" r="3" />
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
          </svg>
        </button>

        {/* Test Et (AHA #4) */}
        <button
          onClick={onTest}
          disabled={simIsLoading || isSaving}
          className={cn(
            'p-2 rounded-md transition-colors',
            simIsOpen
              ? 'bg-emerald-100 text-emerald-700 border border-emerald-300'
              : simIsLoading || isSaving
                ? 'bg-navy-50 text-navy-300 cursor-not-allowed'
                : 'bg-emerald-600 hover:bg-emerald-500 text-white'
          )}
          title={isDirty ? 'Once flow\'u kaydedin' : 'Test Et'}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
            <polygon points="5 3 19 12 5 21 5 3" />
          </svg>
        </button>

        {/* Save */}
        <button
          onClick={onSave}
          disabled={isSaving || !isDirty}
          className={cn(
            'p-2 rounded-md transition-colors',
            isDirty && !isSaving
              ? 'bg-brand-500 hover:bg-brand-600 text-white'
              : 'bg-navy-50 text-navy-300 cursor-not-allowed'
          )}
          title={isSaving ? 'Kaydediliyor...' : 'Kaydet'}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
            <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
            <polyline points="17 21 17 13 7 13 7 21" />
            <polyline points="7 3 7 8 15 8" />
          </svg>
        </button>
      </div>

      {/* Settings Modal */}
      <FlowSettingsModal open={settingsOpen} onClose={() => setSettingsOpen(false)} />
    </>
  );
}
