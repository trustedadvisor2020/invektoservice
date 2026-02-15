# Lessons Learned Archive

> Arsivlenmis dersler. Aktif dersler icin: `arch/lessons-learned.md`
>
> **Arsiv Kurali:** 3 aydan eski girdiler otomatik buraya tasinir.
> TONIVA girdileri kalici olarak burada kalir (farkli proje).

---

## TONIVA (Farkli Proje - SQL Server / Node.js / EF Core)

> **TONIVA (c:\CRMs\TONIVA) InvektoServices ile ALAKASIZ!**
> Evrensel dersler (empty catch, retry pattern vb.) InvektoServices baglaminda
> `lessons-learned.md`'de zaten tekrar yer alir.

### Common Mistakes

| Date | Category | Mistake | Solution | Prevention |
|------|----------|---------|----------|------------|
| TONIVA | SQL | Deploy Manager GO hatasi | GO kaldirildi | mssql driver GO desteklemiyor |
| TONIVA | SQL | Ayni batch'te kolon ekle+kullan | EXEC() dynamic SQL | Compile-time vs runtime ayrimi |
| TONIVA | Codex | Em dash encoding sorunu | Double dash (--) kullan | Em dash YASAK |
| TONIVA | Codex | Empty catch block return false | logger.warn eklendi | Empty catch YASAK |
| TONIVA | DB | Ayni isi yapan 2 fonksiyon - biri lock'siz kaldi | Her ikisine de lock eklendi | Ayni resource'a erisen TUM fonksiyonlari kontrol et |
| TONIVA | Git | Onceki session'lardan kalan staged dosyalar | git reset HEAD + selective staging | Session basinda git status kontrolu |
| TONIVA | Retry | Backoff/limit olmadan retry queue | Backoff + max retry zorunlu | max_retry_count + exponential_backoff |
| TONIVA | Queue | Completion check sonsuz queue bekler | Drain mekanizmasi ekle | max_queue_size + timeout + drain_on_stop |
| TONIVA | Memory | Lambda event handler leak | EventHandlers class ile sakla | Event += lambda icin ayni referans sakla |
| TONIVA | EF | Singleton DbContext concurrent kullanim | IDbContextFactory + scoped context | DbContext thread-safe DEGIL |
| TONIVA | EF | AddDbContext IDbContextFactory register ETMIYOR | AddPooledDbContextFactory kullan | IDbContextFactory icin AddPooledDbContextFactory |
| TONIVA | EF | Entity property + migration ama EF mapping unutuldu | HasColumnName eklendi | Entity = migration + EF mapping |
| TONIVA | API | Backend API response degisti ama type tanimlari guncellenmedi | API typings guncellendi | Backend API degisikliginde type tanimlarini GUNCELLE |
| TONIVA | Race | SHARED mode + retry = race condition | Mutex lock + transaction isolation | SHARED + queue/retry = RACE CONDITION |
| TONIVA | Workflow | lessons-learned'da pattern VAR ama uygulanmadi | Kod yazmadan ONCE oku | Her session basinda lessons-learned OKU |
| TONIVA | PowerShell | Heredoc syntax Git commit'te hata | Temp dosya + git commit -F | PowerShell heredoc guvenilmez |
| TONIVA | SQL | GUID array mssql driver INT'e cevirmeye calisti | STRING_SPLIT + TRY_CAST | GUID array icin string birlestir |
| TONIVA | SQL | Kolon adini yanlis varsaydim | Schema'dan kontrol et | Kolon adini VARSAYMA |
| TONIVA | SQL | MERGE WITH HOLDLOCK deadlock | HOLDLOCK kaldirildi + retry | MERGE'de HOLDLOCK gereksiz |
| TONIVA | Config | Env variable sadece yoksa ekleniyordu | Her zaman override et | Kritik degerler HER ZAMAN override |
| TONIVA | UI | HTTP error'da polling durmuyor | clearInterval + state reset | Error catch'inde polling durdur |
| TONIVA | API | Token ve response null check eksik | if (!token) return + optional chaining | Her API cagrisinda token + response kontrolu |
| TONIVA | Logging | Production'da logger.info gorunmedi | logger.warn + console.log | Production debug icin logger.warn kullan |
| TONIVA | SQL | OUTER APPLY N satir = N*2 subquery | 3 asamali bulk fetch pattern | Bulk query pattern kullan |

### Code Review Insights

| Date | Finding | Action Taken |
|------|---------|--------------|
| TONIVA | NOLOCK + keyset pagination = dirty reads riski | Export'ta kabul edilebilir, kritik islemlerde NOLOCK kullanma |

---

## InvektoServices Archived Entries

> 3 aydan eski girdiler buraya tasinir.
> Su an henuz arsivlenecek giris yok (proje 2026-02-01'de basladi).
