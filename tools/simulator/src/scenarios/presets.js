'use strict';

/**
 * Predefined test scenarios for Automation service.
 * Each scenario is a sequence of webhook events with expected callback actions.
 * Based on automation-flow.json contract and FlowEngine processing logic.
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
