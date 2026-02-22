import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../hooks/useAuth';
import { api, type InstanceDto, type WorkingHoursDto } from '../lib/api';
import { Settings, RefreshCw, Wifi, WifiOff, Smartphone, Globe, Radio, MessageSquare, Clock, Save, Check } from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Card, CardContent } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';

const INSTANCE_TYPE_LABELS: Record<number, { label: string; icon: typeof Smartphone; variant: 'success' | 'info' | 'default' | 'warning' }> = {
  1: { label: 'WhatsApp', icon: Smartphone, variant: 'success' },
  2: { label: 'Web', icon: Globe, variant: 'info' },
  5: { label: 'Kanal', icon: Radio, variant: 'default' },
  6: { label: 'SMS', icon: MessageSquare, variant: 'warning' },
};

const ALL_DAYS = [
  { value: 'Monday', label: 'Pazartesi' },
  { value: 'Tuesday', label: 'Sali' },
  { value: 'Wednesday', label: 'Carsamba' },
  { value: 'Thursday', label: 'Persembe' },
  { value: 'Friday', label: 'Cuma' },
  { value: 'Saturday', label: 'Cumartesi' },
  { value: 'Sunday', label: 'Pazar' },
];

const COMMON_TIMEZONES = [
  { value: 'Europe/Istanbul', label: 'Turkiye (UTC+3)' },
  { value: 'Europe/London', label: 'Londra (UTC+0/+1)' },
  { value: 'Europe/Berlin', label: 'Berlin (UTC+1/+2)' },
  { value: 'Europe/Moscow', label: 'Moskova (UTC+3)' },
  { value: 'Asia/Dubai', label: 'Dubai (UTC+4)' },
  { value: 'America/New_York', label: 'New York (UTC-5/-4)' },
  { value: 'America/Los_Angeles', label: 'Los Angeles (UTC-8/-7)' },
  { value: 'Asia/Tokyo', label: 'Tokyo (UTC+9)' },
];

