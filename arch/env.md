# Environment Configuration

> .NET 8 appsettings yapısı ve environment değişkenleri.

## Configuration Hierarchy

```
appsettings.json                 # Base config (committed)
appsettings.Development.json     # Dev overrides (committed)
appsettings.Production.json      # Prod overrides (NOT committed)
Environment Variables            # Runtime overrides
```

## Appsettings Structure

### Backend (`src/Invekto.Backend/appsettings.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "FilePath": "logs"
  },
  "AllowedHosts": "*"
}
```

### ChatAnalysis (`src/Invekto.ChatAnalysis/appsettings.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "FilePath": "logs"
  }
}
```

## Service Constants

Sabit değerler kod içinde tanımlı: `Invekto.Shared/Constants/ServiceConstants.cs`

| Constant | Value | Description |
|----------|-------|-------------|
| `BackendPort` | 5000 | Backend API port |
| `ChatAnalysisPort` | 7101 | ChatAnalysis microservice port |
| `BackendToMicroserviceTimeoutMs` | 600 | HTTP client timeout |
| `RetryCount` | 0 | Retry disabled (Stage-0) |
| `LogRetentionDays` | 30 | Log file retention |

## Environment-Specific Config

### Development

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    },
    "FilePath": "logs"
  }
}
```

### Production (D:\Invekto\)

```json
// appsettings.Production.json (NOT committed)
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "FilePath": "D:\\Invekto\\Backend\\logs"
  }
}
```

## Production Paths (Stage-0)

| Component | Path |
|-----------|------|
| Backend | `D:\Invekto\Backend\current\` |
| Backend Logs | `D:\Invekto\Backend\logs\` |
| Backend Config | `D:\Invekto\Backend\config\` |
| Microservice | `D:\Invekto\Microservice\current\` |
| Microservice Logs | `D:\Invekto\Microservice\logs\` |
| Microservice Config | `D:\Invekto\Microservice\config\` |

## Environment Variables

Override appsettings via environment variables:

```powershell
# Override log path
$env:Logging__FilePath = "D:\Invekto\Backend\logs"

# Override log level
$env:Logging__LogLevel__Default = "Debug"
```

## Güvenlik Kuralları

1. **Asla commit'leme:** `appsettings.Production.json` `.gitignore`'da olmalı
2. **Secrets:** Connection string, API key gibi değerler environment variable olarak
3. **Ports:** Production'da port değişikliği gerekirse appsettings ile override
