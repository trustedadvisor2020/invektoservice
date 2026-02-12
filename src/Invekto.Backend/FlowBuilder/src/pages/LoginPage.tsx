import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/auth';

export function LoginPage() {
  const [tenantId, setTenantId] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    const tid = parseInt(tenantId, 10);
    if (!tid || tid <= 0) {
      setError('Gecerli bir Tenant ID girin.');
      return;
    }
    if (!apiKey.trim()) {
      setError('API Key bos olamaz.');
      return;
    }

    setLoading(true);
    try {
      await login(tid, apiKey.trim());
      navigate('/', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Giris basarisiz');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50">
      <div className="w-full max-w-sm">
        <div className="bg-white rounded-xl border border-slate-200 p-8 shadow-sm">
          <h1 className="text-2xl font-bold text-slate-900 mb-1">Flow Builder</h1>
          <p className="text-sm text-slate-500 mb-6">
            Giris yapmak icin tenant bilgilerinizi girin.
          </p>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="tenantId" className="block text-sm text-slate-700 mb-1">
                Tenant ID
              </label>
              <input
                id="tenantId"
                type="number"
                min="1"
                value={tenantId}
                onChange={(e) => setTenantId(e.target.value)}
                className="w-full px-3 py-2 bg-white border border-slate-300 rounded-lg text-slate-900 placeholder-slate-400 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition"
                placeholder="ornek: 1"
                required
                disabled={loading}
              />
            </div>

            <div>
              <label htmlFor="apiKey" className="block text-sm text-slate-700 mb-1">
                API Key
              </label>
              <input
                id="apiKey"
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                className="w-full px-3 py-2 bg-white border border-slate-300 rounded-lg text-slate-900 placeholder-slate-400 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition"
                placeholder="flow_builder_api_key"
                required
                disabled={loading}
              />
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                {error}
              </div>
            )}

            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 disabled:opacity-50 text-white font-medium rounded-lg transition"
            >
              {loading ? 'Giris yapiliyor...' : 'Giris Yap'}
            </button>
          </form>
        </div>

        <p className="text-center text-xs text-slate-400 mt-4">
          Invekto Flow Builder v2
        </p>
      </div>
    </div>
  );
}
