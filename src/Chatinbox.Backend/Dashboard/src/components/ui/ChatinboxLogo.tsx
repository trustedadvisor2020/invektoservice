import { cn } from '../../lib/utils';

type LogoVariant = 'ai' | 'super' | 'none';

interface ChatinboxLogoProps {
  size?: 'sm' | 'md' | 'lg';
  showOne?: boolean;
  variant?: LogoVariant;
  /** Ana wordmark fill rengi — suffix renklerini etkilemez */
  color?: string;
  className?: string;
}

// Genislikler Neon fontunun olculen glyph metriklerine gore ayarlandi (getComputedTextLength,
// letterSpacing -0.02em dahil): "chatinbox" = 143/151/216, "AI" = 31/-/47, "super"@lg = 122 px.
// suffixX = wordmark-only (none-variant) kutu genisligi. width = ai-variant kutu (wordmark + gap + "AI" + pad).
// Suffix konumu <tspan> ile wordmark'tan sonra AKAR (font-agnostik); width sadece viewBox/layout kutusu.
const sizes = {
  sm: { width: 190, height: 40, fontSize: 33, suffixX: 147 },
  md: { width: 202, height: 44, fontSize: 35, suffixX: 156 },
  lg: { width: 285, height: 62, fontSize: 50, suffixX: 223 },
};

function getAutoVariant(): LogoVariant {
  const host = window.location.hostname;
  // Dual-domain: hem *.invekto.com (canli) hem *.chatinbox.net (cutover) taninir.
  if (host === 'super.invekto.com' || host === 'super.chatinbox.net') return 'super';
  if (host === 'ai.invekto.com' || host === 'ai.chatinbox.net') return 'ai';
  return 'ai'; // default
}

const suffixConfig: Record<LogoVariant, { text: string; color: string } | null> = {
  ai: { text: 'AI', color: '#8898AA' },
  super: { text: 'super', color: '#E56910' },
  none: null,
};

export function ChatinboxLogo({ size = 'md', showOne, variant, color = '#0A2540', className }: ChatinboxLogoProps) {
  const resolved = variant ?? (showOne === false ? 'none' : getAutoVariant());
  const suffix = suffixConfig[resolved];
  const s = sizes[size];
  const baseline = s.height * 0.78;
  // "super" suffix'i "AI"'dan ~1.5×fontSize daha genis (olculen: 75px@lg, 50px@sm) — viewBox'i o kadar uzat
  const extraWidth = resolved === 'super' ? Math.round(s.fontSize * 1.5) : 0;
  // suffix yokken genislik wordmark sonunda biter — sagda olu bosluk kalmaz (flex hizalama)
  const width = suffix ? s.width + extraWidth : s.suffixX;

  return (
    <svg
      viewBox={`0 0 ${width} ${s.height}`}
      width={width}
      height={s.height}
      className={cn('inline-block select-none', className)}
      role="img"
      aria-label={suffix ? `Chatinbox ${suffix.text}` : 'Chatinbox'}
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
        chatinbox
        {suffix && (
          <tspan
            dx={Math.round(s.fontSize * 0.3)}
            fontWeight={600}
            fill={suffix.color}
          >
            {suffix.text}
          </tspan>
        )}
      </text>
    </svg>
  );
}

export function ChatinboxMark({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 16 28"
      width={16}
      height={28}
      className={cn('inline-block select-none', className)}
      role="img"
      aria-label="Chatinbox"
    >
      <text
        x={0}
        y={22}
        fontFamily="Neon, Inter, sans-serif"
        fontWeight={700}
        fontSize={22}
        fill="#0A2540"
      >
        c
      </text>
    </svg>
  );
}
