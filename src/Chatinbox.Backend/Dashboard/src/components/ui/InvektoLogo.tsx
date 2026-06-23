import { cn } from '../../lib/utils';

type LogoVariant = 'ai' | 'super' | 'none';

interface InvektoLogoProps {
  size?: 'sm' | 'md' | 'lg';
  showOne?: boolean;
  variant?: LogoVariant;
  /** Ana wordmark fill rengi — suffix renklerini etkilemez */
  color?: string;
  className?: string;
}

const sizes = {
  sm: { width: 187, height: 40, fontSize: 33, suffixX: 107 },
  md: { width: 201, height: 44, fontSize: 35, suffixX: 115 },
  lg: { width: 300, height: 62, fontSize: 50, suffixX: 168 },
};

function getAutoVariant(): LogoVariant {
  const host = window.location.hostname;
  if (host === 'super.invekto.com') return 'super';
  if (host === 'ai.invekto.com') return 'ai';
  return 'ai'; // default
}

const suffixConfig: Record<LogoVariant, { text: string; color: string } | null> = {
  ai: { text: 'AI', color: '#8898AA' },
  super: { text: 'super', color: '#E56910' },
  none: null,
};

export function InvektoLogo({ size = 'md', showOne, variant, color = '#0A2540', className }: InvektoLogoProps) {
  const resolved = variant ?? (showOne === false ? 'none' : getAutoVariant());
  const suffix = suffixConfig[resolved];
  const s = sizes[size];
  const baseline = s.height * 0.78;
  // "super" is wider than "ai"/"one" — extend viewBox accordingly
  const extraWidth = resolved === 'super' ? 40 : 0;
  // suffix yokken genislik wordmark sonunda biter — sagda olu bosluk kalmaz (flex hizalama)
  const width = suffix ? s.width + extraWidth : s.suffixX;

  return (
    <svg
      viewBox={`0 0 ${width} ${s.height}`}
      width={width}
      height={s.height}
      className={cn('inline-block select-none', className)}
      role="img"
      aria-label={suffix ? `Invekto ${suffix.text}` : 'Invekto'}
    >
      <text
        x={0}
        y={baseline}
        fontFamily="Neon, Inter, sans-serif"
        fontWeight={700}
        fontSize={s.fontSize}
        fill={color}
        letterSpacing="-0.02em"
      >
        invekto
      </text>
      {suffix && (
        <text
          x={s.suffixX}
          y={baseline}
          fontFamily="Neon, Inter, sans-serif"
          fontWeight={600}
          fontSize={s.fontSize}
          fill={suffix.color}
          letterSpacing="-0.02em"
        >
          {suffix.text}
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
