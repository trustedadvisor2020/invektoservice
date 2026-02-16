/**
 * MCP Codex Review v2.0 - Type Definitions
 * InvektoServices
 * Q: 2026-02-16
 */
export interface CodexReviewInput {
    slug: string;
    risk_level: RiskLevel;
    iteration: number;
    summary: string;
    files_changed: FileChange[];
    git_diff: string;
    diff_file_path?: string;
    verification_questions: VerificationQuestion[];
    build_status: "PASS" | "FAIL";
}
export type RiskLevel = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";
export interface FileChange {
    path: string;
    change?: string;
    is_new: boolean;
}
export interface VerificationQuestion {
    id: string;
    question: string;
    category: string;
}
export interface CodexReviewResult {
    verdict: Verdict;
    code_quality_gate: CodeQualityGate;
    cove_verification: Record<string, CoVeResult>;
    blocking_issues: string[];
    summary: string;
    raw_response: string;
    model_used: string;
    token_usage: TokenUsage;
}
export type Verdict = "PASS" | "FAIL" | "UNKNOWN";
export interface CodeQualityGate {
    CQ1: CQResult;
    CQ2: CQResult;
    CQ3: CQResult;
    CQ4: CQResult;
    CQ5: CQResult;
    CQ6: CQResult;
    CQ7: CQResult;
    CQ8: CQResult;
    overall: "PASS" | "FAIL";
}
export interface CQResult {
    result: Verdict;
    evidence: string;
}
export interface CoVeResult {
    result: Verdict;
    reasoning: string;
}
export interface TokenUsage {
    prompt_tokens: number;
    completion_tokens: number;
    total_tokens: number;
}
export interface CodexConfig {
    model: string;
    maxTokens: number;
    temperature: number;
    diffMaxBytes: number;
}
export declare const DEFAULT_CONFIG: CodexConfig;
