import { cn } from '../../lib/utils';

interface InvektoLogoProps {
  size?: 'sm' | 'md' | 'lg';
  showOne?: boolean;
  className?: string;
}

const sizes = {
  sm: { width: 120, height: 28, fontSize: 22, oneSize: 22, dotR: 2.8, dotCy: 5 },
  md: { width: 150, height: 34, fontSize: 27, oneSize: 27, dotR: 3.2, dotCy: 5.5 },
  lg: { width: 200, height: 44, fontSize: 36, oneSize: 36, dotR: 4.2, dotCy: 6.5 },
};

export function InvektoLogo({ size = 'md', showOne = true, className }: InvektoLogoProps) {
  const s = sizes[size];
  const baseline = s.height * 0.78;
  // "i" without dot: we render the full word, then overlay a red dot
  // The dot sits roughly at the center-x of the first character
  const dotCx = s.fontSize * 0.17;

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
      {/* Red dot over the "i" — covers the original dot */}
      <circle cx={dotCx} cy={s.dotCy} r={s.dotR} fill="#EF4444" />
      {showOne && (
        <text
          x={s.fontSize * 3.05}
          y={baseline}
          fontFamily="Neon, Inter, sans-serif"
          fontWeight={600}
          fontSize={s.oneSize}
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
      viewBox="0 0 16 28"
      width={16}
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
      <circle cx={3.7} cy={5} r={2.8} fill="#EF4444" />
    </svg>
  );
}
