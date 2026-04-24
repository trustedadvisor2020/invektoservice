import { useEffect, useState } from 'react';
import {
  api,
  ApiClientError,
  type MetaLeadgenConfigGetResponse,
  type MetaLeadgenConfigPutRequest,
  type MetaLeadgenDiscoverFormsResponse,
  type MetaLeadgenFormQuestion,
  type MetaLeadgenEventDto,
} from '../../lib/api';

// Paket B-META Dashboard — /settings/meta-leadgen editor.
//
// Secrets discipline: null-means-keep. The server returns only last-4 markers
// (e.g. "****X7a9") for verify_token / app_secret / page_access_token — the
// operator only re-types a field when they want to change it. Untyped fields
// submit as null and the server preserves the stored value.
//
// Disabled UI guard (lessons 2026-04-22 P3): inputs stay usable even when the
// upstream load fails so an operator can still correct state. Only the Save
// button is disabled during the in-flight save.

const CANONICAL_OPTIONS: { value: string; label: string }[] = [
  { value: '', label: '— kanonik alan seç —' },
  { value: 'name', label: 'name (ad soyad)' },
  { value: 'phone', label: 'phone (telefon)' },
  { value: 'email', label: 'email (e-posta)' },
  { value: 'custom_1', label: 'custom_1' },
  { value: 'custom_2', label: 'custom_2' },
  { value: 'custom_3', label: 'custom_3' },
  { value: 'custom_4', label: 'custom_4' },
  { value: 'custom_5', label: 'custom_5' },
  { value: 'consent_marketing', label: 'consent_marketing' },
];

interface FieldMapRow {
  id: string;
  metaQuestionId: string;
  canonical: string;
}

function rowId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function toRows(fieldIdMap: Record<string, string> | null | undefined): FieldMapRow[] {
  if (!fieldIdMap) return [];
  return Object.entries(fieldIdMap).map(([metaQuestionId, canonical]) => ({
    id: rowId(),
    metaQuestionId,
    canonical,
  }));
}

function fromRows(rows: FieldMapRow[]): Record<string, string> {
  const out: Record<string, string> = {};
  for (const r of rows) {
    const key = r.metaQuestionId.trim();
    if (key.length === 0) continue;
    out[key] = r.canonical;
  }
  return out;
}

