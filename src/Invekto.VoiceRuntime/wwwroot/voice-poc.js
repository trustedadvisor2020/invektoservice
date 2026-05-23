// Invekto Voice PoC — F0 browser controller
// Pipeline: getUserMedia (48k mono) → AudioWorklet (PCM16 LE 20ms frames)
//        → WS binary → /ws/voice/microphone
//        ← WS binary (PCM16 LE 48k 20ms bot voice) → AudioBufferSourceNode playback queue
//        ← WS text JSON control (transcript_user / transcript_bot / first_byte / barge_in / error / response_done)

const FRAME_SAMPLES = 960;          // 20ms @ 48kHz
const FRAME_BYTES = FRAME_SAMPLES * 2; // PCM16 LE
const SAMPLE_RATE = 48000;
const MAX_LOG_NODES = 200;          // DOM growth cap (rolling window)

// AD-20: dev=1 query bypass is allowed ONLY when running against localhost. Production must use ?token=<JWT>.
const isLocalhost = ['localhost', '127.0.0.1', '::1'].includes(location.hostname);

const els = {
  startBtn: document.getElementById('startBtn'),
  stopBtn: document.getElementById('stopBtn'),
  status: document.getElementById('status'),
  latencyLast: document.getElementById('latencyLast'),
  latencyP95: document.getElementById('latencyP95'),
  bargeLatency: document.getElementById('bargeLatency'),
  turnCount: document.getElementById('turnCount'),
  transcriptLog: document.getElementById('transcriptLog'),
  eventLog: document.getElementById('eventLog'),
};

const state = {
  ws: null,
  audioCtx: null,
  workletNode: null,
  micStream: null,
  playbackTime: 0,
  firstByteSamples: [],   // rolling window
  turns: 0,
  currentBotTranscript: '',
};

function setStatus(text, cls) {
  els.status.textContent = text;
  els.status.className = 'value ' + cls;
}

function trimLog(container) {
  // Rolling-window cap on appended children to bound long-session memory growth.
  while (container.childNodes.length > MAX_LOG_NODES) {
    container.removeChild(container.firstChild);
  }
}

function logEvent(text, cls = 'event-info') {
  const line = document.createElement('div');
  line.className = cls;
  line.textContent = `[${new Date().toLocaleTimeString()}] ${text}`;
  els.eventLog.appendChild(line);
  trimLog(els.eventLog);
  els.eventLog.scrollTop = els.eventLog.scrollHeight;
}

function logTranscript(text, who) {
  const line = document.createElement('div');
  line.className = who === 'user' ? 'turn-user' : 'turn-bot';
  line.textContent = text;
  els.transcriptLog.appendChild(line);
  trimLog(els.transcriptLog);
  els.transcriptLog.scrollTop = els.transcriptLog.scrollHeight;
}

function recordFirstByte(ms) {
  els.latencyLast.textContent = `${ms} ms`;
  state.firstByteSamples.push(ms);
  if (state.firstByteSamples.length > 100) state.firstByteSamples.shift();
  const sorted = [...state.firstByteSamples].sort((a, b) => a - b);
  const p95idx = Math.max(0, Math.ceil(0.95 * sorted.length) - 1);
  els.latencyP95.textContent = `${sorted[p95idx]} ms`;
}

async function start() {
  els.startBtn.disabled = true;
  setStatus('Mikrofon izni isteniyor...', 'status-listening');

  try {
    state.micStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        channelCount: 1,
        sampleRate: SAMPLE_RATE,
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    });
  } catch (err) {
    // INV-VR-009 is specifically permission denied / no device. Other errors get a distinct label.
    const isPermissionDenied = err.name === 'NotAllowedError' || err.name === 'NotFoundError';
    const code = isPermissionDenied ? 'INV-VR-009' : 'MIC-INIT';
    setStatus(isPermissionDenied ? 'Mikrofon reddedildi' : 'Mikrofon başlatılamadı', 'status-error');
    logEvent(`${code}: ${err.name}: ${err.message}`, 'event-err');
    els.startBtn.disabled = false;
    return;
  }

  state.audioCtx = new AudioContext({ sampleRate: SAMPLE_RATE });
  state.playbackTime = state.audioCtx.currentTime;

  // Validate browser actually honored 48kHz request (some hardware refuses; pipeline assumes 48kHz)
  if (state.audioCtx.sampleRate !== SAMPLE_RATE) {
    setStatus('Örnekleme hızı uyumsuzluğu', 'status-error');
    logEvent(`Donanım AudioContext ${state.audioCtx.sampleRate} Hz verdi, ${SAMPLE_RATE} Hz bekleniyordu — F0 PCM pipeline çalışmaz. Test desteklenmiyor.`, 'event-err');
    cleanup();
    return;
  }

  try {
    await state.audioCtx.audioWorklet.addModule('audio-worklet.js');
  } catch (err) {
    setStatus('AudioWorklet yüklenemedi', 'status-error');
    logEvent(`AudioWorklet hatası: ${err.name}: ${err.message}`, 'event-err');
    cleanup();
    return;
  }

  const source = state.audioCtx.createMediaStreamSource(state.micStream);
  state.workletNode = new AudioWorkletNode(state.audioCtx, 'pcm-frame-processor', {
    processorOptions: { frameSamples: FRAME_SAMPLES },
  });
  state.workletNode.port.onmessage = (e) => {
    if (state.ws && state.ws.readyState === WebSocket.OPEN) {
      state.ws.send(e.data); // Int16Array.buffer
    }
  };
  source.connect(state.workletNode);
  // NOTE: AudioWorkletNode is NOT connected to destination — we don't want mic loopback.

  // Open WS — production requires ?token=<JWT> in URL; dev=1 bypass only when running on localhost.
  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  const queryParts = ['locale=tr-TR'];
  if (isLocalhost) {
    queryParts.push('dev=1');
  } else {
    // Production hosting expects an INV-issued JWT injected by an enclosing Dashboard page.
    // F0 itself does not mint tokens; if window.INVEKTO_VOICE_JWT is missing, we abort.
    const tok = window.INVEKTO_VOICE_JWT;
    if (!tok) {
      setStatus('Token bulunamadı', 'status-error');
      logEvent('Production sayfada window.INVEKTO_VOICE_JWT tanımlı değil. Dashboard wrapper gereklidir.', 'event-err');
      cleanup();
      return;
    }
    queryParts.push(`token=${encodeURIComponent(tok)}`);
  }
  const url = `${proto}://${location.host}/ws/voice/microphone?${queryParts.join('&')}`;
  state.ws = new WebSocket(url);
  state.ws.binaryType = 'arraybuffer';

  state.ws.onopen = () => {
    setStatus('Bağlı, konuşabilirsin', 'status-listening');
    // Mask token in event log — production JWT MUST NOT appear in DOM-visible logs (CQ9 leak fix).
    const maskedUrl = url.replace(/token=[^&]+/, 'token=***MASKED***');
    logEvent(`WebSocket bağlandı: ${maskedUrl}`);
    els.stopBtn.disabled = false;
  };

  state.ws.onerror = (e) => {
    setStatus('Bağlantı hatası', 'status-error');
    logEvent('WebSocket error', 'event-err');
  };

  state.ws.onclose = (e) => {
    setStatus('Bağlantı kapandı', 'status-idle');
    logEvent(`WebSocket kapandı (code=${e.code} reason=${e.reason || '-'})`);
    cleanup();
  };

  state.ws.onmessage = (e) => {
    if (typeof e.data === 'string') {
      let parsed;
      try {
        parsed = JSON.parse(e.data);
      } catch (err) {
        logEvent(`Geçersiz kontrol mesajı (JSON parse hatası): ${err.message}`, 'event-err');
        return;
      }
      handleControl(parsed);
    } else {
      handleAudio(e.data);
    }
  };
}

