# INMA Ekibine — INSE ile Birlikte Çalışma Planı

**Tarih:** 2026-04-13
**Hazırlayan:** Q (Taner)

## Neden Böyle Bir Şey Yapıyoruz?

INMA 5 yıldır sağlam bir mesajlaşma platformu. Firmalar WhatsApp, Instagram, Telegram üzerinden müşterileriyle konuşuyor, agent'lar cevap yazıyor, şablonlar kullanılıyor. Güzel çalışıyor.

**Ama bugünün müşterileri daha fazlasını istiyor:**
- "Yeni gelen mesajı okuyup ne istediğini AI anlasın"
- "Müşteri cevap vermezse 2 gün sonra otomatik hatırlatma gitsin"
- "10 farklı karşılama mesajımı dönüşümlü kullan"
- "Randevu almak isteyen müşteriye otomatik slot göster, Google Meet linki yolla"
- "Etkinlik sonrası 3/7/14 gün drip kampanyası çalışsın"
- "Dashboard'da dönüşüm hunisini göster"

Bu özellikleri INMA'ya tek tek eklemek yerine, bu işi yapan **INSE (Invekto Services)** adlı ikinci bir sistem kuruyoruz. INSE bu AI/otomasyon işlerini yapacak, INMA ise zaten yaptığı işlere odaklanmaya devam edecek.

**Kritik nokta:** Kullanıcı iki ayrı sistem hissetmeyecek. Tek login, tek domain, tek görsel dil. INMA açar, yanında INSE özellikleri de doğal olarak yerleşmiş olacak.

**İlk müşteri:** Dent Adavista (İrlanda Roadshow kampanyası — Dublin & Cork). Bu müşteri üstünden pilot yapıyoruz.

**Peki neden INMA ekibinden bir şey istiyoruz?** Çünkü kullanıcı zaten INMA'ya login oluyor, mesajlar INMA'da, kişiler INMA'da. INSE'nin bu verilere ulaşması ve INMA içinde doğal görünmesi için birkaç küçük kapı açmamız gerek.

---

## İstekler — Üç Grup

- **P0 (hemen):** Dent Adavista pilotu için şart. Bunlar olmadan pilot başlayamaz.
- **P1 (pilot sonrası):** Kullanıcı deneyimini çok güzelleştiren ama ilk gün olmasa da olur.
- **P2 (ileride):** Platform olgunlaştıkça.

---

## P0 — Hemen Yapılması Gerekenler

### 1. Tek Login (SSO)

**Ne istiyoruz?**
Kullanıcı INMA'ya login olduğunda, INSE ekranlarına da şifre sormadan geçebilsin.

**Teknik:** INMA login sonrası verdiği JWT token'ı INSE doğrulayabilmeli. Bize JWT'yi imzaladığınız **public key** (veya JWKS endpoint) lazım. Token'ın içinde `company_code`, `user_id`, `role` claim'leri olsun.

**Senaryo:**
> Dent Adavista'nın koordinatörü sabah INMA'ya giriyor. Sol menüdeki "Flows" veya "Kampanyalar" linkine tıklıyor. Bu link INSE'nin flow builder'ına gidiyor ama koordinatöre bu geçiş hiç hissedilmiyor — tekrar login yok, aynı sayfanın başka bir bölümüne geçmiş gibi.

**Neden önemli?** İkinci bir login ekranı görürse kullanıcı "iki ayrı uygulama" hissi alır. Unification felsefesi çöker.

**Çıktı:**
- JWT imzalama algoritması (RS256 öneriyoruz)
- Public key veya JWKS URL
- Token claim listesi (`sub`, `company_code`, `role`, `exp`)

**Veri Akışı:**
```
[User] → INMA login ekranı → INMA backend: JWT üret (RS256)
                            → Cookie: inma_token=eyJ...
[User] → INMA sidebar "Flows" tıkla → /ai/flows'a git
[Browser] → INSE'ye request (Cookie otomatik gider)
[INSE] → JWT verify (INMA public key) → companyCode, userId, role claim oku
[INSE] → Flow builder ekranını render
```

