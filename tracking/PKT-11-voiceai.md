# PKT-11: Voice Message AI

> VoiceAI mikroservisi — ses mesajı transcription + intent detection

## Faz Durumu

| Faz | Kapsam | Durum | Tarih | Codex |
|-----|--------|-------|-------|-------|
| Faz 1 | VoiceAI MVP: STT + intent (Whisper API → ChatAnalysis) | IN_PROGRESS | 2026-03-27 | - |
| Faz 2 | Voice node, retry queue, per-tenant keys, TTS | PLANNED | - | - |

## Faz 1 — VoiceAI MVP

### Scope
- Yeni mikroservis: `Invekto.VoiceAI` (port 7114)
- Whisper API (platform-level key) ile STT
- Transcript → ChatAnalysis `/api/v1/analyze` (intent detection)
- Transcribe sonrası audio silme (no retention)
- Option A: text inject (flow node yok)
- Backend proxy routes + health aggregation

### Acceptance Criteria

- [ ] AC1: VoiceAI port 7114 + /health healthy
- [ ] AC2: POST /api/v1/voice/transcribe → Whisper → transcript
- [ ] AC3: Transcript → ChatAnalysis → intent result
- [ ] AC4: Temp audio deleted after transcription
- [ ] AC5: Backend proxy /api/v1/voice/* → 7114
- [ ] AC6: /api/ops/health + /api/ops/endpoints aggregation
- [ ] AC7: INV-VA-001..006 error codes
- [ ] AC8: voice_transcriptions table (log)
- [ ] AC9: Solution builds zero errors/warnings

### Dosyalar

| # | Dosya | Tip |
|---|-------|-----|
| 1 | `src/Invekto.VoiceAI/Invekto.VoiceAI.csproj` | NEW |
| 2 | `src/Invekto.VoiceAI/Program.cs` | NEW |
| 3 | `src/Invekto.VoiceAI/Services/WhisperApiService.cs` | NEW |
| 4 | `src/Invekto.VoiceAI/Services/VoiceTranscriptionService.cs` | NEW |
| 5 | `src/Invekto.VoiceAI/appsettings.json` | NEW |
| 6 | `src/Invekto.Shared/Constants/ServiceConstants.cs` | EDIT |
| 7 | `src/Invekto.Shared/Constants/ErrorCodes.cs` | EDIT |
| 8 | `src/Invekto.Shared/DTOs/VoiceTranscriptionDtos.cs` | NEW |
| 9 | `src/Invekto.Backend/Services/VoiceAIClient.cs` | NEW |
| 10 | `src/Invekto.Backend/Program.cs` | EDIT |
| 11 | `InvektoServis.sln` | EDIT |
| 12 | `arch/db/voiceai.sql` | NEW |

### Plan JSON
`arch/plans/20260327-pkt11-f1-voiceai-mvp.json`

### Q Decisions
1. Whisper API cloud, platform-level key
2. Any audio source (not just WhatsApp)
3. Intent via ChatAnalysis pipeline
4. STT+intent only (no TTS)
5. Option A: transcribe → text inject, no new flow node
6. Transcribe and delete audio
