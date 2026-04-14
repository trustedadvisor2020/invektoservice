import { useState, useEffect, useCallback } from 'react';
import { api, TemplateSuggestionItem, TemplateCompareResult } from '../lib/api';
import {
  Upload, CheckCircle, AlertCircle, RefreshCw, ChevronDown, ChevronUp,
} from 'lucide-react';

const TYPE_BADGES: Record<string, { color: string; label: string }> = {
  new: { color: 'bg-emerald-50 text-emerald-700 border-emerald-200', label: 'YENI' },
  update: { color: 'bg-amber-50 text-amber-700 border-amber-200', label: 'GUNCELLEME' },
  merge: { color: 'bg-slate-50 text-slate-600 border-slate-200', label: 'DOGRULAMA' },
};

export function TemplateIngestionPage() {
  // Step state
  const [step, setStep] = useState<'idle' | 'extracting' | 'results' | 'reviewing'>('idle');

  // Extraction inputs
  const [analysisId, setAnalysisId] = useState('');
  const [tenantName, setTenantName] = useState('');
  const [sector, setSector] = useState('eticaret');
  const [threshold] = useState('0.85');

  // Results
  const [compareResult, setCompareResult] = useState<TemplateCompareResult | null>(null);
  const [extractError, setExtractError] = useState('');

  // Suggestions view
  const [suggestions, setSuggestions] = useState<TemplateSuggestionItem[]>([]);
  const [suggestionsTotal, setSuggestionsTotal] = useState(0);
  const [suggestionsPage] = useState(1);
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [suggestionsError, setSuggestionsError] = useState('');

  // Fetch existing suggestions
  const fetchSuggestions = useCallback(async () => {
    setLoading(true);
    setSuggestionsError('');
    try {
      const result = await api.getTemplateSuggestions({
        status: 'pending',
        page: suggestionsPage,
        limit: 20,
      });
      setSuggestions(result.items);
      setSuggestionsTotal(result.total);
    } catch (err) {
      console.error('Failed to fetch suggestions:', err);
      setSuggestionsError('Oneriler yuklenirken hata olustu. Tekrar deneyin.');
    } finally {
      setLoading(false);
    }
  }, [suggestionsPage]);

  useEffect(() => { fetchSuggestions(); }, [fetchSuggestions]);

  // Extract from analysis
  const handleExtract = async () => {
    if (!analysisId || !tenantName) return;
    setStep('extracting');
    setExtractError('');
    try {
      const result = await api.extractFromAnalysis(parseInt(analysisId), {
        tenant_name: tenantName,
        sector: sector || undefined,
        auto_confirm_threshold: parseFloat(threshold),
      });
      setCompareResult(result);
      setStep('results');
      fetchSuggestions(); // Refresh suggestions list
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Extraction failed';
      setExtractError(msg);
      setStep('idle');
    }
  };

  // Review actions
  const handleReview = async (id: number, status: string) => {
    setSuggestionsError('');
    try {
      await api.reviewTemplateSuggestion(id, { status });
      fetchSuggestions();
    } catch (err) {
      console.error('Review failed:', err);
      setSuggestionsError('Oneri incelemesi basarisiz oldu. Tekrar deneyin.');
    }
  };

  const handleBulkReview = async (status: string, typeFilter?: string) => {
    const ids = suggestions
      .filter(s => s.status === 'pending')
      .filter(s => !typeFilter || s.suggestion_type === typeFilter)
      .map(s => s.id);
    if (ids.length === 0) return;

    setSuggestionsError('');
    try {
      await api.bulkReviewSuggestions({ ids, status });
      fetchSuggestions();
    } catch (err) {
      console.error('Bulk review failed:', err);
      setSuggestionsError('Toplu inceleme basarisiz oldu. Tekrar deneyin.');
    }
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div>
        <h1 className="text-lg font-semibold text-navy-900 flex items-center gap-2">
          <Upload className="w-5 h-5" />
          Veri Besleme & Sablon Cikarimi
        </h1>
        <p className="text-xs text-navy-400">WA analiz sonuclarindan sablon cikar, mevcut katalogla karsilastir</p>
      </div>

      {/* Step 1: Extract */}
      <div className="bg-white rounded-lg border border-navy-100 p-4">
        <h2 className="text-sm font-medium text-navy-800 mb-3">1. Analiz Sonucu Karsilastir</h2>
        <div className="grid grid-cols-4 gap-3">
          <div>
            <label className="text-[10px] text-navy-400 block mb-1">Analiz ID</label>
            <input
              type="number"
              value={analysisId}
              onChange={e => setAnalysisId(e.target.value)}
              placeholder="orn: 1"
              className="w-full text-xs border border-navy-200 rounded px-2 py-1.5"
            />
          </div>
          <div>
            <label className="text-[10px] text-navy-400 block mb-1">Firma Adi</label>
            <input
              type="text"
              value={tenantName}
              onChange={e => setTenantName(e.target.value)}
              placeholder="orn: ebrumoda"
              className="w-full text-xs border border-navy-200 rounded px-2 py-1.5"
            />
          </div>
          <div>
            <label className="text-[10px] text-navy-400 block mb-1">Sektor</label>
            <select
              value={sector}
              onChange={e => setSector(e.target.value)}
              className="w-full text-xs border border-navy-200 rounded px-2 py-1.5"
            >
              <option value="eticaret">E-Ticaret</option>
              <option value="dis_klinik">Dis Klinigi</option>
              <option value="estetik">Estetik</option>
              <option value="">Diger</option>
            </select>
          </div>
          <div className="flex items-end">
            <button
              onClick={handleExtract}
              disabled={step === 'extracting' || !analysisId || !tenantName}
              className="w-full flex items-center justify-center gap-1 px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700 disabled:opacity-50"
            >
              {step === 'extracting' ? <RefreshCw className="w-3.5 h-3.5 animate-spin" /> : <Upload className="w-3.5 h-3.5" />}
              {step === 'extracting' ? 'Cikariliyor...' : 'Cikar & Karsilastir'}
            </button>
          </div>
        </div>
        {extractError && (
          <div className="mt-2 flex items-center gap-1 text-xs text-red-600">
            <AlertCircle className="w-3.5 h-3.5" /> {extractError}
          </div>
        )}
      </div>

      {/* Step 2: Compare Results */}
      {compareResult && (
        <div className="bg-white rounded-lg border border-navy-100 p-4">
          <h2 className="text-sm font-medium text-navy-800 mb-3">2. Karsilastirma Sonucu</h2>
          <div className="grid grid-cols-4 gap-3 mb-3">
            <div className="bg-emerald-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-emerald-700">{compareResult.new_count}</div>
              <div className="text-[10px] text-emerald-600">Yeni Sablon</div>
            </div>
            <div className="bg-amber-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-amber-700">{compareResult.update_count}</div>
              <div className="text-[10px] text-amber-600">Guncelleme</div>
            </div>
            <div className="bg-slate-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-slate-700">{compareResult.confirm_count}</div>
              <div className="text-[10px] text-slate-600">Dogrulama</div>
            </div>
            <div className="bg-navy-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-navy-700">{compareResult.duration_ms}</div>
              <div className="text-[10px] text-navy-500">ms sure</div>
            </div>
          </div>
          <p className="text-xs text-navy-400">
            {compareResult.total_clusters_processed} FAQ cluster + {compareResult.total_intents_processed} intent islendi
          </p>
        </div>
      )}

      {/* Step 3: Suggestion Review Queue */}
      <div className="bg-white rounded-lg border border-navy-100 p-4">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-medium text-navy-800">
            3. Oneri Inceleme Kuyrugu
            <span className="text-navy-400 font-normal ml-1">({suggestionsTotal} bekleyen)</span>
          </h2>
          <div className="flex items-center gap-2">
            <button
              onClick={() => handleBulkReview('approved', 'new')}
              className="px-2 py-1 text-[10px] rounded border border-emerald-200 text-emerald-700 hover:bg-emerald-50"
            >
              Yenileri Onayla
            </button>
            <button
              onClick={() => handleBulkReview('approved')}
              className="px-2 py-1 text-[10px] rounded bg-emerald-600 text-white hover:bg-emerald-500"
            >
              Tumunu Onayla
            </button>
            <button onClick={fetchSuggestions} className="p-1 hover:bg-navy-50 rounded">
              <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
            </button>
          </div>
        </div>

        {suggestionsError && (
          <div className="flex items-center justify-between bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700 mb-2">
            <span>{suggestionsError}</span>
            <button onClick={() => setSuggestionsError('')} className="text-red-400 hover:text-red-600 ml-2">&times;</button>
          </div>
        )}

        <div className="space-y-2">
          {suggestions.map(s => {
            const badge = TYPE_BADGES[s.suggestion_type] || TYPE_BADGES.new;
            const isExpanded = expandedId === s.id;
            return (
              <div key={s.id} className="border border-navy-100 rounded-lg overflow-hidden">
                <div
                  className="flex items-center gap-3 px-3 py-2 cursor-pointer hover:bg-navy-25"
                  onClick={() => setExpandedId(isExpanded ? null : s.id)}
                >
                  <span className={`px-1.5 py-0.5 rounded text-[10px] font-medium border ${badge.color}`}>
                    {badge.label}
                  </span>
                  <span className="text-xs font-medium text-navy-800 flex-1">{s.suggested_name}</span>
                  <span className="text-[10px] text-navy-400">{s.suggested_type}</span>
                  {s.similarity_score != null && (
                    <span className="text-[10px] text-navy-400">{(s.similarity_score * 100).toFixed(0)}% benzer</span>
                  )}
                  <div className="flex items-center gap-1" onClick={e => e.stopPropagation()}>
                    <button
                      onClick={() => handleReview(s.id, 'approved')}
                      className="p-1 hover:bg-emerald-50 rounded"
                      title="Onayla"
                    >
                      <CheckCircle className="w-3.5 h-3.5 text-emerald-600" />
                    </button>
                    <button
                      onClick={() => handleReview(s.id, 'rejected')}
                      className="p-1 hover:bg-red-50 rounded"
                      title="Reddet"
                    >
                      <AlertCircle className="w-3.5 h-3.5 text-red-400" />
                    </button>
                  </div>
                  {isExpanded ? <ChevronUp className="w-3.5 h-3.5 text-navy-400" /> : <ChevronDown className="w-3.5 h-3.5 text-navy-400" />}
                </div>
                {isExpanded && (
                  <div className="px-3 py-2 bg-navy-25 border-t border-navy-100">
                    <div className="text-[10px] text-navy-400 mb-1">Slug: {s.suggested_slug}</div>
                    {s.existing_template_name && (
                      <div className="text-[10px] text-navy-400 mb-1">Mevcut: {s.existing_template_name} (#{s.existing_template_id})</div>
                    )}
                    <pre className="text-[10px] bg-white rounded p-2 mt-1 overflow-auto max-h-40 font-mono text-navy-600">
                      {JSON.stringify(s.suggested_content_json, null, 2)}
                    </pre>
                  </div>
                )}
              </div>
            );
          })}

          {suggestions.length === 0 && !loading && (
            <div className="text-center py-6 text-navy-400 text-xs">
              Bekleyen oneri yok. Yukaridaki formu kullanarak yeni bir cikarim baslatin.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
