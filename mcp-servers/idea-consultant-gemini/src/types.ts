/**
 * MCP Idea Consultant (Gemini) v1.0 - Type Definitions
 * InvektoServices - SaaS Idea Consulting via Google Gemini 3.1 Pro
 * Shared types with OpenAI version for consistent response format.
 * Q: 2026-02-24
 */

// ─── Tool Input ───────────────────────────────────────────────

export interface IdeaConsultInput {
  idea: string;
  context: string;
  focus_areas: FocusArea[];
  iteration: number;
  previous_feedback?: string;
  constraints?: string;
  depth: AnalysisDepth;
}

export type FocusArea =
  | "feasibility"
  | "architecture"
  | "monetization"
  | "user_experience"
  | "competitive_analysis"
  | "implementation_plan"
  | "risk_assessment"
  | "scalability"
  | "market_fit"
  | "mvp_definition";

export type AnalysisDepth = "quick" | "standard" | "deep";

// ─── Tool Output ──────────────────────────────────────────────

export interface IdeaConsultResult {
  analysis: string;
  perspectives: ExpertPerspective[];
  action_items: ActionItem[];
  risks: Risk[];
  open_questions: string[];
  feasibility_score: number;         // 1-10
  implementation_complexity: ComplexityLevel;
  thinking_tokens: number;
  token_usage: TokenUsage;
  model_used: string;
}

export interface ExpertPerspective {
  role: string;          // e.g. "SaaS Growth Expert", "Technical Architect"
  opinion: string;
  key_insight: string;
  recommendation: string;
}

export interface ActionItem {
  priority: "P0" | "P1" | "P2" | "P3";
  action: string;
  rationale: string;
  estimated_effort: string;
}

export interface Risk {
  severity: "low" | "medium" | "high" | "critical";
  description: string;
  mitigation: string;
}

export type ComplexityLevel = "trivial" | "low" | "medium" | "high" | "very_high";

export interface TokenUsage {
  input_tokens: number;
  output_tokens: number;
  reasoning_tokens: number;
  total_tokens: number;
}
