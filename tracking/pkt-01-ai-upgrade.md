# PKT-1: AI Upgrade

> **Durum:** DONE | **Tarih:** 2026-02-15 | **Codex:** iter 3, FORCE PASS
> **Commit:** 97d9888

## GR Listesi

- **GR-2.2 Agent Assist v2:** Reply generation Knowledge'dan beslenir, kaynak referansi, tone presets, multi-turn
- **GR-2.3 Multi-lang Support:** Language detection, multi-lang response, Knowledge multi-lang, Outbound dil secimi

## GR Detail

### GR-2.2: Agent Assist v2 (RAG Beslemeli)
- 2.2.1 Reply generation Knowledge'dan beslenecek
- 2.2.2 "Neden bu cevap" açıklaması + kaynak referansı
- 2.2.3 Tone presets (formal / kısa / samimi)
- 2.2.4 Multi-turn: AI takip sorusu sorabiliyor
- 2.2.5 Pipeline: message → intent → knowledge lookup → response → output
- 2.2.6 Kaynak yoksa "insana devret" kuralı

### GR-2.3: Multi-Language AI (TR/EN)
- 2.3.1 ChatAnalysis language detection
- 2.3.2 AgentAI response tespit edilen dilde
- 2.3.3 Knowledge base multi-language (aynı FAQ, farklı diller)
- 2.3.4 Outbound template dil seçimi
- 2.3.5 İngilizce template seti
- 2.3.6 Yabancı hasta flag
- 2.3.7 Diller: TR, EN (AR Phase 5)

## Deliverables

- AgentAI + ChatAnalysis + Knowledge genisletme
- 2 GR, 13 alt madde

## Plan

`arch/plans/20260215-pkt1-ai-upgrade.json`
