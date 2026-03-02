import { useState } from 'react';
import { FileText, MessageSquare, Plus } from 'lucide-react';
import { DocumentUpload } from '../components/knowledge/DocumentUpload';
import { DocumentList } from '../components/knowledge/DocumentList';
import { FaqManager } from '../components/knowledge/FaqManager';
import { useAuth } from '../hooks/useAuth';
import { api } from '../lib/api';
import { cn } from '../lib/utils';

type Tab = 'documents' | 'faqs';

export function KnowledgePage() {
  const { session } = useAuth();
  const tenantId = session?.tenantId ?? 1;
  const [activeTab, setActiveTab] = useState<Tab>('documents');
  const [refreshKey, setRefreshKey] = useState(0);
  const [uploadOpen, setUploadOpen] = useState(false);

  const handleUploadComplete = () => {
    setRefreshKey(k => k + 1);
    api.generateEmbeddings(tenantId).catch((err) => {
      console.warn('[KnowledgePage] Embedding generation failed:', err);
    });
  };

  const tabs: { key: Tab; label: string; icon: typeof FileText }[] = [
    { key: 'documents', label: 'Dokumanlar', icon: FileText },
    { key: 'faqs', label: 'SSS', icon: MessageSquare },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-navy-900">Bilgi Bankasi</h1>
        {activeTab === 'documents' && (
          <button
            onClick={() => setUploadOpen(true)}
            className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium bg-brand-500 text-white rounded-lg hover:bg-brand-600 transition-colors"
          >
            <Plus className="w-4 h-4" />
            Dokuman Ekle
          </button>
        )}
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
        <DocumentList tenantId={tenantId} refreshKey={refreshKey} />
      )}
      {activeTab === 'faqs' && (
        <FaqManager tenantId={tenantId} />
      )}

      {/* Upload modal */}
      <DocumentUpload
        tenantId={tenantId}
        open={uploadOpen}
        onClose={() => setUploadOpen(false)}
        onUploadComplete={handleUploadComplete}
      />
    </div>
  );
}
