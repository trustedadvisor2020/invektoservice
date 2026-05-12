// FEAT-LIW Chunk C: standalone /settings/lead-intake page.
// Container: fetches settings + audit on mount; coordinates 5 child cards
// (FlowWarningBanner, ApiKeyManagerCard, FieldMapEditorCard, DryRunPreviewCard,
// AuditLogTimeline) + NewKeyCopyModal. row_version threaded into every
// mutation; 409 -> refetch + toast + preserve dirty draft state.
// Shared state: useState/useEffect (NOT Zustand) for row_version + audit fetching;
// keeps optimistic-concurrency 409 dirty-draft preservation simple per-form-scope.
import { useCallback, useEffect, useRef, useState } from 'react';
import { Webhook, AlertCircle } from 'lucide-react';
import { api, ApiClientError } from '../../lib/api';
import { Button } from '../../components/ui/Button';
import { FlowWarningBanner } from '../../components/LeadIntake/FlowWarningBanner';
import { ApiKeyManagerCard } from '../../components/LeadIntake/ApiKeyManagerCard';
import { FieldMapEditorCard } from '../../components/LeadIntake/FieldMapEditorCard';
import { DryRunPreviewCard } from '../../components/LeadIntake/DryRunPreviewCard';
import { AuditLogTimeline } from '../../components/LeadIntake/AuditLogTimeline';
import { NewKeyCopyModal } from '../../components/LeadIntake/NewKeyCopyModal';
import type {
  TenantLandingSettingsDto,
  LiwAuditEntryDto,
  UpdateFieldMapResponse,
} from '../../types/leadIntake';

function extractError(err: unknown, fallback: string): { code: string; message: string; is409: boolean } {
  if (err instanceof ApiClientError) {
    return {
      code: err.errorCode && err.errorCode !== 'UNKNOWN' ? err.errorCode : 'INV-LIW-FE-001',
      message: err.message || fallback,
      is409: err.status === 409,
    };
  }
  const msg = err instanceof Error ? err.message : fallback;
  return { code: 'INV-LIW-FE-001', message: msg, is409: false };
}

