import { Fragment, type ChangeEvent } from 'react';
import type { FollowupStageDraft } from '../../types/followupSequence';

// FEAT-EFS Drip Sequence — single editable stage row inside the editor table.
//
// Per-row error rendering uses Fragment + sibling <tr> (lessons 2026-04-22 P3
// CQ5+CQ10 FAIL): NEVER drop an extra <td colSpan=N> inside the same <tr> —
// browsers technically render it but accessibility + table grid semantics
// break. Pattern: <Fragment><tr>main row</tr>{rowError && <tr>error row</tr>}</Fragment>.
// Parent map() supplies the key on the Fragment via React.Children semantics
// (key on Fragment is a runtime warning in React but accepted; alternatively
// the parent can render <Fragment key={draftId}> wrapper).

interface FollowupStageRowProps {
  index: number;
  draft: FollowupStageDraft;
  testMode: boolean;
  onChange: (next: FollowupStageDraft) => void;
  onRemove: () => void;
  /** Disabled when the parent form is mid-save or upstream load failed and there is no existing data. */
  disabled: boolean;
}

export function FollowupStageRow({
  index,
  draft,
  testMode,
  onChange,
  onRemove,
  disabled,
}: FollowupStageRowProps) {
  const unitLabel = testMode ? 'dk' : 'gun';

  const handleField = (field: keyof FollowupStageDraft) =>
    (e: ChangeEvent<HTMLInputElement>) => {
      onChange({ ...draft, [field]: e.target.value });
    };

  return (
    <Fragment>
      <tr className="border-b border-gray-200 hover:bg-gray-50">
        <td className="py-2 px-3 text-sm text-gray-700 font-mono">{index + 1}</td>
        <td className="py-2 px-3">
          <input
            type="number"
            min={1}
            max={30}
            value={draft.delayDaysInput}
            onChange={handleField('delayDaysInput')}
            disabled={disabled}
            className="w-20 px-2 py-1 border border-gray-300 rounded text-sm disabled:bg-gray-100"
            aria-label={`Stage ${index + 1} delay (${unitLabel})`}
          />
          <span className="ml-2 text-xs text-gray-500">{unitLabel}</span>
        </td>
        <td className="py-2 px-3">
          <input
            type="text"
            value={draft.templateSlug}
            onChange={handleField('templateSlug')}
            disabled={disabled}
            placeholder="ornek: post-roadshow-day-3"
            className="w-full px-2 py-1 border border-gray-300 rounded text-sm font-mono disabled:bg-gray-100"
            aria-label={`Stage ${index + 1} template slug`}
          />
        </td>
        <td className="py-2 px-3">
          <input
            type="text"
            value={draft.templateGroup}
            onChange={handleField('templateGroup')}
            disabled={disabled}
            placeholder="(opsiyonel rotation grup)"
            className="w-full px-2 py-1 border border-gray-300 rounded text-sm font-mono disabled:bg-gray-100"
            aria-label={`Stage ${index + 1} template group`}
          />
        </td>
        <td className="py-2 px-3 text-right">
          <button
            type="button"
            onClick={onRemove}
            disabled={disabled}
            className="text-xs text-red-600 hover:text-red-800 disabled:text-gray-400 disabled:cursor-not-allowed"
            aria-label={`Remove stage ${index + 1}`}
          >
            Sil
          </button>
        </td>
      </tr>
      {draft.rowError && (
        <tr className="bg-red-50 border-b border-red-200">
          <td colSpan={5} className="py-2 px-3 text-xs text-red-700">
            <span className="font-semibold">Stage {index + 1}:</span> {draft.rowError}
          </td>
        </tr>
      )}
    </Fragment>
  );
}
