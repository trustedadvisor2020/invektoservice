import { useState } from 'react';
import { FileText, MessageSquare, Sparkles } from 'lucide-react';
import { DocumentUpload } from '../components/knowledge/DocumentUpload';
import { DocumentList } from '../components/knowledge/DocumentList';
import { FaqManager } from '../components/knowledge/FaqManager';
import { api } from '../lib/api';
import { cn } from '../lib/utils';

type Tab = 'documents' | 'faqs';

export function KnowledgePage() {
  const [activeTab, setActiveTab] = useState<Tab>('documents');
  const [tenantId, setTenantId] = useState(1);
  const [refreshKey, setRefreshKey] = useState(0);
  const [embedMsg, setEmbedMsg] = useState<string | null>(null);

  const handleUploadComplete = () => setRefreshKey(k => k + 1);

  const handleGenerateEmbeddings = async () => {
    setEmbedMsg('Uretiliyor...');
    try {
      const result = await api.generateEmbeddings(tenantId);
      setEmbedMsg(`${result.generated} uretildi${result.failed ? `, ${result.failed} basarisiz` : ''}`);
      setTimeout(() => setEmbedMsg(null), 5000);
    } catch (err) {
      setEmbedMsg(`Hata: ${err instanceof Error ? err.message : 'Bilinmeyen'}`);
      setTimeout(() => setEmbedMsg(null), 5000);
    }
  };

  const tabs: { key: Tab; label: string; icon: typeof FileText }[] = [
    { key: 'documents', label: 'Dokumanlar', icon: FileText },
    { key: 'faqs', label: 'SSS', icon: MessageSquare },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-navy-900">Bilgi Bankasi</h1>
        <div className="flex items-center gap-3">
          <label className="text-sm text-navy-400">Firma:</label>
          <input
            type="number"
            value={tenantId}
            onChange={e => setTenantId(Number(e.target.value) || 1)}
            className="w-20 px-2 py-1.5 text-sm border border-navy-100 rounded-lg focus:outline-none focus:border-brand-500 focus:shadow-focus"
            min={1}
          />
          <button
            onClick={handleGenerateEmbeddings}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-brand-500 text-white rounded-lg hover:bg-brand-600 transition-colors font-medium"
          >
            <Sparkles className="w-3.5 h-3.5" />
            Embeddings
          </button>
          {embedMsg && <span className="text-xs text-navy-400">{embedMsg}</span>}
        </div>
      </div>

      {/* Tabs */}
      <div className="border-b border-navy-100">
        <div className="flex gap-1">
          {tabs.map(tab => {
            const Icon = tab.icon;
            return (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={cn(
                  'flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium border-b-2 transition-colors',
                  activeTab === tab.key
                    ? 'border-brand-500 text-brand-600'
                    : 'border-transparent text-navy-400 hover:text-navy-600'
                )}
              >
                <Icon className="w-4 h-4" />
                {tab.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Tab content */}
      {activeTab === 'documents' && (
        <div className="space-y-6">
          <DocumentUpload tenantId={tenantId} onUploadComplete={handleUploadComplete} />
          <DocumentList tenantId={tenantId} refreshKey={refreshKey} />
        </div>
      )}
      {activeTab === 'faqs' && (
        <FaqManager tenantId={tenantId} />
      )}
    </div>
  );
}
