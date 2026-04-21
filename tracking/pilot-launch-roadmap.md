# Pilot Launch Roadmap — Sirali Execution Queue

> **Slug:** pilot-launch-roadmap | **Mode:** ACTIVE (2026-04-21)
> **Hedef:** 17 paket arka arkaya DONE → Dent Adavista pilot full-stack smoke (paket 18)
> **Q tercihi (2026-04-21):** "Hepsini bitirelim, smoke en son"

Bu dosya **pilot launch boyunca execution queue'nun tek kaynagidir**. Session bootstrap zorunlu okuma. `session-memory.md` son durum + detay; roadmap **sira + status** otoritesi.

---

## DEVAM PROTOKOLU (KRITIK)

### Session Basi (her /clear sonrasi)
1. `arch/session-memory.md` oku (son paket detayi)
2. `tracking/pilot-launch-roadmap.md` oku (BU DOSYA — sira + status)
3. Asagidaki **Master Queue** tablosunda `Status = PENDING` olan **ILK** paketi bul
4. Q'ya su formatta sun:
   ```
   Siradaki: P{N} {slug} ({faz})
   Scope: {ozet 1 satir}
   Pre-req: {dep + interview soru sayisi}
   Baslayalim mi?
   ```
5. Q onay → `/auto` workflow (interview → plan → dev → build → /rev → commit)
6. Paket DONE → **Paket Tamamlama Checklist** uygula (asagida)
7. `/clear` + next-session prompt uret

### Q Override Komutlari
| Komut | Etki |
|-------|------|
| `SKIP P{N}` | O paketin status'unu SKIPPED, Notes'a reason |
| `PAUSE` | Roadmap donar, Q manuel task'a gecer |
| `RESUME` | Sonraki PENDING paket kaldiginda devam |
| `REORDER P{A} before P{B}` | Tabloda sira yer degistir |
| `ADD P{N} {slug}` | Yeni paket ekle (row + per-paket dosya) |

### Paket Tamamlama Checklist
- [ ] Kod + build PASS (`dotnet build InvektoServis.sln 0 errors`)
- [ ] `/rev` Codex verdict = PASS (CODEX UTANSIN: iteration=0 hedef)
- [ ] Commit + push master (HEREDOC message + Co-Authored-By)
- [ ] Prod deploy (gerekirse) + /health HEALTHY (MCP invekto-ops server-deploy)
- [ ] `tracking/{slug}.md` guncelle: Status=DONE, deploy date, Codex iter
- [ ] **`tracking/pilot-launch-roadmap.md` Master Queue satirinda Status=DONE**
- [ ] `arch/session-memory.md`: Last Update + Execution Queue + Recently Completed
- [ ] `/clear` oner + next-session prompt

---

## MASTER QUEUE

### FAZ 1 — Retro-Fix & Teknik Borc (context warm, dusuk risk)

| # | Paket | Slug | Status | Dep | Deploy | Exit Criteria |
|---|-------|------|--------|-----|--------|---------------|
| 1 | FEAT-DMP Cache Poison Fix | `20260422-feat-dmp-cache-poison-fix` | **DONE** (Codex iter 0 PASS, 7/7 test) | - | Backend (pending deploy) | Codex PASS + redeploy + unit test cancellation isolation ✅ |
| 2 | Lessons +6 Kayit (TFM MVP + AUTH HOTFIX) | `20260422-lessons-tfm-auth-hotfix` | PENDING | - | Doc-only | `arch/lessons-learned.md` 6 yeni entry |

### FAZ 2 — FEAT-TFM Suite Tamamlama (MVP resolver uzerine eklentiler)

| # | Paket | Slug | Status | Dep | Deploy | Exit Criteria |
|---|-------|------|--------|-----|--------|---------------|
| 3 | FEAT-TFM-UI Dashboard Editor | `20260423-feat-tfm-ui-editor` | PENDING | - | Backend SPA | `/settings/field-mapping` 10-slot editor + E2E smoke |
| 4 | FEAT-TFM-SYNC INMA Mirror | `20260423-feat-tfm-inma-sync` | PENDING | P3 | Integrations | Hangfire recurring + audit log + INMA /api/dynamicfields/create |
| 5 | FEAT-TFM-FLOW Picker | `20260424-feat-tfm-flow-picker` | PENDING | P3 | Dashboard SPA | FlowBuilder + TemplateCreate semantic dropdown |
| 6 | FEAT-TFM-CACHE Redis Invalidate | `20260424-feat-tfm-redis-invalidate` | PENDING (Q onay) | - | Backend+Outbound+Automation | Redis dep + pub/sub + all-instance invalidate |

### FAZ 3 — Pilot Omurgasi Feature'lar

| # | Paket | Slug | Status | Dep | Deploy | Exit Criteria |
|---|-------|------|--------|-----|--------|---------------|
| 7 | FEAT-EFS Drip Sequence | `20260425-feat-efs-drip-sequence` | PENDING | - | Marketing+Automation | Hangfire scheduled + A/B + 4 trigger + per-stage metric |
| 8 | FEAT-MCC Multi-City Campaign | `20260425-feat-mcc-multi-city` | PENDING | - | Backend+Automation | JSONB config + substitution + outbound window guard |
| 9 | FEAT-VCP Chunk C GoogleMeet OAuth | `20260426-feat-vcp-chunk-c-google-oauth` | PENDING | - | Integrations+Dashboard | Prod OAuth + `/settings/video-provider` UI |

### FAZ 4 — Cleanup

