import { useFlowStore } from '../../../stores/flow-store';
import { renderWithNodeChips } from './NodeChip';
import type { WizardMessage } from '../../../types/wizard';

export function WizardHistoryTab() {
  const wizardHistory = useFlowStore(s => s.wizardHistory);

  if (!wizardHistory || wizardHistory.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center px-6">
        <p className="text-sm text-navy-300">AI geçmişi bulunamadı.</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2 px-1 py-1 bg-purple-50 rounded-lg">
        <svg className="w-4 h-4 text-purple-500 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
        </svg>
        <span className="text-xs font-medium text-purple-700">Bu akış AI tarafından oluşturuldu</span>
      </div>

      {wizardHistory.map((msg, i) => (
        <HistoryMessage key={i} message={msg} />
      ))}
    </div>
  );
}

function HistoryMessage({ message }: { message: WizardMessage }) {
  const isUser = message.role === 'user';
  const time = message.timestamp
    ? new Date(message.timestamp).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
    : '';

  return (
    <div className={`flex gap-2 ${isUser ? 'justify-end' : ''}`}>
      {!isUser && (
        <div className="w-5 h-5 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0 mt-0.5">
          <svg className="w-3 h-3 text-purple-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
          </svg>
        </div>
      )}
      <div className={`max-w-[90%] ${isUser ? 'text-right' : ''}`}>
        <div
          className={`inline-block rounded-lg px-3 py-2 text-xs whitespace-pre-wrap ${
            isUser
              ? 'bg-purple-500 text-white'
              : 'bg-navy-50 border border-navy-100 text-navy-700'
          }`}
        >
          {isUser ? message.content : renderWithNodeChips(message.content)}
          {message.flow_config_snapshot && (
            <div className={`mt-1.5 pt-1.5 border-t text-[10px] font-medium flex items-center gap-1 ${
              isUser ? 'border-purple-400 text-purple-200' : 'border-navy-100 text-green-600'
            }`}>
              <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              Akış yapısı üretildi
            </div>
          )}
        </div>
        {time && (
          <div className={`text-[10px] text-navy-300 mt-0.5 ${isUser ? 'text-right' : ''}`}>
            {time}
          </div>
        )}
      </div>
      {isUser && (
        <div className="w-5 h-5 rounded-full bg-navy-100 flex items-center justify-center flex-shrink-0 mt-0.5">
          <svg className="w-3 h-3 text-navy-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 6a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0zM4.501 20.118a7.5 7.5 0 0114.998 0" />
          </svg>
        </div>
      )}
    </div>
  );
}
