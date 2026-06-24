import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { Input } from '../components/ui/Input';
import { Button } from '../components/ui/Button';
import { ChatinboxLogo } from '../components/ui/ChatinboxLogo';

type LoginMode = 'inma' | 'ops';

function getLoginMode(): { mode: LoginMode; locked: boolean; superHost: boolean } {
  const host = window.location.hostname;
  // Dual-domain: hem *.invekto.com (canli) hem *.chatinbox.net (cutover) taninir.
  if (host === 'super.invekto.com' || host === 'super.chatinbox.net') return { mode: 'ops', locked: true, superHost: true };
  if (host === 'ai.invekto.com' || host === 'ai.chatinbox.net') return { mode: 'inma', locked: true, superHost: false };
  // localhost / dev → her iki mod acik (toggle gosterilir)
  return { mode: 'inma', locked: false, superHost: false };
}

export function LoginPage() {
  const navigate = useNavigate();
  const { loginWithInma, loginWithOps, isLoading, error } = useAuth();

  const [hostConfig] = useState(getLoginMode);
  const [mode, setMode] = useState<LoginMode>(hostConfig.mode);
  const [companyName, setCompanyName] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

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
        {/* Logo — marka: yatay kirmizi ikon + wordmark; super host mevcut dikey logoyu korur */}
        <div className="flex flex-col items-center mb-8">
          {hostConfig.superHost ? (
            <>
              <img src="/app/logo.png" alt="Chatinbox" className="w-14 h-14 mb-3" />
              <ChatinboxLogo size="lg" className="mb-2" />
            </>
          ) : (
            <div className="flex items-center gap-2.5 mb-2">
              <img src="/app/logo.png" alt="Chatinbox" className="w-10 h-10" />
              <ChatinboxLogo size="md" variant="none" color="#E54C4C" />
            </div>
          )}
          <p className="text-sm text-navy-300 mt-1">
            {mode === 'inma' ? 'Firma bilgilerinizle giriş yapın' : 'Ops paneli girişi'}
          </p>
        </div>

        {/* Card */}
        <div className="bg-white rounded-2xl border border-navy-100 shadow-card p-6">
          {/* Mode toggle — only show on localhost/dev where both modes available */}
          {!hostConfig.locked && (
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
                Firma Girişi
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
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            {mode === 'inma' && (
              <Input
                label="Firma Adı"
                type="text"
                value={companyName}
                onChange={e => setCompanyName(e.target.value)}
                placeholder="Firma adınızı girin"
                required
                autoFocus
              />
            )}
            <Input
              label="Kullanıcı Adı"
              type="text"
              value={username}
              onChange={e => setUsername(e.target.value)}
              placeholder={mode === 'inma' ? 'Kullanıcı adınızı girin' : 'admin'}
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
              {isLoading ? 'Giriş yapılıyor...' : 'Giriş Yap'}
            </Button>
          </form>
        </div>

        <p className="mt-4 text-center text-2xs text-navy-200">v{__BUILD_TIME__}</p>
      </div>
    </div>
  );
}
