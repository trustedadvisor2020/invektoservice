# Stage-0 Setup Guide

> Bu doküman `invekto_stage0_kurulum_adimlari.txt` dosyasının yapılandırılmış versiyonudur.

## Amaç

Stage-0 hedefi:
- Hızlı ayağa kalksın
- Az parça ile çalışsın
- Basit deploy/rollback olsun
- Minimum troubleshooting

## Bilinçli Exclusions

Stage-0 şunları **içermez**:
- Redis
- RabbitMQ
- Nginx
- Prometheus/Jaeger/Loki/Grafana
- Outbox pattern
- DLQ
- Circuit breaker

## Bileşenler

| Component | Technology | Port |
|-----------|------------|------|
| Backend | .NET 8 Minimal API | 5000 |
| ChatAnalysis | .NET 8 + Windows Service | 7101 |
| Logging | JSON Lines files | - |

## Genel Akış

```
UI → Backend (5000) → ChatAnalysis (7101) → Backend → UI
```

## Sabit Kurallar

| Rule | Value |
|------|-------|
| Timeout | 600ms |
| Retry | 0 (yasak) |
| Log format | JSON Lines |
| Log retention | 30 gün |

## Dev Ortamı Çalıştırma

```powershell
# Terminal 1 - ChatAnalysis
cd C:\CRMs\InvektoServis\src\Invekto.ChatAnalysis
dotnet run

# Terminal 2 - Backend
cd C:\CRMs\InvektoServis\src\Invekto.Backend
dotnet run
```

## Test Endpoints

```powershell
# Health checks
Invoke-RestMethod http://localhost:7101/health
Invoke-RestMethod http://localhost:5000/health

# Ops dashboard
Invoke-RestMethod http://localhost:5000/ops

# Chat analysis (placeholder)
Invoke-RestMethod -Method Post http://localhost:5000/api/v1/chat/analyze -Headers @{"X-Tenant-Id"="test"; "X-Chat-Id"="123"}
```

## Production Dizin Yapısı

```
D:\Invekto\
├── Backend\
│   ├── current\        # Aktif sürüm
│   ├── releases\       # Eski sürümler
│   ├── config\         # appsettings.Production.json
│   └── logs\           # YYYY-MM-DD.jsonl
├── Microservice\
│   ├── current\
│   ├── releases\
│   ├── config\
│   └── logs\
└── Ops\
    ├── snapshots\
    └── exports\
```

## Windows Service Kurulumu

```powershell
# Service oluştur
sc.exe create "Invekto.ChatAnalysis" binPath= "D:\Invekto\Microservice\current\Invekto.ChatAnalysis.exe" start= auto

# Service başlat
sc.exe start "Invekto.ChatAnalysis"

# Service durdur
sc.exe stop "Invekto.ChatAnalysis"
```

## Recovery Ayarları

Windows Services → Properties → Recovery:
- First failure: Restart the Service
- Second failure: Restart the Service
- Subsequent failures: Restart the Service
- Restart service after: 1 minute

## Deploy Prosedürü

```powershell
# 1. Service durdur
sc.exe stop "Invekto.ChatAnalysis"

# 2. Backup
Copy-Item D:\Invekto\Microservice\current D:\Invekto\Microservice\releases\20260202_1200 -Recurse

# 3. Yeni versiyon
Expand-Archive microservice_20260202_1300.zip -DestinationPath D:\Invekto\Microservice\current -Force

# 4. Service başlat
sc.exe start "Invekto.ChatAnalysis"

# 5. Smoke test
Invoke-RestMethod http://localhost:7101/health
```

## Rollback Prosedürü

```powershell
# 1. Service durdur
sc.exe stop "Invekto.ChatAnalysis"

# 2. Eski versiyona dön
Remove-Item D:\Invekto\Microservice\current -Recurse
Copy-Item D:\Invekto\Microservice\releases\20260202_1200 D:\Invekto\Microservice\current -Recurse

# 3. Service başlat
sc.exe start "Invekto.ChatAnalysis"
```

## Checklist

- [ ] Dizin yapısı hazır
- [ ] Microservice current altında çalışıyor
- [ ] Windows Service kuruldu + recovery ayarlı
- [ ] /health 200
- [ ] Backend timeout 600ms, retry=0
- [ ] JSONL log standardı aktif
- [ ] /ops çalışıyor
- [ ] Deploy/rollback test edildi

## Stage-0 → Stage-1 Geçişi

1. Redis ekle
2. RabbitMQ ekle
3. Nginx ekle
4. Observability stack ekle
5. Outbox pattern ekle
