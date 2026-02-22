import { cn } from '../../lib/utils';

interface InvektoLogoProps {
  size?: 'sm' | 'md' | 'lg';
  subtitle?: string;
  className?: string;
}

const sizes = {
  sm: { width: 120, height: 28, fontSize: 22, subSize: 22, dotR: 2.8, dotCy: 5 },
  md: { width: 150, height: 34, fontSize: 27, subSize: 27, dotR: 3.2, dotCy: 5.5 },
  lg: { width: 200, height: 44, fontSize: 36, subSize: 36, dotR: 4.2, dotCy: 6.5 },
};

export function InvektoLogo({ size = 'md', subtitle, className }: InvektoLogoProps) {
  const s = sizes[size];
  const baseline = s.height * 0.78;
  const dotCx = s.fontSize * 0.17;
  const label = subtitle || 'one';

  return (
    <svg
      viewBox={`0 0 ${s.width} ${s.height}`}
      width={s.width}
      height={s.height}
      className={cn('inline-block select-none', className)}
      role="img"
      aria-label={`Invekto ${label}`}
    >
      <text
        x={0}
        y={baseline}
        fontFamily="Neon, Inter, sans-serif"
        fontWeight={700}
        fontSize={s.fontSize}
        fill="#0A2540"
        letterSpacing="-0.02em"
      >
        invekto
      </text>
      <circle cx={dotCx} cy={s.dotCy} r={s.dotR} fill="#EF4444" />
      <text
        x={s.fontSize * 3.05}
        y={baseline}
        fontFamily="Neon, Inter, sans-serif"
        fontWeight={600}
        fontSize={s.subSize}
        fill="#8898AA"
        letterSpacing="-0.02em"
      >
        {label}
      </text>
    </svg>
  );
}
