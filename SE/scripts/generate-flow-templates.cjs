/**
 * Converts 170 SE scenario JSONs → FlowConfigV2 templates for Dashboard.
 * Outputs: Dashboard/src/data/flow-templates.ts (static, auto-generated)
 */
const fs = require('fs');
const path = require('path');

const DATA_DIR = path.join(__dirname, '..', 'src', 'data');
const OUT_FILE = path.join(__dirname, '..', '..', 'src', 'Chatinbox.Backend', 'Dashboard', 'src', 'data', 'flow-templates.ts');

// ── Helpers (reused from generate-ai-prompts.cjs) ──

function parseDelay(content) {
  const d = content.match(/T\+(\d+)\s*Gun/i);
  if (d) return { seconds: Math.min(parseInt(d[1]) * 86400, 300), label: `${d[1]} gun bekle` };
  const m = content.match(/(\d+)\s*dakika/i);
  if (m) return { seconds: Math.min(parseInt(m[1]) * 60, 300), label: `${m[1]} dk bekle` };
  const h = content.match(/(\d+)\s*saat/i);
  if (h) return { seconds: Math.min(parseInt(h[1]) * 3600, 300), label: `${h[1]} saat bekle` };
  return null;
}

function classifyAiStep(content) {
  const l = content.toLowerCase();
  if (l.includes('sentiment') || l.includes('negatif') || l.includes('pozitif') || l.includes('analiz:')) return 'ai_sentiment';
  if (l.includes('intent') || l.includes('niyetini')) return 'ai_intent';
  if (l.includes('faq') || l.includes('bilgi bankasi')) return 'ai_faq';
  if (l.includes('analiz') || l.includes('karar')) return 'ai_sentiment';
  return 'ai_intent';
}

function classifyTrigger(content) {
  const l = content.toLowerCase();
  if (l.includes('cron') || l.includes('her ') || l.includes('dakikada bir') || l.includes('zamanli')) return 'schedule_trigger';
  if (l.includes('webhook') || l.includes('api') || l.includes('callback')) return 'webhook_trigger';
  if (l.includes('kampanya') || l.includes('outbound') || l.includes('toplu mesaj')) return 'outbound_trigger';
  return 'trigger_start';
}

function isApiCallStep(content) {
  const l = content.toLowerCase();
  return l.includes('api') || l.includes('kargo') || l.includes('siparis') ||
    l.includes('entegrasyon') || l.includes('webhook') || l.includes('trendyol') || l.includes('hepsiburada');
}

function hasMenu(content) {
  return content.includes('1️⃣') || content.includes('1)') || content.includes('Hangisi') || content.includes('secin');
}

function isPhoto(content) {
  const l = content.toLowerCase();
  return l.includes('foto') || l.includes('gorsel');
}

function isSelection(content) {
  return /^\d[\s.]?\s*$/.test(content.trim()) || /^\d+\s+numara/i.test(content.trim());
}

function isLogNote(content) {
  const l = content.toLowerCase();
  return l.includes('otomatik cozum') || l.includes('sonuc') || l.includes('kaydedildi') ||
    l.includes('tamamlandi') || l.includes('gerekmedi') || l.includes('islendi');
}

// ── AI node output handles ──

const AI_HANDLES = {
  ai_sentiment: 'positive',
  ai_intent: 'high_confidence',
  ai_faq: 'matched',
};

// ── Convert SE flow steps → FlowConfigV2 ──

