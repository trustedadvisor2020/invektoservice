import { useEffect, useMemo, useState } from 'react';
import { useCampaignConfig } from '../../hooks/useCampaignConfig';
import type {
  CampaignCityDraft,
  CampaignConfigDraft,
  CampaignConfigDto,
  CampaignDateDraft,
  CampaignEntryDraft,
  CampaignEntryDto,
} from '../../types/campaignConfig';

// FEAT-MCC Multi-City Campaign — Dashboard editor page mounted at /settings/campaigns.
//
// MVP scope: array-of-campaigns editor (interview Q5 chose array shape). Operator
// can add up to MAX_CAMPAIGNS rows; each row exposes slug, name, active toggle,
// start/end dates, cities[], dates[]. Save is atomic — full config UPSERT.
//
// Disabled UI guard (lessons 2026-04-22 P3 CQ9 FAIL): inputs are NOT disabled when
// the upstream load fails — operators retain access to their data even if Backend
// is transiently unreachable. They are only disabled DURING save.

const MAX_CAMPAIGNS = 8;
const MAX_CITIES = 20;
const MAX_DATES = 20;
const RESERVED_SLUGS = new Set(['primary', 'system', 'default', 'all']);

function cryptoRandomId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function emptyCity(): CampaignCityDraft {
  return { draftId: cryptoRandomId(), slug: '', name: '', country: '', timezone: '', rowError: null };
}

function emptyDate(): CampaignDateDraft {
  return { draftId: cryptoRandomId(), city: '', date: '', hours: '', rowError: null };
}

function emptyCampaign(): CampaignEntryDraft {
  return {
    draftId: cryptoRandomId(),
    slug: '',
    name: '',
    active: true,
    startDate: '',
    endDate: '',
    cities: [emptyCity()],
    dates: [],
    rowError: null,
  };
}

function toDraft(config: CampaignConfigDto | null): CampaignConfigDraft {
  if (!config || !config.campaigns || config.campaigns.length === 0) {
    return { campaigns: [], formError: null };
  }
  return {
    formError: null,
    campaigns: config.campaigns.map((c: CampaignEntryDto) => ({
      draftId: cryptoRandomId(),
      slug: c.slug,
      name: c.name,
      active: c.active,
      startDate: c.start_date,
      endDate: c.end_date,
      cities: (c.cities ?? []).map((city) => ({
        draftId: cryptoRandomId(),
        slug: city.slug,
        name: city.name,
        country: city.country ?? '',
        timezone: city.timezone ?? '',
        rowError: null,
      })),
      dates: (c.dates ?? []).map((d) => ({
        draftId: cryptoRandomId(),
        city: d.city,
        date: d.date,
        hours: d.hours ?? '',
        rowError: null,
      })),
      rowError: null,
    })),
  };
}

interface ConvertOk { ok: true; config: CampaignConfigDto; }
interface ConvertFail { ok: false; draft: CampaignConfigDraft; }

