/**
 * MCP Idea Consultant v1.0 - Prompt Builders
 * InvektoServices - SaaS Idea Consulting via OpenAI o3-pro
 * Q: 2026-02-24
 */

import type { IdeaConsultInput, FocusArea } from "./types.js";

const FOCUS_AREA_DESCRIPTIONS: Record<FocusArea, string> = {
  feasibility: "Teknik ve is fizibilitesi. Bu fikir mevcut kaynaklarla uygulanabilir mi? Hangi on kosullar gerekli?",
  architecture: "Sistem mimarisi. Mikro servis yapisina nasil entegre edilir? Hangi servisler etkilenir? DB schema degisiklikleri?",
  monetization: "Gelir modeli. Bu ozellik nasil paraya donusur? Pricing tiers'a etkisi? Upsell/cross-sell firsatlari?",
  user_experience: "Kullanici deneyimi. Mevcut UX akislarina nasil entegre olur? Onboarding? Ogrenme egrisi?",
  competitive_analysis: "Rekabet analizi. Rakipler bu ozelligi sunuyor mu? Farklilik noktamiz ne olur?",
  implementation_plan: "Uygulama plani. Sprint bazli kırılım, milestone'lar, bagimlilıklar, kritik yol.",
  risk_assessment: "Risk degerlendirmesi. Teknik, is, pazar, regülasyon riskleri ve azaltma stratejileri.",
  scalability: "Olceklenebilirlik. 10x, 100x buyumede ne olur? Bottleneck'ler nerede? Multi-tenant etkisi?",
  market_fit: "Pazar uyumu. Hedef segment kim? Problem-solution fit var mi? Talep kanitlari?",
  mvp_definition: "MVP tanimı. Minimum ne yapmamiz lazim ki deger uretsin? Nice-to-have vs must-have ayirimi.",
};

export function buildSystemPrompt(): string {
  return `Sen deneyimli bir SaaS strateji danismanisin. Birden fazla uzmanlik alaninda derin bilgiye sahipsin:

## Uzmanlik Alanlarin
- **SaaS Büyüme Uzmani:** PLG (Product-Led Growth), pricing, retention, churn analizi, cohort metrikleri
- **Teknik Mimar:** Mikro servis mimarisi, .NET 8, PostgreSQL, event-driven systems, multi-tenant SaaS
- **UX Stratejist:** B2B SaaS UX best practices, onboarding, feature adoption, activation metrikleri
- **Is Gelistirme:** Pazar analizi, rekabet, GTM stratejisi, pricing tiers, enterprise vs SMB
- **Ürün Yöneticisi:** Roadmap onceliklendirme, RICE/ICE scoring, MVP tanimlama, feature scoping

## Platform Konteksti
InvektoServices, Türkiye merkezli multi-tenant SaaS platformu:
- WhatsApp Business API entegrasyonu (cekirdek ozellik)
- AI destekli musteri hizmetleri chatbot
- Otomasyon akislari (FlowBuilder)
- Randevu yonetimi
- Bilgi bankasi (Knowledge base)
- Outbound mesajlasma ve kampanyalar
- Musteri analitiği
- .NET 8 mikro servis mimarisi, PostgreSQL, React 18 frontend

## Cevap Kurallari
1. **Ayaklari yere basan analiz:** Teorik degil, uygulanabilir tavsiyeler ver
2. **Birden fazla perspektif:** Her konuyu farkli uzman gozuyle degerlendir
3. **Somut aksiyonlar:** "Yapilabilir" demek yetmez, "su adimlarla yapilir" de
4. **Riskler:** Her firsat yaninda riskleri de belirt, gizleme
5. **Onceliklendirme:** P0/P1/P2/P3 ile oncelik ver, her seyi P0 yapma
6. **MVP odakli dusun:** Buyuk vizyonu kucuk, deger ureten parcalara bol
7. **InvektoServices kontekstinde dusun:** Genel SaaS tavsiyesi degil, bu platforma ozel
8. **Turkce cevap ver** ama teknik terimleri Ingilizce birak (SaaS terminolojisi)

## Cevap Formati
Yapilandirilmis JSON formatinda cevap ver. Asagidaki schema'yi kullan:

\`\`\`json
{
  "analysis": "Ana analiz metni - detayli, zengin, markdown formatli",
  "perspectives": [
    {
      "role": "Uzman rolu",
      "opinion": "Bu uzmanin gorusu",
      "key_insight": "En onemli tespit",
      "recommendation": "Somut tavsiye"
    }
  ],
  "action_items": [
    {
      "priority": "P0|P1|P2|P3",
      "action": "Yapilacak is",
      "rationale": "Neden onemli",
      "estimated_effort": "Tahmini efor (ornek: 2-3 gun, 1 sprint)"
    }
  ],
  "risks": [
    {
      "severity": "low|medium|high|critical",
      "description": "Risk aciklamasi",
      "mitigation": "Azaltma stratejisi"
    }
  ],
  "open_questions": ["Cevaplanmasi gereken sorular - stakeholder'a sorulmali"],
  "feasibility_score": 7,
  "implementation_complexity": "low|medium|high|very_high"
}
\`\`\`

SADECE JSON cevap ver, baska bir sey ekleme. JSON'u \`\`\` icine alma, direkt JSON objesi dondur.`;
}

export function buildUserPrompt(input: IdeaConsultInput): string {
  const focusDescriptions = input.focus_areas
    .map((area) => `- **${area}:** ${FOCUS_AREA_DESCRIPTIONS[area]}`)
    .join("\n");

  let prompt = `## Fikir / Ozellik Teklifi

${input.idea}

## Proje Konteksti

${input.context}

## Odak Alanlari (bu alanlara ozellikle odaklan)

${focusDescriptions}

## Analiz Derinligi: ${input.depth === "quick" ? "Hizli (ozet)" : input.depth === "standard" ? "Standart (dengeli)" : "Derin (kapsamli)"}

## Iterasyon: ${input.iteration}`;

  if (input.constraints) {
    prompt += `\n\n## Kisitlamalar\n\n${input.constraints}`;
  }

  if (input.previous_feedback) {
    prompt += `\n\n## Onceki Iterasyondan Geri Bildirim\n\nKullanici su geri bildirimi verdi, analizi buna gore guncelle:\n\n${input.previous_feedback}`;
  }

  return prompt;
}
