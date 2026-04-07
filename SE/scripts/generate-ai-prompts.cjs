/**
 * Generate AI Flow Designer prompts for all scenario JSON files.
 * Maps SE scenario steps to real Invekto Flow Designer node types.
 *
 * Real node types: trigger_start, webhook_trigger, outbound_trigger, schedule_trigger,
 * message_text, message_menu, logic_condition, logic_switch, logic_working_hours,
 * ai_intent, ai_faq, ai_sentiment, action_handoff, action_assign_group,
 * action_api_call, action_delay, action_call_flow, action_ecommerce,
 * utility_set_variable, utility_note
 */
const fs = require('fs');
const path = require('path');

const DATA_DIR = path.join(__dirname, '..', 'src', 'data');

// Detect if content indicates a delay/timer (T+3 Gun, 15 dakika, etc.)
function parseDelay(content) {
  const dayMatch = content.match(/T\+(\d+)\s*Gun/i);
  if (dayMatch) return { seconds: parseInt(dayMatch[1]) * 86400, label: `${dayMatch[1]} gun bekle` };
  const minMatch = content.match(/(\d+)\s*dakika/i);
  if (minMatch) return { seconds: Math.min(parseInt(minMatch[1]) * 60, 300), label: `${minMatch[1]} dk bekle` };
  const hourMatch = content.match(/(\d+)\s*saat/i);
  if (hourMatch) return { seconds: Math.min(parseInt(hourMatch[1]) * 3600, 300), label: `${hourMatch[1]} saat bekle` };
  return null;
}

// Detect if AI step implies sentiment analysis, intent detection, or FAQ
function classifyAiStep(content) {
  const lower = content.toLowerCase();
  if (lower.includes('sentiment') || lower.includes('negatif') || lower.includes('pozitif') || lower.includes('analiz:')) {
    return 'ai_sentiment';
  }
  if (lower.includes('intent') || lower.includes('niyetini')) {
    return 'ai_intent';
  }
  if (lower.includes('faq') || lower.includes('bilgi bankasi')) {
    return 'ai_faq';
  }
  // Default: sentiment for analysis steps, intent for detection steps
  if (lower.includes('analiz') || lower.includes('karar')) return 'ai_sentiment';
  return 'ai_intent';
}

// Extract variable hints from bot message (e.g., [Ad], [Kod], {{var}})
function extractVariables(text) {
  const vars = [];
  const bracketMatches = text.match(/\[([^\]]+)\]/g);
  if (bracketMatches) {
    bracketMatches.forEach(m => {
      const name = m.replace(/[\[\]]/g, '').toLowerCase().replace(/\s+/g, '_');
      vars.push(name);
    });
  }
  return vars;
}

// Detect if step implies an API call
function isApiCallStep(content) {
  const lower = content.toLowerCase();
  return lower.includes('api') || lower.includes('kargo') || lower.includes('siparis') ||
         lower.includes('entegrasyon') || lower.includes('webhook') || lower.includes('trendyol') ||
         lower.includes('hepsiburada');
}

// Detect trigger type from first system step
function classifyTrigger(content) {
  const lower = content.toLowerCase();
  if (lower.includes('cron') || lower.includes('her ') || lower.includes('dakikada bir') || lower.includes('zamanli')) {
    return 'schedule_trigger';
  }
  if (lower.includes('webhook') || lower.includes('api') || lower.includes('callback')) {
    return 'webhook_trigger';
  }
  if (lower.includes('kampanya') || lower.includes('outbound') || lower.includes('toplu mesaj')) {
    return 'outbound_trigger';
  }
  return 'trigger_start';
}