function validateAndConvert(draft: CampaignConfigDraft): ConvertOk | ConvertFail {
  const next: CampaignConfigDraft = {
    formError: null,
    campaigns: draft.campaigns.map((c) => ({
      ...c,
      rowError: null,
      cities: c.cities.map((x) => ({ ...x, rowError: null })),
      dates: c.dates.map((x) => ({ ...x, rowError: null })),
    })),
  };

  if (draft.campaigns.length > MAX_CAMPAIGNS) {
    next.formError = `Cap asimi: max ${MAX_CAMPAIGNS} kampanya. Mevcut ${draft.campaigns.length}.`;
    return { ok: false, draft: next };
  }

  const slugSeen = new Set<string>();
  let anyFail = false;

  next.campaigns = draft.campaigns.map((c) => {
    const out: CampaignEntryDraft = {
      ...c,
      rowError: null,
      cities: c.cities.map((x) => ({ ...x, rowError: null })),
      dates: c.dates.map((x) => ({ ...x, rowError: null })),
    };

    if (!/^[a-z][a-z0-9_-]{1,63}$/.test(c.slug)) {
      out.rowError = 'Slug gecersiz: kucuk harfle baslamali, [a-z0-9_-], 2-64 karakter.';
      anyFail = true;
      return out;
    }
    if (RESERVED_SLUGS.has(c.slug.toLowerCase())) {
      out.rowError = `Slug '${c.slug}' sistem rezerv kelimesi (primary/system/default/all).`;
      anyFail = true;
      return out;
    }
    if (slugSeen.has(c.slug.toLowerCase())) {
      out.rowError = `Ayni slug '${c.slug}' birden fazla kampanyada kullanilmis.`;
      anyFail = true;
      return out;
    }
    slugSeen.add(c.slug.toLowerCase());

    if (!c.name.trim()) {
      out.rowError = 'Kampanya adi bos olamaz.';
      anyFail = true;
      return out;
    }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(c.startDate) || !/^\d{4}-\d{2}-\d{2}$/.test(c.endDate)) {
      out.rowError = 'start_date / end_date YYYY-MM-DD formatinda olmali.';
      anyFail = true;
      return out;
    }
    if (c.endDate < c.startDate) {
      out.rowError = 'end_date start_date\'ten once olamaz.';
      anyFail = true;
      return out;
    }

    if (c.cities.length === 0) {
      out.rowError = 'Kampanyada en az bir sehir olmali.';
      anyFail = true;
      return out;
    }
    if (c.cities.length > MAX_CITIES) {
      out.rowError = `Cap asimi: max ${MAX_CITIES} sehir.`;
      anyFail = true;
      return out;
    }

    const citySlugSeen = new Set<string>();
    out.cities = c.cities.map((city) => {
      if (!/^[a-z][a-z0-9_-]{0,39}$/.test(city.slug)) {
        anyFail = true;
        return { ...city, rowError: 'City slug gecersiz: kucuk harfle baslamali, [a-z0-9_-], max 40 karakter.' };
      }
      if (citySlugSeen.has(city.slug.toLowerCase())) {
        anyFail = true;
        return { ...city, rowError: `Ayni city slug '${city.slug}' birden fazla.` };
      }
      citySlugSeen.add(city.slug.toLowerCase());
      if (!city.name.trim()) {
        anyFail = true;
        return { ...city, rowError: 'City adi bos olamaz.' };
      }
      return city;
    });

    if (c.dates.length > MAX_DATES) {
      out.rowError = `Cap asimi: max ${MAX_DATES} tarih.`;
      anyFail = true;
      return out;
    }
    out.dates = c.dates.map((d) => {
      if (!citySlugSeen.has(d.city.toLowerCase())) {
        anyFail = true;
        return { ...d, rowError: `dates.city '${d.city}' cities[] icinde yok.` };
      }
      if (!/^\d{4}-\d{2}-\d{2}$/.test(d.date)) {
        anyFail = true;
        return { ...d, rowError: 'date YYYY-MM-DD olmali.' };
      }
      if (d.hours.length > 64) {
        anyFail = true;
        return { ...d, rowError: 'hours 64 karakterden uzun olamaz.' };
      }
      return d;
    });

    return out;
  });

  if (anyFail) return { ok: false, draft: next };

  const config: CampaignConfigDto = {
    campaigns: next.campaigns.map((c) => ({
      slug: c.slug,
      name: c.name,
      active: c.active,
      start_date: c.startDate,
      end_date: c.endDate,
      cities: c.cities.map((city) => ({
        slug: city.slug,
        name: city.name,
        country: city.country.trim() || null,
        timezone: city.timezone.trim() || null,
      })),
      dates: c.dates.map((d) => ({
        city: d.city,
        date: d.date,
        hours: d.hours.trim() || null,
      })),
    })),
  };
  return { ok: true, config };
}