export function MetaLeadgenSettingsPage() {
  // Loaded state (server truth)
  const [loaded, setLoaded] = useState<MetaLeadgenConfigGetResponse | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Form fields (operator input, null = keep server value)
  const [appSecret, setAppSecret] = useState<string>('');
  const [pageAccessToken, setPageAccessToken] = useState<string>('');
  const [pageId, setPageId] = useState<string>('');
  const [fieldMapRows, setFieldMapRows] = useState<FieldMapRow[]>([]);

  // Ephemeral UI state
  const [saving, setSaving] = useState(false);
  const [saveToast, setSaveToast] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const [rotating, setRotating] = useState(false);
  const [freshVerifyToken, setFreshVerifyToken] = useState<string | null>(null);

  const [discoverLoading, setDiscoverLoading] = useState(false);
  const [discoverResult, setDiscoverResult] = useState<MetaLeadgenDiscoverFormsResponse | null>(null);
  const [discoverError, setDiscoverError] = useState<string | null>(null);

  const [eventsLoading, setEventsLoading] = useState(false);
  const [eventsResult, setEventsResult] = useState<MetaLeadgenEventDto[] | null>(null);
  const [eventsError, setEventsError] = useState<string | null>(null);

  const [copyStatus, setCopyStatus] = useState<string | null>(null);

  // Load config on mount.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const resp = await api.getMetaLeadgenConfig();
        if (cancelled) return;
        setLoaded(resp);
        setPageId(resp.data.config.page_id ?? '');
        setFieldMapRows(toRows(resp.data.config.field_id_map));
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

  // ── Handlers ──────────────────────────────────────────────────────────────
  async function handleSave() {
    setSaving(true);
    setSaveToast(null);
    setSaveError(null);
    try {
      const req: MetaLeadgenConfigPutRequest = {
        // null = keep server value; non-empty string = overwrite.
        verify_token: null,
        app_secret: appSecret.length > 0 ? appSecret : null,
        page_access_token: pageAccessToken.length > 0 ? pageAccessToken : null,
        page_id: pageId.length > 0 ? pageId : null,
        field_id_map: fromRows(fieldMapRows),
      };
      const resp = await api.putMetaLeadgenConfig(req);
      setSaveToast(`Ayarlar kaydedildi (updated_at: ${resp.data.updated_at ?? '-'}).`);
      // Clear secret inputs after successful save — operator re-types if they
      // want another change; server has the fresh values.
      setAppSecret('');
      setPageAccessToken('');
    } catch (err) {
      setSaveError(err instanceof ApiClientError
        ? `[${err.errorCode ?? 'INV-BE-001'}] ${err.message}`
        : '[INV-BE-001] Kaydetme başarısız — Backend geçici olarak ulaşılamıyor, tekrar deneyin.');
    } finally {
      setSaving(false);
    }
  }

  async function handleRotate() {
    setRotating(true);
    setFreshVerifyToken(null);
    setSaveError(null);
    try {
      const resp = await api.rotateMetaVerifyToken();
      setFreshVerifyToken(resp.data.verify_token);
      setSaveToast('Verify token rotate edildi. Meta Subscription ayarında güncellemeyi unutmayın.');
    } catch (err) {
      setSaveError(err instanceof ApiClientError
        ? `[${err.errorCode ?? 'INV-BE-001'}] ${err.message}`
        : '[INV-BE-001] Token rotasyonu başarısız — Backend geçici olarak ulaşılamıyor.');
    } finally {
      setRotating(false);
    }
  }

  async function handleDiscover() {
    setDiscoverLoading(true);
    setDiscoverError(null);
    try {
      const resp = await api.discoverMetaForms();
      setDiscoverResult(resp);
    } catch (err) {
      setDiscoverError(err instanceof ApiClientError
        ? `[${err.errorCode ?? 'INV-META-003'}] ${err.message}`
        : '[INV-META-003] Form keşfi başarısız — Meta Graph API geçici olarak ulaşılamıyor.');
      setDiscoverResult(null);
    } finally {
      setDiscoverLoading(false);
    }
  }

  async function handleLoadEvents() {
    setEventsLoading(true);
    setEventsError(null);
    try {
      const resp = await api.listMetaLeadgenEvents(5);
      setEventsResult(resp.data);
    } catch (err) {
      setEventsError(err instanceof ApiClientError
        ? `[${err.errorCode ?? 'INV-BE-001'}] ${err.message}`
        : '[INV-BE-001] Olay listesi yüklenemedi — Backend geçici olarak ulaşılamıyor.');
    } finally {
      setEventsLoading(false);
    }
  }

  async function handleCopyWebhookUrl() {
    if (!webhookUrl) return;
    try {
      if (typeof navigator !== 'undefined' && navigator.clipboard) {
        await navigator.clipboard.writeText(webhookUrl);
        setCopyStatus('Kopyalandı');
      } else {
        // Clipboard API yoksa manuel seçim yönergesi göster.
        setCopyStatus('Panoya erişilemiyor — manuel kopyalayın');
      }
    } catch (err) {
      // Clipboard permission denied (iframe, http-only, Firefox private mode, etc.).
      // Typed catch per project rule; the underlying cause is always a DOMException
      // / TypeError thrown by navigator.clipboard.writeText. Surface to user via
      // visible status rather than silently failing.
      const detail = err instanceof Error ? err.message : 'unknown';
      setCopyStatus(`Panoya erişilemiyor (${detail}) — manuel kopyalayın`);
    }
    // 2 saniye sonra toast'ı temizle.
    window.setTimeout(() => setCopyStatus(null), 2000);
  }

  function addFieldMapRow() {
    setFieldMapRows(rows => [...rows, { id: rowId(), metaQuestionId: '', canonical: '' }]);
  }
  function removeFieldMapRow(id: string) {
    setFieldMapRows(rows => rows.filter(r => r.id !== id));
  }
  function updateFieldMapRow(id: string, patch: Partial<FieldMapRow>) {
    setFieldMapRows(rows => rows.map(r => (r.id === id ? { ...r, ...patch } : r)));
  }
  function seedRowsFromForm(questions: MetaLeadgenFormQuestion[]) {
    // Seed one row per question with a suggested canonical based on Meta's
    // question type. Operator adjusts before saving.
    const suggested: FieldMapRow[] = questions.map(q => ({
      id: rowId(),
      metaQuestionId: q.id,
      canonical: suggestCanonical(q.type),
    }));
    setFieldMapRows(suggested);
  }

  // ── Render ────────────────────────────────────────────────────────────────
  const webhookUrl = loaded?.data.webhook_url ?? '';
  const verifyTokenDisplay = freshVerifyToken
    ? `${freshVerifyToken} (yeni — Meta Subscription'a yapıştır)`
    : (loaded?.data.config.verify_token_last4 ?? '— henüz oluşturulmadı —');

  return (
    <div style={{ maxWidth: 900, margin: '0 auto', padding: '1.5rem' }}>
      <h1 style={{ marginBottom: '0.5rem' }}>Meta Leadgen Ayarları</h1>
      <p style={{ color: '#666', marginBottom: '1.5rem' }}>
        Meta Lead Ads webhook entegrasyonu. Zapier / 3rd-party tool gerekmez.
      </p>

      {loadError && (
        <div role="alert" aria-label="Yükleme hatası" style={{ background: '#fee', padding: '0.75rem', marginBottom: '1rem', borderRadius: 4 }}>
          {loadError}
        </div>
      )}

      {/* ─── Section 1: Webhook URL ─── */}
      <section style={{ marginBottom: '2rem' }}>
        <h2>1. Webhook URL</h2>
        <p style={{ color: '#666', fontSize: 14 }}>
          Meta App Dashboard → Webhooks → Leadgen Subscription → Callback URL alanına bu URL'i yapıştırın.
        </p>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <input
            type="text"
            readOnly
            value={webhookUrl}
            style={{ flex: 1, padding: '0.5rem', fontFamily: 'monospace', background: '#f5f5f5' }}
            aria-label="Webhook URL"
          />
          <button
            type="button"
            onClick={handleCopyWebhookUrl}
            disabled={!webhookUrl}
            aria-label="Webhook URL'i panoya kopyala"
          >
            Kopyala
          </button>
          {copyStatus && (
            <span role="status" style={{ color: '#0a0', fontSize: 13 }}>{copyStatus}</span>
          )}
        </div>
      </section>

      {/* ─── Section 2: Verify Token ─── */}
      <section style={{ marginBottom: '2rem' }}>
        <h2>2. Verify Token</h2>
        <p style={{ color: '#666', fontSize: 14 }}>
          Meta Subscription handshake'i için 32-karakter random token. Rotate sonrası Meta tarafında güncelleyin.
        </p>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <input
            type="text"
            readOnly
            value={verifyTokenDisplay}
            style={{ flex: 1, padding: '0.5rem', fontFamily: 'monospace', background: '#f5f5f5' }}
            aria-label="Verify token"
          />
          <button type="button" onClick={handleRotate} disabled={rotating}>
            {rotating ? 'Rotating...' : 'Rotate Verify Token'}
          </button>
        </div>
      </section>

      {/* ─── Section 3: Secrets ─── */}
      <section style={{ marginBottom: '2rem' }}>
        <h2>3. Meta API Anahtarları</h2>
        <p style={{ color: '#666', fontSize: 14 }}>
          Boş bırakılan alanlar mevcut sunucu değerini korur (null-means-keep). Değiştirmek istediğinizi tekrar girin.
        </p>

        <label style={{ display: 'block', marginBottom: '0.75rem' }}>
          <div>App Secret (mevcut: {loaded?.data.config.app_secret_last4 ?? '— yok —'})</div>
          <input
            type="password"
            value={appSecret}
            onChange={e => setAppSecret(e.target.value)}
            autoComplete="off"
            placeholder="Meta App Dashboard → Settings → Basic → App Secret"
            style={{ width: '100%', padding: '0.5rem' }}
          />
        </label>

        <label style={{ display: 'block', marginBottom: '0.75rem' }}>
          <div>Page Access Token (mevcut: {loaded?.data.config.page_access_token_last4 ?? '— yok —'})</div>
          <input
            type="password"
            value={pageAccessToken}
            onChange={e => setPageAccessToken(e.target.value)}
            autoComplete="off"
            placeholder="Graph API Explorer'dan alınmış long-lived Page Access Token"
            style={{ width: '100%', padding: '0.5rem' }}
          />
        </label>

        <label style={{ display: 'block', marginBottom: '0.75rem' }}>
          <div>Page ID</div>
          <input
            type="text"
            value={pageId}
            onChange={e => setPageId(e.target.value)}
            placeholder="Facebook Page numerik ID"
            style={{ width: '100%', padding: '0.5rem' }}
          />
        </label>
      </section>

      {/* ─── Section 4: Field ID Map ─── */}
      <section style={{ marginBottom: '2rem' }}>
        <h2>4. Field ID Map</h2>
        <p style={{ color: '#666', fontSize: 14 }}>
          Meta Lead Form sorularını Invekto kanonik alanlarına eşleyin. phone kanonik alanı zorunludur; eksikse intake reddedilir.
        </p>

        <div style={{ display: 'flex', gap: 8, marginBottom: '0.75rem' }}>
          <button type="button" onClick={handleDiscover} disabled={discoverLoading || !pageId}>
            {discoverLoading ? 'Keşfediliyor...' : 'Keşfet: Form Fields'}
          </button>
          <button type="button" onClick={addFieldMapRow}>+ Satır Ekle</button>
        </div>

        {discoverError && (
          <div role="alert" aria-label="Keşfet hatası" style={{ background: '#fee', padding: '0.5rem', marginBottom: '0.75rem', fontSize: 13 }}>
            {discoverError}
          </div>
        )}

        {discoverResult && discoverResult.data.forms.length > 0 && (
          <div style={{ background: '#f9f9f9', padding: '0.75rem', marginBottom: '0.75rem', fontSize: 13 }}>
            <div>Bulunan formlar:</div>
            <ul>
              {discoverResult.data.forms.map(f => (
                <li key={f.id}>
                  <strong>{f.name}</strong> (id: <code>{f.id}</code>)
                  {f.questions.length > 0 && (
                    <>
                      {' — '}
                      <button
                        type="button"
                        onClick={() => seedRowsFromForm(f.questions)}
                        style={{ fontSize: 12 }}
                      >
                        bu formdan satırları doldur
                      </button>
                    </>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}

        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ background: '#f0f0f0', textAlign: 'left' }}>
              <th style={{ padding: '0.5rem' }}>Meta question_id</th>
              <th style={{ padding: '0.5rem' }}>Kanonik alan</th>
              <th style={{ padding: '0.5rem', width: 80 }}>Sil</th>
            </tr>
          </thead>
          <tbody>
            {fieldMapRows.length === 0 && (
              <tr>
                <td colSpan={3} style={{ padding: '1rem', textAlign: 'center', color: '#888' }}>
                  Henüz eşleme yok. [+ Satır Ekle] ile başlayın veya [Keşfet: Form Fields] kullanın.
                </td>
              </tr>
            )}
            {fieldMapRows.map(row => (
              <tr key={row.id} style={{ borderBottom: '1px solid #eee' }}>
                <td style={{ padding: '0.5rem' }}>
                  <input
                    type="text"
                    value={row.metaQuestionId}
                    onChange={e => updateFieldMapRow(row.id, { metaQuestionId: e.target.value })}
                    placeholder="örn: full_name veya Meta question_id"
                    style={{ width: '100%', padding: '0.35rem' }}
                    aria-label={`Meta question id row ${row.id}`}
                  />
                </td>
                <td style={{ padding: '0.5rem' }}>
                  <select
                    value={row.canonical}
                    onChange={e => updateFieldMapRow(row.id, { canonical: e.target.value })}
                    style={{ width: '100%', padding: '0.35rem' }}
                    aria-label={`Canonical field row ${row.id}`}
                  >
                    {CANONICAL_OPTIONS.map(opt => (
                      <option key={opt.value} value={opt.value}>{opt.label}</option>
                    ))}
                  </select>
                </td>
                <td style={{ padding: '0.5rem' }}>
                  <button type="button" onClick={() => removeFieldMapRow(row.id)}>Sil</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* ─── Section 5: Save ─── */}
      <section style={{ marginBottom: '2rem' }}>
        <button
          type="button"
          onClick={handleSave}
          disabled={saving}
          style={{ padding: '0.6rem 1.2rem', fontSize: 15 }}
        >
          {saving ? 'Kaydediliyor...' : 'Kaydet'}
        </button>
        {saveToast && (
          <span role="status" style={{ marginLeft: '1rem', color: '#0a0' }}>{saveToast}</span>
        )}
        {saveError && (
          <span role="alert" style={{ marginLeft: '1rem', color: '#c00' }}>{saveError}</span>
        )}
      </section>

      {/* ─── Section 6: Test Events ─── */}
      <section>
        <h2>6. Test Events (son 5)</h2>
        <p style={{ color: '#666', fontSize: 14 }}>
          Meta webhook'undan gelen son 5 event. Raw payload gösterilmez — sadece anahtar set + durum.
        </p>
        <button type="button" onClick={handleLoadEvents} disabled={eventsLoading}>
          {eventsLoading ? 'Yükleniyor...' : 'Son Event\'leri Göster'}
        </button>
        {eventsError && (
          <div role="alert" aria-label="Events hatası" style={{ background: '#fee', padding: '0.5rem', marginTop: '0.5rem', fontSize: 13 }}>
            {eventsError}
          </div>
        )}
        {eventsResult && (
          <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '0.75rem', fontSize: 13 }}>
            <thead>
              <tr style={{ background: '#f0f0f0', textAlign: 'left' }}>
                <th style={{ padding: '0.5rem' }}>Alındı</th>
                <th style={{ padding: '0.5rem' }}>leadgen_id</th>
                <th style={{ padding: '0.5rem' }}>form_id</th>
                <th style={{ padding: '0.5rem' }}>HTTP</th>
                <th style={{ padding: '0.5rem' }}>error_code</th>
                <th style={{ padding: '0.5rem' }}>lead_id</th>
                <th style={{ padding: '0.5rem' }}>payload keys</th>
              </tr>
            </thead>
            <tbody>
              {eventsResult.length === 0 && (
                <tr>
                  <td colSpan={7} style={{ padding: '1rem', textAlign: 'center', color: '#888' }}>
                    Henüz event gelmedi. Meta Subscription'ı aktifleştirdikten sonra Meta test lead gönderebilirsiniz.
                  </td>
                </tr>
              )}
              {eventsResult.map(ev => (
                <tr key={ev.id} style={{ borderBottom: '1px solid #eee' }}>
                  <td style={{ padding: '0.5rem' }}>{new Date(ev.received_at).toLocaleString()}</td>
                  <td style={{ padding: '0.5rem', fontFamily: 'monospace', fontSize: 12 }}>{ev.leadgen_id}</td>
                  <td style={{ padding: '0.5rem' }}>{ev.form_id ?? '—'}</td>
                  <td style={{ padding: '0.5rem' }}>{ev.http_status ?? '—'}</td>
                  <td style={{ padding: '0.5rem' }}>{ev.error_code ?? '—'}</td>
                  <td style={{ padding: '0.5rem' }}>{ev.lead_id ?? '—'}</td>
                  <td style={{ padding: '0.5rem', fontSize: 12 }}>
                    {ev.payload_keys.length > 0 ? ev.payload_keys.join(', ') : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

function suggestCanonical(type: string): string {
  switch (type) {
    case 'FULL_NAME':
    case 'NAME':
      return 'name';
    case 'PHONE':
    case 'PHONE_NUMBER':
      return 'phone';
    case 'EMAIL':
      return 'email';
    default:
      return '';
  }
}
