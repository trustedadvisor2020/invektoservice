import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Input } from '../components/ui/Input';
import { Button } from '../components/ui/Button';
import { Zap } from 'lucide-react';

type LoginMode = 'inma' | 'ops';

export function LoginPage() {
  const navigate = useNavigate();
  const { loginWithInma, loginWithOps, loginWithMock, isLoading, error } = useAuth();

  const [mode, setMode] = useState<LoginMode>('inma');
  const [companyName, setCompanyName] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

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
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-br from-slate-50 to-slate-100">
      <Card className="w-full max-w-sm">
        <CardHeader className="text-center pb-2">
          <div className="flex justify-center mb-4">
            <div className="w-14 h-14 bg-blue-600 rounded-2xl flex items-center justify-center shadow-lg shadow-blue-600/20">
              <Zap className="w-7 h-7 text-white" />
            </div>
          </div>
          <CardTitle className="text-xl">Invekto Servisler</CardTitle>
          <p className="text-sm text-slate-500 mt-1">
            {mode === 'inma' ? 'Firma bilgilerinizle giris yapin' : 'Ops paneli girisi'}
          </p>
        </CardHeader>

        <CardContent>
          {/* Mode toggle */}
          <div className="flex rounded-lg border border-slate-200 overflow-hidden mb-5 text-sm font-medium">
            <button
              type="button"
              className={`flex-1 py-2 transition-colors ${
                mode === 'inma'
                  ? 'bg-blue-600 text-white'
                  : 'bg-white text-slate-500 hover:bg-slate-50'
              }`}
              onClick={() => setMode('inma')}
            >
              Firma Girisi
            </button>
            <button
              type="button"
              className={`flex-1 py-2 transition-colors ${
                mode === 'ops'
                  ? 'bg-slate-700 text-white'
                  : 'bg-white text-slate-500 hover:bg-slate-50'
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
              <div className="p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
                {error}
              </div>
            )}

            <Button
              type="submit"
              className="w-full"
              disabled={isLoading}
            >
              {isLoading ? 'Giris yapiliyor...' : 'Giris Yap'}
            </Button>
          </form>

          {mode === 'inma' && (
            <div className="mt-4 pt-4 border-t border-slate-200">
              <p className="text-xs text-slate-400 mb-2 text-center">Test Girisleri</p>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => handleMockLogin('full')}
                  disabled={isLoading}
                  className="flex-1 text-xs py-2 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-600 disabled:opacity-50 transition-colors"
                >
                  Tam Yetkili
                </button>
                <button
                  type="button"
                  onClick={() => handleMockLogin('klinik')}
                  disabled={isLoading}
                  className="flex-1 text-xs py-2 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-600 disabled:opacity-50 transition-colors"
                >
                  Demo Klinik
                </button>
                <button
                  type="button"
                  onClick={() => handleMockLogin('otel')}
                  disabled={isLoading}
                  className="flex-1 text-xs py-2 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-600 disabled:opacity-50 transition-colors"
                >
                  Demo Otel
                </button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