function buildFlowPrompt(scenario, flow) {
  const lines = [];
  const nodeList = [];
  const edgeList = [];
  let nodeCounter = 0;

  // — Header —
  lines.push(`"${scenario.title}" senaryosu icin "${flow.title}" akisini olustur.`);
  lines.push('');
  lines.push(`Aciklama: ${flow.description}`);
  lines.push('');

  // — Build node chain from steps —
  if (flow.steps && flow.steps.length > 0) {
    lines.push(`## Node Zinciri`);
    lines.push('');

    let prevNodeId = null;
    let prevHandle = null;

    // If first step is user (not system), prepend trigger_start
    if (flow.steps[0] && flow.steps[0].role === 'user') {
      const triggerId = `node_${++nodeCounter}`;
      nodeList.push({ id: triggerId, type: 'trigger_start' });
      lines.push(`${nodeCounter}. **trigger_start** — Musteri mesaji ile akis baslar`);
      lines.push('');
      prevNodeId = triggerId;
    }

    flow.steps.forEach((step, i) => {
      const nodeId = `node_${++nodeCounter}`;
      const isLastStep = i === flow.steps.length - 1;

      switch (step.role) {
        case 'system': {
          if (i === 0) {
            // First system step = trigger
            const triggerType = classifyTrigger(step.content);
            nodeList.push({ id: nodeId, type: triggerType });

            if (triggerType === 'schedule_trigger') {
              lines.push(`${nodeCounter}. **${triggerType}** — "${step.content}"`);
              lines.push(`   cron ifadesi ile periyodik tetikleme`);
            } else if (triggerType === 'webhook_trigger') {
              lines.push(`${nodeCounter}. **${triggerType}** — Dis sistem olayi tetikler`);
              lines.push(`   Olay: ${step.content}`);
            } else {
              lines.push(`${nodeCounter}. **trigger_start** — Akis baslangici`);
              lines.push(`   Tetikleyici olay: ${step.content}`);
            }
          } else {
            // Mid-flow system step = delay or API call or variable set or note
            const delay = parseDelay(step.content);
            const lower = step.content.toLowerCase();
            const isLogNote = lower.includes('otomatik cozum') || lower.includes('sonuc') ||
                              lower.includes('kaydedildi') || lower.includes('tamamlandi') ||
                              lower.includes('gerekmedi') || lower.includes('islendi');

            if (delay) {
              nodeList.push({ id: nodeId, type: 'action_delay' });
              lines.push(`${nodeCounter}. **action_delay** — ${delay.label}`);
              lines.push(`   seconds: ${Math.min(delay.seconds, 300)}`);
              lines.push(`   Not: ${step.content}`);
            } else if (isLogNote || isLastStep) {
              // End-of-flow status log — non-executable note
              nodeList.push({ id: nodeId, type: 'utility_note' });
              lines.push(`${nodeCounter}. **utility_note** — Akis sonuc notu`);
              lines.push(`   text: "${step.content}"`);
            } else if (lower.includes('alert') || lower.includes('uyari') || lower.includes('eskal')) {
              nodeList.push({ id: nodeId, type: 'utility_set_variable' });
              lines.push(`${nodeCounter}. **utility_set_variable** — Alert durumu kaydet`);
              lines.push(`   variable_name: "alert_status"`);
              lines.push(`   value: "${step.content}"`);
            } else if (isApiCallStep(step.content)) {
              nodeList.push({ id: nodeId, type: 'action_api_call' });
              lines.push(`${nodeCounter}. **action_api_call** — Dis servis cagrisi`);
              lines.push(`   Islem: ${step.content}`);
              lines.push(`   Basarili → sonraki node | Hata → action_handoff`);
            } else {
              nodeList.push({ id: nodeId, type: 'utility_set_variable' });
              lines.push(`${nodeCounter}. **utility_set_variable** — Durum guncelle`);
              lines.push(`   Deger: ${step.content}`);
            }
          }
          break;
        }

        case 'ai': {
          const aiType = classifyAiStep(step.content);
          nodeList.push({ id: nodeId, type: aiType });

          if (aiType === 'ai_sentiment') {
            lines.push(`${nodeCounter}. **ai_sentiment** — Duygu analizi`);
            lines.push(`   Analiz: ${step.content}`);
            lines.push(`   Cikislar: positive → devam | negative → eskalasyon | neutral → devam`);
          } else if (aiType === 'ai_intent') {
            lines.push(`${nodeCounter}. **ai_intent** — Niyet tespiti`);
            lines.push(`   Analiz: ${step.content}`);
            lines.push(`   Cikislar: high_confidence → ilgili dal | low_confidence → action_handoff`);
          } else {
            lines.push(`${nodeCounter}. **ai_faq** — Bilgi bankasi arama`);
            lines.push(`   Sorgu: ${step.content}`);
            lines.push(`   Cikislar: matched → cevap gonder | no_match → action_handoff`);
          }
          prevHandle = null; // AI nodes have named handles, will be set by edge
          break;
        }

        case 'bot': {
          const vars = extractVariables(step.content);
          const hasMenu = step.content.includes('1️⃣') || step.content.includes('1)') ||
                          step.content.includes('Hangisi') || step.content.includes('secin');

          if (hasMenu) {
            nodeList.push({ id: nodeId, type: 'message_menu' });
            lines.push(`${nodeCounter}. **message_menu** — Secenekli mesaj`);
            lines.push(`   Mesaj: "${step.content}"`);
            lines.push(`   Her secenek icin ayri cikis handle'i olustur`);
          } else {
            nodeList.push({ id: nodeId, type: 'message_text' });
            lines.push(`${nodeCounter}. **message_text** — Mesaj gonder`);
            lines.push(`   text: "${step.content}"`);
          }

          if (vars.length > 0) {
            lines.push(`   Degiskenler: ${vars.map(v => `{{${v}}}`).join(', ')}`);
          }
          break;
        }

        case 'user': {
          // User reply = previous message_text should wait, or we add ai_intent
          const isPhoto = step.content.toLowerCase().includes('foto') || step.content.toLowerCase().includes('gorsel');
          const isSelection = /^\d[\s\.]?\s*$/.test(step.content.trim()) ||
                               /^\d+\s+numara/i.test(step.content.trim());

          if (isPhoto) {
            // Photo upload — treat as wait + variable set
            nodeList.push({ id: nodeId, type: 'utility_set_variable' });
            lines.push(`${nodeCounter}. **utility_set_variable** — Musteri medya yuklemesini kaydet`);
            lines.push(`   variable_name: "customer_media"`);
            lines.push(`   Not: Onceki message_text'in yanit beklemesi gerekir`);
          } else if (isSelection) {
            // Selection — previous node was message_menu, skip
            nodeCounter--;
            lines.push(`   (Onceki message_menu'nun secim handle'i bu yaniti yonlendirir)`);
          } else {
            // General reply — mark previous as wait-for-input
            nodeList.push({ id: nodeId, type: 'ai_intent' });
            lines.push(`${nodeCounter}. **ai_intent** — Musteri yanitini analiz et`);
            lines.push(`   Beklenen yanit: "${step.content}"`);
            lines.push(`   high_confidence → devam | low_confidence → action_handoff`);
          }
          break;
        }

        case 'agent': {
          nodeList.push({ id: nodeId, type: 'action_handoff' });
          lines.push(`${nodeCounter}. **action_handoff** — Canli agent'a devret`);
          lines.push(`   summary_template: "${step.content}"`);
          lines.push(`   (Terminal node — akis burada biter)`);
          break;
        }

        default: {
          nodeList.push({ id: nodeId, type: 'utility_note' });
          lines.push(`${nodeCounter}. **utility_note** — ${step.content}`);
        }
      }

      // Edge from previous node
      if (prevNodeId) {
        edgeList.push({ source: prevNodeId, target: nodeId, handle: prevHandle });
      }
      prevNodeId = nodeId;
      prevHandle = null;

      lines.push('');
    });
  }

  // — Fallback/error handling —
  lines.push(`## Hata Yonetimi`);
  lines.push(`- Timeout (yanit gelmezse): 24 saat sonra hatirlatma message_text gonder`);
  lines.push(`- Bilinmeyen yanit: ai_intent low_confidence → action_handoff`);
  lines.push(`- API hatasi: action_api_call error handle → action_handoff`);
  lines.push('');

  // — Settings —
  lines.push(`## Flow Ayarlari`);
  lines.push(`- session_timeout_minutes: 1440`);
  lines.push(`- handoff_confidence_threshold: 0.5`);
  lines.push(`- max_loop_count: 3`);
  lines.push('');

  return lines.join('\n');
}

function processFile(filePath) {
  const raw = fs.readFileSync(filePath, 'utf-8');
  let data;
  try {
    data = JSON.parse(raw);
  } catch (e) {
    console.error(`JSON parse error: ${filePath} — ${e.message}`);
    return false;
  }

  if (!data.flows || !Array.isArray(data.flows) || data.flows.length === 0) {
    return false;
  }

  let changed = false;
  data.flows.forEach((flow) => {
    const prompt = buildFlowPrompt(data, flow);
    if (flow.aiPrompt !== prompt) {
      flow.aiPrompt = prompt;
      changed = true;
    }
  });

  if (changed) {
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2) + '\n', 'utf-8');
  }

  return changed;
}

// Main
const files = fs.readdirSync(DATA_DIR).filter(f => f.endsWith('.json') && !f.includes('field-scenarios') && !f.includes('test-results'));
let processed = 0;
let updated = 0;

files.forEach(file => {
  const filePath = path.join(DATA_DIR, file);
  const wasUpdated = processFile(filePath);
  processed++;
  if (wasUpdated) updated++;
});

console.log(`Done: ${processed} files processed, ${updated} updated.`);
