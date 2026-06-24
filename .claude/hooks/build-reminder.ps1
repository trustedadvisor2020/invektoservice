# PostToolUse hook: .cs dosyasi edit edildiginde build hatirlatmasi
# + Bilinen hata pattern'lerine cozum onerisi inject eder
# Exit 0 her zaman (non-blocking), systemMessage inject eder
$raw = [Console]::In.ReadToEnd()
try { $inp = $raw | ConvertFrom-Json } catch { exit 0 }

$fp = $inp.tool_input.file_path
if (-not $fp) { exit 0 }
if ($fp -notmatch '\.cs$') { exit 0 }

$svc = if ($fp -match 'Chatinbox\.(\w+)[/\\]') { $Matches[1] } else { 'Unknown' }

# Remediation map: bilinen hata pattern'leri ve cozumleri
$remediations = @"
Known build error remediations:
- CS0234 (type/namespace missing): Add 'using Chatinbox.Shared.xxx;' or check project reference
- CS0246 (type not found): Check if DTO exists in Chatinbox.Shared/DTOs/
- CS8600/CS8602 (null reference): Add null check, use ?? operator, or ?. operator
- CS0103 (name does not exist): Check variable scope and spelling
- CS1061 (no definition): Check if method/property exists on the type, may need cast
- CS0029 (cannot convert): Check type compatibility, may need explicit cast or .ToString()
"@

$msg = "[BUILD-HOOK] $svc servisi: $fp degisti. BUILD CALISTIR: dotnet build Chatinbox.sln --no-restore -v q`n$remediations"
@{ systemMessage = $msg } | ConvertTo-Json -Compress
exit 0