**Angular Örneği (INMA sidebar'daki INSE link'i):**
```typescript
// inse-nav.component.ts (Angular 19, standalone + signals)
import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'inse-nav',
  standalone: true,
  template: `
    <nav class="inse-links">
      @for (link of inseLinks(); track link.key) {
        @if (featureEnabled(link.feature)) {
          <a [href]="inseBaseUrl + link.path" class="nav-item">
            <i [class]="link.icon"></i> {{ link.label }}
          </a>
        }
      }
    </nav>
  `
})
export class InseNavComponent {
  private http = inject(HttpClient);
  readonly inseBaseUrl = '/ai';  // reverse proxy altında

  readonly features = toSignal(
    this.http.get<{ flags: Record<string, boolean> }>('/api/features'),
    { initialValue: { flags: {} } }
  );

  readonly inseLinks = signal([
    { key: 'flows', label: 'Akışlar', path: '/flows', icon: 'i-flow', feature: 'flow_builder' },
    { key: 'campaigns', label: 'Kampanyalar', path: '/campaigns', icon: 'i-send', feature: 'drip' },
    { key: 'appointments', label: 'Randevular', path: '/appointments', icon: 'i-calendar', feature: 'appointments' },
    { key: 'funnel', label: 'Dönüşüm', path: '/funnel', icon: 'i-chart', feature: 'funnel' },
  ]);

  featureEnabled = (f: string) => this.features().flags[f] === true;
}
```

**INMA backend (JWT üretimi) — kavramsal:**
```csharp
// INMA .NET backend'de login endpoint
var claims = new[] {
    new Claim("sub", user.Id),
    new Claim("company_code", user.CompanyCode),
    new Claim("role", user.Role),       // "admin" | "agent" | "manager"
    new Claim("features", featuresJson) // JSON string: INSE feature flag'leri
};
var token = new JwtSecurityToken(
    issuer: "inma.invekto.com",
    claims: claims,
    expires: DateTime.UtcNow.AddHours(8),
    signingCredentials: new SigningCredentials(rsaPrivateKey, SecurityAlgorithms.RsaSha256));
```

---

### 2. Yeni Firma Açılınca INSE Haberdar Olsun

**Ne istiyoruz?**
INMA'da yeni bir firma (tenant) açıldığında, INSE otomatik olarak o firma için de kayıt oluşturabilsin.

**Teknik:** INMA'da zaten per-tenant webhook sistemi var (ekrandaki WebHook URL ayarı). Ona bir event tipi daha eklemenizi istiyoruz: `tenant.created`. Payload: `{ companyCode, companyName, createdAt, plan, features }`.

**Senaryo:**
> Siz INMA admin panelinden yeni bir dental klinik ekliyorsunuz. Klinik Dent Adavista gibi AI flow + randevu sistemi de kullanacak. Siz "Dental Klinik" lisans paketini seçince INMA otomatik olarak INSE'ye haber veriyor. INSE de kendi tarafında boş bir tenant oluşturuyor — default flow, default template'ler hazır gelsin diye. Manuel müdahale yok.

**Neden önemli?** Şu an bir firma iki ayrı sistemde iki kez kayıt edilmek zorunda. Hem iş yükü hem tutarsızlık riski.

**Çıktı:**
- `tenant.created`, `tenant.updated`, `tenant.deactivated` event tipleri
- Hedef URL platform config'inde tutulacak (örn. `https://inse.invekto.com/api/inbound/tenant-lifecycle`)

**Veri Akışı:**
```
[INMA Admin UI] → "Yeni Firma Ekle" formu doldurulur
[INMA backend] → tenants tablosuna INSERT
               → OutboundWebhookService.publish("tenant.created", payload)
[INMA → INSE] POST https://inse.invekto.com/api/inbound/tenant-lifecycle
              Header: X-INMA-Signature: <HMAC-SHA256>
              Body: {
                "event": "tenant.created",
                "companyCode": "dentadavista",
                "companyName": "Dent Adavista Dental Clinic",
                "createdAt": "2026-04-13T11:30:00Z",
                "plan": "premium",
                "features": ["ai_agent", "flow_builder", "drip", "appointments"]
              }
[INSE] → Signature doğrula → tenants tablosuna INSERT → default flow/template seed et
      → 200 OK
```

