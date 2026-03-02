import { useState, useCallback } from 'react';
import { Upload, FileUp, Globe, X } from 'lucide-react';
import { api } from '../../lib/api';

interface Props {
  tenantId: number;
  open: boolean;
  onClose: () => void;
  onUploadComplete: () => void;
}

type Mode = 'pdf' | 'website';

export function DocumentUpload({ tenantId, open, onClose, onUploadComplete }: Props) {
  const [mode, setMode] = useState<Mode>('pdf');
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState('');
  const [url, setUrl] = useState('');
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dragOver, setDragOver] = useState(false);

  const MAX_SIZE_MB = 10;

  const reset = () => {
    setFile(null);
    setTitle('');
    setUrl('');
    setError(null);
    setDragOver(false);
  };

  const handleClose = () => {
    if (uploading) return;
    reset();
    onClose();
  };

  const validateFile = (f: File): string | null => {
    if (!f.name.toLowerCase().endsWith('.pdf')) return 'Sadece PDF dosyalari desteklenir';
    if (f.size > MAX_SIZE_MB * 1024 * 1024) return `Dosya ${MAX_SIZE_MB}MB limitini asiyor`;
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

  const validateUrl = (u: string): string | null => {
    if (!u.trim()) return 'URL gerekli';
    try {
      const parsed = new URL(u.trim());
      if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') return 'Sadece http/https desteklenir';
      return null;
    } catch {
      return 'Gecerli bir URL girin (ornek: https://example.com)';
    }
  };

  const handleSubmit = async () => {
    setUploading(true);
    setError(null);
    try {
      if (mode === 'pdf') {
        if (!file) return;
        await api.uploadDocument(tenantId, file, title || undefined);
      } else {
        const urlErr = validateUrl(url);
        if (urlErr) { setError(urlErr); setUploading(false); return; }
        await api.indexWebsite(tenantId, url.trim(), title || undefined);
      }
      reset();
      onUploadComplete();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Islem basarisiz');
    } finally {
      setUploading(false);
    }
  };

  const canSubmit = mode === 'pdf' ? !!file : !!url.trim();

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={handleClose}>
      <div className="bg-white rounded-xl shadow-xl w-full max-w-lg mx-4" onClick={e => e.stopPropagation()}>
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-navy-100">
          <h3 className="text-base font-semibold text-navy-800">Dokuman Ekle</h3>
          <button onClick={handleClose} className="text-navy-400 hover:text-navy-600 transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Mode tabs */}
        <div className="flex border-b border-navy-100">
          <button
            onClick={() => { setMode('pdf'); setError(null); }}
            className={`flex-1 flex items-center justify-center gap-2 py-3 text-sm font-medium border-b-2 transition-colors ${
              mode === 'pdf' ? 'border-brand-500 text-brand-600' : 'border-transparent text-navy-400 hover:text-navy-600'
            }`}
          >
            <FileUp className="w-4 h-4" />
            PDF Yukle
          </button>
          <button
            onClick={() => { setMode('website'); setError(null); }}
            className={`flex-1 flex items-center justify-center gap-2 py-3 text-sm font-medium border-b-2 transition-colors ${
              mode === 'website' ? 'border-brand-500 text-brand-600' : 'border-transparent text-navy-400 hover:text-navy-600'
            }`}
          >
            <Globe className="w-4 h-4" />
            Web Sitesi
          </button>
        </div>

        {/* Body */}
        <div className="p-5 space-y-4">
          {mode === 'pdf' ? (
            <>
              {/* Drop zone */}
              <div
                onDragOver={e => { e.preventDefault(); setDragOver(true); }}
                onDragLeave={() => setDragOver(false)}
                onDrop={handleDrop}
                className={`border-2 border-dashed rounded-lg p-6 text-center transition-colors ${
                  dragOver ? 'border-blue-400 bg-blue-50' : 'border-navy-200 hover:border-navy-300'
                }`}
              >
                {file ? (
                  <div className="flex items-center justify-center gap-3">
                    <FileUp className="w-5 h-5 text-blue-600" />
                    <span className="text-sm text-navy-700">{file.name}</span>
                    <span className="text-xs text-navy-300">({(file.size / 1024 / 1024).toFixed(1)} MB)</span>
                    <button onClick={() => { setFile(null); setTitle(''); }} className="text-navy-300 hover:text-navy-600">
                      <X className="w-4 h-4" />
                    </button>
                  </div>
                ) : (
                  <label className="cursor-pointer">
                    <Upload className="w-8 h-8 text-navy-300 mx-auto mb-2" />
                    <p className="text-sm text-navy-400">PDF dosyasini surukleyip birakin veya tiklayip secin</p>
                    <p className="text-xs text-navy-300 mt-1">Maks {MAX_SIZE_MB}MB, sadece PDF</p>
                    <input
                      type="file"
                      accept=".pdf"
                      className="hidden"
                      onChange={e => { const f = e.target.files?.[0]; if (f) handleFile(f); }}
                    />
                  </label>
                )}
              </div>
            </>
          ) : (
            <>
              {/* URL input */}
              <div>
                <label className="block text-sm font-medium text-navy-600 mb-1.5">Web Sitesi URL</label>
                <input
                  type="url"
                  value={url}
                  onChange={e => setUrl(e.target.value)}
                  placeholder="https://example.com"
                  className="w-full px-3 py-2 text-sm border border-navy-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-400"
                />
                <p className="text-xs text-navy-300 mt-1.5">Sitemap uzerinden tum sayfalar taranir ve indexlenir</p>
              </div>
            </>
          )}

          {/* Title (shared) */}
          <div>
            <label className="block text-sm font-medium text-navy-600 mb-1.5">Baslik</label>
            <input
              type="text"
              value={title}
              onChange={e => setTitle(e.target.value)}
              placeholder={mode === 'pdf' ? 'Dokuman basligi' : 'Site basligi (opsiyonel)'}
              className="w-full px-3 py-2 text-sm border border-navy-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:border-brand-400"
            />
          </div>

          {error && <p className="text-sm text-red-600">{error}</p>}
        </div>

        {/* Footer */}
        <div className="flex justify-end gap-3 px-5 py-4 border-t border-navy-100">
          <button
            onClick={handleSubmit}
            disabled={uploading || !canSubmit}
            className="px-5 py-2 text-sm font-medium bg-brand-500 text-white rounded-lg hover:bg-brand-600 disabled:opacity-50 transition-colors"
          >
            {uploading
              ? (mode === 'pdf' ? 'Yukleniyor...' : 'Taraniyor...')
              : (mode === 'pdf' ? 'Yukle' : 'Siteyi Tara')}
          </button>
        </div>
      </div>
    </div>
  );
}
