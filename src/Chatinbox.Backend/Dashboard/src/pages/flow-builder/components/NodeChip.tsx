import { useState } from 'react';

/** Shared node-type → color mapping. Used by NodeChip and WizardPreview. */
export const NODE_COLORS: Record<string, string> = {
  trigger_start: '#10b981', webhook_trigger: '#10b981', outbound_trigger: '#10b981', schedule_trigger: '#10b981',
  message_text: '#635BFF', message_menu: '#635BFF',
  logic_condition: '#f59e0b', logic_switch: '#f59e0b',
  ai_intent: '#8b5cf6', ai_faq: '#8b5cf6', ai_sentiment: '#8b5cf6',
  action_handoff: '#ef4444', action_api_call: '#ef4444', action_delay: '#ef4444',
  utility_set_variable: '#6b7280', utility_note: '#6b7280',
};

const NODE_DESCRIPTIONS: Record<string, { label: string; description: string; color: string }> = {
  trigger_start: { label: 'Başlangıç', description: 'Akışın giriş noktası. Müşteri mesaj gönderdiğinde tetiklenir.', color: NODE_COLORS.trigger_start },
  webhook_trigger: { label: 'Webhook', description: 'Dis sistemden gelen HTTP istegi ile tetiklenen baslangic noktasi.', color: NODE_COLORS.webhook_trigger },
  outbound_trigger: { label: 'Outbound', description: 'Toplu mesaj kampanyasi tetikleyicisi.', color: NODE_COLORS.outbound_trigger },
  schedule_trigger: { label: 'Zamanlayıcı', description: 'Belirlenen zamanda otomatik tetiklenen başlangıç noktası (cron).', color: NODE_COLORS.schedule_trigger },
  message_text: { label: 'Mesaj', description: 'Kullanıcıya metin mesaj gönderir.', color: NODE_COLORS.message_text },
  message_menu: { label: 'Menü', description: 'Kullanıcıya seçenekli bir menü sunar. Her seçenek farklı dala yönlendirir.', color: NODE_COLORS.message_menu },
  logic_condition: { label: 'Koşul', description: 'Bir değişkeni kontrol ederek akışı ikiye ayırır (doğru/yanlış).', color: NODE_COLORS.logic_condition },
  logic_switch: { label: 'Switch', description: 'Bir değişkene göre birden fazla dala yönlendirir.', color: NODE_COLORS.logic_switch },
  ai_intent: { label: 'Intent Algılama', description: 'Müşteri mesajını AI ile analiz eder ve niyetini tespit eder (randevu, fiyat, iptal vb).', color: NODE_COLORS.ai_intent },
  ai_faq: { label: 'FAQ Arama', description: 'Müşteri sorusunu bilgi bankasında arar. Eşleşme bulursa otomatik cevap verir.', color: NODE_COLORS.ai_faq },
  ai_sentiment: { label: 'Duygu Analizi', description: 'Müşteri mesajının duygusunu analiz eder (pozitif/negatif).', color: NODE_COLORS.ai_sentiment },
  action_handoff: { label: 'Temsilciye Aktar', description: 'Görüşmeyi canlı bir insan temsilciye aktarır. Akış burada sona erer.', color: NODE_COLORS.action_handoff },
  action_api_call: { label: 'API Çağrısı', description: 'Harici bir API endpoint\'ine istek gönderir (GET/POST/PUT/DELETE).', color: NODE_COLORS.action_api_call },
  action_delay: { label: 'Bekle', description: 'Belirli bir süre bekler (saniye). Kullanıcıya düşünme/bekleme süresi verir.', color: NODE_COLORS.action_delay },
  utility_set_variable: { label: 'Değişken Ata', description: 'Bir değişkene değer atar. Sonraki adımlar bu değeri kullanabilir.', color: NODE_COLORS.utility_set_variable },
  utility_note: { label: 'Not', description: 'Görsel açıklama düğümü. Çalıştırılmaz, sadece tasarımcının notları için.', color: NODE_COLORS.utility_note },
};

// Regex to find node type references in text
const NODE_TYPE_PATTERN = /\b(trigger_start|webhook_trigger|outbound_trigger|schedule_trigger|message_text|message_menu|logic_condition|logic_switch|ai_intent|ai_faq|ai_sentiment|action_handoff|action_api_call|action_delay|utility_set_variable|utility_note)\b/g;

export function NodeChip({ type }: { type: string }) {
  const [showTooltip, setShowTooltip] = useState(false);
  const info = NODE_DESCRIPTIONS[type];
  if (!info) return <code className="text-xs bg-navy-50 px-1 rounded">{type}</code>;

  return (
    <span className="relative inline-block">
      <span
        className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md text-xs font-medium cursor-help transition-colors"
        style={{ backgroundColor: `${info.color}15`, color: info.color, border: `1px solid ${info.color}30` }}
        onMouseEnter={() => setShowTooltip(true)}
        onMouseLeave={() => setShowTooltip(false)}
      >
        <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: info.color }} />
        {info.label}
      </span>
      {showTooltip && (
        <span className="absolute z-50 bottom-full left-1/2 -translate-x-1/2 mb-2 w-60 px-3 py-2 bg-navy-900 text-white text-xs rounded-lg shadow-xl pointer-events-none">
          <span className="font-semibold">{info.label}</span>
          <span className="block mt-0.5 text-navy-200">{info.description}</span>
          <span className="absolute top-full left-1/2 -translate-x-1/2 border-4 border-transparent border-t-navy-900" />
        </span>
      )}
    </span>
  );
}

/** Parse text and replace node type references with NodeChip components */
export function renderWithNodeChips(text: string | undefined | null): (string | JSX.Element)[] {
  if (!text) return [];
  const parts: (string | JSX.Element)[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  const regex = new RegExp(NODE_TYPE_PATTERN.source, 'g');
  while ((match = regex.exec(text)) !== null) {
    if (match.index > lastIndex) {
      parts.push(text.slice(lastIndex, match.index));
    }
    parts.push(<NodeChip key={`${match.index}-${match[0]}`} type={match[0]} />);
    lastIndex = match.index + match[0].length;
  }

  if (lastIndex < text.length) {
    parts.push(text.slice(lastIndex));
  }

  return parts;
}
