"""
Phase 1, Step 1: CSV Cleaner
- Streaming parse (300MB CSV, chunked)
- Text normalization
- Deduplication
- Output: cleaned_messages.csv

Usage:
    python src/01_cleaner.py [--config config.yaml]
"""

import sys
import os
import argparse
import time
from pathlib import Path

import pandas as pd
import yaml
from tqdm import tqdm

# Add project root to path
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.utils.csv_parser import stream_csv, parse_timestamp, count_lines, COLUMN_NAMES
from src.utils.normalizer import normalize_text, normalize_agent_name, normalize_phone
from src.utils.deduplicator import mark_duplicates_fast


def load_config(config_path: str = 'config.yaml') -> dict:
    with open(config_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


def clean_chunk(chunk: pd.DataFrame) -> pd.DataFrame:
    """Apply cleaning transformations to a chunk."""
    df = chunk.copy()

    # Normalize text fields
    df['message_text'] = df['message_text'].apply(normalize_text)
    df['agent_name'] = df['agent_name'].apply(normalize_agent_name)
    df['business_phone'] = df['business_phone'].apply(normalize_phone)

    # Parse timestamp
    df = parse_timestamp(df)

    # Drop rows with invalid timestamps
    invalid_ts = df['timestamp'].isna()
    if invalid_ts.any():
        print(f"  [WARN] {invalid_ts.sum()} rows with invalid timestamps dropped")
        df = df[~invalid_ts]

    # Drop rows with empty messages
    empty_msg = df['message_text'].str.len() == 0
    if empty_msg.any():
        print(f"  [WARN] {empty_msg.sum()} rows with empty messages dropped")
        df = df[~empty_msg]

    # Normalize sender type
    df['sender_type'] = df['sender_type'].str.strip().str.upper()
    valid_senders = df['sender_type'].isin(['ME', 'CUSTOMER'])
    if not valid_senders.all():
        invalid_count = (~valid_senders).sum()
        print(f"  [WARN] {invalid_count} rows with invalid sender_type dropped")
        df = df[valid_senders]

    # Ensure conversation_id is string (for groupby consistency)
    df['conversation_id'] = df['conversation_id'].astype(str).str.strip()

    # Set agent_name to None for CUSTOMER messages
    df.loc[df['sender_type'] == 'CUSTOMER', 'agent_name'] = ''

    return df


def run_cleaner(config: dict):
    """Main cleaner pipeline."""
    raw_path = config['paths']['raw_csv']
    output_path = config['paths']['cleaned_csv']
    chunk_size = config['processing']['chunk_size']
    dedup_window = config['processing']['dedup_window_seconds']

    print(f"{'='*60}")
    print(f"WhatsApp Cleaner - {config['tenant']['name']}")
    print(f"{'='*60}")

    # Ensure output directory exists
    Path(output_path).parent.mkdir(parents=True, exist_ok=True)

    # Count total lines for progress bar
    print(f"\n[1/4] Counting lines in {raw_path}...")
    start = time.time()
    total_lines = count_lines(raw_path)
    print(f"  Total lines: {total_lines:,} ({time.time()-start:.1f}s)")

    # Process chunks
    print(f"\n[2/4] Streaming parse + clean (chunk_size={chunk_size:,})...")
    all_chunks = []
    total_processed = 0
    total_dropped = 0

    chunks_iter = stream_csv(raw_path, chunk_size=chunk_size, delimiter=config['csv']['delimiter'])
    num_chunks = (total_lines // chunk_size) + 1

    for chunk in tqdm(chunks_iter, total=num_chunks, desc="  Cleaning"):
        original_len = len(chunk)
        cleaned = clean_chunk(chunk)
        total_processed += original_len
        total_dropped += (original_len - len(cleaned))
        all_chunks.append(cleaned)

    print(f"  Processed: {total_processed:,} rows")
    print(f"  Dropped (invalid): {total_dropped:,} rows")

    # Concatenate all chunks
    print(f"\n[3/4] Concatenating + deduplicating...")
    start = time.time()
    df = pd.concat(all_chunks, ignore_index=True)
    print(f"  Concatenated: {len(df):,} rows ({time.time()-start:.1f}s)")

    # Sort by conversation_id + timestamp
    df = df.sort_values(['conversation_id', 'timestamp']).reset_index(drop=True)

    # Deduplicate
    start = time.time()
    df = mark_duplicates_fast(df, window_seconds=dedup_window)
    dup_count = df['is_duplicate'].sum()
    dup_pct = (dup_count / len(df)) * 100
    print(f"  Duplicates found: {dup_count:,} ({dup_pct:.1f}%)")
    print(f"  Dedup time: {time.time()-start:.1f}s")

    # Remove duplicates
    df_clean = df[~df['is_duplicate']].drop(columns=['is_duplicate'])
    print(f"  After dedup: {len(df_clean):,} rows")

    # Save cleaned output
    print(f"\n[4/4] Saving to {output_path}...")
    start = time.time()

    # Select output columns
    output_cols = [
        'business_phone', 'date', 'time', 'conversation_id',
        'message_text', 'sender_type', 'agent_name', 'timestamp'
    ]
    df_clean[output_cols].to_csv(
        output_path,
        sep=';',
        index=False,
        encoding='utf-8'
    )

    file_size_mb = Path(output_path).stat().st_size / (1024 * 1024)
    print(f"  Saved: {file_size_mb:.1f} MB ({time.time()-start:.1f}s)")

    # Summary
    print(f"\n{'='*60}")
    print(f"SUMMARY")
    print(f"{'='*60}")
    print(f"  Input:        {total_processed:,} rows")
    print(f"  Invalid:      {total_dropped:,} rows")
    print(f"  Duplicates:   {dup_count:,} rows ({dup_pct:.1f}%)")
    print(f"  Clean output: {len(df_clean):,} rows")
    print(f"  Output file:  {output_path} ({file_size_mb:.1f} MB)")
    print(f"{'='*60}")

    return df_clean


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='WhatsApp CSV Cleaner')
    parser.add_argument('--config', default='config.yaml', help='Config file path')
    args = parser.parse_args()

    os.chdir(Path(__file__).parent.parent)
    config = load_config(args.config)
    run_cleaner(config)
