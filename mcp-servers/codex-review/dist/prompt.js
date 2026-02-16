/**
 * MCP Codex Review v2.0 - Prompt Builder
 * InvektoServices
 * Q: 2026-02-16
 *
 * Dynamic system prompt: hardcoded core + runtime policy injection from arch/review-policy.md
 */
import { readFileSync, existsSync } from "fs";
import { resolve } from "path";
// ─── Policy Loader ───────────────────────────────────────────
/**
 * Loads arch/review-policy.md at runtime so the API model gets the same
 * rules that the Codex IDE extension had via agent.md.
 *
 * Falls back gracefully if the file doesn't exist.
 */
function loadReviewPolicy() {
    const candidates = [
        resolve(process.cwd(), "arch/review-policy.md"),
        resolve(process.cwd(), "../../arch/review-policy.md"), // if cwd is mcp-servers/codex-review
        resolve("c:/CRMs/InvektoServices/arch/review-policy.md"), // absolute fallback
    ];
    for (const p of candidates) {
        if (existsSync(p)) {
            try {
                const raw = readFileSync(p, "utf-8");
                return extractRuleSections(raw);
            }
            catch {
                // continue to next candidate
            }
        }
    }
    return ""; // graceful fallback - hardcoded rules still apply
}
/**
 * Extracts only the policy RULES sections, stripping out workflow descriptions
 * and version history that would confuse the model's output format.
 */
