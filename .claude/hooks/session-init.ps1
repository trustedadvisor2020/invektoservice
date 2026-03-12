# InvektoServices Session Init Hook
# Her session basinda kritik dosyalari hatirlatir

param()

# stdin'den JSON oku (hook sistemi icin gerekli)
$null = [Console]::In.ReadToEnd()

# Session baslangic bilgisi
$context = @"

=== INVEKTO SESSION INIT ===
Tarih: $(Get-Date -Format "yyyy-MM-dd HH:mm")

KRITIK DOSYALAR (oku):
- arch/session-memory.md -> Son durum
- arch/active-work.md -> Devam eden isler
- arch/lessons-learned.md -> Tekrarlanan hatalar

AUTO WORKFLOW: Aktif (interview -> plan -> dev -> build -> /rev -> codex -> commit)

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