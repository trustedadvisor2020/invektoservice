import { cn } from '../../lib/utils';

interface InvektoLogoProps {
  size?: 'sm' | 'md' | 'lg';
  showOne?: boolean;
  className?: string;
}

const sizes = {
  sm: { width: 187, height: 40, fontSize: 33, oneX: 107 },
  md: { width: 201, height: 44, fontSize: 35, oneX: 115 },
  lg: { width: 264, height: 55, fontSize: 44, oneX: 147 },
};

export function InvektoLogo({ size = 'md', showOne = true, className }: InvektoLogoProps) {
  const s = sizes[size];
  const baseline = s.height * 0.78;

  return (
    <svg
      viewBox={`0 0 ${s.width} ${s.height}`}
      width={s.width}
      height={s.height}
      className={cn('inline-block select-none', className)}
      role="img"
      aria-label="Invekto One"
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
      {showOne && (
        <text
          x={s.oneX}
          y={baseline}
          fontFamily="Neon, Inter, sans-serif"
          fontWeight={600}
          fontSize={s.fontSize}
          fill="#8898AA"
          letterSpacing="-0.02em"
        >
          one
        </text>
      )}
    </svg>
  );
}

export function InvektoMark({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 14 28"
      width={14}
      height={28}
      className={cn('inline-block select-none', className)}
      role="img"
      aria-label="Invekto"
    >
      <text
        x={0}
        y={22}
        fontFamily="Neon, Inter, sans-serif"
        fontWeight={700}
        fontSize={22}
        fill="#0A2540"
      >
        i
      </text>
    </svg>
  );
}
