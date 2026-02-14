"""
Hash-based duplicate detection for WhatsApp messages.
Detects near-identical messages sent within a time window.
"""

import hashlib
import pandas as pd


def compute_message_hash(conversation_id: str, message_text: str) -> str:
    """
    SHA256 hash of conversation_id + normalized message text.
    Timestamp NOT included - we want to catch duplicates sent seconds apart.
    """
    normalized = (message_text or '').strip().lower()
    key = f"{conversation_id}|{normalized}"
    return hashlib.sha256(key.encode('utf-8')).hexdigest()[:16]


def mark_duplicates(df: pd.DataFrame, window_seconds: int = 5) -> pd.DataFrame:
    """
    Mark duplicate messages within a conversation.

    A message is duplicate if:
    - Same conversation_id
    - Same message text (case-insensitive)
    - Within window_seconds of another identical message

    Args:
        df: DataFrame with conversation_id, message_text, timestamp columns
        window_seconds: Time window for duplicate detection

    Returns:
        DataFrame with 'is_duplicate' boolean column added
    """
    df = df.copy()
    df['_msg_hash'] = df.apply(
        lambda r: compute_message_hash(
            str(r['conversation_id']),
            str(r['message_text'])
        ),
        axis=1
    )

    df = df.sort_values(['conversation_id', 'timestamp'])
    df['is_duplicate'] = False

    prev_hash = None
    prev_ts = None
    prev_conv = None

    for idx in df.index:
        curr_hash = df.at[idx, '_msg_hash']
        curr_ts = df.at[idx, 'timestamp']
        curr_conv = df.at[idx, 'conversation_id']

        if (curr_conv == prev_conv
                and curr_hash == prev_hash
                and prev_ts is not None
                and curr_ts is not None):
            try:
                diff = abs((curr_ts - prev_ts).total_seconds())
                if diff <= window_seconds:
                    df.at[idx, 'is_duplicate'] = True
                    continue  # Don't update prev_ for duplicates
            except (TypeError, ValueError):
                pass

        prev_hash = curr_hash
        prev_ts = curr_ts
        prev_conv = curr_conv

    df.drop(columns=['_msg_hash'], inplace=True)
    return df


def mark_duplicates_fast(df: pd.DataFrame, window_seconds: int = 5) -> pd.DataFrame:
    """
    Vectorized duplicate detection - much faster for large DataFrames.
    Uses groupby + shift to detect consecutive identical messages.
    """
    df = df.copy()
    df['_msg_hash'] = df.apply(
        lambda r: compute_message_hash(
            str(r['conversation_id']),
            str(r['message_text'])
        ),
        axis=1
    )

    df = df.sort_values(['conversation_id', 'timestamp']).reset_index(drop=True)

    # Shift within each conversation group
    grouped = df.groupby('conversation_id')
    df['_prev_hash'] = grouped['_msg_hash'].shift(1)
    df['_prev_ts'] = grouped['timestamp'].shift(1)

    # Same hash as previous message
    same_hash = df['_msg_hash'] == df['_prev_hash']

    # Within time window
    time_diff = (df['timestamp'] - df['_prev_ts']).dt.total_seconds().abs()
    within_window = time_diff <= window_seconds

    df['is_duplicate'] = same_hash & within_window

    df.drop(columns=['_msg_hash', '_prev_hash', '_prev_ts'], inplace=True)
    return df