**Angular Örneği (INMA admin tenant create form):**
```typescript
// tenant-create.component.ts
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'tenant-create',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()">
      <input formControlName="companyName" placeholder="Firma adı" />
      <input formControlName="companyCode" placeholder="Kod (dentadavista)" />
      <select formControlName="plan">
        <option value="basic">Basic</option>
        <option value="premium">Premium (INSE dahil)</option>
      </select>
      <fieldset formGroupName="features">
        <label><input type="checkbox" formControlName="ai_agent"/> AI Agent</label>
        <label><input type="checkbox" formControlName="flow_builder"/> Flow Builder</label>
        <label><input type="checkbox" formControlName="drip"/> Drip Kampanya</label>
        <label><input type="checkbox" formControlName="appointments"/> Randevular</label>
      </fieldset>
      <button type="submit" [disabled]="saving()">Oluştur</button>
      @if (message()) { <p>{{ message() }}</p> }
    </form>
  `
})
export class TenantCreateComponent {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);

  readonly saving = signal(false);
  readonly message = signal('');

  form = this.fb.group({
    companyName: ['', Validators.required],
    companyCode: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
    plan: ['premium'],
    features: this.fb.group({
      ai_agent: [true], flow_builder: [true], drip: [true], appointments: [true]
    })
  });

  async submit() {
    if (this.form.invalid) return;
    this.saving.set(true);
    // INMA backend bu firma'yı oluşturur + otomatik tenant.created event publish eder
    this.http.post('/api/admin/tenants', this.form.value).subscribe({
      next: () => this.message.set('Firma oluşturuldu, INSE bilgilendirildi ✓'),
      error: (err) => this.message.set('Hata: ' + err.message),
      complete: () => this.saving.set(false)
    });
  }
}
```

**INMA backend webhook publisher (kavramsal):**
```csharp
// INMA TenantService.CreateAsync sonrası
await _outboundWebhookService.PublishAsync(
    eventType: "tenant.created",
    tenantId: tenant.CompanyCode,
    payload: new {
        companyCode = tenant.CompanyCode,
        companyName = tenant.Name,
        createdAt = tenant.CreatedAt,
        plan = tenant.Plan,
        features = tenant.InseFeatures
    });
// OutboundWebhookService zaten her tenant'ın webhook URL'sine POST ediyor
```

---

### 3. Mesajın İçinde Değişken Kullanabilmek

**Ne istiyoruz?**
Şablon mesajlarında `{{name}}`, `{{city}}` gibi yer tutucular kullanıp, mesaj gönderilirken bunların gerçek değerlerle değişmesi.

**Teknik:** INMA'nın mesaj gönderme API'sine `variables: { "name": "John", "city": "Dublin" }` parametresi ekleyin. Şablonun içinde `{{name}}` geçiyorsa, gönderim anında `John` ile değiştirilip gönderilsin.

**Senaryo:**
> INMA'da "Dublin Randevu Onayı" adında bir şablon var: *"Hi {{name}}, your appointment in {{city}} is confirmed for {{date}}."*
>
> INSE bir akış çalıştırıyor, 50 kişiye bu şablonu gönderecek. Ama her kişiye kendi adıyla ve kendi şehriyle. INSE her gönderimde `variables: { name: "John", city: "Dublin", date: "14 March" }` gibi değişken seti yolluyor. INMA da bunu alıp şablonu kişiselleştirilmiş haliyle gönderiyor.

**Neden önemli?** Kişiselleştirmesiz toplu mesaj spam hissi verir, açılma oranı düşer. Bu en temel pazarlama gereği. Şu an INMA şablonları sabit metin, personalization yok.

**Çıktı:**
- `variables` parametresi (key-value map)
- `{{key}}` sözdizimi render
- Eksik değişken varsa ne olacak? (önerimiz: literal `{{key}}` kalsın, hata vermesin)

**Veri Akışı:**
```
[INSE Flow Engine] → "welcome_roadshow" flow node tetiklendi
[INSE] → Lead context'ten değişken set'i hazırla
[INSE → INMA] POST /api/chatsv3/start-chat
              Header: X-CIB-SecretKey: <key>
              Body: {
                "companyCode": "dentadavista",
                "channelId": "<wa-channel-id>",
                "phoneNumber": "+447547762090",
                "templateId": 1042,
                "variables": {
                  "name": "John",
                  "city": "Dublin",
                  "date": "14 March"
                }
              }
[INMA] → Template'i fetch et: "Hi {{name}}, your appointment in {{city}} is on {{date}}"
       → variables map'le render et
       → Final: "Hi John, your appointment in Dublin is on 14 March"
       → WhatsApp API'ye gönder → 200 OK + messageId
[INSE] ← response { messageId, status: "sent" }
```

**INMA backend render logic (örnek):**
```csharp
public string RenderTemplate(string templateBody, Dictionary<string, string> variables)
{
    // "Hi {{name}}, your {{city}} visit" + {name:"John", city:"Dublin"}
    // → "Hi John, your Dublin visit"
    return Regex.Replace(templateBody, @"\{\{(\w+)\}\}", match =>
    {
        var key = match.Groups[1].Value;
        return variables.TryGetValue(key, out var val) ? val : match.Value; // eksikse literal
    });
}
```

**Angular template editor örneği (değişken önizlemesi):**
```typescript
// template-preview.component.ts
import { Component, computed, signal } from '@angular/core';

