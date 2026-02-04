# Deploy Watcher Script
# Runs as Scheduled Task, watches for deploy flag files
# Q: This enables zero-downtime deploys without SSH

$WatchPath = "D:\Invekto"
$StopFlag = Join-Path $WatchPath "deploy-stop.flag"
$StartFlag = Join-Path $WatchPath "deploy-start.flag"
$Nssm = Join-Path $WatchPath "nssm.exe"
$LogFile = Join-Path $WatchPath "logs\deploy-watcher.log"

# Ensure log directory exists
$LogDir = Split-Path $LogFile -Parent
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] $Message"
    Add-Content -Path $LogFile -Value $logEntry
    Write-Host $logEntry
}

function Stop-InvektoServices {
    Write-Log "STOP flag detected - stopping services..."

    & $Nssm stop Invekto.Backend 2>&1 | Out-Null
    Write-Log "Invekto.Backend stopped"

    & $Nssm stop Invekto.ChatAnalysis 2>&1 | Out-Null
    Write-Log "Invekto.ChatAnalysis stopped"

    # Remove the flag file
    Remove-Item $StopFlag -Force
    Write-Log "Stop flag removed - ready for file upload"
}

function Start-InvektoServices {
    Write-Log "START flag detected - starting services..."

    & $Nssm start Invekto.ChatAnalysis 2>&1 | Out-Null
    Write-Log "Invekto.ChatAnalysis started"

    Start-Sleep -Seconds 3

    & $Nssm start Invekto.Backend 2>&1 | Out-Null
    Write-Log "Invekto.Backend started"

    # Remove the flag file
    Remove-Item $StartFlag -Force
    Write-Log "Start flag removed - deploy complete"

    # Health check
    Start-Sleep -Seconds 5
    try {
        $health = Invoke-RestMethod -Uri "http://127.0.0.1:5000/health" -TimeoutSec 10
        Write-Log "Health check PASSED: Backend is healthy"
    } catch {
        Write-Log "Health check WARNING: Backend may still be starting"
    }
}

# Main watch loop
Write-Log "Deploy watcher started - watching $WatchPath"

while ($true) {
    try {
        if (Test-Path $StopFlag) {
            Stop-InvektoServices
        }

        if (Test-Path $StartFlag) {
            Start-InvektoServices
        }
    } catch {
        Write-Log "ERROR: $($_.Exception.Message)"
    }

    Start-Sleep -Seconds 2
}
