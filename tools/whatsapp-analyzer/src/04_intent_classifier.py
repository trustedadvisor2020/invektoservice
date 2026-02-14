"""
Phase 2, Step 4: Intent Classifier
- Keyword-first + Claude Haiku fallback (hybrid)
- 12 intents from config.yaml
- Keyword match = confidence 1.0 (no API call needed)
- No match = batch to Claude Haiku (50 msgs/call)
- Output: data/nlp/intent_classifications.csv

Usage:
    python src/04_intent_classifier.py [--config config.yaml] [--limit N]
"""

import sys
import os
import argparse
import re
import time
import logging
from pathlib import Path

import pandas as pd
import yaml
from tqdm import tqdm

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.utils.claude_client import (
    get_claude_client, call_claude_batch, parse_json_array,
    ClaudeAPIError, ClaudeParseError,
)

logger = logging.getLogger('whatsapp-analyzer')


def load_config(config_path: str = 'config.yaml') -> dict:
    with open(config_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


def build_keyword_matchers(config: dict) -> list[dict]:
    """Build compiled regex matchers from config intent keywords."""
    matchers = []
    for intent_name, intent_config in config.get('intents', {}).items():
        if 'keywords' in intent_config:
            # Escape special chars and join with OR
            keywords = intent_config['keywords']
            escaped = [re.escape(kw) for kw in keywords]
            pattern = re.compile('|'.join(escaped), re.IGNORECASE)
            matchers.append({
                'intent': intent_name,
                'pattern': pattern,
                'type': 'keyword',
            })
        elif 'pattern' in intent_config:
            pattern = re.compile(intent_config['pattern'])
            matchers.append({
                'intent': intent_name,
                'pattern': pattern,
                'type': 'regex',
            })
    return matchers


def classify_keyword(text: str, matchers: list[dict]) -> tuple[str, float] | None:
    """Try to classify text using keyword/regex matchers. Returns (intent, confidence) or None."""
    if not text or not isinstance(text, str):
        return None

    text_lower = text.lower()
    for matcher in matchers:
        if matcher['pattern'].search(text_lower if matcher['type'] == 'keyword' else text):
            return (matcher['intent'], 1.0)

    return None


def classify_batch_claude(messages: list[dict], config: dict,
                          client, model: str) -> list[dict]:
    """
    Classify a batch of messages using Claude Haiku.
    Returns list of {index, intent, confidence}.
    Raises ClaudeAPIError or ClaudeParseError on failures.
    """
    intent_names = list(config.get('intents', {}).keys())
    intent_list = ', '.join(intent_names)

    # Build batch prompt
    msg_lines = []
    for i, msg in enumerate(messages):
        text = msg['text'][:300] if msg['text'] else ''
        msg_lines.append(f"{i}|{text}")

    batch_text = '\n'.join(msg_lines)

    system_prompt = f"""Sen bir WhatsApp mesaj intent siniflandiricisin. Turkce giyim e-ticaret (moda) sektoru.

Mevcut intent'ler: {intent_list}

Her mesaj icin EN UYGUN intent'i belirle. Emin degilsen "unknown" yaz.

Kurallar:
- Sadece MUSTERI mesajlarini siniflandir (agent mesajlari degil)
- Kisa selamlasma (merhaba, selam) = greeting
- Tesekkur/memnuniyet = thank_you
- Urun kodu sorma = product_inquiry
- Fiyat sorma = price_inquiry
- Beden/numara sorma = size_inquiry
- Stok sorma = stock_inquiry
- Kargo/teslimat sorma = shipping_inquiry
- Iade/degisim = return_request
- Sikayet/memnuniyetsizlik = complaint
- Siparis onayi/aliyorum = order_confirmation
- Indirim/kampanya = discount_inquiry
- Adres bilgisi = address_info

JSON formatinda cevap ver:
[{{"i": 0, "intent": "greeting", "conf": 0.95}}, ...]

Sadece JSON array dondur, baska metin yazma."""

    response_text = call_claude_batch(client, model, system_prompt, batch_text)
    parsed = parse_json_array(response_text)

    return [
        {
            'index': r.get('i', -1),
            'intent': r.get('intent', 'unknown'),
            'confidence': r.get('conf', 0.0),
        }
        for r in parsed
        if isinstance(r, dict) and r.get('i', -1) >= 0
    ]


def run_intent_classifier(config: dict, limit: int | None = None):
    """Main intent classification pipeline."""
    input_path = config['paths']['cleaned_csv']
    output_dir = config['paths']['nlp_dir']
    output_path = os.path.join(output_dir, 'intent_classifications.csv')
    batch_size = config['nlp']['batch_size']
    min_confidence = config['nlp']['claude_min_confidence']

    print(f"{'='*60}")
    print(f"Intent Classifier - {config['tenant']['name']}")
    print(f"{'='*60}")

    Path(output_dir).mkdir(parents=True, exist_ok=True)

    # Load data
    print(f"\n[1/4] Loading cleaned messages...")
    start = time.time()
    df = pd.read_csv(input_path, sep=';', encoding='utf-8', dtype=str)
    if limit:
        df = df.head(limit)
    print(f"  Loaded: {len(df):,} messages ({time.time()-start:.1f}s)")

    # Only classify CUSTOMER messages (agent messages are responses)
    customer_mask = df['sender_type'] == 'CUSTOMER'
    customer_df = df[customer_mask].copy()
    print(f"  Customer messages: {len(customer_df):,}")

    # Build keyword matchers
    print(f"\n[2/4] Keyword classification...")
    start = time.time()
    matchers = build_keyword_matchers(config)
    print(f"  Matchers loaded: {len(matchers)} intents")

    # Phase A: Keyword classification
    results = []
    unmatched = []

    for idx, row in tqdm(customer_df.iterrows(), total=len(customer_df), desc="  Keywords"):
        text = row['message_text']
        match = classify_keyword(text, matchers)
        if match:
            results.append({
                'row_index': idx,
                'conversation_id': row['conversation_id'],
                'message_text': text[:200],
                'sender_type': 'CUSTOMER',
                'intent': match[0],
                'confidence': match[1],
                'method': 'keyword',
            })
        else:
            unmatched.append({
                'row_index': idx,
                'conversation_id': row['conversation_id'],
                'text': text,
            })

    keyword_count = len(results)
    keyword_pct = (keyword_count / max(len(customer_df), 1)) * 100
    print(f"  Keyword matched: {keyword_count:,} ({keyword_pct:.1f}%)")
    print(f"  Unmatched (Claude queue): {len(unmatched):,}")
    print(f"  Time: {time.time()-start:.1f}s")

    # Phase B: Claude Haiku fallback for unmatched
    if unmatched:
        print(f"\n[3/4] Claude Haiku classification ({len(unmatched):,} messages)...")
        client, model = get_claude_client(config)

        if client is None:
            print("  [WARN] Claude client unavailable. Marking unmatched as 'unknown' (method=skipped).")
            for item in unmatched:
                results.append({
                    'row_index': item['row_index'],
                    'conversation_id': item['conversation_id'],
                    'message_text': item['text'][:200],
                    'sender_type': 'CUSTOMER',
                    'intent': 'unknown',
                    'confidence': 0.0,
                    'method': 'skipped',
                })
        else:
            batches = [unmatched[i:i+batch_size] for i in range(0, len(unmatched), batch_size)]
            claude_matched = 0
            claude_unknown = 0
            claude_errors = 0

            for batch in tqdm(batches, desc="  Claude batches"):
                try:
                    batch_results = classify_batch_claude(batch, config, client, model)
                except ClaudeAPIError as e:
                    logger.warning(f"Claude API batch failed: {e}")
                    claude_errors += 1
                    batch_results = []
                except ClaudeParseError as e:
                    logger.warning(f"Claude response parse failed: {e}")
                    claude_errors += 1
                    batch_results = []

                # Map results back to original indices
                result_map = {r['index']: r for r in batch_results}

                for j, item in enumerate(batch):
                    if j in result_map and result_map[j]['confidence'] >= min_confidence:
                        results.append({
                            'row_index': item['row_index'],
                            'conversation_id': item['conversation_id'],
                            'message_text': item['text'][:200],
                            'sender_type': 'CUSTOMER',
                            'intent': result_map[j]['intent'],
                            'confidence': result_map[j]['confidence'],
                            'method': 'claude',
                        })
                        claude_matched += 1
                    else:
                        results.append({
                            'row_index': item['row_index'],
                            'conversation_id': item['conversation_id'],
                            'message_text': item['text'][:200],
                            'sender_type': 'CUSTOMER',
                            'intent': 'unknown',
                            'confidence': 0.0,
                            'method': 'claude_low_conf',
                        })
                        claude_unknown += 1

                # Rate limiting
                time.sleep(0.2)

            print(f"  Claude matched: {claude_matched:,}")
            print(f"  Claude unknown/low-conf: {claude_unknown:,}")
            if claude_errors:
                print(f"  Claude batch errors: {claude_errors}")
    else:
        print(f"\n[3/4] All messages matched by keywords. Skipping Claude.")

    # Save results
    print(f"\n[4/4] Saving to {output_path}...")
    results_df = pd.DataFrame(results)
    results_df.to_csv(output_path, sep=';', index=False, encoding='utf-8')
    file_size_mb = Path(output_path).stat().st_size / (1024 * 1024)
    print(f"  Saved: {file_size_mb:.1f} MB")

    # Summary
    print(f"\n{'='*60}")
    print(f"INTENT CLASSIFICATION SUMMARY")
    print(f"{'='*60}")
    print(f"  Total classified: {len(results):,}")

    if results:
        intent_dist = results_df['intent'].value_counts()
        print(f"\n  Intent distribution:")
        for intent, count in intent_dist.items():
            pct = count / len(results) * 100
            print(f"    {intent}: {count:,} ({pct:.1f}%)")

        method_dist = results_df['method'].value_counts()
        print(f"\n  Method distribution:")
        for method, count in method_dist.items():
            pct = count / len(results) * 100
            print(f"    {method}: {count:,} ({pct:.1f}%)")

    print(f"{'='*60}")

    return results_df


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='WhatsApp Intent Classifier')
    parser.add_argument('--config', default='config.yaml', help='Config file path')
    parser.add_argument('--limit', type=int, default=None, help='Limit number of messages to process')
    args = parser.parse_args()

    logging.basicConfig(level=logging.WARNING, format='%(levelname)s: %(message)s')
    os.chdir(Path(__file__).parent.parent)
    config = load_config(args.config)
    run_intent_classifier(config, limit=args.limit)
