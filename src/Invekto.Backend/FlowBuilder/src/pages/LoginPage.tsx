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

  const inputClasses = 'w-full px-3 py-2.5 bg-white border border-navy-100 rounded-lg text-navy-900 placeholder-navy-300 focus:outline-none focus:border-brand-500 focus:shadow-focus transition-all';

  return (
    <div className="min-h-screen flex items-center justify-center bg-navy-50">
      <div className="w-full max-w-sm">
        <div className="bg-white rounded-2xl border border-navy-100 p-8 shadow-card">
          <h1 className="text-xl font-semibold text-navy-900 mb-1">Flow Builder</h1>
          <p className="text-sm text-navy-400 mb-6">
            Giris yapmak icin tenant bilgilerinizi girin.
          </p>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="tenantId" className="block text-sm font-medium text-navy-700 mb-1.5">
                Tenant ID
              </label>
              <input
                id="tenantId"
                type="number"
                min="1"
                value={tenantId}
                onChange={(e) => setTenantId(e.target.value)}
                className={inputClasses}
                placeholder="ornek: 1"
                required
                disabled={loading}
              />
            </div>

            <div>
              <label htmlFor="apiKey" className="block text-sm font-medium text-navy-700 mb-1.5">
                API Key
              </label>
              <input
                id="apiKey"
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                className={inputClasses}
                placeholder="flow_builder_api_key"
                required
                disabled={loading}
              />
            </div>

            {error && (
              <div className="text-sm text-red-600 bg-red-50 border border-red-100 rounded-lg px-3 py-2">
                {error}
              </div>
            )}

            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 bg-brand-500 hover:bg-brand-600 disabled:opacity-40 text-white font-medium rounded-lg transition-colors"
            >
              {loading ? 'Giris yapiliyor...' : 'Giris Yap'}
            </button>
          </form>
        </div>

        <p className="text-center text-2xs text-navy-200 mt-4">
          Invekto Flow Builder v2
        </p>
      </div>
    </div>
  );
}