@Component({
  selector: 'template-preview',
  standalone: true,
  template: `
    <textarea [value]="body()" (input)="body.set($any($event.target).value)"
              placeholder="Hi {{'{{'}}name{{'}}'}}, your {{'{{'}}city{{'}}'}} visit..."></textarea>
    <h4>Değişkenler (INSE doldurulacak):</h4>
    <ul>
      @for (v of detectedVariables(); track v) {
        <li><code>{{'{{'}}{{ v }}{{'}}'}}</code></li>
      }
    </ul>
    <h4>Önizleme (örnek değerlerle):</h4>
    <p class="preview">{{ preview() }}</p>
  `
})
export class TemplatePreviewComponent {
  readonly body = signal('Hi {{name}}, your {{city}} visit is on {{date}}.');

  // Metinden {{xxx}} çıkar
  readonly detectedVariables = computed(() => {
    const matches = this.body().matchAll(/\{\{(\w+)\}\}/g);
    return [...new Set([...matches].map(m => m[1]))];
  });

  // Örnek değerlerle render
  readonly sampleValues: Record<string, string> = {
    name: 'John', city: 'Dublin', date: '14 March'
  };

  readonly preview = computed(() =>
    this.body().replace(/\{\{(\w+)\}\}/g, (_, k) => this.sampleValues[k] ?? `{{${k}}}`)
  );
}
```

---

### 4. Kişi "STOP" Dediğinde Listeden Çıksın

**Ne istiyoruz?**
Bir kişi WhatsApp'tan "STOP" yazarsa, o kişiye bir daha pazarlama/hatırlatma mesajı gitmesin.

**Teknik:** Contact tablosuna `opted_out boolean default false` kolonu + iki endpoint: `PATCH /api/contacts/{id}/opt-out` ve `PATCH /api/contacts/{id}/opt-in`. Outbound send sırasında `opted_out=true` ise pazarlama mesajı bloke edilsin (transactional — randevu onayı, fatura gibi — geçmeye devam etsin).

**Senaryo:**
> Dent Adavista müşterisi Sarah, 3 hatırlatma mesajı aldıktan sonra "STOP" yazıyor. INSE bu kelimeyi algılıyor, INMA'ya "Sarah için opt-out yap" çağrısı yapıyor. Ertesi gün yeni bir kampanya başlıyor — 1000 kişilik listede Sarah da var ama INMA otomatik olarak ona göndermiyor. Sarah randevu onayı istediğinde o mesaj geçiyor (transactional), kampanyalar bloke.

**Neden önemli?** GDPR ve WhatsApp Business Policy gereği zorunlu. Opt-out'a saygı göstermezseniz numaranızı WA banlayabilir. Hukuki + teknik bir risk.

**Çıktı:**
- `opted_out` field
- İki endpoint (opt-out / opt-in)
- Send logic'inde `transactional` flag ayrımı

**Veri Akışı (opt-out):**
```
[Müşteri] → WA'dan "STOP" yaz
[INMA] → inbound webhook → INSE'ye push
[INSE] → MessageClassifier: "opt_out_keyword" tespit
[INSE → INMA] PATCH /api/contacts/{contactId}/opt-out
              Body: { "reason": "user_keyword_STOP", "source": "whatsapp" }
[INMA] → contacts.opted_out = true, log audit
[INSE → INMA] POST /api/chatsv3/start-chat (confirmation)
              Body: { templateId: 999 (opt-out confirmation), variables: {} }
[INMA] → "You've been unsubscribed" mesajı gönder

Sonra yeni kampanya başladığında:
[INSE → INMA] POST /api/chatsv3/bulk-send
              Body: { contacts: [...1000...], templateId, transactional: false }
[INMA] → opted_out=true olanları listeden çıkar, kalanları gönder
       → response: { sent: 943, skipped_optout: 57, failed: 0 }
