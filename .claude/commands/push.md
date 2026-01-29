# Push Skill

Git commit ve push işlemlerini otomatize eder.

## Workflow

1. **Status Check:** `git status` ile değişiklikleri listele
2. **Stage:** Değişiklikleri staging'e ekle (kullanıcı onayı ile)
3. **Commit:** Anlamlı commit message ile commit oluştur
4. **Push:** Remote'a push et

## Instructions

Bu skill çağrıldığında:

1. Önce `git status` çalıştır ve değişiklikleri göster
2. Eğer commit edilmemiş değişiklik varsa:
   - Değişiklikleri özetle
   - Commit message öner (conventional commits formatında)
   - Kullanıcıdan onay al
3. Commit sonrası `git push -u origin <branch>` çalıştır
4. Push başarılıysa sonucu raporla

## Commit Message Format

```
<type>(<scope>): <description>

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
```

**Types:**
- `feat`: Yeni özellik
- `fix`: Bug fix
- `refactor`: Kod düzenleme
- `docs`: Dokümantasyon
- `chore`: Bakım işleri
- `test`: Test ekleme

## Safety Rules

- ASLA `--force` kullanma
- ASLA `main`/`master`'a direkt push yapma (uyar)
- Sensitive dosyaları (.env, secrets) ASLA commit'leme
- Her zaman kullanıcı onayı al

## Example Usage

```
/push
```

Agent otomatik olarak:
1. Değişiklikleri analiz eder
2. Commit message önerir
3. Q onayı ile push eder
