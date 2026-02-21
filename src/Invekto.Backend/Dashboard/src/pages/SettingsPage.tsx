import { useAuth } from '../hooks/useAuth';
import { Settings } from 'lucide-react';

export function SettingsPage() {
  const { session } = useAuth();

  return (
    <div>
      <h1 className="text-xl font-semibold text-navy-900 mb-6 flex items-center gap-2">
        <Settings className="w-5 h-5 text-navy-400" />
        Ayarlar
      </h1>
      {session && (
        <div className="bg-white rounded-xl border border-navy-100 p-5 max-w-sm space-y-3 text-sm shadow-card">
          <div className="flex justify-between">
            <span className="text-navy-400">Firma ID</span>
            <span className="font-medium text-navy-700">{session.tenantId}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-navy-400">Kullanici</span>
            <span className="font-medium text-navy-700">{session.fullName || session.userId}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-navy-400">Rol</span>
            <span className="font-medium text-navy-700 capitalize">{session.role}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-navy-400">Dil</span>
            <span className="font-medium text-navy-700 uppercase">{session.lang}</span>
          </div>
          <div className="pt-3 border-t border-navy-100/60">
            <span className="text-navy-400 block mb-2">Aktif Moduller</span>
            <div className="flex flex-wrap gap-1.5">
              {session.inseFeatures.length > 0
                ? session.inseFeatures.map(f => (
                    <span key={f} className="px-2.5 py-1 bg-brand-50 text-brand-600 rounded-full text-xs font-medium">
                      {f}
                    </span>
                  ))
                : <span className="text-navy-300 text-xs">Lisansli modul yok</span>
              }
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
