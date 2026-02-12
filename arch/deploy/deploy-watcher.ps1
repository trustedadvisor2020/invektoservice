# ============================================
#  INVEKTO - Deploy Watcher Service
#  Watches for deploy flag files and stops/starts NSSM services
#  Install as NSSM service: see install-deploy-watcher.bat
# ============================================

$watchPath = "E:\Invekto"
$nssm = "E:\nssm.exe"
$services = @("InvektoBackend", "InvektoChatAnalysis", "InvektoAutomation", "InvektoAgentAI", "InvektoOutbound")
$logFile = "E:\Invekto\logs\deploy-watcher.log"

# Ensure log directory exists
$logDir = Split-Path $logFile
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

function Write-Log($msg) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $msg"
    Write-Host $line
    Add-Content -Path $logFile -Value $line -Encoding UTF8
}

Write-Log "Deploy watcher started. Watching: $watchPath"

while ($true) {
    $stopFlag = Join-Path $watchPath "deploy-stop.flag"
    $startFlag = Join-Path $watchPath "deploy-start.flag"

    if (Test-Path $stopFlag) {
        Write-Log "STOP flag detected - stopping services..."
        foreach ($svc in $services) {
            Write-Log "  Stopping $svc..."
            & $nssm stop $svc 2>&1 | ForEach-Object { Write-Log "  $_" }
        }
        # Wait for processes to fully release files
        Start-Sleep -Seconds 3
        Remove-Item $stopFlag -Force -ErrorAction SilentlyContinue
        Write-Log "Services stopped, flag removed"
    }

    if (Test-Path $startFlag) {
        Write-Log "START flag detected - starting services..."
        foreach ($svc in $services) {
            Write-Log "  Starting $svc..."
            & $nssm start $svc 2>&1 | ForEach-Object { Write-Log "  $_" }
        }
        Remove-Item $startFlag -Force -ErrorAction SilentlyContinue
        Write-Log "Services started, flag removed"
    }

    Start-Sleep -Seconds 2
}
