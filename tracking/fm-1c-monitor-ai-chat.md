# FM-1c: Monitor AI Chat Paneli

> **Durum:** PLANNED | **Tarih:** - | **Codex:** -
> **Spec:** `arch/specs/flow-monitor.md` | **Risk:** MEDIUM
> **Bagimlilk:** FM-1a (versioning), FM-1b (monitor sayfasi)

## Kapsam

Monitor sayfasinin sag panelindeki AI asistan. 3 katmanli yetenek:

1. **Flow duzenleme:** Node ekle/sil/duzenle, mesaj degistir, flow optimize et
2. **Execution analizi:** "Bu execution neden hata aldi?", "En cok hata alan node?"
3. **Otomatik oneri:** AI sorun tespit edip "Bu node'u su sekilde degistireyim mi?" onerir

## Acceptance Criteria

| # | Kriter | Durum |
|---|--------|-------|
| AC-6 | AI chat paneli: flow duzenleme + execution analizi + otomatik oneri | - |

## Fark: AiChatPanel vs MonitorAiPanel

| Ozellik | AiChatPanel (Flow Builder) | MonitorAiPanel (Monitor) |
|---------|---------------------------|--------------------------|
| Context | Sadece flow config | Flow config + execution log + hata detaylari |
| Mod | Yeni flow olustur / degistir | Analiz + duzelt + oner |
| Trigger | Kullanici sorusu | Kullanici sorusu + otomatik sorun tespiti |
| Save | Flow builder canvas'a uygular | Direkt save (yeni version olusturur) |

## Deliverables

- [ ] MonitorAiPanel.tsx — Sag panel component
- [ ] AI prompt engineering: execution context + flow config
- [ ] Otomatik sorun tespiti: hata orani, timeout, loop tespiti
- [ ] AI onerisi -> flow save -> yeni version akisi
- [ ] Backend: AI chat endpoint (monitor context destekli)
