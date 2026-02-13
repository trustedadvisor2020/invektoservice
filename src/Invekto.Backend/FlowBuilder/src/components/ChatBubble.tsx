import { memo } from 'react';
import { cn } from '../lib/utils';

interface ChatBubbleProps {
  role: 'bot' | 'user' | 'system';
  text: string;
}

function ChatBubbleComponent({ role, text }: ChatBubbleProps) {
  if (role === 'system') {
    return (
      <div className="flex justify-center my-1">
        <span className="text-[11px] text-slate-400 bg-slate-100 rounded-full px-3 py-1 max-w-[90%] text-center">
          {text}
        </span>
      </div>
    );
  }

  const isBot = role === 'bot';

  return (
    <div className={cn('flex mb-2', isBot ? 'justify-start' : 'justify-end')}>
      <div
        className={cn(
          'max-w-[85%] rounded-lg px-3 py-2 text-sm whitespace-pre-wrap break-words',
          isBot
            ? 'bg-white text-slate-800 border border-slate-200 rounded-tl-none'
            : 'bg-emerald-500 text-white rounded-tr-none'
        )}
      >
        {text}
      </div>
    </div>
  );
}

export const ChatBubble = memo(ChatBubbleComponent);
