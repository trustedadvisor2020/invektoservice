# InvektoServices Session Init Hook
# Her session basinda kritik dosyalari hatirlatir

param()

# stdin'den JSON oku (hook sistemi icin gerekli)
$null = [Console]::In.ReadToEnd()

# Session baslangic bilgisi
$context = @"

=== INVEKTO SESSION INIT ===
Tarih: $(Get-Date -Format "yyyy-MM-dd HH:mm")

KRITIK DOSYALAR (TEK STANDART LISTE - shared engine v6.1 uyumlu):
- arch/session-memory.md                           -> Son durum + Execution Queue
- tracking/README.md                               -> Paket durumu (master tracking)
- arch/lessons-learned.md (son 100 satir)          -> Tekrarlanan hatalar
- .claude/agents/INVEKTO_BASE.prompt.md            -> Global rules

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