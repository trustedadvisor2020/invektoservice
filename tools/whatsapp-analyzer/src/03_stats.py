"""
Phase 1, Step 3: Basic Statistics
- Generate metadata.json with key statistics
- Agent summary, timeline, volume metrics
- Output: metadata.json

Usage:
    python src/03_stats.py [--config config.yaml]
"""

import sys
import os
import argparse
import json
import time
from pathlib import Path
from collections import Counter

import pandas as pd
import numpy as np
import yaml

sys.path.insert(0, str(Path(__file__).parent.parent))


def load_config(config_path: str = 'config.yaml') -> dict:
    with open(config_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


def run_stats(config: dict):
    """Generate comprehensive statistics."""
    cleaned_path = config['paths']['cleaned_csv']
    conv_path = config['paths']['conversations_csv']
    output_path = config['paths']['metadata_json']

    print(f"{'='*60}")
    print(f"WhatsApp Stats - {config['tenant']['name']}")
    print(f"{'='*60}")

    Path(output_path).parent.mkdir(parents=True, exist_ok=True)

    # Load data
    print(f"\n[1/3] Loading data...")
    start = time.time()
    messages_df = pd.read_csv(cleaned_path, sep=';', encoding='utf-8', dtype=str)
    messages_df['timestamp'] = pd.to_datetime(messages_df['timestamp'], errors='coerce')

    conv_df = pd.read_csv(conv_path, sep=';', encoding='utf-8', dtype=str)
    conv_df['start_time'] = pd.to_datetime(conv_df['start_time'], errors='coerce')
    conv_df['end_time'] = pd.to_datetime(conv_df['end_time'], errors='coerce')

    # Convert numeric columns
    for col in ['duration_minutes', 'message_count', 'customer_message_count', 'agent_message_count']:
        conv_df[col] = pd.to_numeric(conv_df[col], errors='coerce').fillna(0).astype(int)
    conv_df['first_response_minutes'] = pd.to_numeric(conv_df['first_response_minutes'], errors='coerce')

    print(f"  Messages: {len(messages_df):,}")
    print(f"  Conversations: {len(conv_df):,}")
    print(f"  Load time: {time.time()-start:.1f}s")

    # Calculate stats
    print(f"\n[2/3] Calculating statistics...")

    # --- Message-level stats ---
    me_msgs = messages_df[messages_df['sender_type'] == 'ME']
    cust_msgs = messages_df[messages_df['sender_type'] == 'CUSTOMER']

    # Date range
    valid_ts = messages_df['timestamp'].dropna()
    date_range = {
        'start': str(valid_ts.min().date()) if len(valid_ts) > 0 else None,
        'end': str(valid_ts.max().date()) if len(valid_ts) > 0 else None,
        'total_days': (valid_ts.max() - valid_ts.min()).days if len(valid_ts) > 0 else 0,
    }

    # Unique counts
    unique_phones = messages_df['business_phone'].nunique()
    unique_conversations = messages_df['conversation_id'].nunique()
    unique_agents = me_msgs['agent_name'].nunique()

    # Message lengths
    messages_df['_msg_len'] = messages_df['message_text'].str.len()
    avg_msg_len_me = me_msgs['message_text'].str.len().mean()
    avg_msg_len_customer = cust_msgs['message_text'].str.len().mean()

    # Hourly distribution
    messages_df['_hour'] = messages_df['timestamp'].dt.hour
    hourly = messages_df.groupby('_hour').size().to_dict()
    peak_hours = sorted(hourly.items(), key=lambda x: x[1], reverse=True)[:5]

    # Daily distribution
    messages_df['_weekday'] = messages_df['timestamp'].dt.day_name()
    daily = messages_df.groupby('_weekday').size().to_dict()

    # Monthly volume
    messages_df['_month'] = messages_df['timestamp'].dt.to_period('M').astype(str)
    monthly = messages_df.groupby('_month').size().to_dict()

    # --- Agent stats ---
    agent_stats = []
    for agent_name, agent_msgs in me_msgs.groupby('agent_name'):
        if not agent_name or agent_name == '':
            continue
        agent_convs = conv_df[conv_df['primary_agent'] == agent_name]
        agent_stats.append({
            'name': agent_name,
            'message_count': int(len(agent_msgs)),
            'conversation_count': int(len(agent_convs)),
            'avg_message_length': round(agent_msgs['message_text'].str.len().mean(), 1),
        })
    agent_stats.sort(key=lambda x: x['message_count'], reverse=True)

    # --- Conversation stats ---
    outcome_dist = conv_df['outcome'].value_counts().to_dict()
    outcome_dist = {k: int(v) for k, v in outcome_dist.items()}

    avg_conv_duration = round(conv_df['duration_minutes'].mean(), 1)
    avg_messages_per_conv = round(conv_df['message_count'].mean(), 1)
    avg_first_response = round(conv_df['first_response_minutes'].mean(), 1) if conv_df['first_response_minutes'].notna().any() else None

    # Product codes
    all_product_codes = []
    for codes in conv_df['product_codes'].dropna():
        if codes:
            all_product_codes.extend(codes.split('|'))
    product_code_counts = Counter(all_product_codes).most_common(20)

    # Build metadata
    metadata = {
        'tenant': config['tenant'],
        'generated_at': pd.Timestamp.now().isoformat(),
        'messages': {
            'total': int(len(messages_df)),
            'agent_messages': int(len(me_msgs)),
            'customer_messages': int(len(cust_msgs)),
            'agent_customer_ratio': round(len(me_msgs) / max(len(cust_msgs), 1), 2),
            'avg_message_length_agent': round(avg_msg_len_me, 1),
            'avg_message_length_customer': round(avg_msg_len_customer, 1),
        },
        'conversations': {
            'total': int(len(conv_df)),
            'avg_messages_per_conversation': avg_messages_per_conv,
            'avg_duration_minutes': avg_conv_duration,
            'avg_first_response_minutes': avg_first_response,
            'outcome_distribution': outcome_dist,
        },
        'date_range': date_range,
        'unique_counts': {
            'business_phones': int(unique_phones),
            'conversations': int(unique_conversations),
            'agents': int(unique_agents),
        },
        'agents': agent_stats,
        'temporal': {
            'peak_hours': [{'hour': int(h), 'message_count': int(c)} for h, c in peak_hours],
            'daily_distribution': {k: int(v) for k, v in daily.items()},
            'monthly_volume': {k: int(v) for k, v in sorted(monthly.items())},
        },
        'products': {
            'top_product_codes': [
                {'code': code, 'mentions': int(count)}
                for code, count in product_code_counts
            ],
            'total_unique_products': len(set(all_product_codes)),
        }
    }

    # Save
    print(f"\n[3/3] Saving metadata to {output_path}...")
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(metadata, f, ensure_ascii=False, indent=2)

    # Print summary
    print(f"\n{'='*60}")
    print(f"STATISTICS SUMMARY")
    print(f"{'='*60}")
    print(f"  Date range: {date_range['start']} -> {date_range['end']} ({date_range['total_days']} days)")
    print(f"  Messages: {len(messages_df):,} (ME: {len(me_msgs):,}, CUSTOMER: {len(cust_msgs):,})")
    print(f"  Conversations: {len(conv_df):,}")
    print(f"  Agents: {unique_agents}")
    print(f"  Business phones: {unique_phones}")
    print(f"\n  Avg message length: Agent={avg_msg_len_me:.0f} chars, Customer={avg_msg_len_customer:.0f} chars")
    print(f"  Avg conversation: {avg_messages_per_conv} messages, {avg_conv_duration} minutes")
    if avg_first_response:
        print(f"  Avg first response: {avg_first_response} minutes")
    print(f"\n  Outcomes:")
    for outcome, count in sorted(outcome_dist.items(), key=lambda x: x[1], reverse=True):
        pct = count / len(conv_df) * 100
        print(f"    {outcome}: {count:,} ({pct:.1f}%)")
    print(f"\n  Peak hours: {', '.join(f'{h}:00 ({c:,})' for h, c in peak_hours)}")
    print(f"  Unique products: {len(set(all_product_codes)):,}")
    print(f"\n  Top 10 products: {', '.join(code for code, _ in product_code_counts[:10])}")
    print(f"\n  Agents:")
    for a in agent_stats:
        print(f"    {a['name']}: {a['message_count']:,} msgs, {a['conversation_count']:,} convs")
    print(f"{'='*60}")

    return metadata


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='WhatsApp Basic Statistics')
    parser.add_argument('--config', default='config.yaml', help='Config file path')
    args = parser.parse_args()

    os.chdir(Path(__file__).parent.parent)
    config = load_config(args.config)
    run_stats(config)
