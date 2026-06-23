// FEAT-TFM-UI: standalone /settings/field-mapping page.
// Container: loads current tenant mapping (GET) + INMA dynamic fields (for Label column)
// on mount; builds a stable 10-row draft state (cf1..cf10); tracks dirty state so the
// Save button is enabled only on pending changes; submits the full map atomically (PUT)
// and surfaces INV-BE-096..099 / INV-AUTH-010 backend errors inline per-row.
//
// Live client-side validation (matches backend TenantFieldMappingValidator to avoid
// round-trips for trivial errors):
//   - duplicate semantic name across rows
//   - reserved semantic name (InmaDynamicFieldKeys.Allowlist + leads-core columns)
//   - semantic regex mismatch
//   - enum_values required when type=enum
// Non-trivial backend-only rules (e.g. runtime cf range check) stay on server.
//
// Pattern references: LeadIntakeSettingsPage (extractError + toast + page-level load)
// + TemplateCreatePage (form-level submit with dirty tracking).

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { MapPin, Save, RotateCcw, AlertCircle, CheckCircle } from 'lucide-react';
import { api, ApiClientError } from '../../lib/api';
import { Button } from '../../components/ui/Button';
import { Card, CardContent, CardTitle } from '../../components/ui/Card';
import { FieldMappingTableRow } from '../../components/FieldMapping/FieldMappingTableRow';
import { useDynamicFields } from '../../hooks/useDynamicFields';
import {
  ALLOWED_SOURCE_SLOTS,
  SEMANTIC_NAME_PATTERN,
  isReservedSemanticName,
  type TenantFieldType,
} from '../../constants/tenantFieldMappingReserved';
import type {
  FieldMappingRowDraft,
  TenantFieldMappingEntryDto,
  TenantFieldMappingGetResponse,
} from '../../types/tenantFieldMapping';

/**
 * Normalise an API error. We preserve the backend INV-XX-NNN code verbatim when
 * ApiClientError.errorCode is populated (arch/errors.md contract — code family
 * belongs to whichever service threw). When the client does not have a server
 * code (transport/DOM failure), we surface a plain Turkish message WITHOUT
 * fabricating an "INV-TFM-FE-001" style code that would not exist in
 * arch/errors.md — Codex CQ12 flagged that as nonstandard usage.
 */
function extractError(err: unknown, fallback: string): { code: string | null; message: string; status: number } {
  if (err instanceof ApiClientError) {
    const code = err.errorCode && err.errorCode !== 'UNKNOWN' ? err.errorCode : null;
    return {
      code,
      message: err.message || fallback,
      status: err.status,
    };
  }
  const msg = err instanceof Error ? err.message : fallback;
  return { code: null, message: msg, status: 0 };
}

function formatError(e: { code: string | null; message: string }): string {
  return e.code ? '[' + e.code + '] ' + e.message : e.message;
}

/** Build a fresh 10-row draft from a server mapping response (null = empty tenant). */
function buildDraftRows(
  serverMapping: Record<string, TenantFieldMappingEntryDto>,
  inmaLabelBySource: Map<string, string>,
): FieldMappingRowDraft[] {
  // Build reverse map: source (cf1..cf10) -> { semanticName, entry } for quick lookup.
  const reverseMap = new Map<string, { semantic: string; entry: TenantFieldMappingEntryDto }>();
  for (const [semantic, entry] of Object.entries(serverMapping)) {
    reverseMap.set(entry.source.toLowerCase(), { semantic, entry });
  }
  return ALLOWED_SOURCE_SLOTS.map((source) => {
    const existing = reverseMap.get(source);
    return {
      source,
      semanticName: existing?.semantic ?? '',
      type: (existing?.entry.type ?? 'string') as TenantFieldType,
      enumValuesInput: (existing?.entry.enum_values ?? []).join(','),
      required: existing?.entry.required ?? false,
      inmaLabel: inmaLabelBySource.get(source) ?? null,
    };
  });
}