| # | Paket | Slug | Status | Dep | Deploy | Exit Criteria |
|---|-------|------|--------|-----|--------|---------------|
| 10 | INMA Debug Log Temizligi | `20260427-inma-debug-log-cleanup` | PENDING | - | Dashboard SPA | `[inma-debug]` prefix'li loglar silindi |
| 11 | Prod Yedek Silme | `20260427-prod-bypass-bak-remove` | PENDING (Q onay) | - | Prod file | `appsettings.Production.json.bak-20260416-inma-bypass` silinmis |

### FAZ 5 — Marketing (Dent ile alakasiz, paralel kapatilabilir)

| # | Paket | Slug | Status | Dep | Deploy | Exit Criteria |
|---|-------|------|--------|-----|--------|---------------|
| 12 | PKT-13 Faz 1 Lead Scoring | `20260428-pkt-13-faz-1-lead-scoring` | PENDING | - | Marketing | Scoring engine + dashboard + model call |

### FAZ 6 — FEAT-ICB (INMA Chat Bridge, 5 faz)

**Interview gate:** 6 acik soru Q cevabi gerekli (media storage, webhook owner, Ecom+Zoho agregasyon, team chat kapsam, prefs sync, sticky note). Faz 1 basi oncesi AskUserQuestion.

| # | Paket | Slug | Status | Dep | Deploy | Exit Criteria |
|---|-------|------|--------|-----|--------|---------------|
| 13 | FEAT-ICB Faz 1 Infra | `20260429-feat-icb-faz1-infra` | PENDING | Q interview | Backend | SignalR hub + auth + session + rate limit + 5 D-grup modul |
| 14 | FEAT-ICB Faz 2 Foundational API | `20260430-feat-icb-faz2-api` | PENDING | P13 | Backend | A-grup 11 contract reuse + 6 B-grup yeni endpoint |
| 15 | FEAT-ICB Faz 3 Flow/Template/Conv | `20260501-feat-icb-faz3-flow` | PENDING | P14 | Backend+Automation | WTP/DMP extension + 5 B-grup modul |
| 16 | FEAT-ICB Faz 4 Media/Team/Prefs | `20260502-feat-icb-faz4-media` | PENDING | P15 | Backend | J2 schema ext + 4 B-grup + media storage |
| 17 | FEAT-ICB Faz 5 Reports/Events | `20260503-feat-icb-faz5-events` | PENDING | P16 | Backend+ChatAnalysis | 7 C-grup SignalR event + customer 360 |

### FAZ 7 — Pilot Smoke (TUM PAKETLER BITTIKTEN SONRA)

| # | Paket | Slug | Status | Dep | Deploy | Exit Criteria |
|---|-------|------|--------|-----|--------|---------------|
| 18 | Dent Pilot Full-Stack Smoke | `20260504-dent-pilot-smoke` | PENDING | **1-17 DONE** | Production tenant 18173130 | Translation warmup + 48 template seed + flow wiring + E2E (chunk mesaj + translate + DMP + VCP meeting + EFS drip + MCC city + WTP rotation) + prod log grep |

---

## BLOCKED (External Dep, Roadmap'te Degil)

| # | Task | Bloker | Unblock Sinyali |
|---|------|--------|-----------------|
| B1 | UP0.3 Tenant Lifecycle Handler | INMA `tenant.created` event | INMA team confirm |
| B2 | UP0.5 IInmaSendClient | INMA J1/J4 API | INMA endpoint spec + test |
| B3 | INMA JWT Signature Bypass Rollback | RS256 key/algo | INMA pubkey endpoint |
| B4 | Zoho P4.2 Adavista Paid-Plan Retest | Adavista plan upgrade | Q'dan plan degisim onayi |
| B5 | FEAT-J2 Http Mode Flip | Gercek X-CIB-SecretKey | Q'nun provision karari |

---

## Per-Paket Detay

Her PENDING paket kendi `tracking/{slug}.md` dosyasinda:
- Scope (degisecek dosyalar)
- Pre-implementation interview (plan session girdisi)
- AC (Acceptance Criteria)
- Arch touchpoints
- Risk watchpoints
- Codex verdict history (iter arc)
- Deploy durumu

Dosyalar paket baslama aninda olusturulur (lazy creation). Var olmayan paketi baslarken `tracking/_TEMPLATE.md` referans al.

---

## Progress Tracker

| Metric | Value |
|--------|-------|
| Total Packages | 18 |
| DONE | 1 |
| IN_PROGRESS | 0 |
| PENDING | 17 |
| SKIPPED | 0 |
| Progress | 5.5% (1/18) — P1 DONE, Backend deploy pending |

_Her paket DONE olunca bu tablo guncellenir._

---

## References

- [Session Memory](../arch/session-memory.md) — son durum + detay log
- [Tracking Master](README.md) — tum paket tablosu
- [Lessons Learned](../arch/lessons-learned.md) — tekrarlanan hata onleme
- [INVEKTO_BASE.prompt.md](../.claude/agents/INVEKTO_BASE.prompt.md) — global rules (CODEX UTANSIN + CQ + AQ)
- [Codex Context](../arch/codex-context.md) — review guidance
- Eski planlama dosyasi: [feat-pilot-5-generic-roadmap.md](feat-pilot-5-generic-roadmap.md) — historical (FEAT-5 generic planning, artik kapsama dahil)

---

**Hazirlayan:** Claude 2026-04-21 22:55 UTC | **Ilk baslangic:** P1 `20260422-feat-dmp-cache-poison-fix`
