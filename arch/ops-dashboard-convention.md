# Ops Dashboard Convention

> Yeni SuperAdmin/ops sayfasi eklemek icin izlenecek kalip.

## Genel Mimari

```
Backend ops endpoint ←→ api.ts method ←→ React page component ←→ Sidebar nav item + Route
```

Tum ops endpoint'leri `ValidateOpsAuth` ile korunur. JWT middleware bu path'leri korumaz — auth tamamen `ValidateOpsAuth` local function uzerinden yapilir.

---

## Yeni Ops Sayfasi Ekleme Checklist

### 1. Backend Endpoint (Program.cs)

```csharp
app.MapGet("/api/ops/{domain}", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var repo = ctx.RequestServices.GetService<XxxRepository>();
    if (repo == null)
        return Results.Json(
            new { error = ErrorCodes.XxxFailed, message = "PostgreSQL not configured" },
            statusCode: 503);

    try
    {
        var data = await repo.GetDataAsync();
        return Results.Ok(new { data });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Xxx failed ({ErrorCodes.XxxFailed}): {ex.Message}");
        return Results.Json(
            new { error = ErrorCodes.XxxFailed, message = "Kullanici mesaji." },
            statusCode: 500);
    }
});
```

**Kurallar:**
- `ValidateOpsAuth(ctx)` ILKE satir
- `GetService<T>()` null check → 503
- `NpgsqlException` catch → error code + user message + 500
- Error code `arch/errors.md`'de dokumante et

### 2. Repository (Backend/Data/)

```csharp
public sealed class XxxRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public XxxRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }
    // ...
}
```

**Kurallar:**
- `sealed class`, singleton olarak register et
- `NpgsqlCommand` + parametrik SQL (`@param`)
- Nullable kolonlar `reader.IsDBNull()` ile kontrol et
- Thread-safe (pooled factory, stateless)

### 3. DI Registration (Program.cs)

```csharp
builder.Services.AddSingleton<XxxRepository>();
```

Mevcut repo registration blogunun hemen altina ekle.

### 4. Frontend — api.ts

**Type:**
```typescript
export interface XxxEntry {
  id: number;
  name: string;
  // ...
}
```

**Method:**
```typescript
async getOpsXxx(): Promise<{ data: XxxEntry[] }> {
  return this.request<{ data: XxxEntry[] }>('/api/ops/xxx');
}
```

### 5. Frontend — React Page

`src/Invekto.Backend/Dashboard/src/pages/XxxPage.tsx`

**Pattern (MessagesPage/TenantsPage referans):**
```typescript
const [data, setData] = useState<XxxEntry[]>([]);
const [loading, setLoading] = useState(false);

const fetchData = useCallback(async () => {
  setLoading(true);
  try {
    const result = await api.getOpsXxx();
    setData(result.data);
  } catch (err) {
    console.error('Xxx fetch failed:', err);
  } finally {
    setLoading(false);
  }
}, []);

useEffect(() => { fetchData(); }, [fetchData]);
```

**UI kurallari:**
- Loading: `loading && data.length === 0` → "Yukleniyor..."
- Empty: `data.length === 0` → "Bulunamadi"
- Mevcut veri varken yeniden yuklemede loading spinner gosterme (eski data gorsun)
- Pagination: `totalPages > 1` ise goster
- Auto-refresh: gerekliyse `setInterval(fetchData, 30000)` (cleanup gerekli)

### 6. Frontend — Layout.tsx (Sidebar)

```typescript
{ path: '/xxx', label: 'Xxx', icon: XxxIcon, opsOnly: true },
```

**Visibility kurallari:**

| Flag | Gorulme Kosulu |
|------|----------------|
| `opsOnly: true` | `!session` (Basic Auth ops) VEYA `session.tenantId === 0` (SuperAdmin) |
| `feature: 'X'` | ops mode: her zaman; tenant mode: `api.hasFeature('X')` |
| Ikisi de yok | Her zaman gorunur |

### 7. Frontend — App.tsx (Route)

```tsx
import { XxxPage } from './pages/XxxPage';
// ...
<Route path="/xxx" element={<XxxPage />} />
```

### 8. Error Code

`ErrorCodes.cs` + `arch/errors.md`'ye ekle.

### 9. Endpoint Discovery

`Backend/Program.cs` icindeki `/api/ops/endpoints` handler'ina ekle:
```csharp
new() { Method = "GET", Path = "/api/ops/xxx", Description = "...", Auth = "Basic", Category = "Ops" }
```

---

## Mevcut Ops Sayfalari

| Path | Sayfa | Endpoint | Amac |
|------|-------|----------|------|
| `/` | DashboardPage | coklu `/api/ops/*` | Servis sagligi, genel bakis |
| `/tenants` | TenantsPage | `GET /api/ops/tenants` | Firma listesi + impersonate |
| `/messages` | MessagesPage | `GET /api/ops/messages` | SuperAdmin mesaj izleme |
| `/logs` | LogsPage | `GET /api/ops/logs/*` | Servis log goruntuleme |

---

## IHostedService / Background Service Pattern

CronSchedulerService kalibinden:

```csharp
public sealed class XxxService : IHostedService, IDisposable
{
    private Timer? _timer;
    private int _isRunning; // Interlocked overlap guard

    public Task StartAsync(CancellationToken ct)
    {
        _timer = new Timer(OnTick, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        return Task.CompletedTask;
    }

    private void OnTick(object? state)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0) return; // overlap guard
        _ = Task.Run(ExecuteAsync).ContinueWith(t =>
        {
            if (t.IsFaulted) _logger.SystemError($"Unhandled: {t.Exception?.InnerException?.Message}");
            Interlocked.Exchange(ref _isRunning, 0);
        });
    }
}
```

**Kurallar:**
- `Interlocked.CompareExchange` ile overlap guard
- `Task.Run` + `.ContinueWith(OnlyOnFaulted)` — timer callback'te exception yutma
- `NpgsqlException` tick seviyesinde catch — timer durmasin
- Per-item exception → log + continue (diger item'lar etkilenmesin)
- Idle tick loglanmali (`SystemInfo` seviye)
- `StopAsync`'te `_timer?.Change(Timeout.Infinite, 0)` ile timer durdur
- `Dispose`'da `_timer?.Dispose()`