function convertFlow(scenario, flow) {
  const nodes = [];
  const edges = [];
  let nodeIdx = 0;

  function addNode(type, label, data) {
    const id = `${type}_${++nodeIdx}`;
    nodes.push({
      id,
      type,
      position: { x: 300, y: 50 + (nodes.length * 150) },
      data: { label: label || type, ...data },
    });
    return id;
  }

  function addEdge(src, tgt, srcHandle) {
    edges.push({
      id: `e_${src}_${tgt}`,
      source: src,
      target: tgt,
      ...(srcHandle ? { sourceHandle: srcHandle } : {}),
    });
  }

  const steps = flow.steps || [];
  let prevId = null;
  let prevHandle = null;

  // Prepend trigger_start if first step is user message
  if (steps.length > 0 && steps[0].role === 'user') {
    prevId = addNode('trigger_start', 'Baslangic', {});
  }

  for (let i = 0; i < steps.length; i++) {
    const step = steps[i];
    const isLast = i === steps.length - 1;
    let nodeId = null;

    switch (step.role) {
      case 'system': {
        if (i === 0) {
          const tt = classifyTrigger(step.content);
          nodeId = addNode(tt, step.content.substring(0, 50), {});
        } else {
          const delay = parseDelay(step.content);
          const lower = step.content.toLowerCase();
          if (delay) {
            nodeId = addNode('action_delay', delay.label, { seconds: delay.seconds });
          } else if (isLogNote(lower) || isLast) {
            nodeId = addNode('utility_note', 'Sonuc', { text: step.content });
          } else if (lower.includes('alert') || lower.includes('uyari') || lower.includes('eskal')) {
            nodeId = addNode('utility_set_variable', 'Alert', { variable_name: 'alert_status', value_expression: step.content });
          } else if (isApiCallStep(step.content)) {
            nodeId = addNode('action_api_call', 'API Cagrisi', { method: 'GET', url: '', response_variable: 'api_result' });
          } else {
            nodeId = addNode('utility_set_variable', 'Durum', { variable_name: 'status', value_expression: step.content });
          }
        }
        break;
      }
      case 'ai': {
        const aiType = classifyAiStep(step.content);
        nodeId = addNode(aiType, step.content.substring(0, 50), {});
        prevHandle = AI_HANDLES[aiType] || null;
        break;
      }
      case 'bot': {
        if (hasMenu(step.content)) {
          nodeId = addNode('message_menu', 'Menu', { text: step.content, options: [] });
        } else {
          nodeId = addNode('message_text', 'Mesaj', { text: step.content });
        }
        break;
      }
      case 'user': {
        if (isPhoto(step.content)) {
          nodeId = addNode('utility_set_variable', 'Medya', { variable_name: 'customer_media', value_expression: '{{__last_input}}' });
        } else if (isSelection(step.content)) {
          continue; // menu selection handled by message_menu handles
        } else {
          nodeId = addNode('ai_intent', 'Yanit Analiz', {});
          prevHandle = 'high_confidence';
        }
        break;
      }
      case 'agent': {
        nodeId = addNode('action_handoff', 'Agent Devir', { summary_template: step.content });
        break;
      }
      default: {
        nodeId = addNode('utility_note', 'Not', { text: step.content });
      }
    }

    if (nodeId && prevId) {
      addEdge(prevId, nodeId, prevHandle);
      prevHandle = null;
    }
    if (nodeId) prevId = nodeId;
  }

  return {
    version: 2,
    metadata: { name: `${scenario.id.toUpperCase()}: ${flow.title}`, description: flow.description },
    nodes,
    edges,
    settings: {
      off_hours_message: 'Su anda mesai saatleri disindayiz.',
      unknown_input_message: 'Anlayamadim. Lutfen gecerli bir secenek girin.',
      handoff_confidence_threshold: 0.5,
      session_timeout_minutes: 30,
      max_loop_count: 10,
    },
  };
}

// ── Main ──

const NICHE_LABELS = {
  ecommerce: 'E-Ticaret', dental: 'Dis Klinigi', aesthetic: 'Estetik Klinik',
  hotel: 'Otel / Turizm', beauty: 'Guzellik Salonu', education: 'Egitim',
  mobile: 'Mobil Uygulama', universal: 'Evrensel', crossSector: 'Evrensel', health: 'Saglik',
};

const files = fs.readdirSync(DATA_DIR).filter(f => f.endsWith('.json') && !f.includes('field-scenarios') && !f.includes('test-results'));
const templates = [];

for (const file of files) {
  const raw = fs.readFileSync(path.join(DATA_DIR, file), 'utf-8');
  let data;
  try { data = JSON.parse(raw); } catch { continue; }
  if (!data.flows || data.flows.length === 0) continue;

  const flow = data.flows[0]; // primary flow per scenario
  const config = convertFlow(data, flow);

  templates.push({
    id: data.id,
    title: data.title,
    description: data.description,
    category: data.category || '',
    niche: data.niche || 'universal',
    flowConfig: config,
    aiPrompt: flow.aiPrompt || '',
    nodeCount: config.nodes.length,
    tags: (flow.tags || []).map(t => t.text),
  });
}

// Sort by niche then title
templates.sort((a, b) => a.niche.localeCompare(b.niche) || a.title.localeCompare(b.title));

// Generate TypeScript output
const ts = `// Auto-generated by SE/scripts/generate-flow-templates.cjs — DO NOT EDIT
import type { FlowConfigV2 } from '../types/flow';

export interface FlowTemplate {
  id: string;
  title: string;
  description: string;
  category: string;
  niche: string;
  flowConfig: FlowConfigV2;
  aiPrompt: string;
  nodeCount: number;
  tags: string[];
}

export const NICHE_LABELS: Record<string, string> = ${JSON.stringify(NICHE_LABELS, null, 2)};

export const FLOW_TEMPLATES: FlowTemplate[] = ${JSON.stringify(templates, null, 2)};
`;

fs.mkdirSync(path.dirname(OUT_FILE), { recursive: true });
fs.writeFileSync(OUT_FILE, ts, 'utf-8');
console.log(`Generated ${templates.length} templates → ${OUT_FILE}`);
