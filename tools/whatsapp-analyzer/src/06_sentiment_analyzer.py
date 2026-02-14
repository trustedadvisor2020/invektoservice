"""
Phase 2, Step 6: Sentiment Analyzer
- Per-conversation sentiment (3-level: positive/neutral/negative)
- Keyword-first + Claude Haiku fallback
- Analyzes CUSTOMER messages only
- Output: data/nlp/sentiment.csv

Usage:
    python src/06_sentiment_analyzer.py [--config config.yaml] [--limit N]
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


# Turkish sentiment keywords (fashion e-commerce context)
POSITIVE_PATTERNS = [
    r'\b(teşekkür|sağol|sağolun)\b',
    r'\b(harika|süper|mükemmel|muhteşem)\b',
    r'\b(çok (güzel|iyi|beğendim|teşekkür))\b',
    r'\b(bayıldım|aşık oldum)\b',
    r'\b(tam istediğim|tam aradığım)\b',
    r'\b(çok memnun|memnun kaldım)\b',
    r'\b(eline sağlık|ellerinize sağlık)\b',
    r'\b(kaliteli|şık|zarif)\b',
]

NEGATIVE_PATTERNS = [
    r'\b(memnun değil|memnun kalmadım)\b',
    r'\b(kötü|berbat|rezalet|felaket)\b',
    r'\b(bozuk|defolu|yırtık|lekeli)\b',
    r'\b(yanlış geldi|eksik geldi)\b',
    r'\b(hayal kırıklığı)\b',
    r'\b(uymadı|dar geldi|büyük geldi|küçük geldi|geniş geldi)\b',
    r'\b(geç geldi|geç kaldı|gecikme)\b',
    r'\b(saygısızlık|ilgisiz|umursamaz)\b',
    r'\b(iade|geri gönder|değişim|iptal)\b',
]

POSITIVE_REGEX = re.compile('|'.join(POSITIVE_PATTERNS), re.IGNORECASE)
NEGATIVE_REGEX = re.compile('|'.join(NEGATIVE_PATTERNS), re.IGNORECASE)


def classify_sentiment_keyword(customer_text: str) -> tuple[str, float] | None:
    """
    Keyword-based sentiment classification.
    Returns (sentiment, score) or None if ambiguous/no match.
    Score: -1.0 to 1.0 (negative to positive).
    """
    if not customer_text:
        return None

    text_lower = customer_text.lower()
    pos_matches = len(POSITIVE_REGEX.findall(text_lower))
    neg_matches = len(NEGATIVE_REGEX.findall(text_lower))

    if pos_matches > 0 and neg_matches == 0:
        return ('positive', min(0.5 + pos_matches * 0.1, 1.0))
    elif neg_matches > 0 and pos_matches == 0:
        return ('negative', max(-0.5 - neg_matches * 0.1, -1.0))
    elif pos_matches > 0 and neg_matches > 0:
        # Mixed signals - needs Claude
        return None

    return None


def classify_sentiment_claude(conversations: list[dict], config: dict,
                              client, model: str) -> list[dict]:
    """
    Classify conversation sentiment using Claude Haiku.
    Takes list of {conv_id, text} dicts.
    Returns list of {index, sentiment, score}.
    Raises ClaudeAPIError or ClaudeParseError on failures.
    """
    conv_lines = []
    for i, conv in enumerate(conversations):
        text = conv['text'][:500]
        conv_lines.append(f"{i}|{text}")

    batch_text = '\n'.join(conv_lines)

    system_prompt = """Sen bir musteri sentiment analiz uzmanisin. Turkce giyim e-ticaret WhatsApp konusmalari.

Her konusmadaki MUSTERI mesajlarinin genel duygusal tonunu belirle.

Kurallar:
- positive: Memnuniyet, tesekkur, begeni, mutluluk
- neutral: Bilgi sorma, standart iletisim, duygusal ton yok
- negative: Sikayet, memnuniyetsizlik, hayal kirikligi, ofke

JSON formatinda cevap ver:
[{"i": 0, "s": "positive", "score": 0.8}, ...]

s: sentiment (positive/neutral/negative)
score: -1.0 (cok olumsuz) ile 1.0 (cok olumlu) arasi

