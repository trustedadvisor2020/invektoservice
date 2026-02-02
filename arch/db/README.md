# Database Documentation

> Her mikro servisin DB şeması burada dokümante edilir.

## Genel Kurallar

### Naming Convention

| Element | Convention | Örnek |
|---------|------------|-------|
| Tablo | `snake_case`, çoğul | `users`, `order_items` |
| Kolon | `snake_case` | `created_at`, `user_id` |
| Primary Key | `id` | `id` |
| Foreign Key | `{table}_id` | `user_id`, `order_id` |
| Index | `ix_{table}_{columns}` | `ix_users_email` |
| Unique | `uq_{table}_{columns}` | `uq_users_email` |

### Zorunlu Kolonlar

Her tablo şu kolonları içermeli:

```sql
id              -- Primary key (UUID veya INT)
created_at      -- Oluşturma zamanı (DEFAULT GETUTCDATE())
updated_at      -- Son güncelleme (trigger ile)
```

### Soft Delete

Silme işlemleri için:

```sql
is_deleted      -- BOOLEAN DEFAULT false
deleted_at      -- DATETIME NULL
```

---

## Servis Şemaları

### Service A (Örnek)

```sql
-- services/service-a/db/schema.sql

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) NOT NULL,
    name VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_deleted BOOLEAN DEFAULT false,
    deleted_at TIMESTAMP NULL,

    CONSTRAINT uq_users_email UNIQUE (email)
);

CREATE INDEX ix_users_email ON users(email);
```

---

## Migration Kuralları

1. Her migration dosyası `YYYYMMDD_HHMMSS_{description}.sql` formatında
2. Migration'lar geri alınabilir olmalı (DOWN script)
3. Production'da veri kaybı riski varsa Q onayı al
4. Her servis kendi migration'larını yönetir

### Migration Örneği

```sql
-- migrations/20260201_120000_add_user_status.sql

-- UP
ALTER TABLE users ADD COLUMN status VARCHAR(20) DEFAULT 'active';

-- DOWN
ALTER TABLE users DROP COLUMN status;
```

---

## Servis İzolasyonu

- Her servis kendi DB'sine sahip
- Servisler arası veri paylaşımı API üzerinden
- Join yasak - event-driven sync tercih edilir