```

**Angular contact detail panel (opt-out gösterimi):**
```typescript
// contact-detail.component.ts
import { Component, inject, input, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { rxResource } from '@angular/core/rxjs-interop';

interface Contact {
  id: string; name: string; phone: string;
  optedOut: boolean; optOutReason?: string; optOutAt?: string;
}

@Component({
  selector: 'contact-detail',
  standalone: true,
  template: `
    @let c = contact.value();
    @if (c) {
      <header>
        <h2>{{ c.name }} ({{ c.phone }})</h2>
        @if (c.optedOut) {
          <span class="badge badge-danger">
            ⚠️ Opt-out ({{ c.optOutReason }})
          </span>
          <button (click)="optIn(c.id)" [disabled]="busy()">Tekrar izin ver</button>
        } @else {
          <button (click)="optOut(c.id)" [disabled]="busy()">Opt-out yap</button>
        }
      </header>
    }
  `
})
export class ContactDetailComponent {
  contactId = input.required<string>();
  private http = inject(HttpClient);
  readonly busy = signal(false);

  readonly contact = rxResource({
    request: () => ({ id: this.contactId() }),
    loader: ({ request }) => this.http.get<Contact>(`/api/contacts/${request.id}`)
  });

  optOut(id: string) {
    this.busy.set(true);
    this.http.patch(`/api/contacts/${id}/opt-out`,
      { reason: 'manual_admin', source: 'inma_ui' })
      .subscribe({ complete: () => { this.busy.set(false); this.contact.reload(); }});
  }

  optIn(id: string) {
    this.busy.set(true);
    this.http.patch(`/api/contacts/${id}/opt-in`, {})
      .subscribe({ complete: () => { this.busy.set(false); this.contact.reload(); }});
  }
}
```

---

### 5. Toplu Mesaj Gönderim Endpoint'i

**Ne istiyoruz?**
Tek seferde çok kişiye mesaj gönderebileceğimiz bir API.

**Teknik:** `POST /api/chatsv3/bulk-send`. Body: `{ contacts: [phone1, phone2, ...], templateId, variables: {...}, scheduleAt?: ISO8601 }`. Response'ta bir job id olsun, per-contact status callback verilsin. WhatsApp tier limit'ini INMA yönetsin.

**Senaryo:**
> Dent Adavista Roadshow öncesi 200 kayıtlı katılımcıya "Hazırlıklarınız tamam mı?" hatırlatma mesajı göndermek istiyor. INSE'nin kampanya ekranından "Gönder" diyor. INSE 200 ayrı API çağrısı yapmak yerine tek bulk-send çağrısı yapıyor. INMA bu 200'ü WhatsApp rate limit'ine göre sıraya alıp gönderiyor, her birinin durumunu callback ile INSE'ye bildiriyor. INSE dashboard'da "178 gönderildi, 22 bekliyor, 0 hata" görüyor.

**Neden önemli?** Şu an tek tek API çağrısı yapılsa sunucu çöker, rate limit aşılır. Bulk endpoint ortak altyapı gerektiren bir iş, INMA tarafında yapmak mantıklı.

**Çıktı:**
- Batch endpoint
- Schedule desteği (şimdi gönder ya da 2 saat sonra)
- Status callback (webhook ile INSE'ye per-contact)

**Veri Akışı:**
```
[INSE Campaign UI] → "Gönder" → hedef 200 kişi, schedule: şimdi
[INSE → INMA] POST /api/chatsv3/bulk-send
              Body: {
                "companyCode": "dentadavista",
                "channelId": "<wa-id>",
                "templateId": 1042,
                "contacts": [
                  { "phone": "+353...", "variables": {"name":"John","city":"Dublin"} },
                  { "phone": "+353...", "variables": {"name":"Mary","city":"Cork"} },
                  ... (200 kayıt)
                ],
                "scheduleAt": null,         // şimdi gönder
                "transactional": false,     // pazarlama
                "callbackUrl": "https://inse.invekto.com/api/bulk-callback"
              }
[INMA] → Job create → response: { jobId: "bulk-8842", queued: 200 }
       → Rate-limiter kuyruğuna al (WA tier limit)
       → Her 1 mesaj için async gönderim

Her gönderim için callback:
[INMA → INSE] POST https://inse.invekto.com/api/bulk-callback
              Body: {
                "jobId": "bulk-8842",
                "phone": "+353...",
                "status": "sent" | "failed" | "skipped_optout",
                "messageId": "wa_abc123",
                "error": null,
                "timestamp": "2026-04-13T12:00:05Z"
              }
[INSE] → campaign_deliveries tablosu update → dashboard anlık güncelle
```

**Angular campaign dashboard (INMA tarafı opsiyonel, INSE'de de olabilir):**
```typescript
// campaign-progress.component.ts
import { Component, inject, input } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { rxResource } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

interface JobStatus {
  jobId: string; total: number;
  sent: number; failed: number; skippedOptout: number; queued: number;
  status: 'running' | 'completed' | 'paused';
}

@Component({
  selector: 'campaign-progress',
  standalone: true,
  template: `
    @let s = status.value();
    @if (s) {
      <h3>Kampanya: {{ s.jobId }}</h3>
      <progress [value]="s.sent + s.failed + s.skippedOptout" [max]="s.total"></progress>
      <dl>
        <dt>Gönderildi</dt><dd>{{ s.sent }}</dd>
        <dt>Başarısız</dt><dd>{{ s.failed }}</dd>
        <dt>Opt-out atlandı</dt><dd>{{ s.skippedOptout }}</dd>
        <dt>Kuyrukta</dt><dd>{{ s.queued }}</dd>
      </dl>
      <span class="badge">{{ s.status }}</span>
    }
  `
})
export class CampaignProgressComponent {
  jobId = input.required<string>();
  private http = inject(HttpClient);

  // 3 saniyede bir poll
  private tick = toSignal(interval(3000), { initialValue: 0 });

  readonly status = rxResource({
    request: () => ({ id: this.jobId(), tick: this.tick() }),
    loader: ({ request }) =>
      this.http.get<JobStatus>(`/api/bulk-jobs/${request.id}`)
  });
}
```

---

## P1 — Pilot Sonrası (v1.1)

### 6. Sohbet Ekranında "AI Öneri" Alanı

**Ne istiyoruz?**
Agent bir müşteriye cevap yazarken, INSE'nin AI'ı bir cevap öneri kutusu gösterebilsin.

**Teknik:** INMA sohbet ekranında agent'ın yazı yazdığı alanın **üstünde** bir DOM slot açın. INSE oraya iframe veya Web Component ile kendi widget'ını gömer. INMA widget'a `conversationId`, `contactId`, `lastMessageText` bilgisini postMessage ile gönderir. Agent öneriye tıklayınca o metin INMA input'a yazılır.

**Senaryo:**
> Müşteri yazıyor: *"Is it really free? I don't trust these offers online."*
>
> Agent cevap yazmaya başlamadan önce, input'un üstünde 3 öneri beliriyor:
> - *"Yes, it's completely free. We organize these meetings..."*
> - *"Your concern is understandable. Let me explain..."*
> - *"No obligation at all. Our dentist will just..."*
>
> Agent uygun olana tıklıyor, metin input'a yazılıyor, agent isterse ufak değişiklik yapıp gönderiyor. 30 saniyede cevap hazır, agent-müşteri oranı iyileşiyor.

**Neden önemli?** Agent verimi 2-3x artar, cevap kalitesi standartlaşır, yeni başlayan agent'ın eğitimi kolaylaşır.

**Veri Akışı:**
```
[Müşteri WA] → "Is it really free?" yazıyor
[INMA] → inbound webhook → INSE'ye push
[INMA UI] → Sohbet ekranı açık, agent yeni mesajı görüyor
[INMA UI widget slot] → INSE iframe mount:
                        <iframe src="https://inse.invekto.com/widget/suggest?v=1">

[INMA UI] → postMessage ile context gönder:
            { conversationId, contactId, lastMessageText: "Is it really free?" }
[INSE widget] ← mesajı al
[INSE widget → INSE API] POST /api/ai/suggest-reply
                          Body: { contactId, lastMessage, locale: "en" }
[INSE API] → Intent: "is_it_free" → 3 varyant template'den cevap üret
[INSE widget] ← 3 öneri
[INSE widget] → Agent öneriye tıklar → postMessage(parent, { text: "Yes, it's..." })
[INMA UI] → text-input.value = öneri metni
```

**INMA Angular host slot:**
```typescript
// chat-panel.component.ts
import { Component, ViewChild, ElementRef, signal, input, effect, inject } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Component({
  selector: 'chat-panel',
  standalone: true,
  template: `
    <!-- Mesaj listesi -->
    <div class="messages">...</div>

    <!-- INSE Suggest Reply widget slot -->
    <div class="suggest-slot">
      <iframe #suggestFrame
              [src]="widgetUrl"
              title="AI Öneri"
              frameborder="0"
              style="width:100%; min-height: 80px"></iframe>
    </div>

    <!-- Agent input -->
    <textarea #replyInput [(ngModel)]="replyText"></textarea>
    <button (click)="send()">Gönder</button>
  `
})
export class ChatPanelComponent {
  conversationId = input.required<string>();
  contactId = input.required<string>();
  lastMessage = input<string>('');

  private sanitizer = inject(DomSanitizer);
  readonly widgetUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
    '/ai/widget/suggest-reply'  // same-origin reverse proxy
  );

  @ViewChild('suggestFrame') suggestFrame!: ElementRef<HTMLIFrameElement>;
  @ViewChild('replyInput') replyInput!: ElementRef<HTMLTextAreaElement>;

  readonly replyText = signal('');

  constructor() {
    // Context değiştiğinde widget'a ilet
    effect(() => {
      const ctx = {
        conversationId: this.conversationId(),
        contactId: this.contactId(),
        lastMessageText: this.lastMessage()
      };
      this.suggestFrame?.nativeElement.contentWindow
        ?.postMessage({ type: 'inse:suggest:context', payload: ctx }, '*');
    });

    // Widget'tan öneri seçildiğinde
    window.addEventListener('message', (e) => {
      if (e.data?.type === 'inse:suggest:selected') {
        this.replyText.set(e.data.payload.text);
        this.replyInput?.nativeElement.focus();
      }
    });
  }

  send() {
    // INMA send API...
  }
}
```

---

### 7. Şablonlarda Medya Kütüphanesi Paylaşımı

**Ne istiyoruz?**
INSE'de şablon oluştururken INMA'nın medya kütüphanesinden fotoğraf/PDF seçebilelim.

**Teknik:** INSE'ye read-only media list API: `GET /api/media-library?tenantId=...`. Şablon oluştururken media id referansı ile.

**Senaryo:**
> Dent Adavista yetkilisi INSE'de "Post-Event Day 14" drip mesajı oluşturuyor, mesaja "Treatment Guide PDF" eklemek istiyor. Dosyayı INMA'daki Media Library'ye 6 ay önce yüklemiş. INSE'de "Medyadan seç" butonu INMA'nın kütüphanesini listeliyor, PDF'i seçiyor. Şablon kaydediliyor. Çalışma anında INMA kendi PDF'ini gönderiyor.

**Neden önemli?** Aksi halde aynı dosya iki sistemde ayrı yüklenir, güncellenince iki tarafta da güncellenmek zorunda kalır — yönetim kabusu.

**Veri Akışı:**
```
[INSE Template Builder] → "Medyadan seç" butonu tıklandı
[INSE → INMA] GET /api/media-library?tenantId=dentadavista&type=pdf&page=1
              Header: X-CIB-SecretKey: <key>
