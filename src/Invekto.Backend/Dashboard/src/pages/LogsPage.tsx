import { useSearchParams } from 'react-router-dom';
import { LogStream } from '../components/LogStream';

export function LogsPage() {
  const [searchParams] = useSearchParams();

  const initialFilter = {
    levels: searchParams.get('level')?.split(',') || undefined,
    service: searchParams.get('service') || undefined,
    search: searchParams.get('search') || undefined,
    after: searchParams.get('after') || undefined,
  };

  return (
    <div className="h-[calc(100vh-theme(spacing.24))]">
      <div className="mb-4">
        <h1 className="text-2xl font-bold">Logs</h1>
        <p className="text-gray-400">Log izleme ve arama</p>
      </div>
      <LogStream initialFilter={initialFilter} />
    </div>
  );
}
