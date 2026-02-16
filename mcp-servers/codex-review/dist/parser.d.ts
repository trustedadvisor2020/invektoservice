/**
 * MCP Codex Review v2.0 - Response Parser
 * InvektoServices
 * Q: 2026-02-16
 *
 * 3-level fallback: strict regex -> heuristic -> raw fallback.
 */
import type { CodexReviewResult, VerificationQuestion, TokenUsage } from "./types.js";
export declare function parseCodexResponse(responseText: string, questions: VerificationQuestion[], modelUsed: string, tokenUsage: TokenUsage): CodexReviewResult;
