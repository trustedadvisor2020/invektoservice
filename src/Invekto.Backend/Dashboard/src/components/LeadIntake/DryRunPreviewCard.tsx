// FEAT-LIW Chunk C: diagnostic dry-run. UI sends payload + unsaved draft
// field_map (from parent page state) to POST /dry-run; backend reuses the real
// FieldMapResolver + PhoneE164Normalizer + consent path so preview output
// cannot drift from runtime behaviour. No DB write, no flow enqueue.
import { useState } from 'react';
import { PlayCircle, CheckCircle2, XCircle, AlertTriangle } from 'lucide-react';
import { Card, CardTitle } from '../ui/Card';
import { Button } from '../ui/Button';
import type { DryRunResponse } from '../../types/leadIntake';

interface DryRunPreviewCardProps {
  draftFieldMap: Record<string, string>;
  draftPhoneCountryHint: string | null;
  onRun: (payload: Record<string, unknown>, fieldMapOverride: Record<string, string>, phoneHint: string | null) => Promise<DryRunResponse>;
}

const SAMPLE_PAYLOAD = JSON.stringify({
  ad_soyad: 'Nur Seyit',
  telefon: '+905551234567',
  eposta: 'nur@example.com',
  kvkk_onay: true,
  sehir: 'Dublin',
}, null, 2);

export function DryRunPreviewCard({ draftFieldMap, draftPhoneCountryHint, onRun }: DryRunPreviewCardProps) {
  const [payloadText, setPayloadText] = useState<string>(SAMPLE_PAYLOAD);
  const [result, setResult] = useState<DryRunResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [parseError, setParseError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);

  async function handleRun() {
    setParseError(null);
    setServerError(null);
    let payload: Record<string, unknown>;
    try {
      const parsed: unknown = JSON.parse(payloadText);
      if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
        setParseError('Payload bir JSON nesnesi olmalı.');
        return;
      }
      payload = parsed as Record<string, unknown>;
    } catch (err) {
      const msg = err instanceof SyntaxError ? `Geçersiz JSON format: ${err.message}` : 'Geçersiz JSON format.';
      setParseError(msg);
      return;
    }
    setBusy(true);
    try {
      const resp = await onRun(payload, draftFieldMap, draftPhoneCountryHint);
      setResult(resp);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Dry-run başarısız.';
      setServerError(msg);
      setResult(null);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card className="p-5">
      <CardTitle className="flex items-center gap-2 mb-4">
        <PlayCircle className="w-4 h-4 text-navy-600" />
        Dry-Run Önizleme
      </CardTitle>

      <p className="text-sm text-navy-500 mb-3 leading-relaxed">
        Örnek bir landing payload&#39;i yapıştırın; mevcut alan eşlemesi (kaydedilmemiş taslak dahil)
        üzerinden gerçek intake yolunun ne yapacağını görün. Hiçbir lead kaydı oluşturulmaz,
        welcome flow tetiklenmez.
      </p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label className="block text-xs text-navy-500 mb-1">Payload (JSON)</label>
          <textarea
            value={payloadText}
            onChange={e => setPayloadText(e.target.value)}
            rows={12}
            className="w-full border border-navy-200 rounded p-2 font-mono text-xs focus:border-navy-400 focus:outline-none"
          />
          {parseError && <p className="text-xs text-red-600 mt-1">{parseError}</p>}
          <div className="mt-3">
            <Button variant="primary" onClick={handleRun} disabled={busy}>
              {busy ? 'Çalışıyor...' : 'Dry Run Çalıştır'}
            </Button>
          </div>
        </div>

        <div>
          <label className="block text-xs text-navy-500 mb-1">Sonuc</label>
          <div className="border border-navy-200 rounded p-3 min-h-[280px] bg-navy-50">
            {serverError && <div className="text-xs text-red-700">{serverError}</div>}
            {!result && !serverError && (
              <div className="text-xs text-navy-400">Henüz çalıştırılmadı.</div>
            )}
            {result && (
              <div className="space-y-3 text-xs">
                <div className="flex flex-wrap gap-2">
                  <span className={`px-2 py-0.5 rounded font-medium ${result.would_upsert ? 'bg-green-100 text-green-800' : 'bg-navy-100 text-navy-600'}`}>
                    would_upsert: {result.would_upsert ? 'true' : 'false'}
                  </span>
                  <span className={`px-2 py-0.5 rounded font-medium ${result.would_trigger_welcome ? 'bg-green-100 text-green-800' : 'bg-navy-100 text-navy-600'}`}>
                    would_trigger_welcome: {result.would_trigger_welcome ? 'true' : 'false'}
                  </span>
                </div>

                <div className="flex items-center gap-2">
                  {result.phone_e164 ? (
                    <>
                      <CheckCircle2 className="w-4 h-4 text-green-600" />
                      <span>Phone E.164: <code className="font-mono bg-white px-1 rounded">{result.phone_e164}</code></span>
                    </>
                  ) : (
                    <>
                      <XCircle className="w-4 h-4 text-red-600" />
                      <span className="text-red-700">Telefon normalize edilemedi</span>
                    </>
                  )}
                </div>

                <div className="flex items-center gap-2">
                  {result.consent_ok ? (
                    <><CheckCircle2 className="w-4 h-4 text-green-600" /><span>Onay alani OK</span></>
                  ) : (
                    <><XCircle className="w-4 h-4 text-red-600" /><span className="text-red-700">Onay alani eksik / false</span></>
                  )}
                </div>

                {Object.keys(result.resolved_fields).length > 0 && (
                  <div>
                    <div className="text-navy-500 font-medium mb-1">Çözümlenen alanlar:</div>
                    <table className="w-full text-xs">
                      <tbody>
                        {Object.entries(result.resolved_fields).map(([k, v]) => (
                          <tr key={k} className="border-b border-navy-100 last:border-0">
                            <td className="py-1 pr-2 font-mono text-navy-500">{k}</td>
                            <td className="py-1 font-mono text-navy-800 break-all">
                              {v === null || v === undefined ? <span className="text-navy-400">null</span> : JSON.stringify(v)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}

                {result.warnings.length > 0 && (
                  <div>
                    <div className="text-navy-500 font-medium mb-1 flex items-center gap-1">
                      <AlertTriangle className="w-3.5 h-3.5 text-amber-600" /> Uyarılar:
                    </div>
                    <ul className="list-disc list-inside text-amber-800 space-y-0.5">
                      {result.warnings.map((w, i) => <li key={i}>{w}</li>)}
                    </ul>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </Card>
  );
}
