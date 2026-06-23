import { cn } from '../../lib/utils';

interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  options: { value: string; label: string }[];
}

export function Select({ label, options, className, ...props }: SelectProps) {
  return (
    <div className="w-full">
      {label && (
        <label className="block text-sm font-medium text-navy-700 mb-1.5">
          {label}
        </label>
      )}
      <select
        className={cn(
          'w-full h-10 px-3 bg-white border border-navy-100 rounded-lg text-navy-900 text-sm',
          'transition-all duration-150',
          'focus:outline-none focus:border-brand-500 focus:shadow-focus',
          'hover:border-navy-200',
          'cursor-pointer appearance-none',
          'bg-[url("data:image/svg+xml,%3Csvg xmlns=\'http://www.w3.org/2000/svg\' width=\'12\' height=\'12\' viewBox=\'0 0 12 12\'%3E%3Cpath fill=\'%238898AA\' d=\'M6 8L1 3h10z\'/%3E%3C/svg%3E")] bg-no-repeat bg-[right_12px_center]',
          className
        )}
        {...props}
      >
        {options.map(opt => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
    </div>
  );
}
