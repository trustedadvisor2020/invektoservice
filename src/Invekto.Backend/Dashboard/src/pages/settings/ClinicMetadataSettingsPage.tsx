import { useEffect, useState } from 'react';
import {
  api,
  ApiClientError,
  type ClinicContactDto,
  type ClinicMetadataGetResponse,
  type ClinicMetadataPutRequest,
  type TeamMemberDraft,
  type TeamMemberDto,
} from '../../lib/api';

// FEAT-CLINIC-METADATA Dashboard — /settings/clinic-metadata editor.
//
// Iki bölüm: (1) klinik iletişim bilgileri (5 metin alanı + 2 ek alan), (2) ekip üyeleri
// CRUD listesi (drag-handle / yukarı-aşağı butonları ile sıralama, ilk satır "primary"
// olarak ClinicTemplateApplier first-match rule ile değerlendirilir).
//
// Validation: Backend INV-BE-122/123/124 hatalarını ApiClientError üzerinden gösterir;
// client-side ön kontrol yok (Codex iter 0 sade tutmak için), backend validasyonu
// canonical truth.

const LANGUAGE_OPTIONS = [
  { value: '', label: '— dil seç —' },
  { value: 'en', label: 'English' },
  { value: 'tr', label: 'Türkçe' },
  { value: 'ar', label: 'العربية' },
  { value: 'de', label: 'Deutsch' },
  { value: 'fr', label: 'Français' },
  { value: 'es', label: 'Español' },
  { value: 'it', label: 'Italiano' },
  { value: 'ru', label: 'Русский' },
];

const ROLE_SUGGESTIONS = ['dentist', 'receptionist', 'coordinator', 'manager', 'assistant', 'doctor'];

function draftId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function toDrafts(team: TeamMemberDto[] | null | undefined): TeamMemberDraft[] {
  if (!team) return [];
  return team.map(m => ({
    draftId: draftId(),
    role: m.role ?? '',
    name: m.name ?? '',
    title: m.title ?? '',
    language: m.language ?? '',
    pronouns: m.pronouns ?? '',
    rowError: null,
  }));
}

function fromDrafts(drafts: TeamMemberDraft[]): TeamMemberDto[] {
  return drafts.map(d => ({
    role: d.role.trim(),
    name: d.name.trim(),
    title: d.title.trim() || null,
    language: d.language.trim() || null,
    pronouns: d.pronouns.trim() || null,
  }));
}

