"""
Shared Claude API client utilities.
- API key validation and client initialization
- JSON response parsing with typed errors
- Reused by 04_intent_classifier.py and 06_sentiment_analyzer.py
"""

import os
import json
import re
import logging

from dotenv import load_dotenv

logger = logging.getLogger('whatsapp-analyzer')


class ClaudeAPIError(Exception):
    """Raised on Claude API communication failures."""
    pass


class ClaudeParseError(Exception):
    """Raised when Claude response cannot be parsed as JSON."""
    pass


def get_claude_client(config: dict):
    """
    Initialize and return an Anthropic client.
    Returns (client, model_name) tuple.
    Returns (None, None) if API key not configured - logs warning.
    """
    load_dotenv()
    api_key = os.getenv('ANTHROPIC_API_KEY')

    if not api_key or api_key.startswith('sk-ant-xxxx'):
        logger.warning(
            "ANTHROPIC_API_KEY not set or placeholder. "
            "Claude classification will be skipped. "
            "Set a valid key in .env for full NLP coverage."
        )
        return None, None

    import anthropic
    client = anthropic.Anthropic(api_key=api_key)
    model = config['nlp']['claude_model']
    return client, model


def parse_json_array(response_text: str) -> list[dict]:
    """
    Parse Claude's response as a JSON array.
    Raises ClaudeParseError on failure with diagnostic info.
    """
    text = response_text.strip()

    # Direct JSON array
    if text.startswith('['):
        try:
            result = json.loads(text)
            if not isinstance(result, list):
                raise ClaudeParseError(f"Expected JSON array, got {type(result).__name__}")
            return result
        except json.JSONDecodeError as e:
            raise ClaudeParseError(f"Direct JSON parse failed: {e}") from e

    # Extract embedded JSON array from surrounding text
    json_match = re.search(r'\[.*\]', text, re.DOTALL)
    if json_match:
        try:
            result = json.loads(json_match.group())
            if not isinstance(result, list):
                raise ClaudeParseError(f"Expected JSON array, got {type(result).__name__}")
            return result
        except json.JSONDecodeError as e:
            raise ClaudeParseError(
                f"Embedded JSON parse failed: {e}. "
                f"Response prefix: {text[:100]}"
            ) from e

    raise ClaudeParseError(
        f"No JSON array found in response. "
        f"Response prefix: {text[:100]}"
    )


def call_claude_batch(client, model: str, system_prompt: str,
                      batch_text: str, max_tokens: int = 4096) -> str:
    """
    Call Claude API with system prompt and batch text.
    Returns raw response text.
    Raises ClaudeAPIError on API failures with typed context.
    """
    import anthropic

    try:
        response = client.messages.create(
            model=model,
            max_tokens=max_tokens,
            system=system_prompt,
            messages=[{"role": "user", "content": batch_text}],
        )
        return response.content[0].text.strip()
    except anthropic.APIConnectionError as e:
        raise ClaudeAPIError(f"Connection failed: {e}") from e
    except anthropic.RateLimitError as e:
        raise ClaudeAPIError(f"Rate limited: {e}") from e
    except anthropic.APIStatusError as e:
        raise ClaudeAPIError(f"API status {e.status_code}: {e.message}") from e
