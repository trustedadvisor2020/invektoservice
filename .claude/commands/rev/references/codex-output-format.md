# Codex Output Format

Codex produces exactly 2 blocks:

## Block 1: Code Quality Gate

```
=== CODE QUALITY GATE ===

CQ1: "Where is error catching and user feedback?"
Result: PASS | FAIL | UNKNOWN
Evidence: {file:line + message format}

CQ2: "Can this produce a silent failure?"
Result: PASS | FAIL | UNKNOWN
Evidence: {catch blocks + broad try-catch + early-return}

CQ3: "Is the diff minimal? Any out-of-scope refactoring?"
Result: PASS | FAIL | UNKNOWN
Evidence: {changed file/line count}

CQ4: "Does this code already exist in the codebase? (duplicate)"
Result: PASS | FAIL | UNKNOWN
Evidence: {grep/search result}

CQ5: "Does it follow codebase patterns?"
Result: PASS | FAIL | UNKNOWN
Evidence: {naming, error handling, file structure}

CQ6: "Performance issue? (O(n^2), N+1 query, memory leak)"
Result: PASS | FAIL | UNKNOWN
Evidence: {nested loops, in-loop queries, unclosed resources}

CQ7: "New TODO/HACK/FIXME added?"
Result: PASS | FAIL | UNKNOWN
Evidence: {new tech debt markers}

CQ8: "Breaking change? (API contract, export, shared type)"
Result: PASS | FAIL | UNKNOWN
Evidence: {removed exports, changed interfaces}

CODE QUALITY VERDICT: PASS | FAIL
```

## Block 2: CoVe Verification

```
=== COVE VERIFICATION ===

Q1: {verification question}
Result: PASS | FAIL | UNKNOWN
Reasoning: {brief, concrete explanation}

Q2: {verification question}
Result: PASS | FAIL | UNKNOWN
Reasoning: {brief, concrete explanation}

Q3: {verification question}
Result: PASS | FAIL | UNKNOWN
Reasoning: {brief, concrete explanation}

CoVe VERDICT: PASS | FAIL
```

## Combined Verdict

```
=== VERDICT ===

OVERALL: PASS | FAIL | UNKNOWN
BLOCKING ISSUES: [list or "None"]
```
