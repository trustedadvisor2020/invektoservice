// Adim 4: Zoho Stage Mapping editor. 7 lifecycle event rows, transitions dropdown, Test/Save/Discover.
// Connection-yok gate (AC4): Zoho bagli degilken editor disabled + info banner.
import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, Info, RefreshCw, CheckCircle2, XCircle } from 'lucide-react';
import { useZohoStore } from '../../stores/zoho-store';
import { Card, CardTitle } from '../../components/ui/Card';
import { Button } from '../../components/ui/Button';
import {
  api,
  ApiClientError,
  type ZohoBlueprintTransitionDto,
  type ZohoStageMappingUpsertEntry,
  type ZohoStageMappingTestResponse,
} from '../../lib/api';

const LIFECYCLE_EVENTS: { key: string; label: string }[] = [
  { key: 'welcome_sent',  label: 'welcome_sent (Hos geldin mesaji gonderildi)' },
  { key: 'engaged',       label: 'engaged (Etkilesime girdi)' },
  { key: 'qualified',     label: 'qualified (Uygunluk dogrulandi)' },
  { key: 'offer_sent',    label: 'offer_sent (Teklif gonderildi)' },
  { key: 'closed_won',    label: 'closed_won (Kazanildi)' },
  { key: 'deposit_paid',  label: 'deposit_paid (Kapora odendi)' },
  { key: 'closed_lost',   label: 'closed_lost (Kaybedildi)' },
];

function extractError(err: unknown, fallback: string): string {
  if (err instanceof ApiClientError) {
    const code = err.errorCode && err.errorCode !== 'UNKNOWN' ? err.errorCode : 'INV-INT-FE-132';
    const msg = err.message && err.message !== `HTTP ${err.status}` ? err.message : fallback;
    return `[${code}] ${msg}`;
  }
  if (err instanceof Error && err.message) return `[INV-INT-FE-132] ${err.message}`;
  return `[INV-INT-FE-132] ${fallback}`;
}

