"""
Phase 2, Step 5: FAQ Extractor
- Mines Q&A pairs from customer-agent conversations
- Customer question -> agent answer pairing
- MiniBatchKMeans clustering of similar questions (O(N*sqrt(N)))
- Output: data/nlp/faq_pairs.csv, data/nlp/faq_clusters.json

Usage:
    python src/05_faq_extractor.py [--config config.yaml] [--limit N]
"""

import sys
import os
import argparse
import json
import re
import time
from pathlib import Path
from collections import defaultdict

import pandas as pd
import numpy as np
import yaml
from tqdm import tqdm
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.cluster import MiniBatchKMeans
from sklearn.metrics.pairwise import cosine_similarity

sys.path.insert(0, str(Path(__file__).parent.parent))


def load_config(config_path: str = 'config.yaml') -> dict:
    with open(config_path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)


# Turkish question indicators
QUESTION_PATTERNS = [
    r'\?',                          # Explicit question mark
    r'\b(kaç|kaçtır|ne kadar)\b',    # How much
    r'\b(var mı|varmı)\b',           # Is there / available?
    r'\b(nasıl|nası)\b',             # How
    r'\b(ne zaman|nezaman)\b',      # When
    r'\b(nerede|nere)\b',           # Where
    r'\b(hangisi|hangi)\b',         # Which
    r'\b(olur mu|olurmu)\b',        # Can it be / is it ok?
    r'\b(mümkün mü|mümkünmü)\b',    # Is it possible?
    r'\b(gönderir misiniz)\b',       # Would you send?
    r'\b(ister misiniz)\b',         # Would you like?
    r'\b(beden|numara)\b.*\b(kaç|ne)\b',   # Size question
]

QUESTION_REGEX = re.compile('|'.join(QUESTION_PATTERNS), re.IGNORECASE)

# Turkish stopwords for TF-IDF
TR_STOPWORDS = [
    'bir', 'bu', 'da', 'de', 've', 'ile', 'için', 'gibi', 'çok',
    'var', 'ben', 'sen', 'biz', 'siz', 'ne', 'ama', 'ya', 'ki',
    'mi', 'mı', 'mu', 'mü', 'daha', 'en', 'olan', 'olarak',
    'iyi', 'güzel', 'evet', 'hayır', 'tamam', 'olur', 'abi',
    'hocam', 'merhaba', 'selam', 'teşekkür', 'ederim', 'kolay',
    'gelsin', 'günler', 'merhabalar',
]


def is_question(text: str) -> bool:
    """Check if text is likely a question."""
    if not text or not isinstance(text, str):
        return False
    return bool(QUESTION_REGEX.search(text))


def extract_qa_pairs(messages_df: pd.DataFrame) -> list[dict]:
    """
    Extract question-answer pairs from conversations.
    Logic: CUSTOMER message (question) -> next ME message with >=10 chars (answer).
    Skips short agent responses and keeps looking within 5-message window.
    """
    pairs = []

    for conv_id, conv_msgs in tqdm(
        messages_df.groupby('conversation_id'),
        desc="  Extracting Q&A"
    ):
        conv_msgs = conv_msgs.sort_values('timestamp')
        rows = conv_msgs.to_dict('records')

        for i, msg in enumerate(rows):
            # Only look at CUSTOMER messages that look like questions
            if msg['sender_type'] != 'CUSTOMER':
                continue
            if not is_question(msg['message_text']):
                continue

            question_text = msg['message_text'].strip()
            # Skip very short questions (single word like "?")
            if len(question_text) < 5:
                continue

            # Find next agent response (skip short ones, keep looking)
            answer_text = None
            for j in range(i + 1, min(i + 5, len(rows))):
                if rows[j]['sender_type'] == 'ME':
                    candidate = rows[j]['message_text'].strip()
                    if len(candidate) >= 10:
                        answer_text = candidate
                        break
                    # Short response (<10 chars) - keep looking for better answer

            if answer_text:
                pairs.append({
                    'conversation_id': conv_id,
                    'question': question_text[:500],
                    'answer': answer_text[:500],
                    'question_len': len(question_text),
                    'answer_len': len(answer_text),
                })

    return pairs


