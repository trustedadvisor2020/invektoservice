# Active Work Tracker

> Devam eden işler. Session başında kontrol et.

## In Progress

| Slug | Status | Started | Description |
|------|--------|---------|-------------|
| (none) | - | - | Henüz aktif iş yok |

---

## Recently Completed

| Slug | Completed | Description |
|------|-----------|-------------|
| 20260202-stage0-scaffold | 2026-02-02 | Stage-0 scaffold: Backend + ChatAnalysis + Shared |
| 20260201-initial-setup | 2026-02-01 | Proje workflow yapısı kuruldu |

---

## Blocked

| Slug | Blocked Since | Reason | Waiting For |
|------|---------------|--------|-------------|
| (none) | - | - | - |

---

## Stage-0 Checklist

| Item | Status |
|------|--------|
| Solution yapısı | ✅ |
| Invekto.Shared | ✅ |
| Invekto.ChatAnalysis | ✅ |
| Invekto.Backend | ✅ |
| /health endpoint | ✅ |
| /ops endpoint | ✅ |
| JSON Lines logger | ✅ |
| 600ms timeout | ✅ |
| 0 retry | ✅ |
| Windows Service ready | ✅ |
| Build PASS | ✅ |

---

## Usage

### Yeni İş Başlatma
```markdown
| {slug} | IN_PROGRESS | {tarih} | {açıklama} |
```

### İş Tamamlama
1. In Progress'ten kaldır
2. Recently Completed'a ekle

### İş Engellenirse
1. In Progress'ten Blocked'a taşı
2. Waiting For alanını doldur