export function ZohoStageMappingPage() {
  const { connection, loadConnection, stageMappings, loadStageMappings } = useZohoStore();

  const [transitions, setTransitions] = useState<ZohoBlueprintTransitionDto[] | null>(null);
  const [transitionsLoading, setTransitionsLoading] = useState(false);
  const [transitionsError, setTransitionsError] = useState<string | null>(null);
  const [transitionsFromCache, setTransitionsFromCache] = useState<boolean>(false);

  // Local editable mapping state: { event: transitionId | '' }
  const [edited, setEdited] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [saveMessage, setSaveMessage] = useState<{ kind: 'ok' | 'err'; text: string } | null>(null);

  const [testResult, setTestResult] = useState<Record<string, ZohoStageMappingTestResponse | { error: string }>>({});
  const [testingKey, setTestingKey] = useState<string | null>(null);

  const connected = connection?.connected ?? false;

  const loadTransitions = useCallback(async (forceRefresh: boolean) => {
    setTransitionsLoading(true);
    setTransitionsError(null);
    try {
      const res = await api.getZohoBlueprintTransitions(forceRefresh);
      setTransitions(res.items);
      setTransitionsFromCache(res.fromCache);
    } catch (err) {
      setTransitionsError(extractError(err, 'Blueprint transition listesi alinamadi.'));
    } finally {
      setTransitionsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadConnection();
    void loadStageMappings();
  }, [loadConnection, loadStageMappings]);

  useEffect(() => {
    if (connected) void loadTransitions(false);
  }, [connected, loadTransitions]);

  // Preselect from existing mappings when loaded (AC3).
  useEffect(() => {
    if (!stageMappings) return;
    const seeded: Record<string, string> = {};
    for (const ev of LIFECYCLE_EVENTS) {
      const match = stageMappings.find((m) => m.zohoEvent === ev.key);
      seeded[ev.key] = match?.zohoTransitionId ?? '';
    }
    setEdited(seeded);
  }, [stageMappings]);

  const handleSave = async () => {
    setSaving(true);
    setSaveMessage(null);
    try {
      const payload: ZohoStageMappingUpsertEntry[] = [];
      for (const ev of LIFECYCLE_EVENTS) {
        const tid = edited[ev.key];
        if (!tid) continue;
        const t = transitions?.find((x) => x.transitionId === tid);
        payload.push({ zohoEvent: ev.key, zohoTransitionId: tid, zohoTransitionName: t?.name });
      }
      await api.putZohoStageMappings(payload);
      await loadStageMappings();
      setSaveMessage({ kind: 'ok', text: `${payload.length} eslesme kaydedildi.` });
    } catch (err) {
      setSaveMessage({ kind: 'err', text: extractError(err, 'Eslesmeler kaydedilemedi.') });
    } finally {
      setSaving(false);
    }
  };

  const handleTest = async (eventKey: string) => {
    const tid = edited[eventKey];
    if (!tid) return;
    setTestingKey(eventKey);
    try {
      const res = await api.testZohoStageMapping(eventKey, tid);
      setTestResult((prev) => ({ ...prev, [eventKey]: res }));
    } catch (err) {
      setTestResult((prev) => ({ ...prev, [eventKey]: { error: extractError(err, 'Test basarisiz.') } }));
    } finally {
      setTestingKey(null);
    }
  };

  const handleClearRow = (eventKey: string) => {
    setEdited((prev) => ({ ...prev, [eventKey]: '' }));
    setTestResult((prev) => {
      const next = { ...prev };
      delete next[eventKey];
      return next;
    });
  };

  // AC4: Connection-yok gate = banner + tum controls disabled (tek sayfa render, disabled state).
  const formDisabled = !connected;

  return (
    <div className="max-w-4xl">
      <div className="mb-4 flex items-start justify-between">
        <div>
          <h1 className="text-xl font-semibold text-navy-900">Asama Eslesmeleri</h1>
          <p className="text-sm text-navy-500 mt-1">
            Her lifecycle olayi icin Zoho Blueprint gecisini secin. Sync sirasinda bu eslesme kullanilir.
          </p>
        </div>
        <Button
          variant="secondary"
          onClick={() => void loadTransitions(true)}
          disabled={transitionsLoading || formDisabled}
        >
          <RefreshCw className={`w-4 h-4 mr-2 ${transitionsLoading ? 'animate-spin' : ''}`} />
          Discover
        </Button>
      </div>

      {!connected && (
        <div className="flex items-start gap-2 bg-yellow-50 border border-yellow-200 rounded-lg px-4 py-3 mb-4 text-sm text-yellow-800">
          <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
          <div>
            <strong className="font-semibold">Zoho hesabiniz bagli degil.</strong>
            <p className="mt-1">
              Eslesme editorunu kullanmadan once{' '}
              <a className="underline font-medium" href="/app/integrations/zoho/connection">
                Zoho hesabinizi baglayin
              </a>
              . Asagidaki form Zoho bagli olana kadar devre disidir.
            </p>
          </div>
        </div>
      )}

      {transitionsFromCache && !transitionsLoading && (
        <div className="flex items-start gap-2 bg-brand-50 border border-brand-100 rounded-lg px-4 py-2 mb-3 text-xs text-brand-700">
          <Info className="w-3 h-3 shrink-0 mt-0.5" />
          <span>Transition listesi cache'ten yuklendi (10 dk). Guncellemek icin Discover.</span>
        </div>
      )}

      {transitionsError && (
        <Card className="border-red-100 bg-red-50/40 mb-3">
          <CardTitle className="text-red-700 mb-1">Blueprint transition'lari alinamadi</CardTitle>
          <p className="text-sm text-red-600 mb-2">{transitionsError}</p>
          <Button variant="secondary" size="sm" onClick={() => void loadTransitions(true)}>
            Tekrar Dene
          </Button>
        </Card>
      )}

      {transitionsLoading && !transitions && (
        <div className="text-sm text-navy-400 mb-3">Transitions yukleniyor...</div>
      )}

      {saveMessage && (
        <div className={`flex items-start gap-2 rounded-lg px-4 py-3 mb-3 text-sm ${
          saveMessage.kind === 'ok'
            ? 'bg-green-50 border border-green-200 text-green-800'
            : 'bg-red-50 border border-red-200 text-red-700'
        }`}>
          {saveMessage.kind === 'ok'
            ? <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5" />
            : <XCircle className="w-4 h-4 shrink-0 mt-0.5" />}
          <span>{saveMessage.text}</span>
        </div>
      )}

      <Card className="p-0 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-navy-50 text-navy-500 text-xs uppercase tracking-wide">
            <tr>
              <th className="text-left font-semibold px-4 py-3 w-1/3">Lifecycle Olayi</th>
              <th className="text-left font-semibold px-4 py-3">Zoho Transition</th>
              <th className="text-left font-semibold px-4 py-3 w-48">Aksiyon</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-navy-100">
            {LIFECYCLE_EVENTS.map((ev) => {
              const selected = edited[ev.key] ?? '';
              const test = testResult[ev.key];
              return (
                <tr key={ev.key}>
                  <td className="px-4 py-3 align-top">
                    <div className="font-medium text-navy-900 font-mono text-xs">{ev.key}</div>
                    <div className="text-xs text-navy-500 mt-0.5">{ev.label.split('(')[1]?.replace(')', '')}</div>
                  </td>
                  <td className="px-4 py-3 align-top">
                    <select
                      value={selected}
                      onChange={(e) => {
                        setEdited((prev) => ({ ...prev, [ev.key]: e.target.value }));
                        setTestResult((prev) => {
                          const n = { ...prev };
                          delete n[ev.key];
                          return n;
                        });
                      }}
                      disabled={formDisabled || !transitions || transitionsLoading}
                      className="w-full border border-navy-200 rounded px-2 py-1.5 text-sm disabled:bg-navy-50 disabled:text-navy-400"
                    >
                      <option value="">— Eslesme yok —</option>
                      {(transitions ?? []).map((t) => (
                        <option key={t.transitionId} value={t.transitionId}>
                          {t.name}{t.nextState ? ` → ${t.nextState}` : ''}
                        </option>
                      ))}
                    </select>
                    {test && 'valid' in test && (
                      <div className={`mt-1 text-xs flex items-start gap-1 ${test.valid ? 'text-green-700' : 'text-red-700'}`}>
                        {test.valid
                          ? <><CheckCircle2 className="w-3 h-3 mt-0.5 shrink-0" /> Gecerli: {test.transitionName}{test.nextState ? ` → ${test.nextState}` : ''}</>
                          : <><XCircle className="w-3 h-3 mt-0.5 shrink-0" /> {test.reason}</>}
                      </div>
                    )}
                    {test && 'error' in test && (
                      <div className="mt-1 text-xs text-red-700 flex items-start gap-1">
                        <XCircle className="w-3 h-3 mt-0.5 shrink-0" /> {test.error}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3 align-top">
                    <div className="flex gap-1">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => void handleTest(ev.key)}
                        disabled={formDisabled || !selected || testingKey === ev.key || !transitions}
                      >
                        {testingKey === ev.key ? 'Test...' : 'Test'}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleClearRow(ev.key)}
                        disabled={formDisabled || !selected}
                      >
                        Temizle
                      </Button>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </Card>

      <div className="mt-4 flex justify-end gap-2">
        <Button
          variant="primary"
          onClick={() => void handleSave()}
          disabled={formDisabled || saving || !transitions}
        >
          {saving ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      </div>
    </div>
  );
}
