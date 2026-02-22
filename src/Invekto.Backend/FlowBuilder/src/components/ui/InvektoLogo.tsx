import { cn } from '../../lib/utils';

interface InvektoLogoProps {
  size?: 'sm' | 'md' | 'lg';
  subtitle?: string;
  className?: string;
}

const sizes = {
  sm: { width: 161, height: 35, fontSize: 28, subX: 92 },
  md: { width: 201, height: 44, fontSize: 35, subX: 115 },
  lg: { width: 264, height: 55, fontSize: 44, subX: 147 },
};

export function InvektoLogo({ size = 'md', subtitle, className }: InvektoLogoProps) {
  const s = sizes[size];
  const baseline = s.height * 0.78;
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
      <text
        x={s.subX}
        y={baseline}
        fontFamily="Neon, Inter, sans-serif"
        fontWeight={600}
        fontSize={s.fontSize}
        fill="#8898AA"
        letterSpacing="-0.02em"
      >
        {label}
      </text>
    </svg>
  );
}