export function ClinicMetadataSettingsPage() {
  const [loaded, setLoaded] = useState<ClinicMetadataGetResponse | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Contact form fields (operator input)
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [website, setWebsite] = useState('');
  const [instagram, setInstagram] = useState('');
  const [facebook, setFacebook] = useState('');
  const [address, setAddress] = useState('');
  const [city, setCity] = useState('');

  // Team draft list (CRUD)
  const [teamDrafts, setTeamDrafts] = useState<TeamMemberDraft[]>([]);

  // Ephemeral UI state
  const [saving, setSaving] = useState(false);
  const [saveToast, setSaveToast] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  // Load metadata on mount.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const resp = await api.getClinicMetadata();
        if (cancelled) return;
        setLoaded(resp);
        const c = resp.data.clinic_contact ?? {};
        setName(c.name ?? '');
        setPhone(c.phone ?? '');
        setWebsite(c.website ?? '');
        setInstagram(c.instagram ?? '');
        setFacebook(c.facebook ?? '');
        setAddress(c.address ?? '');
        setCity(c.city ?? '');
        setTeamDrafts(toDrafts(resp.data.team_members));
        setLoadError(null);
      } catch (err) {
        if (cancelled) return;
        const msg = err instanceof ApiClientError
          ? `[${err.errorCode ?? 'INV-BE-001'}] ${err.message}`
          : '[INV-BE-001] Ayarlar yüklenemedi — Backend geçici olarak ulaşılamıyor.';
        setLoadError(msg);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  // ── Team CRUD handlers ─────────────────────────────────────────────────────

  function addTeamMember() {
    setTeamDrafts(prev => [
      ...prev,
      { draftId: draftId(), role: '', name: '', title: '', language: '', pronouns: '', rowError: null },
    ]);
  }

  function removeTeamMember(id: string) {
    setTeamDrafts(prev => prev.filter(d => d.draftId !== id));
  }

  function updateTeamMember(id: string, patch: Partial<TeamMemberDraft>) {
    setTeamDrafts(prev => prev.map(d => (d.draftId === id ? { ...d, ...patch } : d)));
  }

  function moveTeamMember(id: string, direction: -1 | 1) {
    setTeamDrafts(prev => {
      const idx = prev.findIndex(d => d.draftId === id);
      if (idx < 0) return prev;
      const target = idx + direction;
      if (target < 0 || target >= prev.length) return prev;
      const copy = [...prev];
      [copy[idx], copy[target]] = [copy[target], copy[idx]];
      return copy;
    });
  }

  // ── Save ───────────────────────────────────────────────────────────────────

  async function handleSave() {
    setSaving(true);
    setSaveToast(null);
    setSaveError(null);
    try {
      const contact: ClinicContactDto = {
        name:      name.trim()      || null,
        phone:     phone.trim()     || null,
        website:   website.trim()   || null,
        instagram: instagram.trim() || null,
        facebook:  facebook.trim()  || null,
        address:   address.trim()   || null,
        city:      city.trim()      || null,
      };
      const req: ClinicMetadataPutRequest = {
        clinic_contact: contact,
        team_members: fromDrafts(teamDrafts),
      };
      const resp = await api.putClinicMetadata(req);
      setSaveToast(`Klinik bilgileri kaydedildi (updated_at: ${resp.data.updated_at ?? '-'}). Yeni mesajlar 5 dakika içinde güncel veriyi kullanır (cache TTL).`);
      setLoaded(resp);
    } catch (err) {
      setSaveError(err instanceof ApiClientError
        ? `[${err.errorCode ?? 'INV-BE-001'}] ${err.message}`
        : '[INV-BE-001] Kaydetme başarısız — Backend geçici olarak ulaşılamıyor, tekrar deneyin.');
    } finally {
      setSaving(false);
    }
  }

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="settings-page">
      <h1>Klinik Bilgileri</h1>
      <p className="settings-intro">
        WhatsApp şablon ve FAQ metinlerinde kullanılan klinik iletişim bilgileri ve ekip üyeleri.
        Bu sayfada doldurulan değerler <code>{`{{clinic.X}}`}</code> ve <code>{`{{team.role.field}}`}</code>{' '}
        placeholder'ları ile otomatik render edilir. Değişiklikler sonraki gönderimlerde 5 dakika içinde
        etkili olur (Backend instance cache anlık temizlenir, peer servisler 5dk TTL ile senkronize olur).
      </p>

      {loadError && (
        <div className="alert alert-error" role="alert">
          {loadError}
        </div>
      )}

      {/* ── Klinik İletişim ─────────────────────────────────────────────────── */}
      <section className="settings-section">
        <h2>Klinik İletişim</h2>

        <div className="form-row">
          <label>
            <span>Klinik Adı</span>
            <input
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="Örn. Dent Adavista Dental Clinic"
              maxLength={120}
            />
            <small>{`{{clinic.name}}`} placeholder ile render edilir.</small>
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Telefon</span>
            <input
              type="tel"
              value={phone}
              onChange={e => setPhone(e.target.value)}
              placeholder="+90 545 123 4567"
              maxLength={32}
            />
            <small>E.164 formatı (+ ile başlayan, rakam ve boşluk). Örn. <code>+90 545 343 09 09</code>.</small>
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Web Sitesi</span>
            <input
              type="url"
              value={website}
              onChange={e => setWebsite(e.target.value)}
              placeholder="https://klinikadi.com"
              maxLength={256}
            />
            <small>HTTPS URL.</small>
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Instagram</span>
            <input
              type="url"
              value={instagram}
              onChange={e => setInstagram(e.target.value)}
              placeholder="https://www.instagram.com/klinik"
              maxLength={256}
            />
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Facebook</span>
            <input
              type="url"
              value={facebook}
              onChange={e => setFacebook(e.target.value)}
              placeholder="https://www.facebook.com/klinik"
              maxLength={256}
            />
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Şehir</span>
            <input
              type="text"
              value={city}
              onChange={e => setCity(e.target.value)}
              placeholder="Örn. Kuşadası"
              maxLength={64}
            />
            <small>Mesaj içinde kısa şehir referansı için (<code>{`{{clinic.city}}`}</code>).</small>
          </label>
        </div>

        <div className="form-row">
          <label>
            <span>Adres</span>
            <textarea
              value={address}
              onChange={e => setAddress(e.target.value)}
              placeholder="Örn. Kuşadası, Aydın, Türkiye"
              rows={2}
              maxLength={256}
            />
          </label>
        </div>
      </section>

      {/* ── Ekip Üyeleri ───────────────────────────────────────────────────── */}
      <section className="settings-section">
        <h2>Ekip Üyeleri</h2>
        <p className="hint">
          İlk satır her rol için "primary" kabul edilir — örn. <code>{`{{team.dentist.name}}`}</code>{' '}
          aynı role sahip ilk üyenin adını döner. Sıralamayı değiştirmek için yukarı/aşağı butonları kullanın.
        </p>

        {teamDrafts.length === 0 && (
          <p className="empty-state">Henüz ekip üyesi tanımlı değil. "Ekle" ile yeni üye ekleyin.</p>
        )}

        {teamDrafts.map((d, idx) => (
          <div key={d.draftId} className="team-row">
            <div className="team-row-header">
              <strong>#{idx + 1}</strong>
              <div className="team-row-actions">
                <button
                  type="button"
                  onClick={() => moveTeamMember(d.draftId, -1)}
                  disabled={idx === 0}
                  title="Yukarı"
                >↑</button>
                <button
                  type="button"
                  onClick={() => moveTeamMember(d.draftId, 1)}
                  disabled={idx === teamDrafts.length - 1}
                  title="Aşağı"
                >↓</button>
                <button
                  type="button"
                  onClick={() => removeTeamMember(d.draftId)}
                  title="Sil"
                  aria-label="Üyeyi sil"
                >✕</button>
              </div>
            </div>

            <div className="form-grid">
              <label>
                <span>Rol</span>
                <input
                  type="text"
                  list={`role-suggestions-${d.draftId}`}
                  value={d.role}
                  onChange={e => updateTeamMember(d.draftId, { role: e.target.value })}
                  placeholder="dentist, receptionist, coordinator, ..."
                  maxLength={32}
                />
                <datalist id={`role-suggestions-${d.draftId}`}>
                  {ROLE_SUGGESTIONS.map(r => <option key={r} value={r} />)}
                </datalist>
              </label>

              <label>
                <span>İsim</span>
                <input
                  type="text"
                  value={d.name}
                  onChange={e => updateTeamMember(d.draftId, { name: e.target.value })}
                  placeholder="Örn. Dr. Özge Yılmazoğlu"
                  maxLength={120}
                />
              </label>

              <label>
                <span>Unvan</span>
                <input
                  type="text"
                  value={d.title}
                  onChange={e => updateTeamMember(d.draftId, { title: e.target.value })}
                  placeholder="Örn. Founder Dentist"
                  maxLength={80}
                />
              </label>

              <label>
                <span>Dil</span>
                <select
                  value={d.language}
                  onChange={e => updateTeamMember(d.draftId, { language: e.target.value })}
                >
                  {LANGUAGE_OPTIONS.map(o => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </label>

              <label>
                <span>Zamir</span>
                <input
                  type="text"
                  value={d.pronouns}
                  onChange={e => updateTeamMember(d.draftId, { pronouns: e.target.value })}
                  placeholder="she/her, he/him, they/them"
                  maxLength={32}
                />
              </label>
            </div>
          </div>
        ))}

        <button type="button" className="btn-secondary" onClick={addTeamMember}>
          + Üye Ekle
        </button>
      </section>

      {/* ── Save ────────────────────────────────────────────────────────────── */}
      <div className="settings-actions">
        <button
          type="button"
          className="btn-primary"
          onClick={handleSave}
          disabled={saving}
        >
          {saving ? 'Kaydediliyor...' : 'Kaydet'}
        </button>
        {loaded?.data.updated_at && (
          <span className="updated-at">Son güncelleme: {loaded.data.updated_at}</span>
        )}
      </div>

      {saveToast && (
        <div className="alert alert-success" role="status">
          {saveToast}
        </div>
      )}
      {saveError && (
        <div className="alert alert-error" role="alert">
          {saveError}
        </div>
      )}
    </div>
  );
}
