# Multi-Tenant Voice Agent Runtime Spec

> **Status:** DRAFT / canonical design (Q spec, 2026-05-31). Bu doküman FEAT-VFB'nin F0.5'ten
> sonraki hedef mimarisidir. Tek seferde implement EDİLMEZ — fazlı yol haritası aşağıda.
> İlgili: [voice-flow-builder.md](voice-flow-builder.md) (mevcut F0/F0.5 PoC).
>
> **Çekirdek ilke:** KB cevap ÜRETMEZ, sadece kaynak olur. Cevabı üreten katman
> response_rewriter + sector_adapter + tenant_profile'dır. Bu ayrım konmazsa agent her sektörde
> KB metnini okuyup robotlaşır.

---

## Mevcut Durum vs Spec (Gap Analysis — 2026-05-31)

| Spec Katmanı | Şu an | Gap |
|--------------|-------|-----|
| **§1 global_voice_behavior** | ✅ KISMEN — `InstructionsBuilder.cs` Persona (telefon temsilcisi, kısa, no-chatbot dili) | hard_rules prompt'ta; mekanik enforcement yok |
| **§2 forbidden_phrases** | ✅ KISMEN — prompt'ta yasak ifade listesi | Mekanik QA validator (§13) yok — sadece prompt seviyesi |
| **§3 tenant_profile** | ❌ YOK — sadece tenant_name + sector + flow_name inject (VoiceTestContext) | capability flags (pricing_enabled, appointment_enabled, lead_capture...) DB+config yok |
| **§4 sector_adapter** | ❌ YOK — sector sadece prompt'a string olarak giriyor | vocabulary / intent priority / banned_words / required_fields per-sector katmanı yok |
| **§5 kb_retrieval (confidence)** | ✅ KISMEN — `search_knowledge_base` tool + **confidence banding (F-VR-E, 2026-06-01):** top semantic skor → high/medium/low band, low<0.55 mekanik DROP + Türkçe steer, medium caution, config-driven (`Knowledge:Confidence*`). Keyword fallback thresholdlanmaz. | calendar/order lookup gibi intent-spesifik aksiyonlar (§7) hâlâ yok; band yalnız KB cevabını yönlendirir |
| **§6 response_rewriter** | ✅ KISMEN — prompt "KB'yi aynen okuma, telefona çevir" diyor | ayrı deterministik rewriter katmanı yok (LLM'e gömülü) |
| **§7 intent_routing** | ❌ YOK — model serbest | yapılandırılmış intent detection + routing rules yok |
| **§8 lead_collection_policy** | ✅ KISMEN — prompt "parça parça topla" diyor | per-sector field şeması + state tracking yok |
| **§9 appointment_policy** | ❌ YOK | takvim entegrasyonu / booking modes yok |
| **§10 pricing_policy** | ✅ KISMEN — prompt fiyat kuralı | per-sector pricing response template engine yok |
| **§11 escalation_policy** | ✅ KISMEN — prompt "insani dönüş" | always_escalate listesi + template engine yok |
| **§12 conversation_state** | ❌ YOK — collected_fields state machine yok | structured state + next_action enum yok |
| **§13 qa_validation** | ❌ YOK | konuşmadan önce response validator (forbidden/hallucination/length) yok |
| **§17 admin_controls** | ❌ YOK | tenant-level voice ayar paneli yok |
| **§19 metrics** | ✅ KISMEN — LatencyTracker (barge/first-byte) | conversation_quality / business_outcomes / qa_flags metrikleri yok |

**Özet:** F0.5 = tek global LLM prompt + KB tool. Spec = **çok-katmanlı runtime** (config + adapter + rewriter
+ validator + state + metrics). 2026-05-31 deploy'u §1/§2/§6'nın prompt-seviyesi yaklaşımını ekledi
(InstructionsBuilder Persona). Geri kalan katmanlar yapısal iş — fazlandırılmalı.

---

## Önerilen Fazlandırma (taslak — Q onayı bekliyor)

- **F-VR-A — Prompt katmanı (KISMEN YAPILDI):** §1/§2/§6/§8/§10/§11 prompt seviyesinde. ✅ deploy 2026-05-31.
- **F-VR-B — tenant_profile + sector_adapter (config/DB):** §3/§4. Capability flags + sector vocabulary/intent/banned_words/required_fields. Prompt bunlardan dinamik kurulur.
- **F-VR-C — qa_validation katmanı:** §13. Konuşmadan önce mekanik validator (forbidden phrase / length / unsupported claim → rewrite/escalate). Prompt'a güvenmeyen sert kapı.
- **F-VR-D — conversation_state + intent_routing + lead engine:** §7/§8/§12. Structured state machine + next_action.
- **F-VR-E — confidence routing + pricing/escalation engine:** §5/§10/§11. KB confidence_score bazlı aksiyon. ✅ **§5 confidence banding YAPILDI (2026-06-01):** `SearchKnowledgeBaseTool` top semantic skoru high(≥0.80)/medium(0.55-0.79)/low(<0.55) band'e map eder; low → tüm hit DROP + Türkçe "iletişim al/yönlendir" steer, medium → hit kalır + temkin steer, high → normal. Eşikler config-driven (`KbConfidenceOptions`, appsettings `Knowledge:Confidence*`). Keyword fallback skorları thresholdlanmaz (farklı ölçek). Pricing/escalation template engine (§10/§11) hâlâ prompt seviyesinde.
- **F-VR-F — appointment + admin controls + metrics:** §9/§17/§19.

> Sıralama Q kararı. Her faz = ayrı paket (interview → plan → dev → /rev → commit).

---

# (Canonical Spec — Q, 2026-05-31)

## Core Rule

Do not create separate agents per sector or customer. Use one global voice agent runtime.

Behavior controlled by layers:
`global_voice_behavior → tenant_profile → sector_adapter → kb_retrieval → response_rewriter → lead_collection_policy → escalation_policy → qa_validation`

## 1. Global Voice Agent Behavior
- role: phone_customer_representative, channel: voice_call, language tr-TR
- tone: natural, clear, short, professional; max 5 cümle / ~30 sn
- no_chatbot_language, no_internal_system_language
- hard_rules: never_read_kb_text_raw, never_say_kb_or_database_or_system, never_invent_missing_info, answer_directly_first, rewrite_all_kb_output_into_spoken_language, keep_voice_response_short, ask_one_question_at_a_time, collect_required_fields_progressively, escalate_if_uncertain_or_sensitive, do_not_overexplain

## 2. Forbidden Phrases
internal_source: "bilgi bankasında", "KB'ye göre", "veritabanında", "sistemde görünüyor", "dokümanda yazıyor", "kayıtlara göre", "net bilgi görünmüyor", "bilgi bulunamadı"
robotic: "daha detaylı fiyatlandırma sunulabilir", "ilgili birime aktarılacaktır", "talebiniz alınmıştır", "müşteri temsilcisine yönlendirileceksiniz", "sizlere yardımcı olmaktan memnuniyet duyarım"
Replacements: fiyat→"fiyatlarımız şöyle"; "net bilgi görünmüyor"→"bu konuda şu an net bilgi paylaşmam doğru olmaz"; "...bilgilerinizi paylaşabilirsiniz"→"bilgilerinizi alayım, size uygun teklif için dönüş yapılsın"; "daha detaylı fiyatlandırma sunulabilir"→"ekibimiz ihtiyacınıza göre net teklif hazırlasın"

## 3. Tenant Profile Schema
tenant_id, business_name, business_type(enum: clinic|dental_clinic|aesthetic_clinic|ecommerce|service_business|education|real_estate|generic), default_language, voice_brand_tone, primary_goal, secondary_goal, human_handoff_enabled, lead_capture_enabled, appointment_enabled, order_lookup_enabled, pricing_enabled, sensitive_topics_enabled

## 4. Sector Adapter
Vocabulary + intent priority + lead fields + escalation. Global agent'ı DEĞİŞTİRMEZ.
- clinic: customer=hasta, action=randevu; intents: appointment/pricing/service/doctor/location/hours; banned: müşteri/sipariş/ürün
- dental_clinic: hasta, muayene randevusu; implant/ortodonti/cleaning/appointment/pricing
- aesthetic_clinic: danışan, ön görüşme; escalation_required_for: medical_guarantee/diagnosis/side_effects/suitability
- ecommerce: müşteri, sipariş kontrolü; product/stock/price/shipping/return/order_status; required_lookup: order_status→[order_number,phone], return→[order_number,product_name]

## 5. KB Retrieval Contract
input: tenant_id, user_utterance, detected_intent, business_type, conversation_state
output: kb_answer_raw, confidence_score, source_type, missing_fields, requires_handoff
confidence: high≥0.80 (answer+rewrite), medium 0.55-0.79 (answer_only_if_safe, avoid_specific_claims, offer_handoff/lead), low<0.55 (no specifics, collect_contact/escalate)

## 6. KB-to-Voice Rewriter
remove_internal_references, remove_document_language, remove_uncertainty (gorunuyor), convert_prices_to_spoken, convert_policy_to_plain, answer_first_then_next_step, one_topic_per_response, no_markdown, no_lists_unless_asked, use_tenant_sector_vocabulary. shape: direct_answer + short_context + next_action. max 5 cümle.

## 7. Intent Routing
intents: pricing/appointment_booking/cancel/reschedule/service/product/stock/order_status/shipping/return/location/hours/campaign/complaint/human_request/unknown
rules: pricing→use_kb+collect_lead_if_custom; appointment→calendar_if_enabled+collect_fields; order_status→require_lookup+do_not_guess; complaint→empathy_short+collect+escalate; unknown→clarify_once+escalate_if_unclear

## 8. Lead Collection Policy
global: do_not_request_all_at_once, ask_one_group_at_a_time, confirm_critical, do_not_repeat_collected
default: full_name, phone, email_optional, note
clinic: +requested_service, preferred_date; dental: +treatment_interest, preferred_date, previous_examination_status; aesthetic: +treatment_interest, preferred_date; ecommerce: order_number_optional, product_name_optional, issue_type
prompts: name="Adınızı soyadınızı alabilir miyim?"; phone="Size dönüş yapılması için telefon numaranızı alayım."; date="Randevu için düşündüğünüz gün/saat aralığı var mı?"

## 9. Appointment Policy
enabled: clinic/dental/aesthetic=true, ecommerce=false
modes: collect_lead_only | calendar_slot_suggestion | direct_booking
calendar yoksa: "Randevu için bilgilerinizi alayım, ekip uygun saatler için size dönüş yapsın."
required: full_name, phone, requested_service, preferred_date

## 10. Pricing Policy
if exact→say; if range→say range; if case-dependent→do_not_guess; if medical/custom→offer_consultation; if annual/custom missing→collect_lead
generic: "Bu konuda sabit bir fiyat paylaşmam doğru olmaz. İhtiyaca göre netleşiyor. Bilgilerinizi alayım, ekip size uygun teklif için dönüş yapsın."
clinic: "Bu işlemde fiyat kişiye ve ihtiyaca göre değişebiliyor. Yanlış bilgi vermemek için bilgilerinizi alayım, klinik ekibi size net bilgiyle dönüş yapsın."
ecommerce: "Ürünün güncel fiyatını kontrol edip paylaşmam gerekir. Ürün adını ya da linkini alabilir miyim?"

## 11. Escalation Policy
always_escalate: medical_diagnosis, treatment_suitability, legal_claim, refund_dispute, angry_customer, complaint, missing_kb_confidence_low, payment_issue, data_privacy_request, human_request
templates: medical="...klinik ekibi size net bilgiyle dönüş yapsın"; ecommerce="...sipariş numaranızı/telefon... destek ekibi kontrol edip dönüş yapsın"; generic="Bu konuyu netleştirip size dönüş yapılması daha doğru olur. Bilgilerinizi alayım."

## 12. Conversation State
tenant_id, business_type, caller_phone, detected_intent, collected_fields{full_name,phone,email,company,requested_service,treatment_interest,preferred_date,order_number,product_name,issue_type,note}, kb_confidence, escalation_required, next_action
next_action enum: answer_only, ask_followup, collect_lead, lookup_order, suggest_appointment, create_callback_task, escalate_to_human, end_call

## 13. Runtime Response Validator (konuşmadan ÖNCE)
checks: no_forbidden_phrases, no_internal_source_reference, no_raw_kb_copy, no_unverified_claim, no_over_5_sentences, contains_direct_answer, contains_next_action_when_needed, matches_sector_vocabulary, does_not_ask_multiple_questions_unnecessarily
fail_action: rewrite_response; second_fail→safe_escalation_template

## 14. Sector Response Examples
- dental implant (fiyat yok): "İmplant fiyatı, kullanılacak markaya ve tedavi planına göre değişebiliyor. Bu yüzden telefonda net rakam söylemem doğru olmaz. Adınızı ve telefonunuzu alayım, klinik ekibi sizi muayene ve fiyat bilgisi için yönlendirsin."
- clinic randevu: "Tabii, randevu için yardımcı olayım. Adınızı soyadınızı ve hangi işlem için randevu almak istediğinizi alabilir miyim?"
- aesthetic botoks: "Botoks fiyatı uygulama bölgesi ve ihtiyaca göre değişebiliyor. Net fiyat için ön görüşme yapılması daha doğru olur. İsterseniz adınızı ve telefonunuzu alayım, klinik ekibi size dönüş yapsın."
- ecommerce sipariş: "Siparişinizi kontrol edebilmem için sipariş numaranızı alabilir miyim? Sipariş numaranız yoksa telefon numaranızla da kontrol edebiliriz."

## 15. LLM System Prompt Template
Phone voice agent, multi-tenant. Inputs: tenant profile, sector adapter, customer utterance, conversation state, KB result. Rules: understand intent; KB=source-of-truth only; never mention KB/db/system/document/tools; never read KB raw; rewrite to natural spoken Turkish; short phone-friendly; never invent; if missing/uncertain→say exact info can't be shared + collect contact/escalate; one question at a time; sector vocabulary; tenant lead/escalation policies; no robotic phrases; validate before output. Output only final spoken response, no markdown/bullets/explanations.

## 16. Developer Runtime Flow
onIncomingCall→loadTenantProfile+loadSectorAdapter+initState. onUserUtterance→detectIntent→retrieveKB→decideNextAction(intent,confidence,missing_fields,tenant,sector)→generateVoiceResponse→validateAndRewrite→speak→updateState.

## 17. Multi-Tenant Admin Controls
tenant_settings (business_name/type/lang/tone/goal + capability flags), kb_settings (upload/edit/priority/disable/mark pricing|policy|medical_sensitive), voice_behavior_settings (max_answer_length, allowed_escalation, collect_lead_after_uncertainty, ask_callback_permission, call_recording_notice), sector_settings (select adapter, override words, configure required_fields)

## 18. Test Cases (aktivasyon öncesi zorunlu)
pricing_exact, annual_price_missing (no invent + custom offer + lead), clinic_medical (no diagnose + escalate + contact), ecommerce_order_status (ask order#/phone + no guess), raw_kb_language (never repeat internal phrase), angry_customer (short ack + collect + escalate), unknown_intent (short clarify + no info dump)

## 19. Production Metrics (per tenant)
quality: kb_answer_used_rate, unsupported_claim_rate, forbidden_phrase_rate, escalation_rate, lead_capture_rate, appointment_request_rate, avg_response_length, avg_turn_count
outcomes: booked_appointments, callback_requests, pricing_leads, order_support_resolved, abandoned_calls, human_handoff_count
qa_flags: raw_kb_leak_detected, hallucinated_price_detected, medical_advice_risk, wrong_sector_language, repeated_question

## 20. Non-Negotiable Rules
one_global_agent_runtime_only, tenant_config_controls_behavior, sector_adapter_controls_language, kb_is_source_not_script, voice_response_is_generated_not_read, no_internal_system_words_to_caller, no_fake_prices, no_fake_availability, no_medical_diagnosis, no_order_status_without_lookup, every_response_passes_validator

## 21. Final Architecture
global_agent (voice behavior/speech/control) · tenant_profile (identity/capabilities/goal/lead fields) · sector_adapter (vocabulary/intent priority/sensitive rules/required fields) · kb_layer (source of truth) · response_rewriter (KB→spoken, de-robotize, shorten) · qa_validator (block forbidden/hallucination, enforce short+sector fit) · analytics (tenant quality/conversion/error)
