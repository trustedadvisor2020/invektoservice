import { cn } from '../../lib/utils';

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
}

const variants = {
  primary: 'bg-brand-500 hover:bg-brand-600 active:bg-brand-700 text-white shadow-soft',
  secondary: 'bg-white hover:bg-navy-50 active:bg-navy-100 text-navy-700 border border-navy-100 shadow-soft',
  danger: 'bg-red-500 hover:bg-red-600 active:bg-red-700 text-white shadow-soft',
  ghost: 'bg-transparent hover:bg-navy-50 active:bg-navy-100 text-navy-500',
};

const sizes = {
  sm: 'h-8 px-3 text-xs gap-1.5',
  md: 'h-9 px-4 text-sm gap-2',
  lg: 'h-10 px-5 text-sm gap-2',
};

export function Button({
  children,
  variant = 'primary',
  size = 'md',
  className,
  disabled,
  ...props
}: ButtonProps) {
  return (
    <button
      className={cn(
        'inline-flex items-center justify-center rounded-lg font-medium',
        'transition-all duration-150 ease-out',
        'focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:ring-offset-1',
        variants[variant],
        sizes[size],
        disabled && 'opacity-40 cursor-not-allowed pointer-events-none',
        className
      )}
      disabled={disabled}
      {...props}
    >
      {children}
    </button>
  );
}
