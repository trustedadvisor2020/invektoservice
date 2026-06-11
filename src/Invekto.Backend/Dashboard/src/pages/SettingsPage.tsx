import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { api, ApiClientError, type InstanceDto, type WorkingHoursDto } from '../lib/api';
import { Settings, RefreshCw, Wifi, WifiOff, Smartphone, Globe, Radio, MessageSquare, Clock, Save, Check, Building2, Phone, Factory, Webhook, ChevronRight, MapPin } from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Card, CardContent } from '../components/ui/Card';
import { Badge } from '../components/ui/Badge';
import { cn } from '../lib/utils';

type SettingsTab = 'general' | 'working-hours' | 'lines';

interface TabDef { id: SettingsTab; label: string; icon: typeof Settings; tenantOnly?: boolean }

const ALL_TABS: TabDef[] = [
  { id: 'general', label: 'Temel Bilgiler', icon: Building2 },
  { id: 'working-hours', label: 'Çalışma Saatleri', icon: Clock, tenantOnly: true },
  { id: 'lines', label: 'Hatlar', icon: Phone, tenantOnly: true },
];

const INSTANCE_TYPE_LABELS: Record<number, { label: string; icon: typeof Smartphone; variant: 'success' | 'info' | 'default' | 'warning' }> = {
  1: { label: 'WhatsApp', icon: Smartphone, variant: 'success' },
  2: { label: 'Web', icon: Globe, variant: 'info' },
  5: { label: 'Kanal', icon: Radio, variant: 'default' },
  6: { label: 'SMS', icon: MessageSquare, variant: 'warning' },
};

const ALL_DAYS = [
  { value: 'Monday', label: 'Pazartesi' },
  { value: 'Tuesday', label: 'Salı' },
  { value: 'Wednesday', label: 'Çarşamba' },
  { value: 'Thursday', label: 'Perşembe' },
  { value: 'Friday', label: 'Cuma' },
  { value: 'Saturday', label: 'Cumartesi' },
  { value: 'Sunday', label: 'Pazar' },
];

