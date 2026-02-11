'use strict';

/**
 * Predefined test scenarios for InvektoServis microservices.
 *
 * Step types:
 * - (default) webhook: Sends webhook event to Automation service
 * - api_call: Direct HTTP request to any service (AgentAI, ChatAnalysis, health checks)
 *
 * api_call step: { type:'api_call', service:'agentAI', endpoint:'/api/v1/suggest', method:'POST', body:{...}, expected:{...} }
 */

const scenarios = {
  welcome_flow: {
    name: 'Welcome Flow',
    description: 'Yeni musteri ilk mesaj gonderir, karsilama + menu alir',
    steps: [
      {
        delay_ms: 0,
        webhook: {
          event_type: 'new_message',
          chat_id: 90001,
          channel: 'whatsapp',
          data: {
            phone: '+905551234567',
            customer_name: 'Test Musteri',
            message_text: 'Merhaba',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Welcome mesaji + menu gonderilmeli'
        }
      }
    ]
  },

  menu_selection: {
    name: 'Menu Selection',
    description: 'Musteri menu secenegi secer (static reply)',
    steps: [
      {
        delay_ms: 0,
        webhook: {
          event_type: 'new_message',
          chat_id: 90002,
          channel: 'whatsapp',
          data: {
            phone: '+905559876543',
            customer_name: 'Menu Test',
            message_text: 'Merhaba',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Welcome + menu'
        }
      },
      {
        delay_ms: 2000,
        webhook: {
          event_type: 'new_message',
          chat_id: 90002,
          channel: 'whatsapp',
          data: {
            phone: '+905559876543',
            customer_name: 'Menu Test',
            message_text: '1',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Secenek 1 icin static reply'
        }
      }
    ]
  },

  faq_flow: {
    name: 'FAQ Flow',
    description: 'Musteri FAQ secenegini secer ve soru sorar',
    steps: [
      {
        delay_ms: 0,
        webhook: {
          event_type: 'new_message',
          chat_id: 90003,
          channel: 'whatsapp',
          data: {
            phone: '+905550001111',
            customer_name: 'FAQ Test',
            message_text: 'Merhaba',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Welcome + menu'
        }
      },
      {
        delay_ms: 2000,
        webhook: {
          event_type: 'new_message',
          chat_id: 90003,
          channel: 'whatsapp',
          data: {
            phone: '+905550001111',
            customer_name: 'FAQ Test',
            message_text: '2',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'FAQ arama prompt'
        }
      },
      {
        delay_ms: 2000,
        webhook: {
          event_type: 'new_message',
          chat_id: 90003,
          channel: 'whatsapp',
          data: {
            phone: '+905550001111',
            customer_name: 'FAQ Test',
            message_text: 'Fiyat bilgisi almak istiyorum',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'FAQ cevabi veya intent fallback'
        }
      }
    ]
  },

  intent_detection: {
    name: 'Intent Detection',
    description: 'AI intent algilama akisi (Claude API kullanir)',
    steps: [
      {
        delay_ms: 0,
        webhook: {
          event_type: 'new_message',
          chat_id: 90004,
          channel: 'whatsapp',
          data: {
            phone: '+905550002222',
            customer_name: 'Intent Test',
            message_text: 'Merhaba',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Welcome + menu'
        }
      },
      {
        delay_ms: 2000,
        webhook: {
          event_type: 'new_message',
          chat_id: 90004,
          channel: 'whatsapp',
          data: {
            phone: '+905550002222',
            customer_name: 'Intent Test',
            message_text: '4',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Intent detection prompt'
        }
      },
      {
        delay_ms: 3000,
        webhook: {
          event_type: 'new_message',
          chat_id: 90004,
          channel: 'whatsapp',
          data: {
            phone: '+905550002222',
            customer_name: 'Intent Test',
            message_text: 'Implant fiyatlari hakkinda bilgi almak istiyorum, en uygun tedavi secenegi nedir?',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'AI intent algilandi, cevap veya suggest_reply'
        }
      }
    ]
  },

  handoff_flow: {
    name: 'Handoff Flow',
    description: 'Musteri insan temsilciye aktarilma ister',
    steps: [
      {
        delay_ms: 0,
        webhook: {
          event_type: 'new_message',
          chat_id: 90005,
          channel: 'whatsapp',
          data: {
            phone: '+905550003333',
            customer_name: 'Handoff Test',
            message_text: 'Merhaba',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Welcome + menu'
        }
      },
      {
        delay_ms: 2000,
        webhook: {
          event_type: 'new_message',
          chat_id: 90005,
          channel: 'whatsapp',
          data: {
            phone: '+905550003333',
            customer_name: 'Handoff Test',
            message_text: '5',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'handoff_to_human',
          description: 'Insan temsilciye aktarildi'
        }
      }
    ]
  },

  off_hours: {
    name: 'Off-Hours Reply',
    description: 'Mesai disi mesaj gonderilir (tenant calisma saati ayarliysa)',
    note: 'Tenant calisma saatleri DB\'de ayarli olmalidir. Aksi halde normal flow calisir.',
    steps: [
      {
        delay_ms: 0,
        webhook: {
          event_type: 'new_message',
          chat_id: 90006,
          channel: 'whatsapp',
          data: {
            phone: '+905550004444',
            customer_name: 'OffHours Test',
            message_text: 'Merhaba, bilgi almak istiyorum',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Mesai disi oto-cevap mesaji'
        }
      }
    ]
  },

  // ========== AGENTAI SCENARIOS ==========

  agentai_suggest: {
    name: 'AgentAI - Suggest Reply',
    description: 'Musteri mesajina AI cevap onerisi iste (Claude Haiku)',
    note: 'AgentAI servisi ayakta ve Claude API key ayarli olmali. 15s timeout.',
    service: 'agentAI',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'agentAI',
        endpoint: '/api/v1/suggest',
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
    description: 'Template ile AI cevap onerisi (degisken substitution)',
    note: 'Template {{isim}}, {{tedavi}} gibi degiskenler icerir. AI bunlari kullanarak cevap uretir.',
    service: 'agentAI',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'agentAI',
        endpoint: '/api/v1/suggest',
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
    description: 'AI oneri al -> Agent kabul/duzenle/reddet',
    note: 'Suggest sonrasi donen suggestion_id feedback\'e gonderilir. Feedback fire-and-forget (202).',
    service: 'agentAI',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'agentAI',
        endpoint: '/api/v1/suggest',
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
        service: 'agentAI',
        endpoint: '/api/v1/feedback',
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

  agentai_via_backend: {
    name: 'AgentAI - Backend Proxy',
    description: 'Backend uzerinden AgentAI suggest (Main App akisi)',
    note: 'Gercek akis: Main App -> Backend:5000/api/v1/agent-assist/suggest -> AgentAI:7105',
    service: 'backend',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/api/v1/agent-assist/suggest',
        method: 'POST',
        body: {
          chat_id: 95004,
          message_text: 'Sigorta kapsaminda mi?',
          customer_name: 'Ali Veli',
          channel: 'web',
          conversation_history: [
            { source: 'CUSTOMER', text: 'Merhaba' },
            { source: 'AGENT', text: 'Hosgeldiniz!' },
            { source: 'CUSTOMER', text: 'Sigorta kapsaminda mi?' }
          ],
          language: 'tr'
        },
        expected: {
          action: 'suggest_reply',
          description: 'Backend proxy uzerinden AI oneri (15s timeout)'
        }
      }
    ]
  },

  // ========== HEALTH CHECK SCENARIOS ==========

  health_all_services: {
    name: 'Health - All Services',
    description: 'Tum servislerin health endpointini kontrol et',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/health',
        method: 'GET',
        expected: { action: 'health_ok', description: 'Backend :5000 saglikli' }
      },
      {
        delay_ms: 500,
        type: 'api_call',
        service: 'chatAnalysis',
        endpoint: '/health',
        method: 'GET',
        expected: { action: 'health_ok', description: 'ChatAnalysis :7101 saglikli' }
      },
      {
        delay_ms: 500,
        type: 'api_call',
        service: 'automation',
        endpoint: '/health',
        method: 'GET',
        expected: { action: 'health_ok', description: 'Automation :7108 saglikli' }
      },
      {
        delay_ms: 500,
        type: 'api_call',
        service: 'agentAI',
        endpoint: '/health',
        method: 'GET',
        expected: { action: 'health_ok', description: 'AgentAI :7105 saglikli' }
      }
    ]
  },

  // ========== END-TO-END SCENARIOS ==========

  e2e_customer_message: {
    name: 'E2E - Musteri Mesaji Akisi',
    description: 'Tam akis: Musteri mesaj gonderir -> Automation isler -> Agent AI oneri alir -> Feedback gonderir',
    note: 'Tum servisler (Backend, Automation, AgentAI) ayakta olmali. Gercek uretim akisini simule eder.',
    steps: [
      {
        delay_ms: 0,
        type: 'api_call',
        service: 'backend',
        endpoint: '/health',
        method: 'GET',
        expected: { action: 'health_ok', description: 'Step 1: Backend saglikli mi kontrol et' }
      },
      {
        delay_ms: 500,
        webhook: {
          event_type: 'new_message',
          chat_id: 99001,
          channel: 'whatsapp',
          data: {
            phone: '+905559999999',
            customer_name: 'E2E Test Musteri',
            message_text: 'Merhaba, fiyat bilgisi almak istiyorum',
            message_source: 'CUSTOMER'
          }
        },
        expected: {
          action: 'send_message',
          description: 'Step 2: Automation webhook -> karsilama + menu (callback beklenir)'
        }
      },
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
          description: 'Step 3: AgentAI cevap onerisi (Backend proxy uzerinden)'
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
          description: 'Step 4: Agent oneriyi kabul etti (202 Accepted)'
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