/** Compute { [semantic]: entry } from draft rows, skipping empty rows. */
function draftRowsToMapping(
  rows: FieldMappingRowDraft[],
): Record<string, TenantFieldMappingEntryDto> {
  const map: Record<string, TenantFieldMappingEntryDto> = {};
  for (const row of rows) {
    const semantic = row.semanticName.trim();
    if (semantic.length === 0) continue;
    const entry: TenantFieldMappingEntryDto = {
      source: row.source,
      type: row.type,
    };
    if (row.type === 'enum') {
      entry.enum_values = row.enumValuesInput
        .split(',')
        .map((s) => s.trim())
        .filter((s) => s.length > 0);
    }
    if (row.required) entry.required = true;
    map[semantic] = entry;
  }
  return map;
}

/** Semantic name -> count across all rows (for duplicate detection). */
function countSemanticNames(rows: FieldMappingRowDraft[]): Map<string, number> {
  const counts = new Map<string, number>();
  for (const row of rows) {
    const key = row.semanticName.trim().toLowerCase();
    if (key.length === 0) continue;
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return counts;
}

export function FieldMappingSettingsPage() {
  useEffect(() => { document.title = 'Invekto AI - Field Mapping'; }, []);

  const [draftRows, setDraftRows] = useState<FieldMappingRowDraft[]>([]);
  const [originalSnapshot, setOriginalSnapshot] = useState<string>('{}');
  const [tenantId, setTenantId] = useState<number | null>(null);
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [pageError, setPageError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [rowErrors, setRowErrors] = useState<Record<string, string>>({});

  const inmaFields = useDynamicFields();

  // Toast dismiss timer — tracked via ref so repeated saves cancel the previous
  // timer and an unmount clears the pending one (Codex CQ6 flagged the leak).
  const toastTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(() => {
    return () => {
      if (toastTimerRef.current !== null) {
        clearTimeout(toastTimerRef.current);
        toastTimerRef.current = null;
      }
    };
  }, []);

  // Guard the initial mapping GET against re-firing when INMA dynamic-fields later
  // change (refresh, cache-invalidate) — once mapping is loaded we only need label
  // lookups updated, not a fresh GET.
  const mappingLoadedRef = useRef(false);

  // Build INMA label lookup (cf1 -> 'Sehir'). Empty until inmaFields loads; until then
  // rows render with inmaLabel=null and stay disabled — Q interview confirmed this UX.
  const inmaLabelBySource = useMemo(() => {
    const m = new Map<string, string>();
    for (const f of inmaFields.fields) {
      m.set(f.FieldKey.toLowerCase(), f.FieldName);
    }
    return m;
  }, [inmaFields.fields]);

  const loadMapping = useCallback(async () => {
    setPageError(null);
    try {
      const resp: TenantFieldMappingGetResponse = await api.getTenantFieldMapping();
      const mapping = resp.data.field_mapping ?? {};
      setDraftRows(buildDraftRows(mapping, inmaLabelBySource));
      setOriginalSnapshot(JSON.stringify(mapping));
      setTenantId(resp.data.tenant_id);
      setUpdatedAt(resp.data.updated_at);
      setRowErrors({});
    } catch (err) {
      const e = extractError(err, 'Field mapping yüklenemedi.');
      if (e.status === 403) {
        setPageError('Yetkiniz yok: başka tenant datasına erişemezsiniz.' + (e.code ? ' (' + e.code + ')' : ''));
      } else {
        setPageError(formatError(e));
      }
      // Still give the user an empty-but-usable grid on upstream failure so they
      // can see the 10-slot layout (Codex CQ9: upstream-fail should not trap the
      // page). buildDraftRows({}) builds 10 empty rows; labels may be missing if
      // /api/v1/dynamic-fields also failed — row-level disabled logic handles it.
      setDraftRows(buildDraftRows({}, inmaLabelBySource));
    } finally {
      setLoading(false);
    }
  }, [inmaLabelBySource]);

  useEffect(() => {
    // Fire a single mapping GET once INMA dynamic-fields has resolved (success OR
    // error — we do not block on label availability). Subsequent label refreshes
    // (e.g. user-triggered invalidate) must NOT re-trigger a mapping GET; only the
    // inmaLabelBySource lookup used by each row updates. Codex CQ6 flagged the
    // previous coupling as a lifecycle concern.
    if (inmaFields.isLoading || mappingLoadedRef.current) return;
    mappingLoadedRef.current = true;
    loadMapping();
    // loadMapping closes over inmaLabelBySource at first call; downstream label
    // updates re-project via the useMemo below, not via another GET.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [inmaFields.isLoading]);

  // When INMA labels change (e.g. hook refresh), re-project ONLY the inmaLabel
  // column onto existing drafts — do NOT touch semanticName/type/enum/required.
  useEffect(() => {
    if (!mappingLoadedRef.current) return;
    setDraftRows((rows) =>
      rows.map((r) => ({ ...r, inmaLabel: inmaLabelBySource.get(r.source) ?? null })),
    );
  }, [inmaLabelBySource]);

  const semanticCounts = useMemo(() => countSemanticNames(draftRows), [draftRows]);

  // Dirty check: compare current draft serialized vs original snapshot.
  const isDirty = useMemo(() => {
    const current = JSON.stringify(draftRowsToMapping(draftRows));
    return current !== originalSnapshot;
  }, [draftRows, originalSnapshot]);

  // Block save when any row has a client-detectable error.
  const hasClientError = useMemo(() => {
    for (const row of draftRows) {
      const semantic = row.semanticName.trim();
      if (semantic.length === 0) continue;
      if (isReservedSemanticName(semantic)) return true;
      if (!SEMANTIC_NAME_PATTERN.test(semantic)) return true;
      if ((semanticCounts.get(semantic.toLowerCase()) ?? 0) > 1) return true;
      if (row.type === 'enum' && row.enumValuesInput.trim().length === 0) return true;
    }
    return false;
  }, [draftRows, semanticCounts]);

  const handleRowChange = useCallback((index: number, next: FieldMappingRowDraft) => {
    setDraftRows((rows) => rows.map((r, i) => (i === index ? next : r)));
    // Clear previously-surfaced backend error for this row as the user edits.
    setRowErrors((prev) => {
      if (!prev[next.source]) return prev;
      const copy = { ...prev };
      delete copy[next.source];
      return copy;
    });
  }, []);

  const handleClear = useCallback((index: number) => {
    setDraftRows((rows) =>
      rows.map((r, i) =>
        i === index
          ? { ...r, semanticName: '', type: 'string', enumValuesInput: '', required: false }
          : r,
      ),
    );
  }, []);

  const handleReset = useCallback(() => {
    const original: Record<string, TenantFieldMappingEntryDto> = JSON.parse(originalSnapshot);
    setDraftRows(buildDraftRows(original, inmaLabelBySource));
    setRowErrors({});
    setToast(null);
  }, [originalSnapshot, inmaLabelBySource]);

  const handleSave = useCallback(async () => {
    // tenantId is bootstrapped from the first successful GET response. If we are
    // somehow invoked before load completes (should be unreachable — Save button
    // is disabled while !isDirty and isDirty requires a loaded snapshot) we
    // surface an explicit error rather than silently dropping the click. Codex
    // CQ2 flagged the prior silent return.
    if (!tenantId) {
      setPageError('Tenant bilgisi henüz yüklenmedi. Sayfayı yenileyip tekrar deneyin.');
      return;
    }
    setSaving(true);
    setPageError(null);
    setRowErrors({});
    try {
      const mapping = draftRowsToMapping(draftRows);
      const resp = await api.putTenantFieldMapping({ tenant_id: tenantId, field_mapping: mapping });
      setOriginalSnapshot(JSON.stringify(resp.data.field_mapping ?? {}));
      setUpdatedAt(resp.data.updated_at);
      setDraftRows(buildDraftRows(resp.data.field_mapping ?? {}, inmaLabelBySource));
      setToast('Field mapping kaydedildi.');
      // Auto-dismiss toast after 3s. Track via ref so repeated saves cancel the
      // previous pending timer and unmount clears it.
      if (toastTimerRef.current !== null) clearTimeout(toastTimerRef.current);
      toastTimerRef.current = setTimeout(() => {
        setToast(null);
        toastTimerRef.current = null;
      }, 3000);
    } catch (err) {
      const e = extractError(err, 'Kaydedilemedi.');
      if (e.status === 400) {
        // Map INV-BE-096..099 to the offending row when possible. Backend message
        // includes the semantic name via TenantFieldMappingValidationException.SemanticName;
        // ApiClientError.message retains that text so we scan for a match.
        const rowMatch = draftRows.find((r) => e.message.includes("'" + r.semanticName + "'") || e.message.includes('"' + r.semanticName + '"'));
        if (rowMatch) {
          setRowErrors({ [rowMatch.source]: formatError(e) });
        } else {
          setPageError(formatError(e));
        }
      } else if (e.status === 403) {
        setPageError('Yetkiniz yok (INV-AUTH-010). Lütfen kendi tenant hesabınızla deneyin.');
      } else {
        setPageError(formatError(e));
      }
    } finally {
      setSaving(false);
    }
  }, [tenantId, draftRows, inmaLabelBySource]);

  if (loading) {
    return (
      <div className="p-6 text-navy-500">Field mapping yükleniyor...</div>
    );
  }

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex items-center gap-3 mb-2">
        <MapPin size={24} className="text-brand-500" />
        <h1 className="text-2xl font-semibold text-navy-900">Field Mapping</h1>
      </div>
      <p className="text-sm text-navy-600 mb-6">
        INMA'nin 10 custom field slotuna (cf1..cf10) tenant'a özel semantic isimler bağla.
        Örnek: <code className="bg-navy-50 px-1 rounded">roadshow_city</code> &rarr; cf1 (enum: Dublin, Cork).
        FlowBuilder + Template editörde semantic isim kullanılabilir.
      </p>

      {/* INMA dynamic-fields hook error banner — covers ALL 4 documented errorKinds
          (useDynamicFields DynamicFieldsErrorKind: not_configured | upstream_fail |
          invalidate_partial | unknown). Codex CQ1 flagged the prior 2-branch version. */}
      {inmaFields.errorKind === 'not_configured' && (
        <div className="mb-4 p-3 bg-amber-50 border border-amber-200 rounded flex items-start gap-2">
          <AlertCircle size={16} className="text-amber-600 mt-0.5" />
          <div className="text-sm text-amber-800">
            INMA config edilmemiş (INV-OB-038). Tenant admin paneli üzerinden en az 1 custom
            field aktif edildikten sonra bu sayfada semantic mapping girebilirsiniz. Mevcut
            kaydedilmiş mapping'ler editable kalır.
          </div>
        </div>
      )}
      {inmaFields.errorKind === 'upstream_fail' && (
        <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded flex items-start gap-2">
          <AlertCircle size={16} className="text-red-600 mt-0.5" />
          <div className="text-sm text-red-800">
            INMA servisine ulaşılamıyor (INV-OB-037). Label bilgileri geçici olarak boş
            görünecek; mevcut kaydedilmiş mapping'ler editable kalır. Birkaç dakika sonra
            sayfayı yenileyerek tekrar deneyin.
          </div>
        </div>
      )}
      {inmaFields.errorKind === 'invalidate_partial' && (
        <div className="mb-4 p-3 bg-amber-50 border border-amber-200 rounded flex items-start gap-2">
          <AlertCircle size={16} className="text-amber-600 mt-0.5" />
          <div className="text-sm text-amber-800">
            Cache yenileme kısmen başarısız (INV-OB-037). Gösterilen label listesi en fazla
            1 saat bayat olabilir. Sonuçlar bekleneni karşılamıyorsa sayfayı yenileyin.
          </div>
        </div>
      )}
      {inmaFields.errorKind === 'unknown' && (
        <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded flex items-start gap-2">
          <AlertCircle size={16} className="text-red-600 mt-0.5" />
          <div className="text-sm text-red-800">
            INMA label listesi alınamadı (bilinmeyen hata). Auth durumunuzu kontrol edip
            sayfayı yenileyin. Sorun devam ederse destek ekibine bildirin.
          </div>
        </div>
      )}
      {pageError && (
        <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded flex items-start gap-2">
          <AlertCircle size={16} className="text-red-600 mt-0.5" />
          <div className="text-sm text-red-800">{pageError}</div>
        </div>
      )}
      {toast && (
        <div className="mb-4 p-3 bg-green-50 border border-green-200 rounded flex items-start gap-2">
          <CheckCircle size={16} className="text-green-600 mt-0.5" />
          <div className="text-sm text-green-800">{toast}</div>
        </div>
      )}

      <Card>
        <CardContent className="p-0">
          <CardTitle className="px-4 py-3 border-b border-navy-100">
            Mapping Tablosu (cf1..cf10)
            {updatedAt && (
              <span className="ml-2 text-xs font-normal text-navy-400">
                Son güncellenme: {new Date(updatedAt).toLocaleString('tr-TR')}
              </span>
            )}
          </CardTitle>
          <table className="w-full text-left">
            <thead className="bg-navy-50 border-b border-navy-100">
              <tr>
                <th className="px-3 py-2 text-xs font-medium text-navy-600 uppercase w-16">INMA Key</th>
                <th className="px-3 py-2 text-xs font-medium text-navy-600 uppercase w-40">INMA Label</th>
                <th className="px-3 py-2 text-xs font-medium text-navy-600 uppercase">Semantic</th>
                <th className="px-3 py-2 text-xs font-medium text-navy-600 uppercase w-32">Tip</th>
                <th className="px-3 py-2 text-xs font-medium text-navy-600 uppercase">Enum Değerleri</th>
                <th className="px-3 py-2 text-xs font-medium text-navy-600 uppercase text-center w-20">Zorunlu</th>
                <th className="px-3 py-2 text-xs font-medium text-navy-600 uppercase text-right w-24">Action</th>
              </tr>
            </thead>
            <tbody>
              {draftRows.map((row, idx) => {
                const semanticLower = row.semanticName.trim().toLowerCase();
                const isDup = semanticLower.length > 0 && (semanticCounts.get(semanticLower) ?? 0) > 1;
                return (
                  <FieldMappingTableRow
                    key={row.source}
                    draft={row}
                    isDuplicateSemantic={isDup}
                    rowError={rowErrors[row.source] ?? null}
                    onChange={(next) => handleRowChange(idx, next)}
                    onClear={() => handleClear(idx)}
                  />
                );
              })}
            </tbody>
          </table>
        </CardContent>
      </Card>

      <div className="mt-6 flex items-center justify-between">
        <p className="text-xs text-navy-500">
          {isDirty ? 'Kaydedilmemiş değişiklikler var.' : 'Tüm değişiklikler kaydedildi.'}
        </p>
        <div className="flex gap-2">
          <Button
            variant="secondary"
            onClick={handleReset}
            disabled={!isDirty || saving}
          >
            <RotateCcw size={14} className="mr-1" />
            Vazgeç
          </Button>
          <Button
            onClick={handleSave}
            disabled={!isDirty || saving || hasClientError}
          >
            <Save size={14} className="mr-1" />
            {saving ? 'Kaydediliyor...' : 'Kaydet'}
          </Button>
        </div>
      </div>
    </div>
  );
}