const COMMON_TIMEZONES = [
  { value: 'Europe/Istanbul', label: 'Türkiye (UTC+3)' },
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
  const isTenant = session != null;
  const tabs = ALL_TABS.filter(t => !t.tenantOnly || isTenant);
  const [activeTab, setActiveTab] = useState<SettingsTab>('general');

  // Lines state
  const [instances, setInstances] = useState<InstanceDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Sector state
  const [sector, setSector] = useState<string | null>(null);
  const [sectorLoading, setSectorLoading] = useState(false);
  const [sectorSaving, setSectorSaving] = useState(false);
  const [sectorError, setSectorError] = useState<string | null>(null);
  const [sectorSuccess, setSectorSuccess] = useState(false);

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

  const fetchSector = useCallback(async () => {
    setSectorLoading(true);
    setSectorError(null);
    try {
      const result = await api.getSector();
      setSector(result.sector);
    } catch (err) {
      const code = err instanceof ApiClientError ? err.errorCode : '';
      const msg = err instanceof Error ? err.message : String(err);
      setSectorError(`Sektör yüklenemedi${code ? ` [${code}]` : ''}: ${msg}`);
    } finally {
      setSectorLoading(false);
    }
  }, []);

  const handleSaveSector = useCallback(async (newSector: string) => {
    setSectorSaving(true);
    setSectorError(null);
    setSectorSuccess(false);
    try {
      await api.updateSector(newSector);
      setSector(newSector);
      setSectorSuccess(true);
      setTimeout(() => setSectorSuccess(false), 3000);
    } catch (err) {
      const code = err instanceof ApiClientError ? err.errorCode : '';
      const msg = err instanceof Error ? err.message : String(err);
      setSectorError(`Sektör kaydedilemedi${code ? ` [${code}]` : ''}: ${msg}`);
    } finally {
      setSectorSaving(false);
    }
  }, []);

  const fetchInstances = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.getInstances();
      setInstances(result.instances);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      if (msg.includes('WapCRM') || msg.includes('yapilandirilmamis')) {
        setError('WapCRM API anahtarı yapılandırılmamış. Lütfen firma ayarlarınızı kontrol edin.');
      } else {
        setError(`Hat listesi yüklenemedi: ${msg}`);
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
      setWhError(`Çalışma saatleri yüklenemedi: ${msg}`);
    } finally {
      setWhLoading(false);
    }
  }, []);

  useEffect(() => {
    if (session) {
      fetchSector();
      fetchInstances();
      fetchWorkingHours();
    }
  }, [session, fetchSector, fetchInstances, fetchWorkingHours]);

  const handleRefresh = async () => {
    setRefreshing(true);
    setError(null);
    try {
      const result = await api.refreshInstances();
      setInstances(result.instances);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      setError(`WapCRM yenileme başarısız: ${msg}`);
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
      alert(msg.includes('flow') ? 'Bu hat bir akış tarafından kullanılıyor. Önce akıştan çıkarın.' : msg);
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
      setWhError(`Çalışma saatleri kaydedilemedi: ${msg}`);
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
    <div>
      <h1 className="-mx-8 -mt-8 px-8 py-5 mb-6 text-xl font-semibold text-navy-900 flex items-center gap-2 border-b border-navy-100">
        <Settings className="w-5 h-5 text-navy-400" />
        Ayarlar
      </h1>

      <div className="flex items-start gap-6">
        {/* Vertical Tabs */}
        <nav className="w-52 flex-shrink-0">
          <div className="bg-white rounded-xl border border-navy-100 shadow-card p-2 space-y-1">
            {tabs.map(tab => {
              const Icon = tab.icon;
              const isActive = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={cn(
                    'w-full flex items-center gap-2.5 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors text-left',
                    isActive
                      ? 'bg-brand-50 text-brand-700 border border-brand-200'
                      : 'text-navy-500 hover:bg-navy-50 hover:text-navy-700 border border-transparent'
                  )}
                >
                  <Icon className={cn('w-4 h-4 flex-shrink-0', isActive ? 'text-brand-500' : 'text-navy-400')} />
                  {tab.label}
                </button>
              );
            })}
          </div>
        </nav>

        {/* Tab Content */}
        <div className="flex-1 min-w-0">
          {activeTab === 'general' && (
            <GeneralTab
              session={session}
              sector={sector}
              sectorLoading={sectorLoading}
              sectorSaving={sectorSaving}
              sectorError={sectorError}
              sectorSuccess={sectorSuccess}
              onSaveSector={handleSaveSector}
            />
          )}

          {activeTab === 'working-hours' && (
            <WorkingHoursTab
              whLoading={whLoading}
              whSaving={whSaving}
              whError={whError}
              whSuccess={whSuccess}
              whStart={whStart}
              whEnd={whEnd}
              whTimezone={whTimezone}
              whDaysOff={whDaysOff}
              whConfigured={whConfigured}
              setWhStart={setWhStart}
              setWhEnd={setWhEnd}
              setWhTimezone={setWhTimezone}
              toggleDayOff={toggleDayOff}
              onSave={handleSaveWorkingHours}
            />
          )}

          {activeTab === 'lines' && (
            <LinesTab
              instances={instances}
              loading={loading}
              refreshing={refreshing}
              togglingId={togglingId}
              error={error}
              onRefresh={handleRefresh}
              onToggle={handleToggle}
            />
          )}
        </div>
      </div>
    </div>
  );
}

/* ─── General Tab ──────────────────────────────────────────── */

const SECTOR_OPTIONS = [
  { value: 'eticaret', label: 'E-Ticaret' },
  { value: 'dis_klinik', label: 'Diş Kliniği' },
  { value: 'estetik', label: 'Estetik Merkezi' },
  { value: 'saglik', label: 'Sağlık' },
  { value: 'otel', label: 'Otel & Konaklama' },
  { value: 'guzellik', label: 'Güzellik Salonu' },
  { value: 'egitim', label: 'Eğitim' },
  { value: 'mobil', label: 'Mobil Uygulama' },
  { value: 'genel', label: 'Genel' },
];

interface GeneralTabProps {
  session: ReturnType<typeof useAuth>['session'];
  sector: string | null;
  sectorLoading: boolean;
  sectorSaving: boolean;
  sectorError: string | null;
  sectorSuccess: boolean;
  onSaveSector: (sector: string) => void;
}

