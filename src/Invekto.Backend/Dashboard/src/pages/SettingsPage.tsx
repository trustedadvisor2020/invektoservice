import { useAuth } from '../hooks/useAuth';
import { Settings } from 'lucide-react';

export function SettingsPage() {
  const { session } = useAuth();

  return (
    <div>
      <h1 className="text-lg font-semibold text-slate-800 mb-4 flex items-center gap-2">
        <Settings className="w-5 h-5" />
        Ayarlar
      </h1>
      {session && (
        <div className="bg-white rounded-xl border border-slate-200 p-4 max-w-sm space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-slate-500">Firma ID</span>
            <span className="font-medium text-slate-700">{session.tenantId}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-slate-500">Kullanici</span>
            <span className="font-medium text-slate-700">{session.fullName || session.userId}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-slate-500">Rol</span>
            <span className="font-medium text-slate-700 capitalize">{session.role}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-slate-500">Dil</span>
            <span className="font-medium text-slate-700 uppercase">{session.lang}</span>
          </div>
          <div className="pt-2 border-t border-slate-100">
            <span className="text-slate-500 block mb-1">Aktif Moduller</span>
            <div className="flex flex-wrap gap-1">
              {session.inseFeatures.length > 0
                ? session.inseFeatures.map(f => (
                    <span key={f} className="px-2 py-0.5 bg-blue-50 text-blue-700 rounded text-xs font-medium">
                      {f}
                    </span>
                  ))
                : <span className="text-slate-400 text-xs">Lisansli modul yok</span>
              }
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
