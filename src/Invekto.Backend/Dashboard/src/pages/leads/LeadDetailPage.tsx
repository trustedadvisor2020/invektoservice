import { useState } from 'react';
import { Card, CardContent } from '../../components/ui/Card';
import { PhotoTab } from './components/PhotoTab';

// FEAT-PHOTO Lead Detay sayfasi shell. Pilot kapsami sadece "Fotograflar"
// sekmesini render etmek; mevcut Lead Detay yapisi (henuz baska bir component
// olarak baska pakette yapildi) router uzerinden bu sayfayi ileride
// /leads/:id route'unda mount edebilir. Bu pakette Programs.cs / route
// wiring out of scope (allowed_files), bu yuzden LeadDetailPage standalone
// olarak hazir; post-pilot scope-correction patch'inde App.tsx routing
// dosyasina baglanir.
//
// Tab pattern: Fotograflar (active by default, pilot baska tab yok). Mevcut
// SPA tab'lari flow-builder Wizard pattern'ini takip ediyor — o boyutta
// karmasiklik yok, basit conditional render.

interface LeadDetailPageProps {
  /** Route param - integer leads.id. Caller (router veya parent) saglar. */
  leadId: number;
}

type Tab = 'photos';

export function LeadDetailPage({ leadId }: LeadDetailPageProps) {
  const [activeTab, setActiveTab] = useState<Tab>('photos');

  return (
    <div className="p-6 space-y-4">
      <Card>
        <CardContent>
          <h2 className="text-lg font-semibold text-navy-900">
            Lead #{leadId}
          </h2>
          <p className="text-sm text-navy-500 mt-1">
            Detay panelindeki sekmeler asagida.
          </p>
        </CardContent>
      </Card>

      <div className="border-b border-navy-100 flex gap-4">
        <TabButton
          isActive={activeTab === 'photos'}
          onClick={() => setActiveTab('photos')}
        >
          Fotograflar
        </TabButton>
      </div>

      {activeTab === 'photos' && <PhotoTab leadId={leadId} />}
    </div>
  );
}

function TabButton({
  isActive,
  onClick,
  children
}: {
  isActive: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={
        'px-3 py-2 text-sm font-medium border-b-2 transition-colors ' +
        (isActive
          ? 'border-brand-500 text-brand-700'
          : 'border-transparent text-navy-500 hover:text-navy-700')
      }
    >
      {children}
    </button>
  );
}

export default LeadDetailPage;