function GeneralTab({ session, sector, sectorLoading, sectorSaving, sectorError, sectorSuccess, onSaveSector }: GeneralTabProps) {
  const [localSector, setLocalSector] = useState(sector ?? '');

  // Sync when sector loads from API
  useEffect(() => {
    if (sector != null) setLocalSector(sector);
  }, [sector]);

  if (!session) return null;

  const fields = [
    { label: 'Firma ID', value: session.tenantId },
    { label: 'Kullanici', value: session.fullName || session.userId },
    { label: 'Rol', value: session.role },
  ];

  const sectorDirty = localSector !== (sector ?? '');

  return (
    <div className="space-y-4">
      <div>
        <div className="mb-3">
          <h2 className="text-base font-semibold text-navy-900 flex items-center gap-2">
            <Building2 className="w-4 h-4 text-navy-400" />
            Temel Bilgiler
          </h2>
          <p className="text-xs text-navy-400 mt-0.5">Firma ve oturum bilgileri.</p>
        </div>
        <Card>
          <CardContent className="py-5">
            <div className="space-y-4">
              {fields.map(f => (
                <div key={f.label} className="flex items-center">
                  <span className="w-32 text-sm text-navy-400">{f.label}</span>
                  <span className="text-sm font-medium text-navy-800 capitalize">{f.value}</span>
                </div>
              ))}
              {session.inseFeatures.length > 0 && (
                <div className="flex items-center">
                  <span className="w-32 text-sm text-navy-400">Modüller</span>
                  <div className="flex gap-1.5">
                    {session.inseFeatures.map(f => (
                      <Badge key={f} variant="info">{f}</Badge>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Sector picker */}
      <div>
        <div className="mb-3">
          <h2 className="text-base font-semibold text-navy-900 flex items-center gap-2">
            <Factory className="w-4 h-4 text-navy-400" />
            Sektör
          </h2>
          <p className="text-xs text-navy-400 mt-0.5">
            İşletmenizin sektörünü belirleyin. Şablon ve içerik önerileri sektöre göre kişiselleşir.
          </p>
        </div>
        <Card>
          <CardContent className="py-5">
            {sectorError && (
              <div className="p-3 mb-3 bg-red-50 border border-red-100 rounded-xl text-sm text-red-700">
                {sectorError}
              </div>
            )}
            {sectorSuccess && (
              <div className="p-3 mb-3 bg-emerald-50 border border-emerald-100 rounded-xl text-sm text-emerald-700 flex items-center gap-2">
                <Check className="w-4 h-4" />
                Sektör başarıyla kaydedildi.
              </div>
            )}
            {sectorLoading ? (
              <div className="py-4 text-center text-navy-300">Yükleniyor...</div>
            ) : (
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-navy-700 mb-1">Sektör</label>
                  <select
                    value={localSector}
                    onChange={e => setLocalSector(e.target.value)}
                    className="w-full max-w-xs px-3 py-2 border border-navy-100 rounded-lg text-sm focus:ring-2 focus:ring-brand-500 focus:border-brand-500 bg-white"
                  >
                    <option value="">Seçiniz...</option>
                    {SECTOR_OPTIONS.map(s => (
                      <option key={s.value} value={s.value}>{s.label}</option>
                    ))}
                  </select>
                </div>
                <div className="flex justify-end">
                  <Button
                    variant="primary"
                    size="sm"
                    onClick={() => onSaveSector(localSector)}
                    disabled={sectorSaving || !localSector || !sectorDirty}
                  >
                    <Save className="w-4 h-4" />
                    <span>{sectorSaving ? 'Kaydediliyor...' : 'Kaydet'}</span>
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* FEAT-LIW Chunk C: entry card to standalone /settings/lead-intake page. */}
      <div>
        <div className="mb-3">
          <h2 className="text-base font-semibold text-navy-900 flex items-center gap-2">
            <Webhook className="w-4 h-4 text-navy-400" />
            Lead Kaynaklari
          </h2>
          <p className="text-xs text-navy-400 mt-0.5">
            Landing webhook API anahtarı, alan eşlemesi ve dry-run önizlemesi.
          </p>
        </div>
        <Card>
          <CardContent className="py-5">
            <Link
              to="/settings/lead-intake"
              className="flex items-center justify-between gap-3 p-2 -m-2 rounded-lg hover:bg-navy-50 transition-colors"
            >
              <div>
                <div className="text-sm font-medium text-navy-900">Landing Webhook Ayarlari</div>
                <div className="text-xs text-navy-400 mt-0.5">
                  API key yönetimi, alan eşlemesi (field map), dry-run önizlemesi ve değişiklik geçmişi.
                </div>
              </div>
              <ChevronRight className="w-5 h-5 text-navy-400 flex-shrink-0" />
            </Link>
          </CardContent>
        </Card>
      </div>

      {/* FEAT-TFM-UI P3: entry card to standalone /settings/field-mapping page. */}
      <div>
        <div className="mb-3">
          <h2 className="text-base font-semibold text-navy-900 flex items-center gap-2">
            <MapPin className="w-4 h-4 text-navy-400" />
            Field Mapping
          </h2>
          <p className="text-xs text-navy-400 mt-0.5">
            INMA custom field slotlarını (cf1..cf10) semantic isimlere bağla (FlowBuilder + Template editor için).
          </p>
        </div>
        <Card>
          <CardContent className="py-5">
            <Link
              to="/settings/field-mapping"
              className="flex items-center justify-between gap-3 p-2 -m-2 rounded-lg hover:bg-navy-50 transition-colors"
            >
              <div>
                <div className="text-sm font-medium text-navy-900">Tenant Field Mapping Editor</div>
                <div className="text-xs text-navy-400 mt-0.5">
                  10-slot tablo: INMA Label + semantic isim + tip (enum/date/bool/int/string) + enum değerleri.
                </div>
              </div>
              <ChevronRight className="w-5 h-5 text-navy-400 flex-shrink-0" />
            </Link>
          </CardContent>
        </Card>

        {/* FEAT-EFS Drip Sequence: entry card to standalone /settings/followup-sequence page. */}
        <Card>
          <CardContent className="py-5">
            <Link
              to="/settings/followup-sequence"
              className="flex items-center justify-between gap-3 p-2 -m-2 rounded-lg hover:bg-navy-50 transition-colors"
            >
              <div>
                <div className="text-sm font-medium text-navy-900">Follow-Up Sequence Editor</div>
                <div className="text-xs text-navy-400 mt-0.5">
                  N-aşamalı drip nurture: stage liste + delay + template_slug + A/B split (deterministik kontrol grubu).
                </div>
              </div>
              <ChevronRight className="w-5 h-5 text-navy-400 flex-shrink-0" />
            </Link>
          </CardContent>
        </Card>

        {/* FEAT-MCC Multi-City Campaign P6: entry card to standalone /settings/campaigns page. */}
        <Card>
          <CardContent className="py-5">
            <Link
              to="/settings/campaigns"
              className="flex items-center justify-between gap-3 p-2 -m-2 rounded-lg hover:bg-navy-50 transition-colors"
            >
              <div>
                <div className="text-sm font-medium text-navy-900">Multi-City Campaign Config</div>
                <div className="text-xs text-navy-400 mt-0.5">
                  Tenant kampanyaları + şehirler + event tarihleri. {'{{campaign.cities_human}}'} substitution + outbound window guard (INV-BE-119).
                </div>
              </div>
              <ChevronRight className="w-5 h-5 text-navy-400 flex-shrink-0" />
            </Link>
          </CardContent>
        </Card>

        {/* FEAT-CLINIC-METADATA: entry card to standalone /settings/clinic-metadata page. */}
        <Card>
          <CardContent className="py-5">
            <Link
              to="/settings/clinic-metadata"
              className="flex items-center justify-between gap-3 p-2 -m-2 rounded-lg hover:bg-navy-50 transition-colors"
            >
              <div>
                <div className="text-sm font-medium text-navy-900">Klinik Bilgileri</div>
                <div className="text-xs text-navy-400 mt-0.5">
                  Klinik iletişim + ekip üyeleri. {'{{clinic.name}}'} {'{{clinic.phone}}'} {'{{team.dentist.name}}'} substitution. Multi-tenant için hardcoded yerine.
                </div>
              </div>
              <ChevronRight className="w-5 h-5 text-navy-400 flex-shrink-0" />
            </Link>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

/* ─── Working Hours Tab ────────────────────────────────────── */

interface WorkingHoursTabProps {
  whLoading: boolean;
  whSaving: boolean;
  whError: string | null;
  whSuccess: boolean;
  whStart: string;
  whEnd: string;
  whTimezone: string;
  whDaysOff: string[];
  whConfigured: boolean;
  setWhStart: (v: string) => void;
  setWhEnd: (v: string) => void;
  setWhTimezone: (v: string) => void;
  toggleDayOff: (day: string) => void;
  onSave: () => void;
}

function WorkingHoursTab({
  whLoading, whSaving, whError, whSuccess,
  whStart, whEnd, whTimezone, whDaysOff, whConfigured,
  setWhStart, setWhEnd, setWhTimezone, toggleDayOff, onSave,
}: WorkingHoursTabProps) {
  return (
    <div>
      <div className="mb-3">
        <div className="flex items-center gap-2">
          <h2 className="text-base font-semibold text-navy-900 flex items-center gap-2">
            <Clock className="w-4 h-4 text-navy-400" />
            Çalışma Saatleri
          </h2>
          {whConfigured && <Badge variant="success">Aktif</Badge>}
          {!whConfigured && !whLoading && <Badge variant="default">Yapılandırılmamış</Badge>}
        </div>
        <p className="text-xs text-navy-400 mt-0.5">
          Mesai saatlerini belirleyin. Mesai dışında gelen mesajlara otomatik cevap gönderilir.
        </p>
      </div>
      <Card>
        <CardContent className="py-5">
          {whError && (
          <div className="p-3 mb-3 bg-red-50 border border-red-100 rounded-xl text-sm text-red-700">
            {whError}
          </div>
        )}

        {whSuccess && (
          <div className="p-3 mb-3 bg-emerald-50 border border-emerald-100 rounded-xl text-sm text-emerald-700 flex items-center gap-2">
            <Check className="w-4 h-4" />
            Çalışma saatleri başarıyla kaydedildi.
          </div>
        )}

        {whLoading ? (
          <div className="py-8 text-center text-navy-300">
            Yükleniyor...
          </div>
        ) : (
          <div className="space-y-5">
            {/* Time range */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-navy-700 mb-1">Başlangıç Saati</label>
                <input
                  type="time"
                  value={whStart}
                  onChange={(e) => setWhStart(e.target.value)}
                  className="w-full px-3 py-2 border border-navy-100 rounded-lg text-sm focus:ring-2 focus:ring-brand-500 focus:border-brand-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-navy-700 mb-1">Bitiş Saati</label>
                <input
                  type="time"
                  value={whEnd}
                  onChange={(e) => setWhEnd(e.target.value)}
                  className="w-full px-3 py-2 border border-navy-100 rounded-lg text-sm focus:ring-2 focus:ring-brand-500 focus:border-brand-500"
                />
              </div>
            </div>

            {/* Timezone */}
            <div>
              <label className="block text-sm font-medium text-navy-700 mb-1">Saat Dilimi</label>
              <select
                value={whTimezone}
                onChange={(e) => setWhTimezone(e.target.value)}
                className="w-full px-3 py-2 border border-navy-100 rounded-lg text-sm focus:ring-2 focus:ring-brand-500 focus:border-brand-500 bg-white"
              >
                {COMMON_TIMEZONES.map(tz => (
                  <option key={tz.value} value={tz.value}>{tz.label}</option>
                ))}
              </select>
            </div>

            {/* Days off */}
            <div>
              <label className="block text-sm font-medium text-navy-700 mb-2">Kapalı Günler</label>
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
                          : 'bg-navy-50 text-navy-500 border border-navy-100 hover:bg-navy-100'
                      }`}
                    >
                      {day.label}
                    </button>
                  );
                })}
              </div>
              <p className="text-xs text-navy-300 mt-1">
                Kapalı günlerde tüm gün mesai dışı mesajı gönderilir.
              </p>
            </div>

            {/* Save button */}
            <div className="flex justify-end pt-2">
              <Button
                variant="primary"
                size="sm"
                onClick={onSave}
                disabled={whSaving}
              >
                <Save className="w-4 h-4" />
                <span>{whSaving ? 'Kaydediliyor...' : 'Kaydet'}</span>
              </Button>
            </div>
          </div>
        )}
        </CardContent>
      </Card>
    </div>
  );
}

/* ─── Lines Tab ────────────────────────────────────────────── */

interface LinesTabProps {
  instances: InstanceDto[];
  loading: boolean;
  refreshing: boolean;
  togglingId: string | null;
  error: string | null;
  onRefresh: () => void;
  onToggle: (inst: InstanceDto) => void;
}

function LinesTab({ instances, loading, refreshing, togglingId, error, onRefresh, onToggle }: LinesTabProps) {
  return (
    <div>
      <div className="flex items-center justify-between mb-3">
        <div>
          <h2 className="text-base font-semibold text-navy-900 flex items-center gap-2">
            <Phone className="w-4 h-4 text-navy-400" />
            Hatlar
          </h2>
          <p className="text-xs text-navy-400 mt-0.5">
            Mesaj alınacak hatları yönetin. Kapalı hatlardan gelen mesajlar yoksayılır.
            {instances.length > 0 && <span className="ml-1">({instances.length} hat)</span>}
          </p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          onClick={onRefresh}
          disabled={refreshing || loading}
        >
          <RefreshCw className={`w-4 h-4 flex-shrink-0 ${refreshing ? 'animate-spin' : ''}`} />
          <span>WapCRM&apos;den Yenile</span>
        </Button>
      </div>
      <Card className="overflow-hidden">
        <CardContent className="py-5">
          {error && (
          <div className="p-3 mb-3 bg-amber-50 border border-amber-100 rounded-xl text-sm text-amber-700">
            {error}
          </div>
        )}

        <div className="overflow-x-auto -mx-5">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-navy-50 border-b border-navy-100">
                <th className="text-left px-4 py-2.5 font-medium text-navy-500 w-12">Durum</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Hat Adı</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Numara</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Tip</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Akış</th>
                <th className="text-center px-4 py-2.5 font-medium text-navy-500 w-16">Aktif</th>
              </tr>
            </thead>
            <tbody>
              {loading && instances.length === 0 ? (
                <tr>
                  <td colSpan={6} className="text-center py-12 text-navy-300">
                    Hatlar yükleniyor...
                  </td>
                </tr>
              ) : instances.length === 0 ? (
                <tr>
                  <td colSpan={6} className="text-center py-12 text-navy-300">
                    {error ? 'Hat bulunamadı' : 'Henüz hat eklenmemiş. WapCRM\'den yenileyin.'}
                  </td>
                </tr>
              ) : (
                instances.map(inst => {
                  const typeInfo = INSTANCE_TYPE_LABELS[inst.instanceType] || INSTANCE_TYPE_LABELS[1];
                  const TypeIcon = typeInfo.icon;
                  const isFlowAssigned = inst.flowId != null;
                  const isToggling = togglingId === inst.instanceId;

                  return (
                    <tr key={inst.instanceId} className="border-b border-navy-100 hover:bg-navy-50/50">
                      <td className="px-4 py-2.5 text-center">
                        {inst.isEnabled ? (
                          <Wifi className="w-4 h-4 text-emerald-500 inline" />
                        ) : (
                          <WifiOff className="w-4 h-4 text-navy-200 inline" />
                        )}
                      </td>
                      <td className="px-4 py-2.5">
                        <span
                          className="font-medium text-navy-900 cursor-default"
                          title={`Instance ID: ${inst.instanceId}`}
                        >
                          {inst.instanceName}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-xs text-navy-400 font-mono">
                        {inst.account || '-'}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="inline-flex items-center gap-1">
                          <TypeIcon className="w-3 h-3" />
                          <Badge variant={typeInfo.variant}>{typeInfo.label}</Badge>
                        </span>
                      </td>
                      <td className="px-4 py-2.5">
                        {inst.flowName ? (
                          <Badge variant="info">{inst.flowName}</Badge>
                        ) : (
                          <span className="text-xs text-navy-200">Atanmamış</span>
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-center">
                        <input
                          type="checkbox"
                          checked={inst.isEnabled}
                          disabled={isToggling || (isFlowAssigned && inst.isEnabled)}
                          onChange={() => onToggle(inst)}
                          className="w-4 h-4 rounded border-navy-200 text-brand-600 focus:ring-brand-500 disabled:opacity-50 disabled:cursor-not-allowed"
                          title={
                            isFlowAssigned && inst.isEnabled
                              ? `"${inst.flowName}" akışında kullanılıyor — önce akıştan çıkarın`
                              : inst.isEnabled ? 'Kapat' : 'Aç'
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
        </CardContent>
      </Card>
    </div>
  );
}
