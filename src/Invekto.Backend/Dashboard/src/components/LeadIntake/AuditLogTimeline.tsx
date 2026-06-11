// FEAT-LIW Chunk C: audit history timeline. Reads last 50 liw_audit_log entries
// newest-first; refreshed on mount + after every successful mutation elsewhere
// on the page. Summary line is client-computed from before_json/after_json;
// clicking an entry expands the raw JSON in a <details> block.
import { Card, CardTitle } from '../ui/Card';
import { Clock, KeyRound, ShieldOff, Save } from 'lucide-react';
import type { LiwAuditEntryDto, LiwAuditAction } from '../../types/leadIntake';

interface AuditLogTimelineProps {
  entries: LiwAuditEntryDto[];
  loading: boolean;
}

const ACTION_LABELS: Record<LiwAuditAction, { label: string; icon: typeof KeyRound }> = {
  'apikey.rotate':       { label: 'API anahtarı yenilendi',       icon: KeyRound },
  'apikey.revoke':       { label: 'API anahtarı iptal edildi',    icon: ShieldOff },
  'fieldmap.save':       { label: 'Alan eşlemesi güncellendi',    icon: Save },
  'welcome_slug.change': { label: 'Welcome akışı değişti',         icon: Save },
};

function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'az önce';
  if (minutes < 60) return `${minutes} dk önce`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} saat önce`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days} gün önce`;
  return new Date(iso).toLocaleDateString('tr-TR');
}

function summarizeEntry(entry: LiwAuditEntryDto): string {
  if (entry.action === 'apikey.rotate') {
    const beforeMasked = (entry.before_json?.masked_active_key as string | null | undefined) ?? null;
    const afterMasked = (entry.after_json?.masked_active_key as string | null | undefined) ?? null;
    if (!beforeMasked && afterMasked) return `İlk anahtar oluşturuldu: ${afterMasked}`;
    if (beforeMasked && afterMasked) return `Eski: ${beforeMasked} -> Yeni: ${afterMasked}`;
    return 'Anahtar yenilendi';
  }
  if (entry.action === 'apikey.revoke') return 'Landing webhook kanalı devre dışı bırakıldı';
  if (entry.action === 'fieldmap.save') {
    const beforeKeys = countFieldMap(entry.before_json);
    const afterKeys = countFieldMap(entry.after_json);
    return `${beforeKeys} satır -> ${afterKeys} satır`;
  }
  if (entry.action === 'welcome_slug.change') {
    const b = (entry.before_json?.welcome_flow_slug as string | null | undefined) ?? null;
    const a = (entry.after_json?.welcome_flow_slug as string | null | undefined) ?? null;
    return `${b ?? 'default'} -> ${a ?? 'default'}`;
  }
  return '';
}

function countFieldMap(obj: Record<string, unknown> | null): number {
  if (!obj) return 0;
  return Object.keys(obj).filter(k => k !== 'phone.country_hint').length;
}

export function AuditLogTimeline({ entries, loading }: AuditLogTimelineProps) {
  return (
    <Card className="p-5">
      <CardTitle className="flex items-center gap-2 mb-4">
        <Clock className="w-4 h-4 text-navy-600" />
        Değişiklik Geçmişi
      </CardTitle>

      {loading && entries.length === 0 && (
        <div className="text-xs text-navy-400">Yükleniyor...</div>
      )}
      {!loading && entries.length === 0 && (
        <div className="text-xs text-navy-400">Henüz değişiklik kaydı yok.</div>
      )}

      <ol className="space-y-3">
        {entries.map(entry => {
          const meta = ACTION_LABELS[entry.action] ?? { label: entry.action, icon: Clock };
          const Icon = meta.icon;
          const summary = summarizeEntry(entry);
          return (
            <li key={entry.id} className="border-l-2 border-navy-200 pl-3">
              <div className="flex items-center gap-2 text-sm text-navy-800">
                <Icon className="w-3.5 h-3.5 text-navy-500" />
                <span className="font-medium">{meta.label}</span>
                <span className="text-xs text-navy-400">• {relativeTime(entry.created_at)}</span>
                <span className="text-xs text-navy-400">• {entry.user_display}</span>
              </div>
              {summary && <div className="text-xs text-navy-500 mt-0.5 ml-5">{summary}</div>}
              <details className="ml-5 mt-1">
                <summary className="text-xs text-navy-400 cursor-pointer select-none hover:text-navy-600">
                  Ham JSON
                </summary>
                <pre className="mt-2 bg-navy-50 border border-navy-200 rounded p-2 text-[11px] font-mono overflow-x-auto">
{JSON.stringify({ before: entry.before_json, after: entry.after_json }, null, 2)}
                </pre>
              </details>
            </li>
          );
        })}
      </ol>
    </Card>
  );
}
