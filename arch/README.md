# InvektoServis Architecture

Bu klasör projenin mimari dokümanlarını içerir.

## Klasör Yapısı

```
arch/
├── README.md           # Bu dosya
├── errors.md           # Error codes (INV-xxx)
├── env.md              # Environment variables
├── logging.md          # Loglama standartları
├── session-memory.md   # Session durumu + execution queue + recently completed
├── lessons-learned.md  # Öğrenilen dersler
├── contracts/          # Data contracts
│   └── plan-schema.json
├── db/                 # Database şemaları
│   └── README.md
├── docs/               # Teknik dokümanlar
│   └── microservice-guide.md
├── plans/              # Feature planları
│   └── diffs/          # Codex review diff'leri
└── specs/              # SDD Feature Spec'leri
    └── _TEMPLATE.md    # Yeni feature spec şablonu
```

## Önemli Kurallar

1. **arch/ tek gerçek kaynak** - Kurallar burada tanımlı
2. **Kod yazmadan ÖNCE oku** - İlgili dokümanı oku
3. **Contracts değişmez** - Schema değişikliği Q onayı gerektirir
4. **Error codes kullan** - `arch/errors.md`'den kod al

## Mikro Servis Mimarisi

InvektoServis bağımsız mikro servislerden oluşur:

```
services/
├── service-a/          # Her servis kendi başına deploy edilebilir
├── service-b/
└── ...

shared/                 # Paylaşılan kod
├── contracts/          # Servisler arası API kontratları
├── types/              # Paylaşılan type'lar
└── utils/              # Ortak utility'ler
```

### Servis İzolasyonu

- Her servis kendi DB'sine sahip olabilir
- Servisler arası iletişim API/Event üzerinden
- Shared kod değişikliği tüm servisleri etkiler

## Session Dosyaları

| Dosya | Amaç | Güncelleme |
|-------|------|------------|
| `session-memory.md` | Son durum + Execution Queue + Recently Completed | Her session sonunda (`/wrap` step 2) |
| `lessons-learned.md` | Öğrenilen dersler | Q onayıyla (`/learn`) |

> **active-work.md KALDIRILDI** (shared engine v6.1, 2026-03-04). Execution queue ve recently completed bilgisi artık `session-memory.md` içinde. Eski `active-work.md` referansları (arch/plans/ içindeki historical JSON'lar) arşiv niteliğindedir, güncel truth değildir.
