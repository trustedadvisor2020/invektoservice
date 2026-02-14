"""
Turkish text normalization for WhatsApp messages.
Handles encoding quirks, emoji, whitespace, and Turkish-specific characters.
"""

import re
import unicodedata


def normalize_text(text: str) -> str:
    """
    Clean and normalize message text.
    - Strip whitespace and quotes
    - Normalize unicode (NFC)
    - Collapse multiple spaces
    - Remove zero-width chars
    """
    if not text or not isinstance(text, str):
        return ''

    # Unicode NFC normalization
    text = unicodedata.normalize('NFC', text)

    # Strip surrounding quotes and whitespace
    text = text.strip().strip('"').strip()

    # Remove zero-width chars
    text = re.sub(r'[\u200b\u200c\u200d\ufeff]', '', text)

    # Collapse multiple whitespace
    text = re.sub(r'\s+', ' ', text)

    return text.strip()


def normalize_agent_name(name: str) -> str:
    """
    Clean agent name: strip quotes, whitespace, standardize.
    """
    if not name or not isinstance(name, str):
        return ''

    name = name.strip().strip('"').strip()
    # Collapse multiple spaces
    name = re.sub(r'\s+', ' ', name)
    return name


def normalize_phone(phone: str) -> str:
    """
    Standardize phone number format.
    Remove non-digit chars, ensure country code.
    """
    if not phone or not isinstance(phone, str):
        return ''

    # Keep only digits
    digits = re.sub(r'\D', '', phone)

    # Turkish numbers: ensure 90 prefix
    if len(digits) == 10 and digits.startswith('5'):
        digits = '90' + digits
    elif len(digits) == 11 and digits.startswith('05'):
        digits = '9' + digits

    return digits


def clean_for_comparison(text: str) -> str:
    """
    Aggressive normalization for comparison/dedup.
    Lowercases, removes punctuation, normalizes Turkish chars.
    """
    text = normalize_text(text).lower()

    # Turkish char normalization for comparison
    replacements = {
        'İ': 'i', 'I': 'i',
        'Ş': 's', 'ş': 's',
        'Ğ': 'g', 'ğ': 'g',
        'Ü': 'u', 'ü': 'u',
        'Ö': 'o', 'ö': 'o',
        'Ç': 'c', 'ç': 'c',
        'ı': 'i',
    }
    for old, new in replacements.items():
        text = text.replace(old, new)

    # Remove punctuation
    text = re.sub(r'[^\w\s]', '', text)
    text = re.sub(r'\s+', ' ', text)

    return text.strip()


def extract_urls(text: str) -> list[str]:
    """Extract URLs from message text."""
    if not text:
        return []
    return re.findall(r'https?://[^\s\'"]+', text)


def extract_phone_numbers(text: str) -> list[str]:
    """Extract phone numbers from message text."""
    if not text:
        return []
    # Turkish phone patterns
    patterns = [
        r'0\d{3}\s?\d{3}\s?\d{2}\s?\d{2}',  # 0532 123 45 67
        r'\+90\d{10}',                          # +905321234567
        r'05\d{9}',                              # 05321234567
    ]
    results = []
    for pattern in patterns:
        results.extend(re.findall(pattern, text))
    return results