Sadece JSON array dondur."""

    response_text = call_claude_batch(client, model, system_prompt, batch_text, max_tokens=2048)
    parsed = parse_json_array(response_text)

    return [
        {
            'index': r.get('i', -1),
            'sentiment': r.get('s', 'neutral'),
            'score': r.get('score', 0.0),
        }
        for r in parsed
        if isinstance(r, dict) and r.get('i', -1) >= 0
    ]


def run_sentiment_analyzer(config: dict, limit: int | None = None):
    """Main sentiment analysis pipeline."""
    input_path = config['paths']['cleaned_csv']
    conv_path = config['paths']['conversations_csv']
    output_dir = config['paths']['nlp_dir']
    output_path = os.path.join(output_dir, 'sentiment.csv')
    batch_size = config['nlp']['batch_size']

    print(f"{'='*60}")
    print(f"Sentiment Analyzer - {config['tenant']['name']}")
    print(f"{'='*60}")

    Path(output_dir).mkdir(parents=True, exist_ok=True)

    # Load messages
    print(f"\n[1/4] Loading data...")
    start = time.time()
    messages_df = pd.read_csv(input_path, sep=';', encoding='utf-8', dtype=str)
    conv_df = pd.read_csv(conv_path, sep=';', encoding='utf-8', dtype=str)

    if limit:
        conv_ids = conv_df['conversation_id'].unique()[:limit]
        messages_df = messages_df[messages_df['conversation_id'].isin(conv_ids)]
        conv_df = conv_df[conv_df['conversation_id'].isin(conv_ids)]

    print(f"  Messages: {len(messages_df):,}, Conversations: {len(conv_df):,}")
    print(f"  Time: {time.time()-start:.1f}s")

    # Aggregate customer text per conversation
    print(f"\n[2/4] Aggregating customer text per conversation...")
    start = time.time()
    customer_msgs = messages_df[messages_df['sender_type'] == 'CUSTOMER']
    conv_texts = customer_msgs.groupby('conversation_id')['message_text'].apply(
        lambda x: ' '.join(x.dropna().tolist())
    ).to_dict()
    print(f"  Conversations with customer text: {len(conv_texts):,}")
    print(f"  Time: {time.time()-start:.1f}s")

    # Phase A: Keyword sentiment
    print(f"\n[3/4] Sentiment classification...")
    start = time.time()
    results = []
    unmatched = []

    for conv_id in tqdm(conv_df['conversation_id'], desc="  Keywords"):
        customer_text = conv_texts.get(conv_id, '')
        if not customer_text:
            results.append({
                'conversation_id': conv_id,
                'sentiment': 'neutral',
                'score': 0.0,
                'method': 'empty',
            })
            continue

        match = classify_sentiment_keyword(customer_text)
        if match:
            results.append({
                'conversation_id': conv_id,
                'sentiment': match[0],
                'score': match[1],
                'method': 'keyword',
            })
        else:
            unmatched.append({
                'conv_id': conv_id,
                'text': customer_text,
            })

    keyword_count = len(results)
    print(f"  Keyword classified: {keyword_count:,}")
    print(f"  Unmatched (Claude queue): {len(unmatched):,}")

    # Phase B: Claude fallback
    if unmatched:
        client, model = get_claude_client(config)

        if client is None:
            print("  [WARN] Claude client unavailable. Marking unmatched as 'neutral' (method=skipped).")
            for item in unmatched:
                results.append({
                    'conversation_id': item['conv_id'],
                    'sentiment': 'neutral',
                    'score': 0.0,
                    'method': 'skipped',
                })
        else:
            batches = [unmatched[i:i+batch_size] for i in range(0, len(unmatched), batch_size)]
            claude_errors = 0

            for batch in tqdm(batches, desc="  Claude batches"):
                try:
                    batch_results = classify_sentiment_claude(batch, config, client, model)
                except ClaudeAPIError as e:
                    logger.warning(f"Claude API batch failed: {e}")
                    claude_errors += 1
                    batch_results = []
                except ClaudeParseError as e:
                    logger.warning(f"Claude response parse failed: {e}")
                    claude_errors += 1
                    batch_results = []

                result_map = {r['index']: r for r in batch_results}

                for j, item in enumerate(batch):
                    if j in result_map:
                        results.append({
                            'conversation_id': item['conv_id'],
                            'sentiment': result_map[j]['sentiment'],
                            'score': result_map[j]['score'],
                            'method': 'claude',
                        })
                    else:
                        results.append({
                            'conversation_id': item['conv_id'],
                            'sentiment': 'neutral',
                            'score': 0.0,
                            'method': 'claude_miss',
                        })

                time.sleep(0.2)

            if claude_errors:
                print(f"  Claude batch errors: {claude_errors}")

    print(f"  Total classified: {len(results):,}")
    print(f"  Time: {time.time()-start:.1f}s")

    # Save
    print(f"\n[4/4] Saving to {output_path}...")
    results_df = pd.DataFrame(results)
    results_df.to_csv(output_path, sep=';', index=False, encoding='utf-8')
    file_size_mb = Path(output_path).stat().st_size / (1024 * 1024)
    print(f"  Saved: {file_size_mb:.1f} MB")

    # Summary
    print(f"\n{'='*60}")
    print(f"SENTIMENT ANALYSIS SUMMARY")
    print(f"{'='*60}")
    print(f"  Total conversations: {len(results):,}")

    if results:
        sentiment_dist = results_df['sentiment'].value_counts()
        print(f"\n  Sentiment distribution:")
        for sentiment, count in sentiment_dist.items():
            pct = count / len(results) * 100
            print(f"    {sentiment}: {count:,} ({pct:.1f}%)")

        avg_score = results_df['score'].astype(float).mean()
        print(f"\n  Average score: {avg_score:.3f}")

        method_dist = results_df['method'].value_counts()
        print(f"\n  Method distribution:")
        for method, count in method_dist.items():
            pct = count / len(results) * 100
            print(f"    {method}: {count:,} ({pct:.1f}%)")

    print(f"{'='*60}")

    return results_df


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='WhatsApp Sentiment Analyzer')
    parser.add_argument('--config', default='config.yaml', help='Config file path')
    parser.add_argument('--limit', type=int, default=None, help='Limit conversations to process')
    args = parser.parse_args()

    logging.basicConfig(level=logging.WARNING, format='%(levelname)s: %(message)s')
    os.chdir(Path(__file__).parent.parent)
    config = load_config(args.config)
    run_sentiment_analyzer(config, limit=args.limit)