def cluster_questions(pairs: list[dict], min_cluster_size: int = 3) -> dict:
    """
    Cluster similar questions using TF-IDF + MiniBatchKMeans.
    O(N * sqrt(N)) complexity instead of O(N^2) brute-force cosine similarity.
    Returns cluster_id -> cluster info mapping.
    """
    if not pairs:
        return {}

    questions = [p['question'] for p in pairs]

    # TF-IDF vectorization
    vectorizer = TfidfVectorizer(
        max_features=5000,
        stop_words=TR_STOPWORDS,
        min_df=2,
        max_df=0.8,
        ngram_range=(1, 2),
    )

    try:
        tfidf_matrix = vectorizer.fit_transform(questions)
    except ValueError as e:
        print(f"  [WARN] TF-IDF failed (possibly too few unique terms): {e}")
        return {}

    # Auto-determine cluster count: sqrt(N), capped at 5000
    n = len(questions)
    n_clusters = min(int(n ** 0.5), 5000)
    n_clusters = max(n_clusters, 10)  # Minimum 10 clusters
    n_clusters = min(n_clusters, n)   # Cannot exceed sample count

    # MiniBatchKMeans - O(N * K * iterations)
    kmeans = MiniBatchKMeans(
        n_clusters=n_clusters,
        batch_size=1000,
        random_state=42,
        n_init=3,
    )
    labels = kmeans.fit_predict(tfidf_matrix)

    # Group pairs by cluster label
    cluster_groups = defaultdict(list)
    for i, label in enumerate(labels):
        cluster_groups[label].append(i)

    # Build output: filter small clusters, pick representative
    clusters = {}
    cluster_id = 0
    for label, indices in sorted(cluster_groups.items(), key=lambda x: -len(x[1])):
        if len(indices) < min_cluster_size:
            continue

        # Pick representative: closest to cluster centroid
        centroid = kmeans.cluster_centers_[label]
        member_vectors = tfidf_matrix[indices]
        dists = cosine_similarity(member_vectors, centroid.reshape(1, -1)).flatten()
        representative_local = np.argmax(dists)
        representative_idx = indices[representative_local]

        clusters[cluster_id] = {
            'representative_question': pairs[representative_idx]['question'],
            'question_count': len(indices),
            'sample_questions': [pairs[j]['question'] for j in indices[:5]],
            'sample_answers': [pairs[j]['answer'] for j in indices[:3]],
        }
        cluster_id += 1

    return clusters


def run_faq_extractor(config: dict, limit: int | None = None):
    """Main FAQ extraction pipeline."""
    input_path = config['paths']['cleaned_csv']
    output_dir = config['paths']['nlp_dir']
    pairs_path = os.path.join(output_dir, 'faq_pairs.csv')
    clusters_path = os.path.join(output_dir, 'faq_clusters.json')

    print(f"{'='*60}")
    print(f"FAQ Extractor - {config['tenant']['name']}")
    print(f"{'='*60}")

    Path(output_dir).mkdir(parents=True, exist_ok=True)

    # Load data
    print(f"\n[1/4] Loading cleaned messages...")
    start = time.time()
    df = pd.read_csv(input_path, sep=';', encoding='utf-8', dtype=str)
    df['timestamp'] = pd.to_datetime(df['timestamp'], errors='coerce')
    if limit:
        # Limit by conversation count, not message count
        conv_ids = df['conversation_id'].unique()[:limit]
        df = df[df['conversation_id'].isin(conv_ids)]
    print(f"  Loaded: {len(df):,} messages ({time.time()-start:.1f}s)")

    # Extract Q&A pairs
    print(f"\n[2/4] Extracting Q&A pairs...")
    start = time.time()
    pairs = extract_qa_pairs(df)
    print(f"  Q&A pairs found: {len(pairs):,}")
    print(f"  Time: {time.time()-start:.1f}s")

    # Save pairs
    print(f"\n[3/4] Saving pairs to {pairs_path}...")
    pairs_df = pd.DataFrame(pairs)
    pairs_df.to_csv(pairs_path, sep=';', index=False, encoding='utf-8')
    file_size_mb = Path(pairs_path).stat().st_size / (1024 * 1024)
    print(f"  Saved: {file_size_mb:.1f} MB ({len(pairs):,} pairs)")

    # Cluster similar questions
    print(f"\n[4/4] Clustering similar questions (MiniBatchKMeans)...")
    start = time.time()
    clusters = cluster_questions(pairs)
    print(f"  Clusters found: {len(clusters)}")
    print(f"  Time: {time.time()-start:.1f}s")

    # Save clusters
    with open(clusters_path, 'w', encoding='utf-8') as f:
        json.dump(clusters, f, ensure_ascii=False, indent=2)
    print(f"  Clusters saved to {clusters_path}")

    # Summary
    print(f"\n{'='*60}")
    print(f"FAQ EXTRACTION SUMMARY")
    print(f"{'='*60}")
    print(f"  Q&A pairs: {len(pairs):,}")
    print(f"  Question clusters: {len(clusters)}")

    if pairs:
        avg_q_len = sum(p['question_len'] for p in pairs) / len(pairs)
        avg_a_len = sum(p['answer_len'] for p in pairs) / len(pairs)
        print(f"  Avg question length: {avg_q_len:.0f} chars")
        print(f"  Avg answer length: {avg_a_len:.0f} chars")

    if clusters:
        print(f"\n  Top 10 FAQ clusters:")
        sorted_clusters = sorted(
            clusters.items(),
            key=lambda x: x[1]['question_count'],
            reverse=True
        )
        for cid, cluster in sorted_clusters[:10]:
            q = cluster['representative_question'][:80]
            try:
                print(f"    [{cluster['question_count']:,}x] {q}")
            except UnicodeEncodeError:
                print(f"    [{cluster['question_count']:,}x] (encoding error)")

    print(f"{'='*60}")

    return pairs_df, clusters


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='WhatsApp FAQ Extractor')
    parser.add_argument('--config', default='config.yaml', help='Config file path')
    parser.add_argument('--limit', type=int, default=None, help='Limit conversations to process')
    args = parser.parse_args()

    os.chdir(Path(__file__).parent.parent)
    config = load_config(args.config)
    run_faq_extractor(config, limit=args.limit)
