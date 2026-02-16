/**
 * MCP Codex Review v2.0 - Response Parser
 * InvektoServices
 * Q: 2026-02-16
 *
 * 3-level fallback: strict regex -> heuristic -> raw fallback.
 */

import type {
  CodexReviewResult,
  CodeQualityGate,
  CQResult,
  CoVeResult,
  Verdict,
  VerificationQuestion,
  TokenUsage,
} from "./types.js";

const UNKNOWN_CQ: CQResult = { result: "UNKNOWN", evidence: "" };

// ─── Main Parser ──────────────────────────────────────────────

export function parseCodexResponse(
  responseText: string,
  questions: VerificationQuestion[],
  modelUsed: string,
  tokenUsage: TokenUsage,
): CodexReviewResult {
  const cqGate = parseCQGate(responseText);
  const coveResults = parseCoVe(responseText, questions);
  const overallVerdict = parseOverallVerdict(responseText, cqGate, coveResults);
  const blockingIssues = parseBlockingIssues(responseText);
  const summary = parseSummary(responseText);

  return {
    verdict: overallVerdict,
    code_quality_gate: cqGate,
    cove_verification: coveResults,
    blocking_issues: blockingIssues,
    summary,
    raw_response: responseText,
    model_used: modelUsed,
    token_usage: tokenUsage,
  };
}

// ─── CQ Gate Parser ───────────────────────────────────────────

function parseCQGate(text: string): CodeQualityGate {
  const results: Record<string, CQResult> = {};

  // Strict regex: match each CQ block
  const cqPattern = /CQ(\d):\s*"?(.+?)"?\s*\nResult:\s*(PASS|FAIL|UNKNOWN)\s*\nEvidence:\s*(.+?)(?=\n\nCQ\d|CODE_QUALITY_VERDICT|=== COVE|$)/gs;
  let match: RegExpExecArray | null;

  while ((match = cqPattern.exec(text)) !== null) {
    const num = match[1];
    results[`CQ${num}`] = {
      result: match[3] as Verdict,
      evidence: match[4].trim(),
    };
  }

  // Heuristic fallback for missing CQs
  if (Object.keys(results).length < 8) {
    for (let i = 1; i <= 8; i++) {
      const key = `CQ${i}`;
      if (!results[key]) {
        results[key] = heuristicCQ(text, i);
      }
    }
  }

  // Parse overall CQ verdict
  const cqVerdictMatch = text.match(/CODE_QUALITY_VERDICT:\s*(PASS|FAIL)/);
  let overall: "PASS" | "FAIL" = "FAIL";
  if (cqVerdictMatch) {
    overall = cqVerdictMatch[1] as "PASS" | "FAIL";
  } else {
    // Derive: any FAIL or UNKNOWN = FAIL
    overall = Object.values(results).every(r => r.result === "PASS") ? "PASS" : "FAIL";
  }

  return {
    CQ1: results.CQ1 || UNKNOWN_CQ,
    CQ2: results.CQ2 || UNKNOWN_CQ,
    CQ3: results.CQ3 || UNKNOWN_CQ,
    CQ4: results.CQ4 || UNKNOWN_CQ,
    CQ5: results.CQ5 || UNKNOWN_CQ,
    CQ6: results.CQ6 || UNKNOWN_CQ,
    CQ7: results.CQ7 || UNKNOWN_CQ,
    CQ8: results.CQ8 || UNKNOWN_CQ,
    overall,
  };
}

/** Heuristic: look for CQ{n} near a PASS/FAIL keyword */
function heuristicCQ(text: string, num: number): CQResult {
  const pattern = new RegExp(`CQ${num}[^\\n]*?(PASS|FAIL|UNKNOWN)`, "i");
  const match = text.match(pattern);
  if (match) {
    return { result: match[1].toUpperCase() as Verdict, evidence: "(heuristic parse)" };
  }
  return UNKNOWN_CQ;
}

// ─── CoVe Parser ──────────────────────────────────────────────