export function CampaignConfigSettingsPage() {
  const { config, updatedAt, isLoading, error, save } = useCampaignConfig(true);
  const [draft, setDraft] = useState<CampaignConfigDraft>({ campaigns: [], formError: null });
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savedAt, setSavedAt] = useState<string | null>(null);

  useEffect(() => {
    setDraft(toDraft(config));
  }, [config]);

  const updateCampaign = (idx: number, mut: (c: CampaignEntryDraft) => CampaignEntryDraft) => {
    setDraft((d) => ({ ...d, campaigns: d.campaigns.map((c, i) => (i === idx ? mut(c) : c)) }));
  };

  const addCampaign = () => {
    setDraft((d) => ({ ...d, campaigns: [...d.campaigns, emptyCampaign()] }));
  };

  const removeCampaign = (idx: number) => {
    setDraft((d) => ({ ...d, campaigns: d.campaigns.filter((_, i) => i !== idx) }));
  };

  const handleSave = async () => {
    const result = validateAndConvert(draft);
    if (!result.ok) {
      setDraft(result.draft);
      return;
    }
    setIsSaving(true);
    setSaveError(null);
    try {
      await save(result.config);
      setSavedAt(new Date().toISOString());
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'Kayit basarisiz.');
    } finally {
      setIsSaving(false);
    }
  };

  const totalCampaigns = useMemo(() => draft.campaigns.length, [draft.campaigns]);

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="mb-6">
        <h1 className="text-2xl font-semibold mb-2">Multi-City Campaign Config</h1>
        <p className="text-sm text-gray-600">
          Tenant kampanyalarini, sehirlerini ve event tarihlerini yonetin. Flow icindeki{' '}
          <code className="bg-gray-100 px-1 rounded">{'{{campaign.cities_human}}'}</code>,{' '}
          <code className="bg-gray-100 px-1 rounded">{'{{campaign.event_date}}'}</code> gibi
          placeholder'lar bu kayittan beslenir. Aktif window disinda outbound dispatch
          otomatik suspend edilir (INV-BE-119).
        </p>
        {updatedAt && (
          <p className="text-xs text-gray-500 mt-2">Son guncelleme: {new Date(updatedAt).toLocaleString()}</p>
        )}
      </div>

      {isLoading && <div className="text-sm text-gray-500 mb-4">Yukleniyor...</div>}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded p-3 mb-4 text-sm text-red-800">
          {error.message}
        </div>
      )}

      {draft.formError && (
        <div className="bg-amber-50 border border-amber-200 rounded p-3 mb-4 text-sm text-amber-800">
          {draft.formError}
        </div>
      )}

      <div className="space-y-6">
        {draft.campaigns.map((campaign, idx) => (
          <CampaignCard
            key={campaign.draftId}
            index={idx}
            campaign={campaign}
            onChange={(mut) => updateCampaign(idx, mut)}
            onRemove={() => removeCampaign(idx)}
            disabled={isSaving}
          />
        ))}
      </div>

      <div className="flex items-center gap-3 mt-6">
        <button
          type="button"
          onClick={addCampaign}
          disabled={isSaving || totalCampaigns >= MAX_CAMPAIGNS}
          className="px-4 py-2 bg-gray-100 hover:bg-gray-200 rounded text-sm disabled:opacity-50"
        >
          + Kampanya Ekle ({totalCampaigns}/{MAX_CAMPAIGNS})
        </button>

        <button
          type="button"
          onClick={handleSave}
          disabled={isSaving}
          className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded text-sm disabled:opacity-50"
        >
          {isSaving ? 'Kaydediliyor...' : 'Kaydet'}
        </button>

        {savedAt && !saveError && (
          <span className="text-sm text-green-700">Kaydedildi: {new Date(savedAt).toLocaleTimeString()}</span>
        )}
      </div>

      {saveError && (
        <div className="bg-red-50 border border-red-200 rounded p-3 mt-4 text-sm text-red-800">
          {saveError}
        </div>
      )}
    </div>
  );
}

