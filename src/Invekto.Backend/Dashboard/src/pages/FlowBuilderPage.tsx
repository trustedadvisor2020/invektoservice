import { useAuth } from '../hooks/useAuth';

export function FlowBuilderPage() {
  const { session } = useAuth();

  return (
    <div className="h-screen">
      <iframe
        src={`/flow-builder/?tenant=${session?.tenantId ?? ''}`}
        className="w-full h-full border-0"
        title="Flow Builder"
        allow="clipboard-read; clipboard-write"
      />
    </div>
  );
}
