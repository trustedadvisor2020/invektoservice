import { useState } from 'react';
import { useFlowStore } from '../store/flow-store';
import { useSimulationStore } from '../store/simulation-store';
import { FlowSettingsModal } from '../panels/FlowSettingsPanel';
import { cn } from '../lib/utils';

interface ToolbarProps {
  onSave?: () => void;
  isSaving?: boolean;
  onBack?: () => void;
  onTest?: () => void;
  previewOpen?: boolean;
  onTogglePreview?: () => void;
  aiChatOpen?: boolean;
  onToggleAiChat?: () => void;
}

export function Toolbar({ onSave, isSaving, onBack, onTest, previewOpen, onTogglePreview, aiChatOpen, onToggleAiChat }: ToolbarProps) {
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

        {/* Preview Toggle */}
        {onTogglePreview && (
          <button
            onClick={onTogglePreview}
            className={cn(
              'p-2 rounded-md transition-colors',
              previewOpen
                ? 'bg-sky-500 text-white'
                : 'bg-navy-50 hover:bg-navy-100 text-navy-500'
            )}
            title="Onizleme"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
              <line x1="16" y1="13" x2="8" y2="13" />
              <line x1="16" y1="17" x2="8" y2="17" />
              <polyline points="10 9 9 9 8 9" />
            </svg>
          </button>
        )}

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
