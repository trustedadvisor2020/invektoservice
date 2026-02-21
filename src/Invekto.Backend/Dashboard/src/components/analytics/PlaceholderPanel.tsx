import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';

interface Props {
  title: string;
  description?: string;
}

export default function PlaceholderPanel({ title, description }: Props) {
  return (
    <Card>
      <CardHeader><CardTitle>{title}</CardTitle></CardHeader>
      <CardContent>
        <div className="flex flex-col items-center justify-center py-8 text-navy-300">
          <div className="w-16 h-16 rounded-full bg-navy-50 flex items-center justify-center mb-3">
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
            </svg>
          </div>
          <p className="text-sm font-medium">Yakinda</p>
          {description && <p className="text-xs mt-1 text-center max-w-xs">{description}</p>}
        </div>
      </CardContent>
    </Card>
  );
}
