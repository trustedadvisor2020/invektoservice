# InvektoServices Check Shared Microservice Hook
# Edit/Write sonrasi Shared veya cross-service dosya degisikligi advisory uyarisi

param()

# stdin'den JSON oku
$inputJson = [Console]::In.ReadToEnd()

try {
    $hookData = $inputJson | ConvertFrom-Json
    $toolInput = $hookData.tool_input

    # file_path veya command'dan dosya yolunu al
    $filePath = ""
    if ($toolInput.file_path) {
        $filePath = $toolInput.file_path
    } elseif ($toolInput.path) {
        $filePath = $toolInput.path
    }

    if (-not $filePath) {
        exit 0
    }

    # Path normalize (forward slash)
    $normalized = $filePath -replace '\\', '/'

    $additionalContext = ""

    # === Shared kategorisi (tum servisleri etkiler) ===
    if ($normalized -match '/Chatinbox\.Shared/') {
        $subCategory = "Shared (genel)"
        if ($normalized -match '/Chatinbox\.Shared/DTOs/') { $subCategory = "Shared/DTOs (cross-service contract)" }
        elseif ($normalized -match '/Chatinbox\.Shared/Middleware/') { $subCategory = "Shared/Middleware (auth/tenant)" }
        elseif ($normalized -match '/Chatinbox\.Shared/Auth/') { $subCategory = "Shared/Auth (JWT/token)" }
        elseif ($normalized -match '/Chatinbox\.Shared/Constants/') { $subCategory = "Shared/Constants" }

        $fileName = Split-Path $filePath -Leaf
        $additionalContext = @"

=== MIKRO SERVIS IZOLASYON UYARISI ===
Shared dosya degistirildi: $fileName
Kategori: $subCategory

Bu dosya TUM mikro servisleri etkiler (Chatinbox.Backend, ChatAnalysis,
Automation, AgentAI, Appointments, Integrations, Knowledge, Marketing,
Outbound, VoiceAI, WebChat, WhatsAppAnalytics).

Kontrol edilmesi gerekenler:
- Breaking change var mi? (field sil, tip degistir, required ekle)
- Tum tuketici servisler etkileniyor mu?
- Integration test gerekli mi?

service-isolation-checker agent'i ile dogrulama onerilir.
========================================

"@
    }
    # === Cross-service direct reference tespiti (uyari amacli) ===
    elseif ($normalized -match '/Chatinbox\.([^/]+)/') {
        $currentService = $matches[1]
        if ($currentService -ne "Shared") {
            $fileName = Split-Path $filePath -Leaf
            $additionalContext = @"

=== SERVIS DOSYASI DEGISTI ===
Servis: Chatinbox.$currentService
Dosya: $fileName

Hatirlatma: Bu servis sadece Chatinbox.Shared uzerinden diger servislerle
iletisim kurabilir. Dogrudan 'using Chatinbox.<DigerServis>' YASAK.

Kontrol:
- Yeni eklenen using'ler Chatinbox.Shared haricinde Chatinbox.* icermiyor mu?
- csproj'da yeni ProjectReference sadece Chatinbox.Shared'a mi?
==============================

"@
        }
    }

    if ($additionalContext) {
        $output = @{
            hookSpecificOutput = @{
                hookEventName = "PostToolUse"
                additionalContext = $additionalContext
            }
        }
        $output | ConvertTo-Json -Depth 10
    }

    exit 0
}
catch {
    # Hata durumunda sessizce devam et
    exit 0
}