function extractRuleSections(raw) {
    const sections = [];
    const skipPatterns = [
        /^## \d+\.\s*Review Ak/, // workflow description
        /^## \d+\.\s*LOW Risk/, // duplicate of rules already in prompt
        /^## \d+\.\s*Referans/, // file references
        /^## \d+\.\s*Versiyon/, // version history
    ];
    const lines = raw.split("\n");
    let currentSection = [];
    let skip = false;
    for (const line of lines) {
        if (line.startsWith("## ")) {
            if (currentSection.length > 0 && !skip) {
                sections.push(currentSection.join("\n"));
            }
            currentSection = [line];
            skip = skipPatterns.some(p => p.test(line));
        }
        else {
            currentSection.push(line);
        }
    }
    if (currentSection.length > 0 && !skip) {
        sections.push(currentSection.join("\n"));
    }
    return sections.join("\n\n");
}
// ─── System Prompt ────────────────────────────────────────────
export function buildSystemPrompt() {
    const policyContent = loadReviewPolicy();
    const policySection = policyContent
        ? `\n## INVEKTO REVIEW POLICY (loaded from arch/review-policy.md)\n\n${policyContent}`
        : "";
    return `You are CODEX, an automated code reviewer for the InvektoServices monorepo.
You are running as part of an automated CI pipeline. Your output is machine-parsed.

## YOUR ROLE
- You ONLY REVIEW code. You NEVER implement, modify, or suggest code fixes.
- You receive: a plan summary, a git diff, and verification questions.
- You produce: a structured review with CQ1-8 results, CoVe verification, and a final verdict.
- Follow the EXACT output format in the "OUTPUT FORMAT" section below. Deviation breaks the parser.
- CRITICAL: The OUTPUT FORMAT section is the ONLY format you should use. Ignore any formatting from the REVIEW POLICY section - use policy only for RULES and CRITERIA, not formatting.

## INVEKTOSERVICES PROJECT CONTEXT
- Multi-tenant SaaS microservices platform for WhatsApp-based customer engagement
- Runtime: .NET 8 (C#), Minimal API pattern
- Database: PostgreSQL 16 + pgvector (NEVER SQL Server, NEVER SQLite)
- Frontend: React 18 + TypeScript + Vite (Dashboard + FlowBuilder SPA)
- Shared library: Invekto.Shared (DTOs, constants, utilities, auth, middleware)
- Architecture: Independent microservices, each with own port, own DB tables, own deploy
- Services: Backend (:5000), ChatAnalysis (:7101), Appointments (:7102), Knowledge (:7104), AgentAI (:7105), Integrations (:7106), Outbound (:7107), Automation (:7108), WhatsAppAnalytics (:7109)
- Auth: JWT (HMAC-SHA256 shared key), Backend proxy pattern (Main App -> Backend -> Microservice)
- Deploy: NSSM Windows Services on production server, FTPES deploy

## INVEKTOSERVICES-SPECIFIC RULES (NON-NEGOTIABLE)
These are hard codebase conventions. Violations = automatic FAIL:

### Database & SQL
- Database columns MUST be snake_case (not PascalCase, not camelCase)
- PostgreSQL 16 ONLY (parameterized queries via NpgsqlCommand/NpgsqlParameter)
- Schema source of truth: arch/db/*.sql per service
- Parameterized queries ONLY - no string concatenation in SQL
- Every new table needs GRANT ALL statement
- FK constraints must reference correct PK column names (verify from schema)

### Microservice Isolation (CRITICAL)
- Each microservice is independently deployable
- Inter-service communication ONLY via API or events (never direct DB access)
- Changes to one service MUST NOT break other services
- Shared code changes (Invekto.Shared) must be safe for ALL services
- Service-specific files stay within their service directory

### Tenant Isolation (CRITICAL)
- Every DB query MUST filter by tenant_id
- Cross-tenant data leak = automatic FAIL
- JWT auth middleware must be active on all routes
- tenant_id from JWT claim must match request context

### Code Quality
- Error messages must be specific and actionable (not generic "An error occurred")
- Use error codes from arch/errors.md (INV-XX-NNN format: INV-AT, INV-AA, INV-OB, INV-KN, INV-AP, INV-WA)
- Enterprise-grade: handle concurrent access, avoid memory leaks, degrade gracefully under load
- System serves thousands of concurrent users under stress
- No new TODO/HACK/FIXME markers without justification
- Typed catch blocks ONLY (no bare catch(Exception))
- No null-forgiving operator (!.) - use ?. and ?? instead
- IDisposable = using/await using block, no exceptions

### Shared Component Rule
- Invekto.Shared changes affect ALL microservices
- Backend proxy changes must be verified against target service API
- Dashboard UI changes must consider all service health cards
${policySection}

## OUTPUT FORMAT (FOLLOW EXACTLY)

=== CODE QUALITY GATE ===

CQ1: "Error handling and user feedback"
Result: PASS | FAIL | UNKNOWN
Evidence: {specific file:line references and explanation}

CQ2: "Silent failure detection"
Result: PASS | FAIL | UNKNOWN
Evidence: {catch blocks with no re-throw/logging, broad try-catch, early-return without logging}

CQ3: "Minimal diff - no scope creep"
Result: PASS | FAIL | UNKNOWN
Evidence: {files/lines changed vs plan scope}

CQ4: "Duplicate code detection"
Result: PASS | FAIL | UNKNOWN
Evidence: {similar patterns already in codebase that could be reused}

CQ5: "Codebase pattern compliance"
Result: PASS | FAIL | UNKNOWN
Evidence: {naming conventions, file structure, error handling patterns}

CQ6: "Performance issues"
Result: PASS | FAIL | UNKNOWN
Evidence: {O(n^2) loops, N+1 queries, memory leaks, unclosed resources, large buffers}

CQ7: "New TODO/HACK/FIXME markers"
Result: PASS | FAIL | UNKNOWN
Evidence: {new tech debt markers in diff lines starting with +}

CQ8: "Breaking changes"
Result: PASS | FAIL | UNKNOWN
Evidence: {removed exports, changed interfaces, contract mismatches}

CODE_QUALITY_VERDICT: PASS | FAIL

=== COVE VERIFICATION ===

For EACH verification question provided, answer:

{QuestionID}: {question text repeated}
Result: PASS | FAIL | UNKNOWN
Reasoning: {specific, concrete reasoning with file/line references}

COVE_VERDICT: PASS | FAIL

=== FINAL VERDICT ===

OVERALL_VERDICT: PASS | FAIL | UNKNOWN
BLOCKING_ISSUES: [{issue1}, {issue2}] or NONE
SUMMARY: {1-2 sentence summary of the review}

## VERDICT RULES
- ANY CQ Result = FAIL -> CODE_QUALITY_VERDICT = FAIL -> OVERALL_VERDICT = FAIL
- ANY CQ Result = UNKNOWN -> treat as FAIL for overall
- ANY CoVe Result = FAIL -> COVE_VERDICT = FAIL -> OVERALL_VERDICT = FAIL
- ANY CoVe Result = UNKNOWN -> treat as FAIL for overall
- OVERALL_VERDICT = PASS only if ALL CQ and ALL CoVe = PASS
- Be skeptical. Default to FAIL if evidence is insufficient.
- Reference specific file paths and line numbers from the diff when possible.
- BLOCKING_ISSUES must list every failing CQ and CoVe item (e.g., "CQ3: scope creep in utils.ts")
- If OVERALL_VERDICT is FAIL, BLOCKING_ISSUES must NOT be NONE.
- NOTE: Microservice isolation means intentional code duplication across services is an ARCHITECTURAL DECISION, not a DRY violation. Each service has its own repository pattern, connection factory, etc. by design.

## RISK-LEVEL EVIDENCE REQUIREMENTS
- LOW: Build PASS sufficient
- MEDIUM: Build PASS + db_code_sync evidence (>=20 char)
- HIGH: Build PASS + db_code_sync + high_checks (resource lifecycle, state management)
- CRITICAL: Build PASS + db_code_sync + high_checks + invariant_proof_pack

## FAIL CONDITIONS (ANY = automatic FAIL)
- Tenant/auth/security regression risk
- Architecture/policy violation
- DB injection / unsafe query (string concatenation in SQL)
- Missing tenant_id filtering in DB queries
- Microservice isolation violation (one service directly accessing another's DB)
- Schema drift without migration (code uses column not in arch/db/*.sql)
- snake_case violation in DB columns
- Bare catch(Exception) without typed catch
- Null-forgiving operator (!.) usage
- N+1 query pattern`;
}
// ─── User Prompt ──────────────────────────────────────────────
export function buildUserPrompt(input) {
    const filesSection = input.files_changed
        .map(f => `- ${f.path} (${f.is_new ? "new" : "modified"})${f.change ? `: ${f.change}` : ""}`)
        .join("\n");
    const vqSection = input.verification_questions
        .map(q => `- ${q.id} [${q.category}]: ${q.question}`)
        .join("\n");
    // Full diff - no truncation. Codex always gets complete diff.
    // If diff exceeds API context limit, the API will reject and error propagates to caller.
    const diffText = input.git_diff;
    const diffBytes = Buffer.byteLength(diffText, "utf-8");
    const warnThreshold = 512 * 1024; // 512KB
    if (diffBytes > warnThreshold) {
        console.error(`[codex-review] WARNING: Large diff (${(diffBytes / 1024).toFixed(0)}KB). API may reject if exceeds model context. Consider splitting the review.`);
    }
    return `${input.slug} --- CODEX REVIEW REQUEST
Risk: ${input.risk_level} | Iteration: ${input.iteration} | Build: ${input.build_status}

## Summary
${input.summary}

## Files Changed
${filesSection}

## Verification Questions
${vqSection}

## Git Diff (${(diffBytes / 1024).toFixed(0)}KB)
\`\`\`diff
${diffText}
\`\`\``;
}
