import { CalendarDays } from 'lucide-react';

export function AppointmentsPage() {
  return (
    <div className="flex flex-col items-center justify-center h-64 text-slate-400 gap-3">
      <CalendarDays className="w-10 h-10 opacity-40" />
      <p className="text-sm font-medium">Randevular — Yakin zamanda</p>
    </div>
  );
}
