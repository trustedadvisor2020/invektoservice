import { cn } from '../../lib/utils';

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
}

export function Input({ label, error, className, ...props }: InputProps) {
  return (
    <div className="w-full">
      {label && (
        <label className="block text-sm font-medium text-navy-700 mb-1.5">
          {label}
        </label>
      )}
      <input
        className={cn(
          'w-full h-10 px-3 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm',
          'placeholder:text-navy-300',
          'transition-all duration-150',
          'focus:outline-none focus:border-brand-500 focus:shadow-focus',
          'hover:border-navy-200',
          error && 'border-red-400 focus:border-red-500 focus:shadow-[0_0_0_3px_rgba(237,95,116,0.15)]',
          className
        )}
        {...props}
      />
      {error && (
        <p className="mt-1.5 text-xs text-red-500">{error}</p>
      )}
    </div>
  );
}
