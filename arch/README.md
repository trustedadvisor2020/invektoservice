# InvektoServis Architecture

Bu klasör projenin mimari dokümanlarını içerir.

## Klasör Yapısı

```
arch/
├── README.md           # Bu dosya
├── errors.md           # Error codes (INV-xxx)
├── env.md              # Environment variables
├── logging.md          # Loglama standartları
├── session-memory.md   # Session durumu (runtime)
├── active-work.md      # Devam eden işler
├── lessons-learned.md  # Öğrenilen dersler
├── contracts/          # Data contracts
│   └── plan-schema.json
├── db/                 # Database şemaları
│   └── README.md
├── docs/               # Teknik dokümanlar
│   └── microservice-guide.md
└── plans/              # Feature planları
    └── diffs/          # Codex review diff'leri
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
| `session-memory.md` | Son durum | Her session sonunda |
| `active-work.md` | Devam eden işler | İş başlayınca/bitince |
| `lessons-learned.md` | Öğrenilen dersler | Q onayıyla |