function handleControl(msg) {
  switch (msg.type) {
    case 'ready':
      logEvent(`Session hazır: ${msg.session_id}`);
      break;
    case 'transcript_user':
      logTranscript(msg.text, 'user');
      state.turns += 1;
      els.turnCount.textContent = state.turns;
      break;
    case 'transcript_bot':
      state.currentBotTranscript += msg.delta;
      // Update last line incrementally
      const lastLine = els.transcriptLog.querySelector('.turn-bot:last-child');
      if (lastLine && !lastLine.dataset.final) {
        lastLine.textContent = state.currentBotTranscript;
      } else {
        const line = document.createElement('div');
        line.className = 'turn-bot';
        line.textContent = state.currentBotTranscript;
        els.transcriptLog.appendChild(line);
      }
      els.transcriptLog.scrollTop = els.transcriptLog.scrollHeight;
      break;
    case 'response_done':
      const lastBot = els.transcriptLog.querySelector('.turn-bot:last-child');
      if (lastBot) lastBot.dataset.final = '1';
      state.currentBotTranscript = '';
      setStatus('Bağlı, konuşabilirsin', 'status-listening');
      break;
    case 'first_byte':
      recordFirstByte(msg.elapsed_ms);
      setStatus('Bot konuşuyor', 'status-bot-speaking');
      logEvent(`İlk byte: ${msg.elapsed_ms}ms`);
      break;
    case 'barge_in':
      els.bargeLatency.textContent = `${msg.elapsed_ms} ms`;
      logEvent(`Barge-in tepkisi: ${msg.elapsed_ms}ms`, 'event-warn');
      setStatus('Bağlı, konuşabilirsin', 'status-listening');
      break;
    case 'error':
      setStatus('Hata', 'status-error');
      logEvent(`${msg.code || 'ERROR'}: ${msg.message || JSON.stringify(msg)}`, 'event-err');
      break;
    default:
      logEvent(`unknown control: ${JSON.stringify(msg)}`);
  }
}

function handleAudio(arrayBuffer) {
  if (!state.audioCtx) return;
  // PCM16 LE 48k → Float32 normalized [-1, 1]
  const view = new DataView(arrayBuffer);
  const samples = arrayBuffer.byteLength / 2;
  const floatArr = new Float32Array(samples);
  for (let i = 0; i < samples; i++) {
    floatArr[i] = view.getInt16(i * 2, true) / 32768;
  }
  const buf = state.audioCtx.createBuffer(1, samples, SAMPLE_RATE);
  buf.copyToChannel(floatArr, 0);
  const src = state.audioCtx.createBufferSource();
  src.buffer = buf;
  src.connect(state.audioCtx.destination);

  // Schedule continuous playback (queue frames back-to-back)
  const now = state.audioCtx.currentTime;
  if (state.playbackTime < now) state.playbackTime = now;
  src.start(state.playbackTime);
  state.playbackTime += buf.duration;
}

function stop() {
  if (state.ws && state.ws.readyState === WebSocket.OPEN) state.ws.close();
  cleanup();
}

function cleanup() {
  if (state.micStream) {
    state.micStream.getTracks().forEach((t) => t.stop());
    state.micStream = null;
  }
  if (state.audioCtx) {
    state.audioCtx.close().catch((err) => {
      logEvent(`AudioContext.close uyarısı: ${err.name}: ${err.message}`, 'event-warn');
    });
    state.audioCtx = null;
  }
  state.workletNode = null;
  els.startBtn.disabled = false;
  els.stopBtn.disabled = true;
  setStatus('Hazır', 'status-idle');
}

els.startBtn.addEventListener('click', start);
els.stopBtn.addEventListener('click', stop);
