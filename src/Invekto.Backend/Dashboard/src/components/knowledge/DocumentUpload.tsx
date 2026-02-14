import { useState, useCallback } from 'react';
import { Upload, FileUp, X } from 'lucide-react';
import { api } from '../../lib/api';

interface Props {
  tenantId: number;
  onUploadComplete: () => void;
}

export function DocumentUpload({ tenantId, onUploadComplete }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dragOver, setDragOver] = useState(false);

  const MAX_SIZE_MB = 10;

  const validateFile = (f: File): string | null => {
    if (!f.name.toLowerCase().endsWith('.pdf')) return 'Only PDF files are supported';
    if (f.size > MAX_SIZE_MB * 1024 * 1024) return `File exceeds ${MAX_SIZE_MB}MB limit`;
    return null;
  };

  const handleFile = (f: File) => {
    const err = validateFile(f);
    if (err) { setError(err); return; }
    setFile(f);
    setError(null);
    if (!title) setTitle(f.name.replace(/\.pdf$/i, ''));
  };

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const f = e.dataTransfer.files[0];
    if (f) handleFile(f);
  }, [title]);

  const handleUpload = async () => {
    if (!file) return;
    setUploading(true);
    setError(null);
    try {
      await api.uploadDocument(tenantId, file, title || undefined);
      setFile(null);
      setTitle('');
      onUploadComplete();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upload failed');
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="bg-white rounded-lg border border-slate-200 p-4">
      <h3 className="text-sm font-medium text-slate-700 mb-3">Upload Document</h3>

      {/* Drop zone */}
      <div
        onDragOver={e => { e.preventDefault(); setDragOver(true); }}
        onDragLeave={() => setDragOver(false)}
        onDrop={handleDrop}
        className={`border-2 border-dashed rounded-lg p-6 text-center transition-colors ${
          dragOver ? 'border-blue-400 bg-blue-50' : 'border-slate-300 hover:border-slate-400'
        }`}
      >
        {file ? (
          <div className="flex items-center justify-center gap-3">
            <FileUp className="w-5 h-5 text-blue-600" />
            <span className="text-sm text-slate-700">{file.name}</span>
            <span className="text-xs text-slate-400">({(file.size / 1024 / 1024).toFixed(1)} MB)</span>
            <button onClick={() => { setFile(null); setTitle(''); }} className="text-slate-400 hover:text-slate-600">
              <X className="w-4 h-4" />
            </button>
          </div>
        ) : (
          <div>
            <Upload className="w-8 h-8 text-slate-400 mx-auto mb-2" />
            <p className="text-sm text-slate-500">Drag & drop a PDF here, or click to browse</p>
            <p className="text-xs text-slate-400 mt-1">Max {MAX_SIZE_MB}MB, PDF only</p>
            <input
              type="file"
              accept=".pdf"
              className="absolute inset-0 opacity-0 cursor-pointer"
              style={{ position: 'relative', marginTop: '8px' }}
              onChange={e => { const f = e.target.files?.[0]; if (f) handleFile(f); }}
            />
          </div>
        )}
      </div>

      {/* Title + Upload button */}
      {file && (
        <div className="flex items-center gap-3 mt-3">
          <input
            type="text"
            value={title}
            onChange={e => setTitle(e.target.value)}
            placeholder="Document title"
            className="flex-1 px-3 py-1.5 text-sm border border-slate-300 rounded-md"
          />
          <button
            onClick={handleUpload}
            disabled={uploading}
            className="px-4 py-1.5 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            {uploading ? 'Uploading...' : 'Upload'}
          </button>
        </div>
      )}

      {error && <p className="text-sm text-red-600 mt-2">{error}</p>}
    </div>
  );
}
