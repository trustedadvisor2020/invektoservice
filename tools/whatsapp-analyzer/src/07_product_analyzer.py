"""
Phase 2, Step 7: Product Analyzer
- Refined product code extraction (price vs product code fix)
- Price detection: xx99/xx00 endings + TL/lira context
- Product code: 4-digit non-price + kmr-prefixed codes
- Product demand analysis, price point analysis
- Output: data/nlp/product_analysis.csv, data/nlp/price_analysis.csv

Usage:
    python src/07_product_analyzer.py [--config config.yaml] [--limit N]
"""

import sys
import os
import argparse
import json
import re
import time
from pathlib import Path
from collections import Counter, defaultdict

import pandas as pd
import numpy as np
import yaml
from tqdm import tqdm

sys.path.insert(0, str(Path(__file__).parent.parent))


def load_config(config_path: str = 'config.yaml') -> dict:
    with open(config_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


# Price context patterns (Turkish)
PRICE_SUFFIX_PATTERN = re.compile(
    r'(\d{3,5})\s*(?:tl|₺|lira|türk lirası)',
    re.IGNORECASE
)

# Price answer pattern: "fiyatı/fiyat: 1599" (the number IS the price)
# NOT "4868 fiyatı ne?" (the number is the product being asked about)
PRICE_ANSWER_PATTERN = re.compile(
    r'(?:fiyat[ıi]?|ücret[i]?|tutar[ı]?)\s*[:=]?\s*(\d{3,5})',
    re.IGNORECASE
)

# KMR-prefixed product codes
KMR_PATTERN = re.compile(r'\b(kmr[\w-]*)\b', re.IGNORECASE)

# Generic 4-digit number pattern
FOUR_DIGIT_PATTERN = re.compile(r'\b(\d{4})\b')


def is_likely_price(number_str: str) -> bool:
    """
    Heuristic: is this 4-digit number likely a price?
    Turkish fashion prices typically: 999, 1099, 1199, 1299, 1399, 1499, 1599, 1799, 1999, 2000, 3899
    """
    num = int(number_str)

    # Numbers ending in 99 are almost always prices (1099, 1199, 1299, etc.)
    if number_str.endswith('99'):
        return True

    # Round thousands (1000, 2000, 3000, 4000, 5000)
    if num % 1000 == 0:
        return True

    # Numbers ending in 00 (1500, 2500, etc.)
    if number_str.endswith('00'):
        return True

    # Numbers ending in 50 (1250, 1350, etc.) are also common prices
    if number_str.endswith('50'):
        return True

    return False


def extract_products_and_prices(text: str) -> tuple[list[str], list[str]]:
    """
    Extract product codes and prices from text.
    Returns (product_codes, prices).
    """
    if not text or not isinstance(text, str):
        return [], []

    product_codes = []
    prices = []

    # Extract KMR codes first (always product codes)
    kmr_matches = KMR_PATTERN.findall(text)
    product_codes.extend(kmr_matches)

    # Extract numbers with explicit price suffixes
    price_suffix_matches = PRICE_SUFFIX_PATTERN.findall(text)
    explicit_prices = set(price_suffix_matches)
    prices.extend(price_suffix_matches)

    # Extract numbers from price answers (e.g., "fiyatı: 1599")
    price_answer_matches = PRICE_ANSWER_PATTERN.findall(text)
    for g in price_answer_matches:
        if g:
            explicit_prices.add(g)
            if g not in prices:
                prices.append(g)

    # Extract all 4-digit numbers
    all_four_digit = FOUR_DIGIT_PATTERN.findall(text)

    for num_str in all_four_digit:
        # Already identified as price by context
        if num_str in explicit_prices:
            continue

        # Apply heuristic
        if is_likely_price(num_str):
            prices.append(num_str)
        else:
            product_codes.append(num_str)

    return product_codes, prices


def run_product_analyzer(config: dict, limit: int | None = None):
    """Main product analysis pipeline."""
    input_path = config['paths']['cleaned_csv']
    conv_path = config['paths']['conversations_csv']
    output_dir = config['paths']['nlp_dir']
    product_path = os.path.join(output_dir, 'product_analysis.csv')
    price_path = os.path.join(output_dir, 'price_analysis.csv')
    summary_path = os.path.join(output_dir, 'product_summary.json')

    print(f"{'='*60}")
    print(f"Product Analyzer - {config['tenant']['name']}")
    print(f"{'='*60}")

    Path(output_dir).mkdir(parents=True, exist_ok=True)

    # Load data
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

    # Pre-group messages by conversation for O(1) lookup
    print(f"\n[2/4] Extracting products & prices per conversation...")
    start = time.time()

    # Pre-aggregate text per conversation (avoids O(N*M) lookup)
    conv_text_map = messages_df.groupby('conversation_id')['message_text'].apply(
        lambda x: ' '.join(x.dropna().tolist())
    ).to_dict()

    conv_products = []
    all_product_counter = Counter()
    all_price_counter = Counter()
    product_by_agent = defaultdict(Counter)
    product_by_outcome = defaultdict(Counter)

    for _, conv_row in tqdm(conv_df.iterrows(), total=len(conv_df), desc="  Analyzing"):
        conv_id = conv_row['conversation_id']
        all_text = conv_text_map.get(conv_id, '')
        products, prices = extract_products_and_prices(all_text)

        unique_products = list(set(products))
        unique_prices = list(set(prices))

        conv_products.append({
            'conversation_id': conv_id,
            'product_codes': '|'.join(unique_products) if unique_products else '',
            'product_count': len(unique_products),
            'prices_mentioned': '|'.join(unique_prices) if unique_prices else '',
            'price_count': len(unique_prices),
            'outcome': conv_row.get('outcome', ''),
            'primary_agent': conv_row.get('primary_agent', ''),
        })

        for p in unique_products:
            all_product_counter[p] += 1
        for p in unique_prices:
            all_price_counter[p] += 1

        # Track by agent and outcome
        agent = conv_row.get('primary_agent', 'unknown')
        outcome = conv_row.get('outcome', 'unknown')
        for p in unique_products:
            product_by_agent[agent][p] += 1
            product_by_outcome[outcome][p] += 1

    print(f"  Time: {time.time()-start:.1f}s")
    print(f"  Unique products: {len(all_product_counter):,}")
    print(f"  Unique prices: {len(all_price_counter):,}")

    # Save product analysis
    print(f"\n[3/4] Saving analysis...")
    conv_prod_df = pd.DataFrame(conv_products)
    conv_prod_df.to_csv(product_path, sep=';', index=False, encoding='utf-8')
    print(f"  Product analysis: {product_path}")

    # Save price analysis
    price_data = []
    for price, count in all_price_counter.most_common():
        price_data.append({
            'price': price,
            'mention_count': count,
            'likely_tl': f"{int(price):,} TL" if price.isdigit() else price,
        })
    price_df = pd.DataFrame(price_data)
    price_df.to_csv(price_path, sep=';', index=False, encoding='utf-8')
    print(f"  Price analysis: {price_path}")

    # Build summary JSON
    top_products = [
        {'code': code, 'mentions': count}
        for code, count in all_product_counter.most_common(50)
    ]

    top_prices = [
        {'price': price, 'mentions': count}
        for price, count in all_price_counter.most_common(20)
    ]

    # Products by outcome (top 10 per outcome)
    products_by_outcome_summary = {}
    for outcome, counter in product_by_outcome.items():
        products_by_outcome_summary[outcome] = [
            {'code': code, 'mentions': count}
            for code, count in counter.most_common(10)
        ]

    summary = {
        'total_unique_products': len(all_product_counter),
        'total_unique_prices': len(all_price_counter),
        'total_product_mentions': sum(all_product_counter.values()),
        'total_price_mentions': sum(all_price_counter.values()),
        'top_products': top_products,
        'top_prices': top_prices,
        'products_by_outcome': products_by_outcome_summary,
    }

    print(f"\n[4/4] Saving summary...")
    with open(summary_path, 'w', encoding='utf-8') as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print(f"  Summary: {summary_path}")

    # Print summary
    print(f"\n{'='*60}")
    print(f"PRODUCT ANALYSIS SUMMARY")
    print(f"{'='*60}")
    print(f"  Unique products: {len(all_product_counter):,}")
    print(f"  Unique prices: {len(all_price_counter):,}")
    print(f"  Total product mentions: {sum(all_product_counter.values()):,}")
    print(f"  Total price mentions: {sum(all_price_counter.values()):,}")

    print(f"\n  Top 15 products:")
    for code, count in all_product_counter.most_common(15):
        try:
            print(f"    {code}: {count:,}")
        except UnicodeEncodeError:
            print(f"    (code): {count:,}")

    print(f"\n  Top 10 prices:")
    for price, count in all_price_counter.most_common(10):
        print(f"    {price} TL: {count:,}")

    # Compare with old analysis
    old_product_codes_path = config['paths']['conversations_csv']
    old_conv_df = pd.read_csv(old_product_codes_path, sep=';', encoding='utf-8', dtype=str)
    old_codes = []
    for codes in old_conv_df['product_codes'].dropna():
        if codes:
            old_codes.extend(codes.split('|'))
    old_unique = len(set(old_codes))

    print(f"\n  Comparison with Phase 1:")
    print(f"    Phase 1 unique 'products' (includes prices): {old_unique:,}")
    print(f"    Phase 2 unique products (prices excluded): {len(all_product_counter):,}")
    print(f"    Phase 2 unique prices (separated): {len(all_price_counter):,}")
    print(f"    Reduction: {old_unique - len(all_product_counter):,} false positives removed")

    print(f"{'='*60}")

    return conv_prod_df


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='WhatsApp Product Analyzer')
    parser.add_argument('--config', default='config.yaml', help='Config file path')
    parser.add_argument('--limit', type=int, default=None, help='Limit conversations to process')
    args = parser.parse_args()

    os.chdir(Path(__file__).parent.parent)
    config = load_config(args.config)
    run_product_analyzer(config, limit=args.limit)
