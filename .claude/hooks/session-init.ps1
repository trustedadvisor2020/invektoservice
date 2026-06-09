# InvektoServices Session Init Hook
# Her session basinda kritik dosyalari hatirlatir

param()

# stdin'den JSON oku (hook sistemi icin gerekli)
$null = [Console]::In.ReadToEnd()

# Session baslangic bilgisi
$context = @"

=== INVEKTO SESSION INIT ===
Tarih: $(Get-Date -Format "yyyy-MM-dd HH:mm")

KRITIK DOSYALAR (BOUNDED - buyuyen dosyayi ASLA tam Read etme):
- arch/session-memory.md (limit=320; END_CURRENT_STATE pencerede yoksa DUR) -> Son durum + Queue
- arch/hot-lessons.md (<=80 satir, hep yuklenir)                            -> Standing kurallar
- tracking/pilot-launch-roadmap.md (PILOT MODE - queue + protokol)          -> Master queue
- tracking/README.md                                                        -> Paket durumu
- .claude/agents/INVEKTO_BASE.prompt.md                                     -> Global rules
GREP-ONLY (asla tam Read): arch/lessons-learned.md, arch/lessons-learned-archive.md, arch/session-memory-archive.md

NOT: active-work.md KULLANILMIYOR (shared v6.1, 2026-03-04 itibariyla).
Execution queue ve recently completed session-memory.md icindedir.

AUTO WORKFLOW: Aktif (interview -> plan -> dev -> build -> /rev -> MCP codex -> commit)
REVIEW: LOW dahil tum risk seviyeleri Codex review alir (SKIP yolu YOK)

MICROSERVICE IZOLASYONU: Servisler arasi dogrudan referans YASAK - Shared uzerinden iletisim!
=============================

"@

$output = @{
    hookSpecificOutput = @{
        hookEventName = "SessionStart"
        additionalContext = $context
    }
}

$output | ConvertTo-Json -Depth 10
exit 0