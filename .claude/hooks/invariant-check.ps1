# PostToolUse hook: Edit/Write sonrasi invariant kontrolleri
# Exit 0 + systemMessage (non-blocking ama uyari inject eder)
$raw = [Console]::In.ReadToEnd()
try { $inp = $raw | ConvertFrom-Json } catch { exit 0 }

$fp = $inp.tool_input.file_path
if (-not $fp) { exit 0 }
$warnings = @()

# CHECK 1: SQL dosyasinda PascalCase/camelCase kolon adi
if ($fp -match '\.sql$') {
    $content = Get-Content $fp -Raw 2>$null
    $colMatches = [regex]::Matches($content, '(?<=\s)([a-z]+[A-Z][a-zA-Z]+)(?=\s+(TEXT|VARCHAR|INT|BIGINT|BOOLEAN|TIMESTAMP|JSONB|UUID|SERIAL|NUMERIC|FLOAT|DOUBLE))', 'IgnoreCase')
    if ($colMatches.Count -gt 0) {
        $cols = ($colMatches | ForEach-Object { $_.Groups[1].Value }) -join ', '
        $warnings += "[SNAKE_CASE] PascalCase/camelCase kolon tespit edildi: $cols -> snake_case'e cevir"
    }
}

# CHECK 2: C# dosyasinda yanlis error code formati
if ($fp -match '\.cs$') {
    $content = Get-Content $fp -Raw 2>$null
    $badCodes = [regex]::Matches($content, 'ErrorCode\s*=\s*"(?!INV-)([^"]+)"')
    if ($badCodes.Count -gt 0) {
        $codes = ($badCodes | ForEach-Object { $_.Groups[1].Value }) -join ', '
        $warnings += "[ERROR-CODE] INV-xxx formatina uymuyor: $codes -> arch/errors.md'den dogru kodu kullan"
    }
}

# CHECK 3: C# dosyasinda baska servise dogrudan referans (isolation ihlali)
# NOT: -match $Matches global'ini ezer, once service name'i yakala
$currentService = $null
if ($fp -match 'Invekto\.(\w+)[/\\]') { $currentService = $Matches[1] }
if ($currentService -and $fp -match '\.cs$') {
    if (-not $content) { $content = Get-Content $fp -Raw 2>$null }
    $otherServices = @('Backend','Automation','AgentAI','Knowledge','Outbound','WhatsAppAnalytics') | Where-Object { $_ -ne $currentService -and $_ -ne 'Shared' }
    foreach ($svc in $otherServices) {
        if ($content -match "using\s+Invekto\.$svc" -or $content -match "Invekto\.$svc\.") {
            $warnings += "[ISOLATION] $currentService servisi Invekto.$svc'e dogrudan referans veriyor -> Shared uzerinden iletisim kur"
        }
    }
}

if ($warnings.Count -gt 0) {
    $msg = "[INVARIANT-CHECK] " + ($warnings -join " | ")
    @{ systemMessage = $msg } | ConvertTo-Json -Compress
}
exit 0