[INMA] → response: {
           items: [
             { id: 882, name: "Treatment Guide EN.pdf", size: 2453104, url: "...", uploadedAt: "..." },
             { id: 891, name: "Before-After.jpg", ... }
           ],
           totalPages: 3
         }
[INSE UI] → grid'de göster, kullanıcı seçer
[INSE] → template_media table'a INSERT: { templateId, inmaMediaId: 882 }

Gönderim anında:
[INSE → INMA] POST /api/chatsv3/start-chat
              Body: { templateId: 1042, attachMediaIds: [882], variables: {...} }
[INMA] → kendi storage'ından media 882'yi okur, WhatsApp'a attach eder
```

**INMA Angular media library picker (opsiyonel):**
```typescript
// media-picker.component.ts (aynı component INMA + INSE iframe'inde reuse edilebilir)
import { Component, inject, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { rxResource } from '@angular/core/rxjs-interop';

interface MediaItem { id: number; name: string; url: string; type: string; size: number; }

@Component({
  selector: 'media-picker',
  standalone: true,
  template: `
    <div class="filters">
      <select (change)="typeFilter.set($any($event.target).value)">
        <option value="">Hepsi</option>
        <option value="image">Resim</option>
        <option value="pdf">PDF</option>
      </select>
    </div>
    @let items = media.value()?.items ?? [];
    <div class="grid">
      @for (m of items; track m.id) {
        <button class="media-card" (click)="select(m)">
          @if (m.type === 'image') { <img [src]="m.url" /> }
          @else { <span class="file-icon">📎</span> }
          <span>{{ m.name }}</span>
        </button>
      }
    </div>
  `
})
export class MediaPickerComponent {
  private http = inject(HttpClient);
  readonly typeFilter = signal('');
  readonly picked = output<MediaItem>();

  readonly media = rxResource({
    request: () => ({ type: this.typeFilter() }),
    loader: ({ request }) => this.http.get<{ items: MediaItem[] }>(
      '/api/media-library',
      { params: { type: request.type } }
    )
  });

  select(m: MediaItem) { this.picked.emit(m); }
}
```

---

### 8. INMA Header'a Bildirim Çanı

**Ne istiyoruz?**
INMA'nın sağ üst köşesinde bir bell icon + badge + dropdown olsun. Bunu INSE render etsin.

**Teknik:** INMA header'ın sağ üstünde INSE widget için DOM slot + iframe/Web Component embed. INSE kendi notification service'ini inşa edecek; bell widget'ı bu service'e bağlanır.

**Senaryo:**
> Koordinatör sabah INMA'ya giriyor. Sağ üstte "🔔 5" görüyor. Tıklıyor:
> - "Offer accepted — Sarah (Dublin) — 2 saat önce"
> - "Appointment booked — John (Cork) — 3 saat önce"
> - "X-ray uploaded — Mary — dün"
> - "SLA breach: Peter 45 dakikadır cevapsız"
> - "Flow completed: 23 kişi post-event drip'e alındı"
>
> Tıklayınca ilgili INSE sayfasına gidiyor. İşlemler bir yerde toplanmış, kaçırma riski yok.

**Neden önemli?** INMA'da şu an in-app bildirim hiç yok — kritik bir UX eksiği. INSE zaten notification altyapısını kurdu, sadece UI sürgüsü açılsın yeter.

**Veri Akışı:**
```
[Olay] → INSE'de bir şey olur (offer accepted, appointment booked, vb.)
[INSE] → NotificationService.publish(userId, { type, title, body, link })
       → notifications tablosuna INSERT
       → WebSocket push (o kullanıcının aktif bağlantısına)

[INMA UI] → sağ üst köşede iframe: <iframe src="/ai/widget/notifications">
[INSE widget iframe] → /api/notifications?unread=true GET
                     → WebSocket listen (/ws/notifications)
                     → badge sayısını güncelle

[Kullanıcı] → bell'e tıklar → dropdown aç
[INSE widget] → son 20 notification listele
[Kullanıcı] → "Offer accepted" tıklar → postMessage(parent, { nav: '/ai/offers/8821' })
[INMA shell] → history.pushState('/ai/offers/8821') → iframe navigate
```

**INMA header slot (Angular):**
```typescript
// app-header.component.ts
import { Component, inject } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { Router } from '@angular/router';

@Component({
  selector: 'app-header',
  standalone: true,
  template: `
    <header class="inma-header">
      <div class="logo">INMA</div>
      <nav class="primary">...</nav>
      <div class="spacer"></div>

      <!-- INSE Notification Bell widget slot -->
      <div class="notif-slot">
        <iframe [src]="notifWidgetUrl"
                title="Bildirimler"
                frameborder="0"
                style="width: 48px; height: 48px; border: 0"></iframe>
      </div>

      <user-menu></user-menu>
    </header>
  `
})
export class AppHeaderComponent {
  private sanitizer = inject(DomSanitizer);
  private router = inject(Router);

  readonly notifWidgetUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
    '/ai/widget/notification-bell'
  );

  constructor() {
    // Widget'tan gelen navigation isteklerini yakala
    window.addEventListener('message', (e) => {
      if (e.data?.type === 'inse:notif:navigate') {
        this.router.navigateByUrl(e.data.payload.path);
      }
      if (e.data?.type === 'inse:notif:resize') {
        // dropdown açılırken iframe'i büyüt
        const frame = document.querySelector<HTMLIFrameElement>('.notif-slot iframe');
        if (frame) {
          frame.style.width = e.data.payload.width + 'px';
          frame.style.height = e.data.payload.height + 'px';
        }
      }
    });
  }
}
```

---

## P2 — İleride (v2)

### 9. Tek WebSocket Bağlantısı

İki sistemin ayrı WebSocket'leri var. Tek bağlantı üzerinden birleşik event stream'i için ayrı teknik toplantı yapacağız.

### 10. Ortak Audit Trail

INMA'daki kritik aksiyonlar (user login, contact create, chat transfer, template edit) INSE'nin audit log'una event publish etsin. Event bus mekaniği kararlaştırılacak.

---

## Test Ortamı

- **INMA test:** `testapi.wapcrm.net` (mevcut)
- **INSE staging:** `staging.inse.invekto.com` (kurulacak)
- **Joint test tenant:** `joint-test-tenant` — her iki tarafta senkron kurulacak

## Beklenen Zaman

| Faz | Takvim | INMA Tahmini Efor |
|-----|--------|-------------------|
| P0 (1–5) | 4 hafta | ~2 hafta (1 backend dev) |
| P1 (6–8) | 3 hafta | ~1.5 hafta |
| P2 (9–10) | v2 | ayrı sprint |

## Sıradaki Adımlar

1. **Kickoff toplantısı** — bu dokümanı birlikte gözden geçirelim, sorular netleşsin
2. P0 5 maddesi için ticket açılsın
3. Her madde için API contract draft'ı (INSE tarafı yazar, INMA review eder)
4. Staging joint test planı

**Not:** INMA tarafında gereksiz refactor istemiyoruz. Mevcut koda dokunmadan yeni endpoint'ler/event'ler ekleme odaklı bir iş. Her maddenin kendi mini PR'ı olabilir, paralel ilerleyebilir.
