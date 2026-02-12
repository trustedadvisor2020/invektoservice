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
    <div className="min-h-screen flex items-center justify-center bg-[#0f0f23]">
      <div className="w-full max-w-sm">
        <div className="bg-[#1e1e3a] rounded-xl border border-[#2d2d4a] p-8 shadow-2xl">
          <h1 className="text-2xl font-bold text-white mb-1">Flow Builder</h1>
          <p className="text-sm text-gray-400 mb-6">
            Giris yapmak icin tenant bilgilerinizi girin.
          </p>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="tenantId" className="block text-sm text-gray-300 mb-1">
                Tenant ID
              </label>
              <input
                id="tenantId"
                type="number"
                min="1"
                value={tenantId}
                onChange={(e) => setTenantId(e.target.value)}
                className="w-full px-3 py-2 bg-[#0f0f23] border border-[#3d3d5a] rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-blue-500 transition"
                placeholder="ornek: 1"
                required
                disabled={loading}
              />
            </div>

            <div>
              <label htmlFor="apiKey" className="block text-sm text-gray-300 mb-1">
                API Key
              </label>
              <input
                id="apiKey"
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                className="w-full px-3 py-2 bg-[#0f0f23] border border-[#3d3d5a] rounded-lg text-white placeholder-gray-500 focus:outline-none focus:border-blue-500 transition"
                placeholder="flow_builder_api_key"
                required
                disabled={loading}
              />
            </div>

            {error && (
              <div className="text-sm text-red-400 bg-red-900/20 border border-red-800/30 rounded-lg px-3 py-2">
                {error}
              </div>
            )}

            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-800 disabled:opacity-50 text-white font-medium rounded-lg transition"
            >
              {loading ? 'Giris yapiliyor...' : 'Giris Yap'}
            </button>
          </form>
        </div>

        <p className="text-center text-xs text-gray-600 mt-4">
          Invekto Flow Builder v2
        </p>
      </div>
    </div>
  );
}
