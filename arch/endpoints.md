# Endpoint Registration Rule

<!-- FORMAT: agent-first (v6.0). YAML block is the source of truth for rules + naming. -->

> **KURAL:** Her yeni endpoint **3 yerde** kayıt edilmeli.

## Registration Points

```yaml
registration:
  - target: Program.cs Discovery
    path: "/api/ops/endpoints"
    action: Add EndpointInfo to MapGet handler list
    required: true

  - target: Postman Collection
    path: "postman/InvektoServis.postman_collection.json"
    action: Add request to appropriate folder
    required: true
    folders:
      - name: "Backend - Public API"
        for: Business endpoints
      - name: "Backend - Ops Dashboard"
        for: Ops endpoints
      - name: "ChatAnalysis - Direct"
        for: ChatAnalysis endpoints

  - target: Backend Aggregation
    path: "Backend/Program.cs"
    action: Add discovery call for new microservice
    required: only_when_new_service
```

## Category & Auth Values

```yaml
categories:
  API:     { description: Dışarıdan çağrılan business endpoint'ler }
  Health:  { description: Health/ready probe'ları }
  Ops:     { description: Dashboard/monitoring endpoint'leri }
  Legacy:  { description: Eski endpoint'ler (deprecation yolunda) }

auth_types:
  none:    { description: Auth gerektirmiyor }
  Basic:   { description: Ops Basic Auth }
  Bearer:  { description: "JWT/Token auth (gelecekte)" }
```

## Naming Convention

```yaml
naming:
  - pattern: "/api/v{version}/{resource}"
    type: Business API
  - pattern: "/api/ops/{category}/{action}"
    type: Ops/Dashboard
  - pattern: "/health"
    type: Health check (standard)
  - pattern: "/ready"
    type: Readiness probe (standard)
  - pattern: "/api/ops/endpoints"
    type: Discovery (standard — her serviste)
```

## EndpointInfo Example

```csharp
new() {
    Method = "POST",
    Path = "/api/v1/new-endpoint",
    Description = "Açıklama",
    Auth = "none",
    Category = "API"
}
```

## Checklist (Her Yeni Endpoint İçin)

```yaml
checklist:
  - step: Program.cs'de endpoint tanımlandı
  - step: Discovery listesine (/api/ops/endpoints) eklendi
  - step: Postman collection güncellendi
  - step: Auth doğru belirtildi (none/Basic/Bearer)
  - step: Category doğru belirtildi (API/Health/Ops/Legacy)
```
