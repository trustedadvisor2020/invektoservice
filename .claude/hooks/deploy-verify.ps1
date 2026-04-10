# InvektoServices Deploy Verification Hook (Non-blocking - PostToolUse)
# Deploy islemleri sirasinda otomatik dogrulama checklist'i gosterir.
# Tetiklenme: mcp__invekto-ops__server-upload veya mcp__invekto-ops__server-exec kullanildiginda

param()

$raw = [Console]::In.ReadToEnd()
try { $inp = $raw | ConvertFrom-Json } catch { exit 0 }

$toolName = $inp.tool_name
if (-not $toolName) { exit 0 }

# Sadece deploy-related tool kullanimi icin calis
$isDeployTool = $toolName -match 'server-upload|server-exec'
if (-not $isDeployTool) { exit 0 }

$toolInput = $inp.tool_input
$warnings = @()

# === server-upload: Production path + artifact kontrolu ===
if ($toolName -match 'server-upload') {
    $remotePath = if ($toolInput.remote_path) { $toolInput.remote_path } else { "" }
    $localPath  = if ($toolInput.local_path)  { $toolInput.local_path }  else { "" }

    # CHECK 1: Production path C:\Invekto\{Service}\current\ olmali
    if ($remotePath -and $remotePath -notmatch 'C:\\Invekto\\') {
        $warnings += "[DEPLOY-PATH] UYARI: Hedef path production formatina uymuyor! Dogru: C:\Invekto\{Service}\current\. Gelen: $remotePath"
    }

    # CHECK 2: Eski E:\InvektoServices\ path'i YASAK (2026-04-10 cleanup)
    if ($remotePath -match 'E:\\InvektoServices' -or $remotePath -match 'e:/invektoservices') {
        $warnings += "[DEPLOY-DEPRECATED] HATA: 'E:\InvektoServices\' path'i GECERSIZ (2026-04-10 cleanup). Production = C:\Invekto\{Service}\current\"
    }

    # CHECK 3: Dev PC path'ine upload (anlamsiz)
    if ($remotePath -match 'C:\\CRMs\\InvektoServices' -or $remotePath -match 'c:/crms/invektoservices') {
        $warnings += "[DEPLOY-DEV-FORBIDDEN] HATA: 'C:\CRMs\InvektoServices\' dev PC path'i. Production sunucusuna upload ediliyorsa C:\Invekto\{Service}\current\ kullan."
    }

    # CHECK 4: Backend publish icin Dashboard SPA build hatirlatma
    if ($remotePath -match 'Invekto\\Backend\\current' -and $localPath -match 'Invekto\.Backend' -and $localPath -notmatch 'wwwroot.*app') {
        $warnings += "[DEPLOY-BACKEND-SPA] HATIRLATMA: Backend deploy ediliyor. Dashboard SPA build yapildi mi? 'cd src/Invekto.Backend/wwwroot && npx vite build' (output: wwwroot/app/). FlowBuilder Dashboard'a merge edildi (2026-02-22) - tek build yeterli."
    }
}

# === server-exec: NSSM + service restart kontrolu ===
if ($toolName -match 'server-exec') {
    $command = if ($toolInput.command) { $toolInput.command } else { "" }

    # CHECK 5: Restart-Service NSSM servisleri icin sorunlu
    if ($command -match 'Restart-Service.*Invekto-') {
        $warnings += "[DEPLOY-NSSM] UYARI: 'Restart-Service Invekto-*' NSSM servislerinde sibling process kill sorunu yaratabilir. Dogru komut: 'C:\Invekto\nssm.exe restart Invekto-{Service}'"
    }

    # CHECK 6: nssm.exe full path
    if ($command -match '\bnssm\b' -and $command -notmatch 'C:\\Invekto\\nssm\.exe' -and $command -notmatch '\./nssm') {
        $warnings += "[DEPLOY-NSSM-PATH] UYARI: nssm komutu full path ile calistirilmali. Dogru: 'C:\Invekto\nssm.exe restart Invekto-{Service}'"
    }

    # CHECK 7: Dev PC uzerinde production komutu (yanlis makine)
    if ($command -match 'C:\\CRMs\\InvektoServices' -and $command -match 'dotnet publish') {
        # publish dev PC'de yapilir, uyari yok
    }

    # CHECK 8: Migration atlamasi uyarisi
    if ($command -match 'nssm.*restart.*Invekto-' -and $command -notmatch 'migration') {
        # Bu sadece bilgi amacli - migration ayri komutla calisir, engellememek lazim
    }
}

# Uyari varsa JSON output
if ($warnings.Count -gt 0) {
    $msg = "`n=== DEPLOY VERIFICATION ===`n" + ($warnings -join "`n") + "`n===========================`n"
    $output = @{
        hookSpecificOutput = @{
            hookEventName = "PostToolUse"
            additionalContext = $msg
        }
    }
    $output | ConvertTo-Json -Depth 10
}

exit 0