interface CampaignCardProps {
  index: number;
  campaign: CampaignEntryDraft;
  onChange: (mut: (c: CampaignEntryDraft) => CampaignEntryDraft) => void;
  onRemove: () => void;
  disabled: boolean;
}

function CampaignCard({ index, campaign, onChange, onRemove, disabled }: CampaignCardProps) {
  const update = (patch: Partial<CampaignEntryDraft>) => onChange((c) => ({ ...c, ...patch }));

  const updateCity = (cityIdx: number, patch: Partial<CampaignCityDraft>) =>
    onChange((c) => ({ ...c, cities: c.cities.map((x, i) => (i === cityIdx ? { ...x, ...patch } : x)) }));

  const addCity = () => onChange((c) => ({ ...c, cities: [...c.cities, emptyCity()] }));
  const removeCity = (cityIdx: number) =>
    onChange((c) => ({ ...c, cities: c.cities.filter((_, i) => i !== cityIdx) }));

  const updateDate = (dateIdx: number, patch: Partial<CampaignDateDraft>) =>
    onChange((c) => ({ ...c, dates: c.dates.map((x, i) => (i === dateIdx ? { ...x, ...patch } : x)) }));

  const addDate = () => onChange((c) => ({ ...c, dates: [...c.dates, emptyDate()] }));
  const removeDate = (dateIdx: number) =>
    onChange((c) => ({ ...c, dates: c.dates.filter((_, i) => i !== dateIdx) }));

  return (
    <div className="border border-gray-200 rounded-lg p-4 bg-white shadow-sm">
      <div className="flex items-center justify-between mb-4">
        <h2 className="font-semibold text-lg">Kampanya {index + 1}</h2>
        <button
          type="button"
          onClick={onRemove}
          disabled={disabled}
          aria-label="Kampanyayi sil"
          className="text-gray-400 hover:text-red-600 text-xl leading-none"
        >
          ×
        </button>
      </div>

      {campaign.rowError && (
        <div className="bg-red-50 border border-red-200 rounded p-2 mb-3 text-sm text-red-800">
          {campaign.rowError}
        </div>
      )}

      <div className="grid grid-cols-2 gap-3 mb-4">
        <label className="text-sm">
          <span className="block mb-1 text-gray-700">Slug</span>
          <input
            type="text"
            value={campaign.slug}
            onChange={(e) => update({ slug: e.target.value })}
            disabled={disabled}
            placeholder="roadshow_ireland_2026"
            className="w-full border border-gray-300 rounded px-2 py-1 text-sm font-mono"
          />
        </label>
        <label className="text-sm">
          <span className="block mb-1 text-gray-700">Ad</span>
          <input
            type="text"
            value={campaign.name}
            onChange={(e) => update({ name: e.target.value })}
            disabled={disabled}
            placeholder="Ireland Roadshow 2026"
            className="w-full border border-gray-300 rounded px-2 py-1 text-sm"
          />
        </label>
        <label className="text-sm">
          <span className="block mb-1 text-gray-700">Baslangic</span>
          <input
            type="date"
            value={campaign.startDate}
            onChange={(e) => update({ startDate: e.target.value })}
            disabled={disabled}
            className="w-full border border-gray-300 rounded px-2 py-1 text-sm"
          />
        </label>
        <label className="text-sm">
          <span className="block mb-1 text-gray-700">Bitis</span>
          <input
            type="date"
            value={campaign.endDate}
            onChange={(e) => update({ endDate: e.target.value })}
            disabled={disabled}
            className="w-full border border-gray-300 rounded px-2 py-1 text-sm"
          />
        </label>
      </div>

      <label className="flex items-center gap-2 mb-4 text-sm">
        <input
          type="checkbox"
          checked={campaign.active}
          onChange={(e) => update({ active: e.target.checked })}
          disabled={disabled}
        />
        <span>Aktif (window disindaki dispatch'i suspend eder)</span>
      </label>

      <div className="mb-4">
        <h3 className="font-medium mb-2 text-sm">Sehirler ({campaign.cities.length}/{MAX_CITIES})</h3>
        {campaign.cities.map((city, cityIdx) => (
          <div key={city.draftId} className="flex gap-2 mb-2">
            <input
              type="text"
              value={city.slug}
              onChange={(e) => updateCity(cityIdx, { slug: e.target.value })}
              disabled={disabled}
              placeholder="dublin"
              className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm font-mono"
            />
            <input
              type="text"
              value={city.name}
              onChange={(e) => updateCity(cityIdx, { name: e.target.value })}
              disabled={disabled}
              placeholder="Dublin"
              className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm"
            />
            <input
              type="text"
              value={city.country}
              onChange={(e) => updateCity(cityIdx, { country: e.target.value })}
              disabled={disabled}
              placeholder="IE"
              className="w-16 border border-gray-300 rounded px-2 py-1 text-sm"
            />
            <input
              type="text"
              value={city.timezone}
              onChange={(e) => updateCity(cityIdx, { timezone: e.target.value })}
              disabled={disabled}
              placeholder="Europe/Dublin"
              className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm"
            />
            <button
              type="button"
              onClick={() => removeCity(cityIdx)}
              disabled={disabled}
              aria-label="Sehri sil"
              className="text-gray-400 hover:text-red-600 px-2"
            >
              ×
            </button>
          </div>
        ))}
        {campaign.cities.some((c) => c.rowError) && (
          <ul className="text-xs text-red-700 mt-1">
            {campaign.cities
              .filter((c) => c.rowError)
              .map((c) => (
                <li key={c.draftId}>• {c.rowError}</li>
              ))}
          </ul>
        )}
        <button
          type="button"
          onClick={addCity}
          disabled={disabled || campaign.cities.length >= MAX_CITIES}
          className="text-sm text-blue-600 hover:underline disabled:opacity-50"
        >
          + Sehir Ekle
        </button>
      </div>

      <div>
        <h3 className="font-medium mb-2 text-sm">Tarihler ({campaign.dates.length}/{MAX_DATES})</h3>
        {campaign.dates.map((d, dateIdx) => (
          <div key={d.draftId} className="flex gap-2 mb-2">
            <select
              value={d.city}
              onChange={(e) => updateDate(dateIdx, { city: e.target.value })}
              disabled={disabled}
              className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm"
            >
              <option value="">— sehir sec —</option>
              {campaign.cities.map((c) => (
                <option key={c.draftId} value={c.slug}>
                  {c.name || c.slug}
                </option>
              ))}
            </select>
            <input
              type="date"
              value={d.date}
              onChange={(e) => updateDate(dateIdx, { date: e.target.value })}
              disabled={disabled}
              className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm"
            />
            <input
              type="text"
              value={d.hours}
              onChange={(e) => updateDate(dateIdx, { hours: e.target.value })}
              disabled={disabled}
              placeholder="09:00-18:00"
              className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm"
            />
            <button
              type="button"
              onClick={() => removeDate(dateIdx)}
              disabled={disabled}
              aria-label="Tarihi sil"
              className="text-gray-400 hover:text-red-600 px-2"
            >
              ×
            </button>
          </div>
        ))}
        {campaign.dates.some((d) => d.rowError) && (
          <ul className="text-xs text-red-700 mt-1">
            {campaign.dates
              .filter((d) => d.rowError)
              .map((d) => (
                <li key={d.draftId}>• {d.rowError}</li>
              ))}
          </ul>
        )}
        <button
          type="button"
          onClick={addDate}
          disabled={disabled || campaign.dates.length >= MAX_DATES}
          className="text-sm text-blue-600 hover:underline disabled:opacity-50"
        >
          + Tarih Ekle
        </button>
      </div>
    </div>
  );
}
