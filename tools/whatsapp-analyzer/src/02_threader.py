"""
Phase 1, Step 2: Conversation Threader
- Groups messages by conversation_id
- Calculates per-conversation metadata
- Detects conversation outcome (sale, no_sale, abandoned)
- Output: conversations.csv

Usage:
    python src/02_threader.py [--config config.yaml]
"""

import sys
import os
import argparse
import time
import re
from pathlib import Path

import pandas as pd
import numpy as np
import yaml
from tqdm import tqdm

sys.path.insert(0, str(Path(__file__).parent.parent))


# Outcome detection patterns (Turkish, fashion e-commerce)
# CONFIRMED: actual sale evidence (tracking code, payment confirmed, order confirmed)
CONFIRMED_SALE_PATTERNS = [
    r'siparişiniz.*oluşturulmuştur',
    r'siparişiniz.*onaylandı',
    r'siparişiniz.*hazırlanıyor',
    r'güzel günlerde giymenizi',
    r'mutlu günlerde giymenizi',
    r'kargo.*takip\s*(no|numar)',
    r'aras.*kargo.*takip',
    r'yurtiçi.*kargo.*takip',
    r'mng.*kargo.*takip',
    r'havale.*yapıldı',
    r'eft.*yapıldı',
    r'ödeme.*alındı',
    r'ödemeniz.*onaylandı',
]

# OFFERED: agent proposing to create order (not yet confirmed)
OFFER_PATTERNS = [
    r'siparişinizi.*oluşturalım',
    r'sipariş.*oluşturayım',
    r'sipariş.*verelim',
    r'sipariş.*vermek\s*ister',
    r'sipariş.*oluşturmamı',
    r'kapıda.*ödeme.*ister',
    r'kargo.*gönderelim',
    r'aras.*kargo\b',
    r'yurtiçi.*kargo\b',
    r'mng.*kargo\b',
]

RETURN_PATTERNS = [
    r'iade.*taleb',
    r'değişim.*taleb',
    r'geri.*gönder',
    r'iade.*başlat',
    r'değişim.*başlat',
    r'kargo.*iade',
]

COMPLAINT_PATTERNS = [
    r'memnun\s*değil',
    r'yanlış.*geldi',
    r'bozuk.*geldi',
    r'kötü.*kalite',
    r'berbat',
    r'rezalet',
]


