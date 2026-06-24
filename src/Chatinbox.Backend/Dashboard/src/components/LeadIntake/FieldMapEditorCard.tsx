// FEAT-LIW Chunk C: editable field_map table. Rows = source_field + canonical
// dropdown + remove button. 'Kaynak Yapistirma Yardimi' extracts JSON top-level
// keys into empty rows (does not overwrite existing). Save disabled until
// required canonicals (phone + consent) are present. Dirty state tracked;
// window.beforeunload prompts on unsaved changes.
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Trash2, Save, Lightbulb, X, AlertCircle, AlertTriangle } from 'lucide-react';
import { Card, CardTitle } from '../ui/Card';
import { Button } from '../ui/Button';
import { CANONICAL_FIELDS, REQUIRED_CANONICALS } from '../../types/leadIntake';
import type { TenantLandingSettingsDto, UpdateFieldMapResponse } from '../../types/leadIntake';

interface Row {
  id: string;
  source: string;
  canonical: string;
}

interface FieldMapEditorCardProps {
  settings: TenantLandingSettingsDto;
  onSave: (fieldMap: Record<string, string>, phoneCountryHint: string | null) => Promise<UpdateFieldMapResponse>;
  busy: boolean;
  /** Parent uses this to consume live draft state (for DryRunPreviewCard override). */
  onDraftChange?: (fieldMap: Record<string, string>, phoneCountryHint: string | null) => void;
}

function rowsFromSettings(settings: TenantLandingSettingsDto): Row[] {
  const rows = Object.entries(settings.field_map).map(([source, canonical], idx) => ({
    id: `r${Date.now()}-${idx}`,
    source,
    canonical,
  }));
  if (rows.length === 0) {
    return [{ id: `r${Date.now()}-0`, source: '', canonical: '' }];
  }
  return rows;
}

