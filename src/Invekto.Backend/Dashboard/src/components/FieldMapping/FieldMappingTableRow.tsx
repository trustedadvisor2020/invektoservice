// FEAT-TFM-UI: single cf slot row in the field-mapping table.
// Renders read-only columns (INMA Key, INMA Label) + inline-editable columns
// (Semantic, Type, Enum Values, Required) + Clear action. The parent
// FieldMappingSettingsPage owns draft state; this component is purely presentational
// and emits onChange / onClear callbacks.
//
// Validation surfaced inline:
//   - duplicate semantic name (reported by parent via isDuplicateSemantic)
//   - reserved name (self-checked via isReservedSemanticName)
//   - semantic regex mismatch (self-checked via SEMANTIC_NAME_PATTERN)
// Backend-only validation (enum empty when type=enum) surfaces post-save via rowError.

import { Fragment } from 'react';
import { Trash2 } from 'lucide-react';
import {
  ALLOWED_FIELD_TYPES,
  SEMANTIC_NAME_PATTERN,
  isReservedSemanticName,
  type TenantFieldType,
} from '../../constants/tenantFieldMappingReserved';
import type { FieldMappingRowDraft } from '../../types/tenantFieldMapping';
import { EmptyLabelNotice } from './EmptyLabelNotice';

interface FieldMappingTableRowProps {
  draft: FieldMappingRowDraft;
  /** True when another row already uses this draft.semanticName (case-insensitive). */
  isDuplicateSemantic: boolean;
  /** Post-save backend error for this row (INV-BE-096..099), cleared by parent on next edit. */
  rowError: string | null;
  onChange: (next: FieldMappingRowDraft) => void;
  onClear: () => void;
}

const TYPE_OPTIONS: ReadonlyArray<{ value: TenantFieldType; label: string }> = [
  { value: 'enum', label: 'Enum' },
  { value: 'string', label: 'String' },
  { value: 'date', label: 'Tarih' },
  { value: 'bool', label: 'Boolean' },
  { value: 'int', label: 'Int' },
];

