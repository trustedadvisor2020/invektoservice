---
description: "InvektoServices deploy - build + publish + restart services"
---

# /deploy [service]

> Build, publish, and restart InvektoServices on production server.

## Usage

```
/deploy              # Full deploy (all services)
/deploy backend      # Only Backend
/deploy webchat      # Only WebChat
/deploy automation   # Only Automation
/deploy outbound     # Only Outbound
```

## Architecture

```
Dev PC (build)  →  Production Server
C:\CRMs\InvektoServices    E:\InvektoServices\
```

- **Production:** `E:\InvektoServices\{ServiceName}\` — NSSM Windows Services
- **Service naming:** `Invekto-{ServiceName}` (e.g., `Invekto-Backend`, `Invekto-WebChat`)

## Deploy Steps

1. `dotnet publish` for target service(s)
2. Upload to production via `server-upload` MCP tool
3. Restart NSSM service via `server-exec` MCP tool
4. Verify health via `server-health` MCP tool

## Service List

| Service | Port | NSSM Name |
|---------|------|-----------|
| Backend | 5100 | Invekto-Backend |
| WebChat | 5101 | Invekto-WebChat |
| Automation | 5102 | Invekto-Automation |
| AgentAI | 5103 | Invekto-AgentAI |
| Outbound | 5104 | Invekto-Outbound |
| Knowledge | 5105 | Invekto-Knowledge |
| WhatsAppAnalytics | 5106 | Invekto-WhatsAppAnalytics |
