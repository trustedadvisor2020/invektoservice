# Invekto.VoiceRuntime — Models & Runtime Setup

## 1. Silero VAD ONNX Model (REQUIRED)

`silero_vad.onnx` (~2MB, MIT License) — Voice Activity Detection model.

### Download

PowerShell (Windows):

```powershell
Invoke-WebRequest -Uri "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx" -OutFile "silero_vad.onnx"
```

Bash/curl:

```bash
curl -L -o silero_vad.onnx https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx
```

### Verify

```powershell
Get-FileHash silero_vad.onnx -Algorithm SHA256
# Expected SHA256 (snakers4/silero-vad master, captured 2026-05-24, FROZEN for F0): 1A153A22F4509E292A94E67D6F9B85E8DEB25B4988682B7E174C65279D8788E3
# File size: 2,327,524 bytes (2.3 MB)
```

If hash mismatch → re-download. Model file MUST be present at runtime; missing file raises `INV-VR-008` boot-time fail-fast.

### Why not committed to Git?

- Binary file, would bloat repo history
- Git LFS not configured project-wide
- Reproducible download from upstream is sufficient
- Build pipeline runs this script in CI (F2 deploy)

---

## 2. OpenAI API Key (REQUIRED)

The Realtime API key is read from `OPENAI_API_KEY` environment variable at runtime (NOT from appsettings — security).

### Local Dev (Q laptop)

PowerShell — **session-scoped** (terminal kapanınca silinir):

```powershell
$env:OPENAI_API_KEY = "REPLACE_WITH_OPENAI_KEY"
dotnet run --project src/Invekto.VoiceRuntime
```

PowerShell — **persistent user-scope** (önerilen, terminal kapanınca kalır):

```powershell
[Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "REPLACE_WITH_OPENAI_KEY", "User")
# Yeni terminal aç, kontrol et:
echo $env:OPENAI_API_KEY
```

### Production (NSSM service, F2 deploy)

**Canonical policy (F0 and F2):** `OPENAI_API_KEY` is read from environment variable ONLY. `appsettings*.json` never holds the secret in plaintext — `OpenAI:ApiKey` field stays empty and is overridden by env var at runtime via `RealtimeSessionFactory.ApiKey` (`Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? config["OpenAI:ApiKey"]`).

NSSM service in F2 sets env variable on the Windows service registry (per-service scope, not user/machine):

```powershell
nssm set InvektoVoiceRuntime AppEnvironment "OPENAI_API_KEY=YOUR_OPENAI_KEY_HERE"
nssm restart InvektoVoiceRuntime
```

F4 backlog: rotate to Windows DPAPI or Azure Key Vault (deferred — `appsettings.Production.json` direct key write is FORBIDDEN at every stage).

### Key Source

Knowledge servisi prod'da kullanılan aynı OpenAI organization key kullanılır (Realtime tier aktif). Q laptop'a kopyalanır, kod env variable'dan okur.

### Permissions

Önerilen restricted key permissions:
- ✓ Model capabilities: **Realtime**
- ✓ Audio: **Read**
- ✗ Diğerleri kapalı

---

## 3. Runtime Verification

```powershell
cd src/Invekto.VoiceRuntime
ls Models/silero_vad.onnx  # ~2MB dosya görünmeli
echo $env:OPENAI_API_KEY    # sk- ile başlayan key görünmeli
dotnet run
```

Beklenen log:

```
[INFO] VoiceRuntime starting on port 7115
[INFO] Silero VAD model loaded (Models/silero_vad.onnx)
[INFO] OpenAI Realtime endpoint configured (wss://api.openai.com/v1/realtime)
[INFO] WebSocket endpoint ready: /ws/voice/microphone
```

Hata durumları:
- `INV-VR-008` → ONNX model file eksik (Adım 1)
- `INV-VR-002` → OpenAI API key eksik veya geçersiz (Adım 2)
- `INV-VR-004` → JWT secret eksik (`appsettings.Development.json:Jwt:SecretKey`)

---

## 4. Browser Test

`http://localhost:7115/voice-poc.html` aç → "Mikrofonu Başlat" → konuş.

JWT-gated endpoint (`/ws/voice/microphone`) için browser sayfası dev modda otomatik test token üretir. F2'de gerçek tenant JWT gerekecek.
