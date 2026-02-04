import { cn } from '../../lib/utils';

interface BadgeProps {
  children: React.ReactNode;
  variant?: 'default' | 'success' | 'warning' | 'error' | 'info';
  className?: string;
}

const variants = {
  default: 'bg-slate-100 text-slate-600 border-slate-200',
  success: 'bg-emerald-100/80 text-emerald-700 border-emerald-200/60',
  warning: 'bg-amber-100/80 text-amber-700 border-amber-200/60',
  error: 'bg-red-100/80 text-red-700 border-red-200/60',
  info: 'bg-blue-100/80 text-blue-700 border-blue-200/60',
};

export function Badge({ children, variant = 'default', className }: BadgeProps) {
  return (
    <span className={cn(
      'inline-flex items-center justify-center px-2 py-0.5 rounded-md text-xs font-medium border',
      'transition-all duration-150',
      variants[variant],
      className
    )}>
      {children}
    </span>
  );
}
