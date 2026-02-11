'use strict';

/**
 * Predefined test scenarios for InvektoServis microservices.
 *
 * ARCHITECTURE: All traffic goes through Backend proxy.
 * Main App -> Backend:5000 -> Automation/AgentAI/ChatAnalysis (localhost-only)
 *
 * Step types:
 * - api_call: HTTP request through Backend proxy (all scenarios use this)
 *
 * Webhook events are sent via Backend proxy: POST /api/v1/automation/webhook
 * AgentAI requests via Backend proxy: POST /api/v1/agent-assist/suggest|feedback
 */

// Helper: create a webhook step that goes through Backend proxy
function webhookStep(delayMs, webhookPayload, expected) {
  return {
    delay_ms: delayMs,
    type: 'api_call',
    service: 'backend',
    endpoint: '/api/v1/automation/webhook',
    method: 'POST',
    body: webhookPayload,
    expected
  };
}

const scenarios = {
  welcome_flow: {
    name: 'Welcome Flow',
    description: 'Yeni musteri ilk mesaj gonderir, karsilama + menu alir',
    steps: [
      webhookStep(0, {
        event_type: 'new_message',
        chat_id: 90001,
        channel: 'whatsapp',
        data: {
          phone: '+905551234567',
          customer_name: 'Test Musteri',
          message_text: 'Merhaba',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Welcome mesaji + menu gonderilmeli'
      })
    ]
  },

  menu_selection: {
    name: 'Menu Selection',
    description: 'Musteri menu secenegi secer (static reply)',
    steps: [
      webhookStep(0, {
        event_type: 'new_message',
        chat_id: 90002,
        channel: 'whatsapp',
        data: {
          phone: '+905559876543',
          customer_name: 'Menu Test',
          message_text: 'Merhaba',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Welcome + menu'
      }),
      webhookStep(2000, {
        event_type: 'new_message',
        chat_id: 90002,
        channel: 'whatsapp',
        data: {
          phone: '+905559876543',
          customer_name: 'Menu Test',
          message_text: '1',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Secenek 1 icin static reply'
      })
    ]
  },

  faq_flow: {
    name: 'FAQ Flow',
    description: 'Musteri FAQ secenegini secer ve soru sorar',
    steps: [
      webhookStep(0, {
        event_type: 'new_message',
        chat_id: 90003,
        channel: 'whatsapp',
        data: {
          phone: '+905550001111',
          customer_name: 'FAQ Test',
          message_text: 'Merhaba',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Welcome + menu'
      }),
      webhookStep(2000, {
        event_type: 'new_message',
        chat_id: 90003,
        channel: 'whatsapp',
        data: {
          phone: '+905550001111',
          customer_name: 'FAQ Test',
          message_text: '2',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'FAQ arama prompt'
      }),
      webhookStep(2000, {
        event_type: 'new_message',
        chat_id: 90003,
        channel: 'whatsapp',
        data: {
          phone: '+905550001111',
          customer_name: 'FAQ Test',
          message_text: 'Fiyat bilgisi almak istiyorum',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'FAQ cevabi veya intent fallback'
      })
    ]
  },

  intent_detection: {
    name: 'Intent Detection',
    description: 'AI intent algilama akisi (Claude API kullanir)',
    steps: [
      webhookStep(0, {
        event_type: 'new_message',
        chat_id: 90004,
        channel: 'whatsapp',
        data: {
          phone: '+905550002222',
          customer_name: 'Intent Test',
          message_text: 'Merhaba',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Welcome + menu'
      }),
      webhookStep(2000, {
        event_type: 'new_message',
        chat_id: 90004,
        channel: 'whatsapp',
        data: {
          phone: '+905550002222',
          customer_name: 'Intent Test',
          message_text: '4',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Intent detection prompt'
      }),
      webhookStep(3000, {
        event_type: 'new_message',
        chat_id: 90004,
        channel: 'whatsapp',
        data: {
          phone: '+905550002222',
          customer_name: 'Intent Test',
          message_text: 'Implant fiyatlari hakkinda bilgi almak istiyorum, en uygun tedavi secenegi nedir?',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'AI intent algilandi, cevap veya suggest_reply'
      })
    ]
  },

  handoff_flow: {
    name: 'Handoff Flow',
    description: 'Musteri insan temsilciye aktarilma ister',
    steps: [
      webhookStep(0, {
        event_type: 'new_message',
        chat_id: 90005,
        channel: 'whatsapp',
        data: {
          phone: '+905550003333',
          customer_name: 'Handoff Test',
          message_text: 'Merhaba',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Welcome + menu'
      }),
      webhookStep(2000, {
        event_type: 'new_message',
        chat_id: 90005,
        channel: 'whatsapp',
        data: {
          phone: '+905550003333',
          customer_name: 'Handoff Test',
          message_text: '5',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'handoff_to_human',
        description: 'Insan temsilciye aktarildi'
      })
    ]
  },

  off_hours: {
    name: 'Off-Hours Reply',
    description: 'Mesai disi mesaj gonderilir (tenant calisma saati ayarliysa)',
    note: 'Tenant calisma saatleri DB\'de ayarli olmalidir. Aksi halde normal flow calisir.',
    steps: [
      webhookStep(0, {
        event_type: 'new_message',
        chat_id: 90006,
        channel: 'whatsapp',
        data: {
          phone: '+905550004444',
          customer_name: 'OffHours Test',
          message_text: 'Merhaba, bilgi almak istiyorum',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Mesai disi oto-cevap mesaji'
      })
    ]
  },

  // ========== AGENTAI SCENARIOS (all via Backend proxy) ==========

  agentai_suggest: {
    name: 'AgentAI - Suggest Reply',
    description: 'Musteri mesajina AI cevap onerisi iste (Backend -> AgentAI)',
    note: 'Backend + AgentAI ayakta olmali. Claude API key ayarli. 15s timeout.',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/api/v1/agent-assist/suggest',
        method: 'POST',
        body: {
          chat_id: 95001,
          message_text: 'Implant fiyati ne kadar?',
          customer_name: 'Test Musteri',
          channel: 'whatsapp',
          conversation_history: [
            { source: 'CUSTOMER', text: 'Merhaba' },
            { source: 'AGENT', text: 'Merhaba, nasil yardimci olabilirim?' },
            { source: 'CUSTOMER', text: 'Implant fiyati ne kadar?' }
          ],
          language: 'tr'
        },
        expected: {
          action: 'suggest_reply',
          description: 'AI cevap onerisi: suggestion_id, suggested_reply, intent, confidence'
        }
      }
    ]
  },

  agentai_suggest_with_template: {
    name: 'AgentAI - Template Suggest',
    description: 'Template ile AI cevap onerisi (Backend -> AgentAI)',
    note: 'Template {{isim}}, {{tedavi}} gibi degiskenler icerir. AI bunlari kullanarak cevap uretir.',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/api/v1/agent-assist/suggest',
        method: 'POST',
        body: {
          chat_id: 95002,
          message_text: 'Dis beyazlatma fiyati ogrenebilir miyim?',
          customer_name: 'Ayse Kara',
          channel: 'whatsapp',
          conversation_history: [
            { source: 'CUSTOMER', text: 'Merhaba' },
            { source: 'AGENT', text: 'Hosgeldiniz, nasil yardimci olabilirim?' },
            { source: 'CUSTOMER', text: 'Dis beyazlatma fiyati ogrenebilir miyim?' }
          ],
          templates: [
            { id: 1, name: 'Fiyat Bilgisi', text: 'Merhaba {{isim}}, {{tedavi}} fiyatlarimiz {{fiyat_araligi}} arasindadir. Ucretsiz muayene icin randevu almak ister misiniz?' },
            { id: 2, name: 'Randevu', text: 'Merhaba {{isim}}, size en uygun zamani ayarlayalim. Hangi gun musait olursunuz?' }
          ],
          template_variables: {
            isim: 'Ayse',
            tedavi: 'dis beyazlatma',
            fiyat_araligi: '3.000-8.000 TL'
          },
          language: 'tr'
        },
        expected: {
          action: 'suggest_reply',
          description: 'Template degiskenleri doldurulmus AI cevap onerisi'
        }
      }
    ]
  },

  agentai_feedback_cycle: {
    name: 'AgentAI - Suggest + Feedback Cycle',
    description: 'AI oneri al -> Agent kabul/duzenle/reddet (Backend proxy)',
    note: 'Suggest sonrasi donen suggestion_id feedback\'e gonderilir. Feedback fire-and-forget (202).',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/api/v1/agent-assist/suggest',
        method: 'POST',
        body: {
          chat_id: 95003,
          message_text: 'Randevu almak istiyorum',
          customer_name: 'Mehmet Oz',
          channel: 'whatsapp',
          conversation_history: [
            { source: 'CUSTOMER', text: 'Merhaba, randevu almak istiyorum' }
          ],
          language: 'tr'
        },
        expected: {
          action: 'suggest_reply',
          description: 'Step 1: AI cevap onerisi alinir (suggestion_id not et)'
        }
      },
      {
        delay_ms: 2000,
        type: 'api_call',
        service: 'backend',
        endpoint: '/api/v1/agent-assist/feedback',
        method: 'POST',
        body: {
          suggestion_id: '{{step_1.suggestion_id}}',
          agent_action: 'edited',
          final_reply_text: 'Merhaba Mehmet Bey, yarin saat 14:00 icin randevu olusturdum.'
        },
        expected: {
          action: 'feedback_accepted',
          description: 'Step 2: Agent duzenleyip gonderdi (202 Accepted)'
        }
      }
    ]
  },

  // ========== HEALTH CHECK SCENARIOS ==========

  health_backend: {
    name: 'Health - Backend',
    description: 'Backend health check (tek disaridan erisilebilen servis)',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/health',
        method: 'GET',
        expected: { action: 'health_ok', description: 'Backend :5000 saglikli' }
      }
    ]
  },

  health_all_services: {
    name: 'Health - All Services (via Backend)',
    description: 'Backend uzerinden tum servislerin health durumunu kontrol et',
    note: 'Backend, internal servislerin health\'ini aggregate eder. Tek endpoint ile 4 servis kontrolu.',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/api/ops/health',
        method: 'GET',
        expected: { action: 'health_ok', description: 'Backend + ChatAnalysis + Automation + AgentAI saglikli' }
      }
    ]
  },

  // ========== END-TO-END SCENARIOS ==========

  e2e_customer_message: {
    name: 'E2E - Musteri Mesaji Akisi',
    description: 'Tam akis: Webhook -> Automation -> AgentAI suggest -> Feedback (hepsi Backend proxy)',
    note: 'Tum servisler ayakta olmali. Gercek uretim akisini simule eder. Her sey Backend uzerinden gecer.',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/health',
        method: 'GET',
        expected: { action: 'health_ok', description: 'Step 1: Backend saglikli mi kontrol et' }
      },
      webhookStep(500, {
        event_type: 'new_message',
        chat_id: 99001,
        channel: 'whatsapp',
        data: {
          phone: '+905559999999',
          customer_name: 'E2E Test Musteri',
          message_text: 'Merhaba, fiyat bilgisi almak istiyorum',
          message_source: 'CUSTOMER'
        }
      }, {
        action: 'send_message',
        description: 'Step 2: Backend -> Automation webhook (karsilama + menu)'
      }),
      {
        delay_ms: 3000,
        type: 'api_call',
        service: 'backend',
        endpoint: '/api/v1/agent-assist/suggest',
        method: 'POST',
        body: {
          chat_id: 99001,
          message_text: 'Fiyat bilgisi almak istiyorum',
          customer_name: 'E2E Test Musteri',
          channel: 'whatsapp',
          conversation_history: [
            { source: 'CUSTOMER', text: 'Merhaba, fiyat bilgisi almak istiyorum' }
          ],
          language: 'tr'
        },
        expected: {
          action: 'suggest_reply',
          description: 'Step 3: Backend -> AgentAI cevap onerisi'
        }
      },
      {
        delay_ms: 2000,
        type: 'api_call',
        service: 'backend',
        endpoint: '/api/v1/agent-assist/feedback',
        method: 'POST',
        body: {
          suggestion_id: '{{step_3.suggestion_id}}',
          agent_action: 'accepted',
          final_reply_text: null
        },
        expected: {
          action: 'feedback_accepted',
          description: 'Step 4: Backend -> AgentAI feedback (202 Accepted)'
        }
      }
    ]
  }
};

function getScenarioList() {
  return Object.entries(scenarios).map(([key, s]) => ({
    key,
    name: s.name,
    description: s.description,
    note: s.note || null,
    step_count: s.steps.length
  }));
}

function getScenario(key) {
  return scenarios[key] || null;
}

module.exports = { getScenarioList, getScenario };