export function SettingsPage() {
  const { session } = useAuth();
  const [instances, setInstances] = useState<InstanceDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Working Hours state
  const [whLoading, setWhLoading] = useState(false);
  const [whSaving, setWhSaving] = useState(false);
  const [whError, setWhError] = useState<string | null>(null);
  const [whSuccess, setWhSuccess] = useState(false);
  const [whStart, setWhStart] = useState('09:00');
  const [whEnd, setWhEnd] = useState('18:00');
  const [whTimezone, setWhTimezone] = useState('Europe/Istanbul');
  const [whDaysOff, setWhDaysOff] = useState<string[]>(['Saturday', 'Sunday']);
  const [whConfigured, setWhConfigured] = useState(false);

  const fetchInstances = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.getInstances();
      setInstances(result.instances);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      if (msg.includes('WapCRM') || msg.includes('yapilandirilmamis')) {
        setError('WapCRM API anahtari yapilandirilmamis. Lutfen firma ayarlarinizi kontrol edin.');
      } else {
        setError(`Hat listesi yuklenemedi: ${msg}`);
      }
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchWorkingHours = useCallback(async () => {
    setWhLoading(true);
    setWhError(null);
    try {
      const result: WorkingHoursDto = await api.getWorkingHours();
      setWhStart(result.start);
      setWhEnd(result.end);
      setWhTimezone(result.timezone);
      setWhDaysOff(result.days_off);
      setWhConfigured(result.configured);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      setWhError(`Calisma saatleri yuklenemedi: ${msg}`);
    } finally {
      setWhLoading(false);
    }
  }, []);

  useEffect(() => {
    if (session) {
      fetchInstances();
      fetchWorkingHours();
    }
  }, [session, fetchInstances, fetchWorkingHours]);

  const handleRefresh = async () => {
    setRefreshing(true);
    setError(null);
    try {
      const result = await api.refreshInstances();
      setInstances(result.instances);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      setError(`WapCRM yenileme basarisiz: ${msg}`);
    } finally {
      setRefreshing(false);
    }
  };

  const handleToggle = async (inst: InstanceDto) => {
    const newEnabled = !inst.isEnabled;
    setTogglingId(inst.instanceId);
    try {
      await api.toggleInstance(inst.instanceId, newEnabled);
      setInstances(prev => prev.map(i =>
        i.instanceId === inst.instanceId ? { ...i, isEnabled: newEnabled } : i
      ));
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      alert(msg.includes('flow') ? 'Bu hat bir akis tarafindan kullaniliyor. Once akistan cikarin.' : msg);
    } finally {
      setTogglingId(null);
    }
  };

  const handleSaveWorkingHours = async () => {
    setWhSaving(true);
    setWhError(null);
    setWhSuccess(false);
    try {
      await api.updateWorkingHours({
        start: whStart,
        end: whEnd,
        timezone: whTimezone,
        days_off: whDaysOff,
      });
      setWhConfigured(true);
      setWhSuccess(true);
      setTimeout(() => setWhSuccess(false), 3000);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      setWhError(`Calisma saatleri kaydedilemedi: ${msg}`);
    } finally {
      setWhSaving(false);
    }
  };

  const toggleDayOff = (day: string) => {
    setWhDaysOff(prev =>
      prev.includes(day) ? prev.filter(d => d !== day) : [...prev, day]
    );
  };

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold text-navy-900 flex items-center gap-2">
        <Settings className="w-5 h-5 text-navy-400" />
        Ayarlar
      </h1>

      {/* Session Info (compact) */}
      {session && (
        <Card>
          <CardContent className="py-4">
            <div className="flex flex-wrap gap-x-6 gap-y-2 text-sm">
              <div><span className="text-navy-400">Firma ID:</span> <span className="font-medium text-navy-700">{session.tenantId}</span></div>
              <div><span className="text-navy-400">Kullanici:</span> <span className="font-medium text-navy-700">{session.fullName || session.userId}</span></div>
              <div><span className="text-navy-400">Rol:</span> <span className="font-medium text-navy-700 capitalize">{session.role}</span></div>
              {session.inseFeatures.length > 0 && (
                <div className="flex items-center gap-1.5">
                  <span className="text-navy-400">Moduller:</span>
                  {session.inseFeatures.map(f => (
                    <Badge key={f} variant="info">{f}</Badge>
                  ))}
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Working Hours Section */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <div>
            <h2 className="text-lg font-semibold text-navy-900 flex items-center gap-2">
              <Clock className="w-4 h-4 text-navy-400" />
              Calisma Saatleri
            </h2>
            <p className="text-xs text-navy-400">
              Mesai saatlerini belirleyin. Mesai disinda gelen mesajlara otomatik cevap gonderilir.
              {whConfigured && <Badge variant="success" className="ml-2">Aktif</Badge>}
              {!whConfigured && !whLoading && <Badge variant="default" className="ml-2">Yapilandirilmamis</Badge>}
            </p>
          </div>
        </div>

        {whError && (
          <div className="p-3 mb-3 bg-red-50 border border-red-100 rounded-xl text-sm text-red-700">
            {whError}
          </div>
        )}

        {whSuccess && (
          <div className="p-3 mb-3 bg-emerald-50 border border-emerald-100 rounded-xl text-sm text-emerald-700 flex items-center gap-2">
            <Check className="w-4 h-4" />
            Calisma saatleri basariyla kaydedildi.
          </div>
        )}

        {whLoading ? (
          <Card>
            <CardContent className="py-8 text-center text-slate-400">
              Yukleniyor...
            </CardContent>
          </Card>
        ) : (
          <Card>
            <CardContent className="py-5 space-y-5">
              {/* Time range */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Baslangic Saati</label>
                  <input
                    type="time"
                    value={whStart}
                    onChange={(e) => setWhStart(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-brand-500 focus:border-brand-500"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Bitis Saati</label>
                  <input
                    type="time"
                    value={whEnd}
                    onChange={(e) => setWhEnd(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-brand-500 focus:border-brand-500"
                  />
                </div>
              </div>

              {/* Timezone */}
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Saat Dilimi</label>
                <select
                  value={whTimezone}
                  onChange={(e) => setWhTimezone(e.target.value)}
                  className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-brand-500 focus:border-brand-500 bg-white"
                >
                  {COMMON_TIMEZONES.map(tz => (
                    <option key={tz.value} value={tz.value}>{tz.label}</option>
                  ))}
                </select>
              </div>

              {/* Days off */}
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-2">Kapali Gunler</label>
                <div className="flex flex-wrap gap-2">
                  {ALL_DAYS.map(day => {
                    const isOff = whDaysOff.includes(day.value);
                    return (
                      <button
                        key={day.value}
                        type="button"
                        onClick={() => toggleDayOff(day.value)}
                        className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-colors ${
                          isOff
                            ? 'bg-red-100 text-red-700 border border-red-200'
                            : 'bg-slate-50 text-slate-600 border border-slate-200 hover:bg-slate-100'
                        }`}
                      >
                        {day.label}
                      </button>
                    );
                  })}
                </div>
                <p className="text-xs text-slate-400 mt-1">
                  Kapali gunlerde tum gun mesai disi mesaji gonderilir.
                </p>
              </div>

              {/* Save button */}
              <div className="flex justify-end pt-2">
                <Button
                  variant="primary"
                  size="sm"
                  onClick={handleSaveWorkingHours}
                  disabled={whSaving}
                >
                  <Save className="w-4 h-4" />
                  <span>{whSaving ? 'Kaydediliyor...' : 'Kaydet'}</span>
                </Button>
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      {/* WhatsApp Instances Section */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <div>
            <h2 className="text-lg font-semibold text-navy-900">WhatsApp Hatlari</h2>
            <p className="text-xs text-navy-400">
              Mesaj alinacak hatlari yonetin. Kapali hatlardan gelen mesajlar yoksayilir.
              {instances.length > 0 && <span className="ml-1">({instances.length} hat)</span>}
            </p>
          </div>
          <Button
            variant="secondary"
            size="sm"
            onClick={handleRefresh}
            disabled={refreshing || loading}
          >
            <RefreshCw className={`w-4 h-4 flex-shrink-0 ${refreshing ? 'animate-spin' : ''}`} />
            <span>WapCRM&apos;den Yenile</span>
          </Button>
        </div>

        {error && (
          <div className="p-3 mb-3 bg-amber-50 border border-amber-100 rounded-xl text-sm text-amber-700">
            {error}
          </div>
        )}

        <div className="bg-white rounded-lg border border-slate-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200">
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600 w-12">Durum</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Hat Adi</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Numara</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Tip</th>
                  <th className="text-left px-4 py-2.5 font-medium text-slate-600">Akis</th>
                  <th className="text-center px-4 py-2.5 font-medium text-slate-600 w-16">Aktif</th>
                </tr>
              </thead>
              <tbody>
                {loading && instances.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="text-center py-12 text-slate-400">
                      Hatlar yukleniyor...
                    </td>
                  </tr>
                ) : instances.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="text-center py-12 text-slate-400">
                      {error ? 'Hat bulunamadi' : 'Henuz hat eklenmemis. WapCRM\'den yenileyin.'}
                    </td>
                  </tr>
                ) : (
                  instances.map(inst => {
                    const typeInfo = INSTANCE_TYPE_LABELS[inst.instanceType] || INSTANCE_TYPE_LABELS[1];
                    const TypeIcon = typeInfo.icon;
                    const isFlowAssigned = inst.flowId != null;
                    const isToggling = togglingId === inst.instanceId;

                    return (
                      <tr key={inst.instanceId} className="border-b border-slate-100 hover:bg-slate-50/50">
                        {/* Status icon */}
                        <td className="px-4 py-2.5 text-center">
                          {inst.isEnabled ? (
                            <Wifi className="w-4 h-4 text-emerald-500 inline" />
                          ) : (
                            <WifiOff className="w-4 h-4 text-slate-300 inline" />
                          )}
                        </td>

                        {/* Instance name + ID tooltip */}
                        <td className="px-4 py-2.5">
                          <span
                            className="font-medium text-slate-800 cursor-default"
                            title={`Instance ID: ${inst.instanceId}`}
                          >
                            {inst.instanceName}
                          </span>
                        </td>

                        {/* Phone number */}
                        <td className="px-4 py-2.5 text-xs text-slate-500 font-mono">
                          {inst.account || '-'}
                        </td>

                        {/* Type badge */}
                        <td className="px-4 py-2.5">
                          <span className="inline-flex items-center gap-1">
                            <TypeIcon className="w-3 h-3" />
                            <Badge variant={typeInfo.variant}>{typeInfo.label}</Badge>
                          </span>
                        </td>

                        {/* Assigned flow */}
                        <td className="px-4 py-2.5">
                          {inst.flowName ? (
                            <Badge variant="info">{inst.flowName}</Badge>
                          ) : (
                            <span className="text-xs text-slate-300">Atanmamis</span>
                          )}
                        </td>

                        {/* Toggle checkbox */}
                        <td className="px-4 py-2.5 text-center">
                          <input
                            type="checkbox"
                            checked={inst.isEnabled}
                            disabled={isToggling || (isFlowAssigned && inst.isEnabled)}
                            onChange={() => handleToggle(inst)}
                            className="w-4 h-4 rounded border-slate-300 text-brand-600 focus:ring-brand-500 disabled:opacity-50 disabled:cursor-not-allowed"
                            title={
                              isFlowAssigned && inst.isEnabled
                                ? `"${inst.flowName}" akisinda kullaniliyor — once akistan cikarin`
                                : inst.isEnabled ? 'Kapat' : 'Ac'
                            }
                          />
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
