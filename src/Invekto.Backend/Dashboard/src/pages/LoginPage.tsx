import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { Input } from '../components/ui/Input';
import { Button } from '../components/ui/Button';
import { Zap } from 'lucide-react';

type LoginMode = 'inma' | 'ops';

export function LoginPage() {
  const navigate = useNavigate();
  const { loginWithInma, loginWithOps, loginWithMock, loginWithQuickAdmin, isLoading, error } = useAuth();

  const [mode, setMode] = useState<LoginMode>('inma');
  const [companyName, setCompanyName] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  const handleQuickAdminLogin = async () => {
    const success = await loginWithQuickAdmin();
    if (success) navigate('/');
  };

  const handleMockLogin = async (scenario: 'full' | 'klinik' | 'otel') => {
    const success = await loginWithMock(scenario);
    if (success) navigate('/');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    let success = false;

    if (mode === 'inma') {
      success = await loginWithInma(companyName, username, password);
    } else {
      success = await loginWithOps(username, password);
    }

    if (success) {
      navigate('/');
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-4 bg-navy-50">
      <div className="w-full max-w-sm">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-12 h-12 bg-brand-500 rounded-2xl mb-4">
            <Zap className="w-6 h-6 text-white" />
          </div>
          <h1 className="text-xl font-semibold text-navy-900">Invekto Servisler</h1>
          <p className="text-sm text-navy-300 mt-1">
            {mode === 'inma' ? 'Firma bilgilerinizle giris yapin' : 'Ops paneli girisi'}
          </p>
        </div>

        {/* Card */}
        <div className="bg-white rounded-2xl border border-navy-100 shadow-card p-6">
          {/* Mode toggle */}
          <div className="flex rounded-lg bg-navy-50 p-0.5 mb-6 text-sm font-medium">
            <button
              type="button"
              className={`flex-1 py-2 rounded-md transition-all duration-200 ${
                mode === 'inma'
                  ? 'bg-white text-navy-900 shadow-soft'
                  : 'text-navy-400 hover:text-navy-600'
              }`}
              onClick={() => setMode('inma')}
            >
              Firma Girisi
            </button>
            <button
              type="button"
              className={`flex-1 py-2 rounded-md transition-all duration-200 ${
                mode === 'ops'
                  ? 'bg-white text-navy-900 shadow-soft'
                  : 'text-navy-400 hover:text-navy-600'
              }`}
              onClick={() => setMode('ops')}
            >
              Ops
            </button>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            {mode === 'inma' && (
              <Input
                label="Firma Adi"
                type="text"
                value={companyName}
                onChange={e => setCompanyName(e.target.value)}
                placeholder="Firma adinizi girin"
                required
                autoFocus
              />
            )}
            <Input
              label="Kullanici Adi"
              type="text"
              value={username}
              onChange={e => setUsername(e.target.value)}
              placeholder={mode === 'inma' ? 'Kullanici adinizi girin' : 'admin'}
              required
              autoFocus={mode === 'ops'}
            />
            <Input
              label="Parola"
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="••••••••"
              required
            />

            {error && (
              <div className="p-3 bg-red-50 border border-red-100 rounded-lg text-sm text-red-600">
                {error}
              </div>
            )}

            <Button
              type="submit"
              className="w-full h-10"
              disabled={isLoading}
            >
              {isLoading ? 'Giris yapiliyor...' : 'Giris Yap'}
            </Button>
          </form>

          {mode === 'ops' && (
            <div className="mt-5 pt-5 border-t border-navy-100/60">
              <button
                type="button"
                onClick={handleQuickAdminLogin}
                disabled={isLoading}
                className="w-full text-xs py-2.5 rounded-lg bg-navy-50 hover:bg-navy-100 text-navy-500 font-medium disabled:opacity-40 transition-colors"
              >
                Super Admin Girisi
              </button>
            </div>
          )}

          {mode === 'inma' && (
            <div className="mt-5 pt-5 border-t border-navy-100/60">
              <p className="text-2xs text-navy-300 mb-2 text-center font-medium uppercase tracking-wider">Test Girisleri</p>
              <div className="flex gap-2">
                {[
                  { label: 'Tam Yetkili', scenario: 'full' as const },
                  { label: 'Demo Klinik', scenario: 'klinik' as const },
                  { label: 'Demo Otel', scenario: 'otel' as const },
                ].map(({ label, scenario }) => (
                  <button
                    key={scenario}
                    type="button"
                    onClick={() => handleMockLogin(scenario)}
                    disabled={isLoading}
                    className="flex-1 text-xs py-2 rounded-lg bg-navy-50 hover:bg-navy-100 text-navy-500 font-medium disabled:opacity-40 transition-colors"
                  >
                    {label}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        <p className="mt-4 text-center text-2xs text-navy-200">v{__BUILD_TIME__}</p>
      </div>
    </div>
  );
}