export function FieldMappingTableRow({
  draft,
  isDuplicateSemantic,
  rowError,
  onChange,
  onClear,
}: FieldMappingTableRowProps) {
  const mapped = draft.semanticName.trim().length > 0;
  // Row is locked for NEW mapping only when INMA has not enabled the slot AND no
  // mapping currently exists. Pre-existing mappings stay editable even if the
  // /api/v1/dynamic-fields call failed — otherwise an INMA outage would trap the
  // tenant out of their own saved configuration (Codex CQ9 flagged this path).
  const disabled = draft.inmaLabel === null && !mapped;

  // Live client-side validation (matches backend TenantFieldMappingValidator rules
  // so the user sees the problem without a round-trip).
  let semanticError: string | null = null;
  if (mapped) {
    if (isReservedSemanticName(draft.semanticName)) {
      semanticError = '"' + draft.semanticName + '" rezerve kelime (INMA Allowlist / leads kolon / cfN sablonu); farkli semantic secin.';
    } else if (!SEMANTIC_NAME_PATTERN.test(draft.semanticName)) {
      semanticError = 'Kucuk harfle baslayan, a-z 0-9 _ iceren 2-64 karakter.';
    } else if (isDuplicateSemantic) {
      semanticError = 'Bu semantic isim birden fazla cf slotunda kullaniliyor — isimler benzersiz olmali.';
    }
  } else if (draft.type === 'enum' && draft.enumValuesInput.trim().length > 0) {
    // Edge: user started typing enum values without semantic — remind to add semantic.
    semanticError = 'Enum degerleri icin once semantic isim girin.';
  }

  // Enum values required when type=enum AND row is mapped.
  const enumError = mapped && draft.type === 'enum' && draft.enumValuesInput.trim().length === 0
    ? 'Enum icin en az 1 deger (virgulle ayrilmis).'
    : null;

  const anyError = semanticError || enumError || rowError;

  // Tailwind rowbg/border signals: error=red, disabled=soft gray, mapped-ok=white.
  const rowClass = disabled
    ? 'bg-navy-25'
    : anyError
      ? 'bg-red-25'
      : mapped
        ? 'bg-white'
        : 'bg-white';

  return (
    <Fragment>
    <tr className={`${rowClass} ${rowError ? '' : 'border-b border-navy-100'} align-top`}>
      {/* INMA Key — readonly */}
      <td className="px-3 py-2 text-sm font-mono text-navy-700">{draft.source}</td>

      {/* INMA Label — readonly, from useDynamicFields */}
      <td className="px-3 py-2 text-sm text-navy-700">
        {draft.inmaLabel === null
          ? <EmptyLabelNotice source={draft.source} />
          : <span>{draft.inmaLabel}</span>}
      </td>

      {/* Semantic name — editable */}
      <td className="px-3 py-2">
        <input
          type="text"
          value={draft.semanticName}
          disabled={disabled}
          placeholder={disabled ? '' : 'ornek: roadshow_city'}
          onChange={(e) => onChange({ ...draft, semanticName: e.target.value })}
          className={`w-full h-9 px-2 bg-white border rounded text-sm text-navy-900 placeholder:text-navy-300 focus:outline-none ${
            semanticError
              ? 'border-red-400 focus:border-red-500'
              : 'border-navy-100 focus:border-brand-500'
          } ${disabled ? 'opacity-40 cursor-not-allowed' : ''}`}
        />
        {semanticError && (
          <p className="mt-1 text-xs text-red-500">{semanticError}</p>
        )}
      </td>

      {/* Type — editable */}
      <td className="px-3 py-2">
        <select
          value={draft.type}
          disabled={disabled}
          onChange={(e) => onChange({ ...draft, type: e.target.value as TenantFieldType })}
          className={`w-full h-9 px-2 bg-white border border-navy-100 rounded text-sm text-navy-900 focus:outline-none focus:border-brand-500 ${
            disabled ? 'opacity-40 cursor-not-allowed' : ''
          }`}
        >
          {TYPE_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
          {ALLOWED_FIELD_TYPES.length > TYPE_OPTIONS.length && null /* ensure full list used */}
        </select>
      </td>

      {/* Enum values — editable, visible only when type=enum */}
      <td className="px-3 py-2">
        {draft.type === 'enum' ? (
          <>
            <input
              type="text"
              value={draft.enumValuesInput}
              disabled={disabled}
              placeholder="ornek: Dublin,Cork,Galway"
              onChange={(e) => onChange({ ...draft, enumValuesInput: e.target.value })}
              className={`w-full h-9 px-2 bg-white border rounded text-sm text-navy-900 placeholder:text-navy-300 focus:outline-none ${
                enumError
                  ? 'border-red-400 focus:border-red-500'
                  : 'border-navy-100 focus:border-brand-500'
              } ${disabled ? 'opacity-40 cursor-not-allowed' : ''}`}
            />
            {enumError && (
              <p className="mt-1 text-xs text-red-500">{enumError}</p>
            )}
          </>
        ) : (
          <span className="text-sm text-navy-300 italic">-</span>
        )}
      </td>

      {/* Required — editable */}
      <td className="px-3 py-2 text-center">
        <input
          type="checkbox"
          checked={draft.required}
          disabled={disabled || !mapped}
          onChange={(e) => onChange({ ...draft, required: e.target.checked })}
          className="w-4 h-4 accent-brand-500 cursor-pointer disabled:cursor-not-allowed disabled:opacity-40"
        />
      </td>

      {/* Action — clear mapping */}
      <td className="px-3 py-2 text-right">
        {mapped && !disabled && (
          <button
            type="button"
            onClick={onClear}
            className="inline-flex items-center gap-1 px-2 py-1 text-xs text-red-500 hover:bg-red-50 rounded transition-colors"
            title="Bu cf slotunun semantic mapping'ini temizle"
          >
            <Trash2 size={12} />
            Temizle
          </button>
        )}
      </td>

    </tr>
    {/* Sibling <tr> for backend validation error so the 7-column grid stays intact
        (Codex CQ5/CQ10: extra <td> inside the same row produced invalid markup). */}
    {rowError && (
      <tr className="border-b border-red-200 bg-red-25">
        <td colSpan={7} className="px-3 py-2 text-xs text-red-600">
          <strong>Sunucu hatasi:</strong> {rowError}
        </td>
      </tr>
    )}
    </Fragment>
  );
}
