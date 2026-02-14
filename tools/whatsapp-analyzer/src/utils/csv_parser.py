"""
Streaming CSV parser for WhatsApp conversation exports.
Handles large files (300MB+) without loading into memory.
"""

import pandas as pd
from pathlib import Path
from tqdm import tqdm


COLUMN_NAMES = [
    'business_phone',
    'date',
    'time',
    'conversation_id',
    'message_text',
    'sender_type',
    'agent_name'
]

DTYPES = {
    'business_phone': str,
    'conversation_id': str,
    'message_text': str,
    'sender_type': str,
    'agent_name': str
}


def count_lines(filepath: str) -> int:
    """Fast line count without loading file into memory."""
    count = 0
    with open(filepath, 'rb') as f:
        for _ in f:
            count += 1
    return count


def stream_csv(filepath: str, chunk_size: int = 100_000, delimiter: str = ';'):
    """
    Yield DataFrames in chunks from a large CSV file.

    Args:
        filepath: Path to CSV file
        chunk_size: Number of rows per chunk
        delimiter: Column delimiter

    Yields:
        pd.DataFrame chunks
    """
    filepath = Path(filepath)
    if not filepath.exists():
        raise FileNotFoundError(f"CSV not found: {filepath}")

    reader = pd.read_csv(
        filepath,
        sep=delimiter,
        header=None,
        names=COLUMN_NAMES,
        dtype=DTYPES,
        encoding='utf-8-sig',
        chunksize=chunk_size,
        on_bad_lines='warn',
        quotechar='"',
        engine='python'
    )

    for chunk in reader:
        yield chunk


def load_csv_full(filepath: str, delimiter: str = ';') -> pd.DataFrame:
    """
    Load entire CSV into memory. Only use for smaller files or after cleaning.
    """
    return pd.read_csv(
        filepath,
        sep=delimiter,
        header=None,
        names=COLUMN_NAMES,
        dtype=DTYPES,
        encoding='utf-8-sig',
        quotechar='"',
        on_bad_lines='warn',
        engine='python'
    )


def parse_timestamp(df: pd.DataFrame) -> pd.DataFrame:
    """Combine date + time columns into a single timestamp column."""
    df = df.copy()
    df['timestamp'] = pd.to_datetime(
        df['date'] + ' ' + df['time'],
        format='%Y-%m-%d %H:%M:%S',
        errors='coerce'
    )
    return df
