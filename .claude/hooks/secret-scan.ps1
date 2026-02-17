# PreToolUse hook: Bash(git add/commit/push) oncesi secret taramasi
# Exit 2 = BLOCKING, Exit 0 = allow
$raw = [Console]::In.ReadToEnd()
try { $inp = $raw | ConvertFrom-Json } catch { exit 0 }

$cmd = $inp.tool_input.command
if (-not ($cmd -match 'git\s+(add|commit|push)')) { exit 0 }

# High-risk dosya pattern'leri
$staged = git diff --cached --name-only 2>$null
$modified = git diff --name-only 2>$null
$allFiles = @($staged) + @($modified) | Where-Object { $_ } | Sort-Object -Unique

foreach ($f in $allFiles) {
    # Skip hook files themselves (they contain the word "secret" in filename)
    if ($f -match '\.claude/hooks/') { continue }
    if ($f -match '\.(env|pem|key)$' -or $f -match 'credentials|secret' -or $f -match '^deploy_output/') {
        Write-Error "[SECRET-SCAN BLOCKED] High-risk file: $f"
        exit 2
    }
}

# Content scan
$secretPattern = 'sk-[a-zA-Z0-9]{20,}|apikey\s*[:=]\s*[a-zA-Z0-9]{20,}|password\s*[:=]|-----BEGIN.*(RSA|EC|OPENSSH|PRIVATE)'
foreach ($f in $allFiles) {
    if (-not (Test-Path $f)) { continue }
    if ($f -match '\.(dll|exe|png|jpg|gif|pdf|zip)$') { continue }
    if ($f -match '\.claude/hooks/') { continue }
    $hits = Select-String -Path $f -Pattern $secretPattern -AllMatches 2>$null
    if ($hits) {
        Write-Error "[SECRET-SCAN BLOCKED] Potential secret in: $f"
        exit 2
    }
}
exit 0
