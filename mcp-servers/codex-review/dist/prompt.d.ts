/**
 * MCP Codex Review v2.0 - Prompt Builder
 * InvektoServices
 * Q: 2026-02-16
 *
 * Dynamic system prompt: hardcoded core + runtime policy injection from arch/review-policy.md
 */
import type { CodexReviewInput } from "./types.js";
export declare function buildSystemPrompt(): string;
export declare function buildUserPrompt(input: CodexReviewInput): string;
