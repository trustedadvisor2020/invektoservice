import { useState } from 'react';
import {
  Send,
  Phone,
  Hash,
  Loader2,
  CheckCircle,
  XCircle,
  AlertTriangle,
  MessageSquare,
  Clock,
  TrendingUp,
  Tag,
  FileText,
  Sparkles
} from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { Input } from '../components/ui/Input';
import { Badge } from '../components/ui/Badge';

interface AnalysisResult {
  requestId: string;
  phoneNumber: string;
  messageCount: number;
  analysis: {
    sentiment: string;
    category: string;
    summary: string;
    confidence: number;
  };
  analyzedAt: string;
}

interface ErrorResult {
  errorCode: string;
  message: string;
  detail?: string;
  requestId: string;
}

interface LogEntry {
  time: string;
  type: 'info' | 'success' | 'error' | 'warning';
  message: string;
}

export function ChatAnalysisPage() {
  const [phoneNumber, setPhoneNumber] = useState('');
  const [instanceId, setInstanceId] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [error, setError] = useState<ErrorResult | null>(null);
  const [logs, setLogs] = useState<LogEntry[]>([]);

  const addLog = (type: LogEntry['type'], message: string) => {
    setLogs(prev => [...prev, {
      time: new Date().toLocaleTimeString('tr-TR'),
      type,
      message
    }]);
  };

  const clearLogs = () => {
    setLogs([]);
    setResult(null);
    setError(null);
  };

  const handleAnalyze = async () => {
    if (!phoneNumber.trim()) return;

    clearLogs();
    setIsLoading(true);
    addLog('info', `Analiz başlatılıyor: ${phoneNumber}`);

    try {
      const startTime = Date.now();
      addLog('info', 'Backend\'e istek gönderiliyor...');

      const response = await fetch('/api/v1/chat/analyze', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          phoneNumber: phoneNumber.trim(),
          instanceId: instanceId.trim() || undefined
        }),
      });

      const duration = Date.now() - startTime;
      const data = await response.json();

      if (response.ok && data.analysis) {
        addLog('success', `WapCRM\'den ${data.messageCount} mesaj alındı`);
        addLog('success', `Claude analizi tamamlandı (${duration}ms)`);
        addLog('success', `Sentiment: ${data.analysis.sentiment}, Kategori: ${data.analysis.category}`);
        setResult(data);
        setError(null);
      } else if (data.status === 'partial') {
        addLog('warning', `Kısmi sonuç: ${data.warning}`);
        setError({
          errorCode: data.errorCode || 'PARTIAL',
          message: data.warning || 'Kısmi sonuç',
          requestId: data.requestId
        });
        setResult(null);
      } else {
        addLog('error', `Hata: ${data.message || data.error}`);
        setError({
          errorCode: data.errorCode || 'ERROR',
          message: data.message || 'Bilinmeyen hata',
          detail: data.detail,
          requestId: data.requestId
        });
        setResult(null);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Bağlantı hatası';
      addLog('error', message);
      setError({
        errorCode: 'CONNECTION_ERROR',
        message,
        requestId: ''
      });
      setResult(null);
    } finally {
      setIsLoading(false);
    }
  };

  const getSentimentColor = (sentiment: string) => {
    switch (sentiment?.toLowerCase()) {
      case 'positive': return 'text-emerald-600 bg-emerald-50 border-emerald-200';
      case 'negative': return 'text-red-600 bg-red-50 border-red-200';
      case 'neutral': return 'text-slate-600 bg-slate-50 border-slate-200';
      default: return 'text-blue-600 bg-blue-50 border-blue-200';
    }
  };

  const getSentimentIcon = (sentiment: string) => {
    switch (sentiment?.toLowerCase()) {
      case 'positive': return <TrendingUp className="w-5 h-5" />;
      case 'negative': return <AlertTriangle className="w-5 h-5" />;
      default: return <MessageSquare className="w-5 h-5" />;
    }
  };

  const getLogIcon = (type: LogEntry['type']) => {
    switch (type) {
      case 'success': return <CheckCircle className="w-4 h-4 text-emerald-500" />;
      case 'error': return <XCircle className="w-4 h-4 text-red-500" />;
      case 'warning': return <AlertTriangle className="w-4 h-4 text-amber-500" />;
      default: return <Clock className="w-4 h-4 text-blue-500" />;
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-violet-500 to-purple-600 flex items-center justify-center shadow-lg">
          <Sparkles className="w-6 h-6 text-white" />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Chat Analysis</h1>
          <p className="text-sm text-slate-500">WhatsApp mesajlarını Claude ile analiz et</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Input Panel */}
        <div className="lg:col-span-1 space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Phone className="w-4 h-4" />
                Analiz Parametreleri
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <label className="text-sm font-medium text-slate-700">Telefon Numarası *</label>
                <div className="relative">
                  <Phone className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                  <Input
                    type="tel"
                    placeholder="+905551234567"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    className="pl-10"
                  />
                </div>
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium text-slate-700">Instance ID (Opsiyonel)</label>
                <div className="relative">
                  <Hash className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                  <Input
                    type="text"
                    placeholder="default"
                    value={instanceId}
                    onChange={(e) => setInstanceId(e.target.value)}
                    className="pl-10"
                  />
                </div>
              </div>

              <Button
                className="w-full"
                onClick={handleAnalyze}
                disabled={isLoading || !phoneNumber.trim()}
              >
                {isLoading ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <Send className="w-4 h-4" />
                )}
                <span>{isLoading ? 'Analiz ediliyor...' : 'Analiz Et'}</span>
              </Button>
            </CardContent>
          </Card>

          {/* Logs Panel */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="flex items-center gap-2">
                  <FileText className="w-4 h-4" />
                  İşlem Logları
                </CardTitle>
                {logs.length > 0 && (
                  <button
                    onClick={clearLogs}
                    className="text-xs text-slate-400 hover:text-slate-600"
                  >
                    Temizle
                  </button>
                )}
              </div>
            </CardHeader>
            <CardContent>
              {logs.length === 0 ? (
                <div className="text-center py-8 text-slate-400">
                  <Clock className="w-8 h-8 mx-auto mb-2 opacity-50" />
                  <p className="text-sm">Henüz işlem yok</p>
                </div>
              ) : (
                <div className="space-y-2 max-h-64 overflow-y-auto">
                  {logs.map((log, idx) => (
                    <div
                      key={idx}
                      className="flex items-start gap-2 p-2 rounded-lg bg-slate-50 text-sm"
                    >
                      {getLogIcon(log.type)}
                      <div className="flex-1 min-w-0">
                        <p className="text-slate-700 break-words">{log.message}</p>
                        <p className="text-xs text-slate-400">{log.time}</p>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Results Panel */}
        <div className="lg:col-span-2">
          {/* Loading State */}
          {isLoading && (
            <Card>
              <CardContent className="py-16">
                <div className="flex flex-col items-center justify-center">
                  <div className="relative">
                    <div className="w-16 h-16 rounded-full bg-gradient-to-br from-violet-500 to-purple-600 flex items-center justify-center">
                      <Sparkles className="w-8 h-8 text-white animate-pulse" />
                    </div>
                    <div className="absolute inset-0 rounded-full border-4 border-violet-200 border-t-violet-500 animate-spin" />
                  </div>
                  <p className="mt-4 text-lg font-medium text-slate-700">Analiz ediliyor...</p>
                  <p className="text-sm text-slate-500">WapCRM + Claude Haiku</p>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Error State */}
          {!isLoading && error && (
            <Card className="border-red-200 bg-red-50/50">
              <CardContent className="py-8">
                <div className="flex flex-col items-center text-center">
                  <div className="w-16 h-16 rounded-full bg-red-100 flex items-center justify-center mb-4">
                    <XCircle className="w-8 h-8 text-red-500" />
                  </div>
                  <h3 className="text-lg font-semibold text-red-700 mb-2">
                    {error.message}
                  </h3>
                  {error.detail && (
                    <p className="text-sm text-red-600 mb-3">{error.detail}</p>
                  )}
                  <div className="flex gap-2">
                    <Badge variant="error">{error.errorCode}</Badge>
                    {error.requestId && (
                      <Badge variant="default" className="font-mono text-xs">
                        {error.requestId.slice(0, 8)}...
                      </Badge>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Success State */}
          {!isLoading && result && (
            <div className="space-y-4">
              {/* Summary Card */}
              <Card className="border-emerald-200 bg-gradient-to-br from-emerald-50/50 to-white">
                <CardContent className="py-6">
                  <div className="flex items-start gap-4">
                    <div className={`w-14 h-14 rounded-xl flex items-center justify-center border-2 ${getSentimentColor(result.analysis.sentiment)}`}>
                      {getSentimentIcon(result.analysis.sentiment)}
                    </div>
                    <div className="flex-1">
                      <div className="flex items-center gap-2 mb-2">
                        <h3 className="text-lg font-semibold text-slate-900">Analiz Sonucu</h3>
                        <Badge variant="success">Başarılı</Badge>
                      </div>
                      <p className="text-slate-600 leading-relaxed">{result.analysis.summary}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>

              {/* Metrics Grid */}
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <Card className="text-center">
                  <CardContent className="py-4">
                    <div className={`inline-flex items-center justify-center w-10 h-10 rounded-lg mb-2 ${getSentimentColor(result.analysis.sentiment)}`}>
                      {getSentimentIcon(result.analysis.sentiment)}
                    </div>
                    <p className="text-xs text-slate-500 uppercase tracking-wide">Sentiment</p>
                    <p className="text-lg font-bold text-slate-900 capitalize">{result.analysis.sentiment}</p>
                  </CardContent>
                </Card>

                <Card className="text-center">
                  <CardContent className="py-4">
                    <div className="inline-flex items-center justify-center w-10 h-10 rounded-lg bg-blue-50 text-blue-600 mb-2">
                      <Tag className="w-5 h-5" />
                    </div>
                    <p className="text-xs text-slate-500 uppercase tracking-wide">Kategori</p>
                    <p className="text-lg font-bold text-slate-900 capitalize">{result.analysis.category}</p>
                  </CardContent>
                </Card>

                <Card className="text-center">
                  <CardContent className="py-4">
                    <div className="inline-flex items-center justify-center w-10 h-10 rounded-lg bg-purple-50 text-purple-600 mb-2">
                      <MessageSquare className="w-5 h-5" />
                    </div>
                    <p className="text-xs text-slate-500 uppercase tracking-wide">Mesaj</p>
                    <p className="text-lg font-bold text-slate-900">{result.messageCount}</p>
                  </CardContent>
                </Card>

                <Card className="text-center">
                  <CardContent className="py-4">
                    <div className="inline-flex items-center justify-center w-10 h-10 rounded-lg bg-amber-50 text-amber-600 mb-2">
                      <TrendingUp className="w-5 h-5" />
                    </div>
                    <p className="text-xs text-slate-500 uppercase tracking-wide">Güven</p>
                    <p className="text-lg font-bold text-slate-900">{Math.round(result.analysis.confidence * 100)}%</p>
                  </CardContent>
                </Card>
              </div>

              {/* Details Card */}
              <Card>
                <CardHeader>
                  <CardTitle>Detaylar</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div className="p-3 rounded-lg bg-slate-50">
                      <p className="text-slate-500">Request ID</p>
                      <p className="font-mono text-slate-700">{result.requestId}</p>
                    </div>
                    <div className="p-3 rounded-lg bg-slate-50">
                      <p className="text-slate-500">Telefon</p>
                      <p className="font-mono text-slate-700">{result.phoneNumber}</p>
                    </div>
                    <div className="p-3 rounded-lg bg-slate-50 col-span-2">
                      <p className="text-slate-500">Analiz Zamanı</p>
                      <p className="font-mono text-slate-700">
                        {new Date(result.analyzedAt).toLocaleString('tr-TR')}
                      </p>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>
          )}

          {/* Empty State */}
          {!isLoading && !result && !error && (
            <Card>
              <CardContent className="py-16">
                <div className="flex flex-col items-center text-center">
                  <div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-4">
                    <MessageSquare className="w-10 h-10 text-slate-400" />
                  </div>
                  <h3 className="text-lg font-semibold text-slate-700 mb-2">
                    Analiz Bekliyor
                  </h3>
                  <p className="text-slate-500 max-w-sm">
                    Telefon numarasını girin ve "Analiz Et" butonuna tıklayarak
                    WhatsApp mesajlarını analiz edin.
                  </p>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