export function FieldMapEditorCard({ settings, onSave, busy, onDraftChange }: FieldMapEditorCardProps) {
  const [rows, setRows] = useState<Row[]>(() => rowsFromSettings(settings));
  const [phoneCountryHint, setPhoneCountryHint] = useState<string>(settings.phone_country_hint ?? '');
  const [dirty, setDirty] = useState(false);
  const [serverDrifted, setServerDrifted] = useState(false);
  const [pasteOpen, setPasteOpen] = useState(false);
  const [pasteText, setPasteText] = useState('');
  const [pasteError, setPasteError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  // Track the settings.updated_at we last aligned to; lets us detect a server refetch
  // that happened WHILE the user has dirty edits (409 recovery path). In that case we
  // PRESERVE the dirty rows (don't discard the user's unsaved work) but flag the drift
  // with a banner so they can decide whether to re-review + retry save.
  const alignedRowVersionRef = useRef<string | null>(settings.updated_at ?? null);

  useEffect(() => {
    const serverRowVersion = settings.updated_at ?? null;
    if (alignedRowVersionRef.current === serverRowVersion) return;

    if (dirty) {
      // Server state drifted under our feet (concurrent tab/operator). Keep dirty rows
      // intact + flag the drift so the user is not silently confused on the next save.
      setServerDrifted(true);
    } else {
      // Clean state — safe to sync to server (initial load OR post-successful-save refetch).
      setRows(rowsFromSettings(settings));
      setPhoneCountryHint(settings.phone_country_hint ?? '');
      setSaveError(null);
      setServerDrifted(false);
    }
    alignedRowVersionRef.current = serverRowVersion;
  }, [settings.updated_at, settings.field_map, settings.phone_country_hint, dirty]);

  const acceptServerState = useCallback(() => {
    setRows(rowsFromSettings(settings));
    setPhoneCountryHint(settings.phone_country_hint ?? '');
    setDirty(false);
    setServerDrifted(false);
    setSaveError(null);
  }, [settings]);

  // beforeunload guard.
  useEffect(() => {
    if (!dirty) return;
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = ''; };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [dirty]);

  // Push draft upstream for DryRunPreviewCard.
  useEffect(() => {
    if (!onDraftChange) return;
    const map: Record<string, string> = {};
    for (const r of rows) {
      if (r.source.trim() && r.canonical.trim()) map[r.source.trim()] = r.canonical.trim();
    }
    onDraftChange(map, phoneCountryHint.trim() || null);
  }, [rows, phoneCountryHint, onDraftChange]);

  const validation = useMemo(() => {
    const mappedCanonicals = new Set<string>();
    const errors: string[] = [];
    const duplicates = new Map<string, number>();
    for (const r of rows) {
      if (!r.source.trim() && !r.canonical.trim()) continue;
      if (!r.source.trim()) errors.push('Kaynak alan adı boş bırakılamaz.');
      else if (!r.canonical.trim()) errors.push(`'${r.source}' için canonical alan seçilmedi.`);
      else if (!CANONICAL_FIELDS.includes(r.canonical)) errors.push(`Tanımsız canonical alan: '${r.canonical}'.`);
      else {
        mappedCanonicals.add(r.canonical);
        duplicates.set(r.canonical, (duplicates.get(r.canonical) ?? 0) + 1);
      }
    }
    for (const [c, count] of duplicates) {
      if (count > 1 && c !== 'metadata') errors.push(`Canonical alan '${c}' birden fazla kaynak alana atanamaz.`);
    }
    for (const req of REQUIRED_CANONICALS) {
      if (!mappedCanonicals.has(req)) errors.push(`Zorunlu canonical alan '${req}' için en az bir satır gerekli.`);
    }
    return { errors, valid: errors.length === 0 };
  }, [rows]);

  function updateRow(id: string, patch: Partial<Row>) {
    setRows(prev => prev.map(r => r.id === id ? { ...r, ...patch } : r));
    setDirty(true);
  }

  function addRow() {
    setRows(prev => [...prev, { id: `r${Date.now()}-${Math.random()}`, source: '', canonical: '' }]);
    setDirty(true);
  }

  function removeRow(id: string) {
    setRows(prev => prev.length > 1 ? prev.filter(r => r.id !== id) : [{ id: `r${Date.now()}-0`, source: '', canonical: '' }]);
    setDirty(true);
  }

  function extractSourcesFromPaste() {
    setPasteError(null);
    try {
      const parsed: unknown = JSON.parse(pasteText);
      if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
        setPasteError('JSON bir nesne (object) olmali.');
        return;
      }
      const keys = Object.keys(parsed as Record<string, unknown>);
      if (keys.length === 0) {
        setPasteError('Nesnede alan bulunamadi.');
        return;
      }
      const existingSources = new Set(rows.filter(r => r.source.trim()).map(r => r.source.trim()));
      const newRows: Row[] = [...rows];
      let inserted = 0;
      for (const key of keys) {
        if (existingSources.has(key)) continue;
        const emptyIdx = newRows.findIndex(r => !r.source.trim() && !r.canonical.trim());
        if (emptyIdx >= 0) {
          newRows[emptyIdx] = { ...newRows[emptyIdx], source: key };
        } else {
          newRows.push({ id: `r${Date.now()}-p${inserted}`, source: key, canonical: '' });
        }
        inserted++;
      }
      setRows(newRows);
      setDirty(true);
      setPasteOpen(false);
      setPasteText('');
    } catch (err) {
      const msg = err instanceof SyntaxError ? `Geçersiz JSON format: ${err.message}` : 'Geçersiz JSON format.';
      setPasteError(msg);
    }
  }

  async function handleSave() {
    if (!validation.valid) return;
    setSaveError(null);
    const map: Record<string, string> = {};
    for (const r of rows) {
      const s = r.source.trim();
      const c = r.canonical.trim();
      if (s && c) map[s] = c;
    }
    try {
      await onSave(map, phoneCountryHint.trim() || null);
      setDirty(false);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Kayıt başarısız.';
      setSaveError(msg);
    }
  }

  return (
    <Card className="p-5">
      <div className="flex items-center justify-between mb-4">
        <CardTitle className="flex items-center gap-2">
          Alan Eşlemesi (Field Map)
        </CardTitle>
        <Button variant="secondary" onClick={() => setPasteOpen(true)} disabled={busy}>
          <Lightbulb className="w-4 h-4 mr-1" />
          Kaynak Yapıştırma Yardımı
        </Button>
      </div>

      <p className="text-sm text-navy-500 mb-4 leading-relaxed">
        Landing form alanlarınızı (kaynak) Chatinbox canonical alanlarına eşleştirin. Zorunlu:{' '}
        <code className="bg-navy-50 px-1 rounded">phone</code> ve{' '}
        <code className="bg-navy-50 px-1 rounded">consent</code> canonical alanları için en az birer satır olmalı.
      </p>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-navy-500 border-b border-navy-200">
              <th className="py-2 pr-3 font-medium">Kaynak Alan (Landing Form)</th>
              <th className="py-2 pr-3 font-medium">Chatinbox Canonical</th>
              <th className="py-2 w-10" />
            </tr>
          </thead>
          <tbody>
            {rows.map(r => (
              <tr key={r.id} className="border-b border-navy-100">
                <td className="py-2 pr-3">
                  <input
                    type="text"
                    value={r.source}
                    onChange={e => updateRow(r.id, { source: e.target.value })}
                    disabled={busy}
                    placeholder="örnek: ad_soyad"
                    className="w-full border border-navy-200 rounded px-2 py-1 focus:border-navy-400 focus:outline-none"
                  />
                </td>
                <td className="py-2 pr-3">
                  <select
                    value={r.canonical}
                    onChange={e => updateRow(r.id, { canonical: e.target.value })}
                    disabled={busy}
                    className="w-full border border-navy-200 rounded px-2 py-1 focus:border-navy-400 focus:outline-none"
                  >
                    <option value="">— seçin —</option>
                    {CANONICAL_FIELDS.map(c => (
                      <option key={c} value={c}>
                        {c}{REQUIRED_CANONICALS.includes(c) ? ' *' : ''}
                      </option>
                    ))}
                  </select>
                </td>
                <td className="py-2 text-right">
                  <button
                    type="button"
                    aria-label="Satırı sil"
                    onClick={() => removeRow(r.id)}
                    disabled={busy}
                    className="p-1.5 rounded text-navy-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-40"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-3">
        <Button variant="secondary" onClick={addRow} disabled={busy}>
          <Plus className="w-4 h-4 mr-1" />
          Satır Ekle
        </Button>
      </div>

      <div className="mt-5 flex items-end gap-4 flex-wrap">
        <div>
          <label className="block text-xs text-navy-500 mb-1">Telefon Ülke İpucu (ISO 3166-1 alpha-2)</label>
          <input
            type="text"
            maxLength={2}
            value={phoneCountryHint}
            onChange={e => { setPhoneCountryHint(e.target.value.toUpperCase()); setDirty(true); }}
            disabled={busy}
            placeholder="TR"
            className="w-24 border border-navy-200 rounded px-2 py-1 focus:border-navy-400 focus:outline-none text-sm uppercase"
          />
        </div>
      </div>

      {serverDrifted && (
        <div className="mt-4 rounded-md border border-amber-300 bg-amber-50 p-3">
          <div className="flex items-start gap-2">
            <AlertTriangle className="w-4 h-4 text-amber-700 shrink-0 mt-0.5" />
            <div className="flex-1 text-xs text-amber-900">
              <div className="font-semibold">Sunucu tarafında değişiklik tespit edildi</div>
              <div className="mt-0.5">
                Başka bir sekme veya kullanıcı bu ayarları güncelledi. Sizin kaydedilmemiş
                satırlarınız korunuyor. Güncel sunucu haline geçmek için alttaki butonu
                kullanın; aksi halde Kaydet&#39;e basarsanız tekrar 409 alabilirsiniz.
              </div>
              <button
                type="button"
                onClick={acceptServerState}
                disabled={busy}
                className="mt-2 text-xs font-medium text-amber-900 underline hover:text-amber-700 disabled:opacity-40"
              >
                Sunucu halini yükle (değişikliklerimi göz ardı et)
              </button>
            </div>
          </div>
        </div>
      )}
      {validation.errors.length > 0 && (
        <div className="mt-4 rounded-md border border-red-200 bg-red-50 p-3">
          <div className="flex items-start gap-2">
            <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
            <div className="text-xs text-red-800">
              {validation.errors.map((e, i) => <div key={i}>{e}</div>)}
            </div>
          </div>
        </div>
      )}
      {saveError && (
        <div className="mt-3 text-xs text-red-700">{saveError}</div>
      )}

      <div className="mt-5 flex justify-end gap-3">
        <Button
          variant="primary"
          onClick={handleSave}
          disabled={busy || !dirty || !validation.valid}
          title={!validation.valid ? 'Zorunlu alanlar eksik' : dirty ? undefined : 'Kaydedilmemiş değişiklik yok'}
        >
          <Save className="w-4 h-4 mr-1" />
          {busy ? 'Kaydediliyor...' : 'Kaydet'}
        </Button>
      </div>

      {pasteOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          onMouseDown={e => { if (e.target === e.currentTarget) { setPasteOpen(false); setPasteError(null); } }}
        >
          <div className="bg-white rounded-xl shadow-card w-full max-w-xl p-6 relative">
            <button
              type="button"
              aria-label="Kapat"
              onClick={() => { setPasteOpen(false); setPasteError(null); }}
              className="absolute top-3 right-3 p-1.5 rounded-lg text-navy-400 hover:bg-navy-50 hover:text-navy-700"
            >
              <X className="w-4 h-4" />
            </button>
            <h3 className="text-lg font-semibold text-navy-900 mb-2">Kaynak Yapistirma Yardimi</h3>
            <p className="text-sm text-navy-500 mb-3 leading-relaxed">
              Landing formunuzun göndereceği örnek JSON payload&#39;ı yapıştırın; üst seviye anahtarlar
              boş satırlara eklenecek (mevcut kaynak alanlarınız değiştirilmez).
            </p>
            <textarea
              value={pasteText}
              onChange={e => setPasteText(e.target.value)}
              rows={8}
              placeholder='{"ad_soyad": "Nur Seyit", "telefon": "+905551234567", "kvkk_onay": true}'
              className="w-full border border-navy-200 rounded p-2 font-mono text-xs focus:border-navy-400 focus:outline-none"
            />
            {pasteError && <p className="text-xs text-red-600 mt-2">{pasteError}</p>}
            <div className="mt-4 flex justify-end gap-3">
              <Button variant="secondary" onClick={() => { setPasteOpen(false); setPasteError(null); }}>
                Vazgeç
              </Button>
              <Button variant="primary" onClick={extractSourcesFromPaste}>
                Alanları Çıkar
              </Button>
            </div>
          </div>
        </div>
      )}
    </Card>
  );
}