def load_config(config_path: str = 'config.yaml') -> dict:
    with open(config_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


def detect_outcome(messages: pd.DataFrame) -> str:
    """
    Detect conversation outcome based on message content.
    Priority: confirmed_sale > offered > return > complaint > abandoned > no_response > no_sale

    Key distinction:
    - confirmed_sale: Payment/shipment evidence (tracking code, payment confirmed)
    - offered: Agent proposed order creation but no confirmation evidence
    """
    # Only look at agent (ME) messages for sale/offer detection
    agent_msgs = messages[messages['sender_type'] == 'ME']['message_text']
    agent_text = ' '.join(agent_msgs.str.lower().tolist())

    # Check for confirmed sale signals (strongest evidence)
    for pattern in CONFIRMED_SALE_PATTERNS:
        if re.search(pattern, agent_text):
            return 'sale'

    # Check for offer signals (agent proposed, no confirmation)
    for pattern in OFFER_PATTERNS:
        if re.search(pattern, agent_text):
            return 'offered'

    # Check for return/exchange
    all_msgs_text = ' '.join(messages['message_text'].str.lower().tolist())
    for pattern in RETURN_PATTERNS:
        if re.search(pattern, all_msgs_text):
            return 'return'

    # Check for complaint
    customer_text = ' '.join(
        messages[messages['sender_type'] == 'CUSTOMER']['message_text'].str.lower().tolist()
    )
    for pattern in COMPLAINT_PATTERNS:
        if re.search(pattern, customer_text):
            return 'complaint'

    # Abandoned: very few messages, customer initiated but no response
    if len(messages) <= 2:
        return 'abandoned'

    # Check if conversation ended with customer (no agent follow-up)
    last_sender = messages.iloc[-1]['sender_type']
    if last_sender == 'CUSTOMER' and len(messages) >= 3:
        return 'no_response'

    return 'no_sale'


def extract_product_codes(text: str) -> list[str]:
    """Extract 4-digit product codes from text."""
    if not text or not isinstance(text, str):
        return []
    return re.findall(r'\b(\d{4})\b', text)


def thread_conversation(conv_id: str, messages: pd.DataFrame) -> dict:
    """Calculate metadata for a single conversation."""
    messages = messages.sort_values('timestamp')

    customer_msgs = messages[messages['sender_type'] == 'CUSTOMER']
    agent_msgs = messages[messages['sender_type'] == 'ME']

    # Primary agent (most messages)
    primary_agent = ''
    if len(agent_msgs) > 0:
        agent_counts = agent_msgs['agent_name'].value_counts()
        primary_agent = agent_counts.index[0] if len(agent_counts) > 0 else ''

    # Duration
    start_time = messages['timestamp'].min()
    end_time = messages['timestamp'].max()
    duration_minutes = 0
    if pd.notna(start_time) and pd.notna(end_time):
        duration_minutes = int((end_time - start_time).total_seconds() / 60)

    # Response time: first customer message → first agent response
    first_response_minutes = None
    if len(customer_msgs) > 0 and len(agent_msgs) > 0:
        first_customer_ts = customer_msgs['timestamp'].min()
        first_agent_after = agent_msgs[agent_msgs['timestamp'] > first_customer_ts]
        if len(first_agent_after) > 0:
            first_agent_ts = first_agent_after['timestamp'].min()
            first_response_minutes = round(
                (first_agent_ts - first_customer_ts).total_seconds() / 60, 1
            )

    # Product codes mentioned
    all_text = ' '.join(messages['message_text'].tolist())
    product_codes = list(set(extract_product_codes(all_text)))

    # First customer message and last agent message
    first_customer_msg = customer_msgs.iloc[0]['message_text'] if len(customer_msgs) > 0 else ''
    last_agent_msg = agent_msgs.iloc[-1]['message_text'] if len(agent_msgs) > 0 else ''

    # Outcome
    outcome = detect_outcome(messages)

    return {
        'conversation_id': conv_id,
        'business_phone': messages.iloc[0]['business_phone'],
        'start_time': start_time,
        'end_time': end_time,
        'duration_minutes': duration_minutes,
        'message_count': len(messages),
        'customer_message_count': len(customer_msgs),
        'agent_message_count': len(agent_msgs),
        'primary_agent': primary_agent,
        'first_response_minutes': first_response_minutes,
        'outcome': outcome,
        'product_codes': '|'.join(product_codes) if product_codes else '',
        'first_customer_msg': first_customer_msg[:500],  # Truncate
        'last_agent_msg': last_agent_msg[:500],
    }


def run_threader(config: dict):
    """Main threader pipeline."""
    input_path = config['paths']['cleaned_csv']
    output_path = config['paths']['conversations_csv']
    min_messages = config['processing']['min_conversation_messages']

    print(f"{'='*60}")
    print(f"WhatsApp Threader - {config['tenant']['name']}")
    print(f"{'='*60}")

    Path(output_path).parent.mkdir(parents=True, exist_ok=True)

    # Load cleaned data
    print(f"\n[1/3] Loading cleaned data from {input_path}...")
    start = time.time()
    df = pd.read_csv(input_path, sep=';', encoding='utf-8', dtype=str)
    df['timestamp'] = pd.to_datetime(df['timestamp'], errors='coerce')
    print(f"  Loaded: {len(df):,} rows ({time.time()-start:.1f}s)")

    # Group by conversation
    print(f"\n[2/3] Threading conversations (min_messages={min_messages})...")
    start = time.time()

    conversations = []
    grouped = df.groupby('conversation_id')
    total_groups = len(grouped)

    for conv_id, messages in tqdm(grouped, total=total_groups, desc="  Threading"):
        if len(messages) < min_messages:
            continue
        conv_meta = thread_conversation(conv_id, messages)
        conversations.append(conv_meta)

    conv_df = pd.DataFrame(conversations)
    print(f"  Conversations: {len(conv_df):,} (from {total_groups:,} groups)")
    print(f"  Skipped (< {min_messages} messages): {total_groups - len(conv_df):,}")
    print(f"  Time: {time.time()-start:.1f}s")

    # Save
    print(f"\n[3/3] Saving to {output_path}...")
    conv_df.to_csv(output_path, sep=';', index=False, encoding='utf-8')

    file_size_mb = Path(output_path).stat().st_size / (1024 * 1024)
    print(f"  Saved: {file_size_mb:.1f} MB")

    # Summary
    print(f"\n{'='*60}")
    print(f"SUMMARY")
    print(f"{'='*60}")
    print(f"  Total conversations: {len(conv_df):,}")

    if len(conv_df) > 0:
        print(f"\n  Outcome distribution:")
        for outcome, count in conv_df['outcome'].value_counts().items():
            pct = count / len(conv_df) * 100
            print(f"    {outcome}: {count:,} ({pct:.1f}%)")

        print(f"\n  Agent distribution:")
        for agent, count in conv_df['primary_agent'].value_counts().head(10).items():
            pct = count / len(conv_df) * 100
            try:
                print(f"    {agent}: {count:,} ({pct:.1f}%)")
            except UnicodeEncodeError:
                print(f"    {agent.encode('ascii', 'replace').decode()}: {count:,} ({pct:.1f}%)")

        print(f"\n  Avg messages/conversation: {conv_df['message_count'].mean():.1f}")
        print(f"  Avg duration (minutes): {conv_df['duration_minutes'].mean():.1f}")
        if conv_df['first_response_minutes'].notna().any():
            print(f"  Avg first response (minutes): {conv_df['first_response_minutes'].mean():.1f}")

    print(f"{'='*60}")

    return conv_df


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='WhatsApp Conversation Threader')
    parser.add_argument('--config', default='config.yaml', help='Config file path')
    args = parser.parse_args()

    os.chdir(Path(__file__).parent.parent)
    config = load_config(args.config)
    run_threader(config)