export function LeadIntakeSettingsPage() {
  useEffect(() => { document.title = 'Invekto AI - Lead Kaynaklari'; }, []);

  const [settings, setSettings] = useState<TenantLandingSettingsDto | null>(null);
  const [auditEntries, setAuditEntries] = useState<LiwAuditEntryDto[]>([]);
  const [auditLoading, setAuditLoading] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const [newKeyPlaintext, setNewKeyPlaintext] = useState<string | null>(null);
  const [newKeyOldExpires, setNewKeyOldExpires] = useState<string | null>(null);

  // Draft field_map for DryRunPreviewCard override (lifted up from FieldMapEditorCard).
  const draftRef = useRef<{ map: Record<string, string>; phoneHint: string | null }>({ map: {}, phoneHint: null });

  const loadSettings = useCallback(async () => {
    setError(null);
    try {
      const data = await api.getLeadIntakeSettings();
      setSettings(data);
      draftRef.current = { map: data.field_map, phoneHint: data.phone_country_hint };
    } catch (err) {
      const e = extractError(err, 'Ayarlar yuklenemedi.');
      setError(`[${e.code}] ${e.message}`);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadAudit = useCallback(async () => {
    setAuditLoading(true);
    try {
      const resp = await api.listLeadIntakeAudit(50);
      setAuditEntries(resp.entries);
    } catch (err) {
      // Timeline is non-fatal for page functionality, but Codex CQ2 flags silent-swallow.
      // Surface the failure as a toast + structured console log so ops visibility is preserved.
      const e = extractError(err, 'Degisiklik gecmisi yuklenemedi.');
      console.warn(`[${e.code}] LeadIntakeSettings: audit list failed — ${e.message}`);
      setToast(`Degisiklik gecmisi yuklenemedi: [${e.code}]`);
    } finally {
      setAuditLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSettings();
    void loadAudit();
  }, [loadSettings, loadAudit]);

  // Toast auto-dismiss.
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(null), 4000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  async function handleRotate() {
    if (!settings) return;
    setBusy(true);
    try {
      const resp = await api.rotateLeadIntakeApiKey(settings.updated_at);
      setNewKeyPlaintext(resp.active_plaintext);
      setNewKeyOldExpires(resp.old_expires_at);
      await loadSettings();
      await loadAudit();
    } catch (err) {
      const e = extractError(err, 'Anahtar yenileme basarisiz.');
      if (e.is409) {
        setToast('Ayarlar baska bir sekmede degistirildi, son hali yuklendi.');
        await loadSettings();
        await loadAudit();
      } else {
        setToast(`[${e.code}] ${e.message}`);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleRevoke() {
    if (!settings?.updated_at) return;
    setBusy(true);
    try {
      await api.revokeLeadIntakeApiKey(settings.updated_at);
      setToast('API anahtari iptal edildi.');
      await loadSettings();
      await loadAudit();
    } catch (err) {
      const e = extractError(err, 'Anahtar iptal basarisiz.');
      if (e.is409) {
        setToast('Ayarlar baska bir sekmede degistirildi, son hali yuklendi.');
        await loadSettings();
        await loadAudit();
      } else {
        setToast(`[${e.code}] ${e.message}`);
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleFieldMapSave(
    fieldMap: Record<string, string>,
    phoneCountryHint: string | null,
  ): Promise<UpdateFieldMapResponse> {
    if (!settings?.updated_at) throw new Error('row_version missing — refetch first');
    setBusy(true);
    try {
      const resp = await api.updateLeadIntakeFieldMap({
        field_map: fieldMap,
        phone_country_hint: phoneCountryHint,
        expected_row_version: settings.updated_at,
      });
      setToast('Alan eslemesi kaydedildi.');
      await loadSettings();
      await loadAudit();
      return resp;
    } catch (err) {
      const e = extractError(err, 'Alan eslemesi kaydedilemedi.');
      if (e.is409) {
        setToast('Ayarlar baska bir sekmede degistirildi, son hali yuklendi.');
        await loadSettings();
        await loadAudit();
      }
      throw new Error(`[${e.code}] ${e.message}`);
    } finally {
      setBusy(false);
    }
  }

  const handleDryRun = useCallback(async (
    payload: Record<string, unknown>,
    fieldMapOverride: Record<string, string>,
    phoneHint: string | null,
  ) => {
    return api.dryRunLeadIntake({
      source_slug: 'dryrun',
      payload,
      field_map_override: fieldMapOverride,
      phone_country_hint_override: phoneHint,
    });
  }, []);

  const handleDraftChange = useCallback((map: Record<string, string>, phoneHint: string | null) => {
    draftRef.current = { map, phoneHint };
  }, []);

  if (loading) {
    return (
      <div className="p-8">
        <div className="text-navy-400 text-sm">Yukleniyor...</div>
      </div>
    );
  }

  if (error && !settings) {
    return (
      <div className="p-8">
        <div className="rounded-lg border border-red-200 bg-red-50 p-4">
          <div className="flex items-start gap-2">
            <AlertCircle className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
            <div>
              <div className="text-sm font-semibold text-red-900">Ayarlar yuklenemedi</div>
              <div className="text-xs text-red-700 mt-1">{error}</div>
              <div className="mt-3">
                <Button variant="secondary" onClick={() => { setLoading(true); void loadSettings(); }}>
                  Tekrar Dene
                </Button>
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (!settings) return null;

  return (
    <div className="p-6 space-y-4">
      <h1 className="-mx-6 -mt-6 px-6 py-4 mb-2 text-xl font-semibold text-navy-900 flex items-center gap-2 border-b border-navy-100">
        <Webhook className="w-5 h-5 text-navy-400" />
        Lead Kaynaklari (Landing Webhook)
      </h1>

      <FlowWarningBanner flowStatus={settings.flow_status} />

      {toast && (
        <div className="rounded-md border border-navy-200 bg-navy-50 p-3 text-sm text-navy-800">
          {toast}
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-4">
          <ApiKeyManagerCard
            settings={settings}
            onRotate={handleRotate}
            onRevoke={handleRevoke}
            busy={busy}
          />
          <FieldMapEditorCard
            settings={settings}
            onSave={handleFieldMapSave}
            busy={busy}
            onDraftChange={handleDraftChange}
          />
          <DryRunPreviewCard
            draftFieldMap={draftRef.current.map}
            draftPhoneCountryHint={draftRef.current.phoneHint}
            onRun={handleDryRun}
          />
        </div>
        <div className="lg:col-span-1">
          <AuditLogTimeline entries={auditEntries} loading={auditLoading} />
        </div>
      </div>

      <NewKeyCopyModal
        open={newKeyPlaintext !== null}
        plaintext={newKeyPlaintext}
        oldKeyExpiresAt={newKeyOldExpires}
        onClose={() => { setNewKeyPlaintext(null); setNewKeyOldExpires(null); }}
      />
    </div>
  );
}
