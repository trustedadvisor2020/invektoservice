import { useState, useEffect, useCallback } from 'react';
import { api, TenantEntry } from '../lib/api';
import { RefreshCw, LogIn, CheckCircle, XCircle } from 'lucide-react';

export function TenantsPage() {
  const [tenants, setTenants] = useState<TenantEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [impersonating, setImpersonating] = useState<number | null>(null);

  const fetchTenants = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.getOpsTenants();
      setTenants(result.tenants);
    } catch (err) {
      console.error('Tenant list fetch failed:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTenants();
  }, [fetchTenants]);

  const handleImpersonate = async (tenantId: number) => {
    setImpersonating(tenantId);
    try {
      const resp = await api.impersonateTenant(tenantId);
      api.setSession(resp);
      // Full page reload — reinitializes all hooks with new session context
      window.location.href = '/';
    } catch (err) {
      console.error('Impersonate failed:', err);
      setImpersonating(null);
    }
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">Firmalar</h1>
          <p className="text-sm text-slate-500">
            Tum kayitli firmalar — firma admini olarak giris yapin
            {tenants.length > 0 && (
              <span className="ml-2 text-slate-400">({tenants.length} firma)</span>
            )}
          </p>
        </div>
        <button
          onClick={fetchTenants}
          disabled={loading}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-white border border-slate-200 rounded-lg hover:bg-slate-50 disabled:opacity-50"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
          Yenile
        </button>
      </div>

      {/* Table */}
      <div className="bg-white rounded-lg border border-slate-200 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200">
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">ID</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Firma Adi</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Durum</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Sektor</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Plan</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Kayit Tarihi</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Islem</th>
              </tr>
            </thead>
            <tbody>
              {loading && tenants.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-12 text-slate-400">
                    Yukleniyor...
                  </td>
                </tr>
              ) : tenants.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-12 text-slate-400">
                    Firma bulunamadi
                  </td>
                </tr>
              ) : (
                tenants.map(t => (
                  <tr key={t.tenantId} className="border-b border-slate-100 hover:bg-slate-50/50">
                    <td className="px-4 py-2.5">
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-slate-100 text-slate-700">
                        #{t.tenantId}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 font-medium text-slate-800">
                      {t.tenantName}
                    </td>
                    <td className="px-4 py-2.5">
                      {t.isActive ? (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-green-50 text-green-700">
                          <CheckCircle className="w-3 h-3" />
                          Aktif
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-red-50 text-red-700">
                          <XCircle className="w-3 h-3" />
                          Pasif
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-2.5 text-xs text-slate-500">
                      {t.sector ?? '-'}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className="px-2 py-0.5 rounded text-xs font-medium bg-blue-50 text-blue-700">
                        {t.planTier}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-slate-500">
                      {formatDate(t.createdAt)}
                    </td>
                    <td className="px-4 py-2.5">
                      {t.isActive ? (
                        <button
                          onClick={() => handleImpersonate(t.tenantId)}
                          disabled={impersonating === t.tenantId}
                          className="inline-flex items-center gap-1.5 px-3 py-1 text-xs font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          <LogIn className="w-3 h-3" />
                          {impersonating === t.tenantId ? 'Giriliyor...' : 'Giris Yap'}
                        </button>
                      ) : (
                        <span className="text-xs text-slate-400">Devre disi</span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}