function parseCoVe(
  text: string,
  questions: VerificationQuestion[],
): Record<string, CoVeResult> {
  const results: Record<string, CoVeResult> = {};

  // Strict regex for each question
  const covePattern = /(Q\d+):\s*(.+?)\s*\nResult:\s*(PASS|FAIL|UNKNOWN)\s*\nReasoning:\s*(.+?)(?=\n\nQ\d+|COVE_VERDICT|=== FINAL|$)/gs;
  let match: RegExpExecArray | null;

  while ((match = covePattern.exec(text)) !== null) {
    results[match[1]] = {
      result: match[3] as Verdict,
      reasoning: match[4].trim(),
    };
  }

  // Heuristic fallback for missing questions
  for (const q of questions) {
    if (!results[q.id]) {
      results[q.id] = heuristicCoVe(text, q.id);
    }
  }

  return results;
}

/** Heuristic: look for Q{n} near a PASS/FAIL keyword */
function heuristicCoVe(text: string, id: string): CoVeResult {
  const pattern = new RegExp(`${id}[^\\n]*?(PASS|FAIL|UNKNOWN)`, "i");
  const match = text.match(pattern);
  if (match) {
    return { result: match[1].toUpperCase() as Verdict, reasoning: "(heuristic parse)" };
  }
  return { result: "UNKNOWN", reasoning: "" };
}

// ─── Verdict Parser ───────────────────────────────────────────

function parseOverallVerdict(
  text: string,
  cqGate: CodeQualityGate,
  coveResults: Record<string, CoVeResult>,
): Verdict {
  // Try explicit marker first
  const match = text.match(/OVERALL_VERDICT:\s*(PASS|FAIL|UNKNOWN)/);
  if (match) {
    return match[1] as Verdict;
  }

  // Derive from components
  if (cqGate.overall === "FAIL") return "FAIL";

  const coveValues = Object.values(coveResults);
  if (coveValues.some(v => v.result === "FAIL" || v.result === "UNKNOWN")) return "FAIL";

  const cqValues = [cqGate.CQ1, cqGate.CQ2, cqGate.CQ3, cqGate.CQ4, cqGate.CQ5, cqGate.CQ6, cqGate.CQ7, cqGate.CQ8];
  if (cqValues.some(v => v.result === "FAIL" || v.result === "UNKNOWN")) return "FAIL";

  if (cqValues.every(v => v.result === "PASS") && coveValues.every(v => v.result === "PASS")) {
    return "PASS";
  }

  return "UNKNOWN";
}

// ─── Blocking Issues Parser ───────────────────────────────────

function parseBlockingIssues(text: string): string[] {
  // Try structured format: BLOCKING_ISSUES: [issue1, issue2]
  const bracketMatch = text.match(/BLOCKING_ISSUES:\s*\[(.+?)\]/s);
  if (bracketMatch) {
    const inner = bracketMatch[1].trim();
    if (inner.toUpperCase() === "NONE" || inner === "") return [];
    return inner.split(",").map(s => s.trim().replace(/^["']|["']$/g, "")).filter(Boolean);
  }

  // Try NONE format
  const noneMatch = text.match(/BLOCKING_ISSUES:\s*NONE/i);
  if (noneMatch) return [];

  // Fallback: collect all FAIL CQ/Q items
  const issues: string[] = [];
  const failPattern = /(CQ\d|Q\d+).*?Result:\s*FAIL/g;
  let failMatch: RegExpExecArray | null;
  while ((failMatch = failPattern.exec(text)) !== null) {
    issues.push(failMatch[1]);
  }

  return issues;
}

// ─── Summary Parser ───────────────────────────────────────────

function parseSummary(text: string): string {
  const match = text.match(/SUMMARY:\s*(.+)/);
  if (match) return match[1].trim();

  // Fallback: first non-empty line after OVERALL_VERDICT
  const afterVerdict = text.split(/OVERALL_VERDICT:/)[1];
  if (afterVerdict) {
    const lines = afterVerdict.split("\n").filter(l => l.trim() && !l.includes("BLOCKING"));
    if (lines.length > 0) return lines[0].trim();
  }

  return "Review completed (summary not parsed)";
}
