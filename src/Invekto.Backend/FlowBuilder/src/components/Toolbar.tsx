import { useFlowStore } from '../store/flow-store';
import { cn } from '../lib/utils';

interface ToolbarProps {
  onSave?: () => void;
  isSaving?: boolean;
  onBack?: () => void;
}

export function Toolbar({ onSave, isSaving, onBack }: ToolbarProps) {
  const isDirty = useFlowStore((s) => s.isDirty);
  const flowMetadata = useFlowStore((s) => s.flowMetadata);
  const setMetadata = useFlowStore((s) => s.setMetadata);
  const undo = useFlowStore((s) => s.undo);
  const redo = useFlowStore((s) => s.redo);
  const historyIndex = useFlowStore((s) => s.historyIndex);
  const historyLength = useFlowStore((s) => s.history.length);

  const canUndo = historyIndex >= 0;
  const canRedo = historyIndex < historyLength - 1;

  return (
    <div className="h-12 bg-slate-900/95 border-b border-slate-700/50 flex items-center px-4 gap-3 flex-shrink-0">
      {/* Back button */}
      {onBack && (
        <button
          onClick={onBack}
          className="p-1.5 rounded hover:bg-slate-700/50 transition-colors text-slate-300"
          title="Flow Listesine Don"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
            <polyline points="15 18 9 12 15 6" />
          </svg>
        </button>
      )}

      {/* Flow name + description */}
      <div className="flex items-center gap-2 min-w-0">
        <input
          type="text"
          value={flowMetadata.name}
          onChange={(e) => setMetadata({ name: e.target.value })}
          className="bg-transparent text-sm font-medium text-slate-200 border-none outline-none w-40 focus:ring-1 focus:ring-blue-500/50 rounded px-2 py-1"
          placeholder="Flow Adi"
        />
        <span className="text-slate-600">|</span>
        <input
          type="text"
          value={flowMetadata.description ?? ''}
          onChange={(e) => setMetadata({ description: e.target.value })}
          className="bg-transparent text-xs text-slate-400 border-none outline-none w-56 focus:ring-1 focus:ring-blue-500/50 rounded px-2 py-1"
          placeholder="Flow aciklamasi..."
        />
      </div>

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
            'p-1.5 rounded hover:bg-slate-700/50 transition-colors',
            canUndo ? 'text-slate-300' : 'text-slate-600 cursor-not-allowed'
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
            'p-1.5 rounded hover:bg-slate-700/50 transition-colors',
            canRedo ? 'text-slate-300' : 'text-slate-600 cursor-not-allowed'
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
      <div className="w-px h-6 bg-slate-700" />

      {/* Save */}
      <button
        onClick={onSave}
        disabled={isSaving || !isDirty}
        className={cn(
          'flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-colors',
          isDirty && !isSaving
            ? 'bg-blue-600 hover:bg-blue-500 text-white'
            : 'bg-slate-700 text-slate-500 cursor-not-allowed'
        )}
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
          <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
          <polyline points="17 21 17 13 7 13 7 21" />
          <polyline points="7 3 7 8 15 8" />
        </svg>
        {isSaving ? 'Kaydediliyor...' : 'Kaydet'}
      </button>
    </div>
  );
}
