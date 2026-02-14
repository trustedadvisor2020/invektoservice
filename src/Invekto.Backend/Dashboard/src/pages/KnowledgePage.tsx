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
    setEmbedMsg('Generating...');
    try {
      const result = await api.generateEmbeddings(tenantId);
      setEmbedMsg(`${result.generated} generated${result.failed ? `, ${result.failed} failed` : ''}`);
      setTimeout(() => setEmbedMsg(null), 5000);
    } catch (err) {
      setEmbedMsg(`Error: ${err instanceof Error ? err.message : 'Unknown'}`);
      setTimeout(() => setEmbedMsg(null), 5000);
    }
  };

  const tabs: { key: Tab; label: string; icon: typeof FileText }[] = [
    { key: 'documents', label: 'Documents', icon: FileText },
    { key: 'faqs', label: 'FAQs', icon: MessageSquare },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Knowledge Base</h1>
        <div className="flex items-center gap-3">
          <label className="text-sm text-slate-500">Tenant:</label>
          <input
            type="number"
            value={tenantId}
            onChange={e => setTenantId(Number(e.target.value) || 1)}
            className="w-20 px-2 py-1 text-sm border border-slate-300 rounded-md"
            min={1}
          />
          <button
            onClick={handleGenerateEmbeddings}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-violet-600 text-white rounded-md hover:bg-violet-700 transition-colors"
          >
            <Sparkles className="w-3.5 h-3.5" />
            Embeddings
          </button>
          {embedMsg && <span className="text-xs text-slate-500">{embedMsg}</span>}
        </div>
      </div>

      {/* Tabs */}
      <div className="border-b border-slate-200">
        <div className="flex gap-1">
          {tabs.map(tab => {
            const Icon = tab.icon;
            return (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={cn(
                  'flex items-center gap-1.5 px-4 py-2 text-sm font-medium border-b-2 transition-colors',
                  activeTab === tab.key
                    ? 'border-blue-600 text-blue-600'
                    : 'border-transparent text-slate-500 hover:text-slate-700'
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
