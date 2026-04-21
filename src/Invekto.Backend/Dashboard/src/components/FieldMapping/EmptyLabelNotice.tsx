// FEAT-TFM-UI: disabled row for cf slots that INMA has NOT configured yet.
// Shown in the Field Mapping table when /api/v1/dynamic-fields response does
// not include the slot (tenant's CustomFields table does not have that cf
// enabled). Rows stay visible (preserves 10-slot overview) but are read-only
// with an italic explanatory note + tooltip.

import { Info } from 'lucide-react';

interface EmptyLabelNoticeProps {
  source: string; // e.g. 'cf3'
}

export function EmptyLabelNotice({ source }: EmptyLabelNoticeProps) {
  return (
    <span
      className="inline-flex items-center gap-1 italic text-navy-300 text-sm"
      title={`INMA admin panelinde ${source}'i aktif ettikten sonra semantic mapping girebilirsiniz.`}
    >
      (INMA config edilmemis)
      <Info size={12} className="text-navy-300" />
    </span>
  );
}
