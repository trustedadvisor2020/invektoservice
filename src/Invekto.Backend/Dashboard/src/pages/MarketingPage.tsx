import { Star } from 'lucide-react';

export function MarketingPage() {
  return (
    <div className="flex flex-col items-center justify-center h-64 text-slate-400 gap-3">
      <Star className="w-10 h-10 opacity-40" />
      <p className="text-sm font-medium">Pazarlama — Yakin zamanda</p>
    </div>
  );
}
