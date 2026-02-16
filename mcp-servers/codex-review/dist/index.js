#!/usr/bin/env node
/**
 * MCP Codex Review Server v2.1
 *
 * Automated code review via OpenAI API for InvektoServices /auto workflow.
 * Replaces manual copy-paste between Claude Code and Codex.
 *
 * Q: 2026-02-16
 */
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema, } from "@modelcontextprotocol/sdk/types.js";
import OpenAI from "openai";
import { readFileSync, existsSync } from "fs";
import { resolve } from "path";
import { buildSystemPrompt, buildUserPrompt } from "./prompt.js";
import { parseCodexResponse } from "./parser.js";
const MIN_DIFF_LENGTH = 50;
// ─── Configuration ────────────────────────────────────────────
const MODEL = process.env.CODEX_MODEL || "gpt-5.2-codex";
const MAX_TOKENS = parseInt(process.env.CODEX_MAX_TOKENS || "8192", 10);
// ─── OpenAI Client ────────────────────────────────────────────
const openai = new OpenAI({
    apiKey: process.env.OPENAI_API_KEY,
});
// ─── Tool Definition ──────────────────────────────────────────
const CODEX_REVIEW_TOOL = {
    name: "codex_review",
    description: `Submit code changes for automated Codex review via OpenAI.
Returns structured JSON verdict with CQ1-8 gate results, CoVe verification, and PASS/FAIL result.
The DevAgent calls this tool after build PASS. The tool is STATELESS - iteration tracking is the caller's responsibility.`,
    inputSchema: {
        type: "object",
        properties: {
            slug: {
                type: "string",
                description: "Plan slug (e.g. '20260216-pkt3-ops-dashboard')",
            },
            risk_level: {
                type: "string",
                enum: ["LOW", "MEDIUM", "HIGH", "CRITICAL"],
                description: "Risk level from planning phase",
            },
            iteration: {
                type: "number",
                description: "Current review iteration (0-based, caller tracks)",
            },
            summary: {
                type: "string",
                description: "Plan summary - what was done and why",
            },
            files_changed: {
                type: "array",
                items: {
                    type: "object",
                    properties: {
                        path: { type: "string" },
                        change: { type: "string" },
                        is_new: { type: "boolean" },
                    },
                    required: ["path", "is_new"],
                },
                description: "Files changed with metadata",
            },
            git_diff: {
                type: "string",
                description: "Full staged git diff output. If empty/short, diff_file_path is used as fallback.",
            },
            diff_file_path: {
                type: "string",
                description: "Absolute path to diff file on disk (fallback when git_diff is empty/too short). Typically arch/plans/diffs/{slug}.diff",
            },
            verification_questions: {
                type: "array",
                items: {
                    type: "object",
                    properties: {
                        id: { type: "string" },
                        question: { type: "string" },
                        category: { type: "string" },
                    },
                    required: ["id", "question", "category"],
                },
                description: "CoVe verification questions from the plan",
            },
            build_status: {
                type: "string",
                enum: ["PASS", "FAIL"],
                description: "Build status (must be PASS for review)",
            },
        },
        required: [
            "slug",
            "risk_level",
            "iteration",
            "summary",
            "files_changed",
            "verification_questions",
            "build_status",
        ],
    },
};
// ─── Tool Handler ─────────────────────────────────────────────
function resolveDiffContent(args) {
    const inlineDiff = args.git_diff || "";
    if (inlineDiff.length >= MIN_DIFF_LENGTH) {
        return inlineDiff;
    }
    // Fallback: read from diff_file_path
    if (args.diff_file_path) {
        const candidates = [
            args.diff_file_path,
            resolve("c:/CRMs/InvektoServices", args.diff_file_path),
        ];
        for (const filePath of candidates) {
            if (existsSync(filePath)) {
                try {
                    const content = readFileSync(filePath, "utf-8");
                    if (content.length >= MIN_DIFF_LENGTH) {
                        console.error(`[codex-review] Diff loaded from file: ${filePath} (${content.length} bytes)`);
                        return content;
                    }
                }
                catch (readErr) {
                    const msg = readErr instanceof Error ? readErr.message : String(readErr);
                    console.error(`[codex-review] Failed to read diff file ${filePath}: ${msg}`);
                }
            }
        }
    }
    // Auto-discover: try arch/plans/diffs/{slug}.diff
    if (args.slug) {
        const autoPath = resolve("c:/CRMs/InvektoServices/arch/plans/diffs", `${args.slug}.diff`);
        if (existsSync(autoPath)) {
            try {
                const content = readFileSync(autoPath, "utf-8");
                if (content.length >= MIN_DIFF_LENGTH) {
                    console.error(`[codex-review] Diff auto-discovered: ${autoPath} (${content.length} bytes)`);
                    return content;
                }
            }
            catch (readErr) {
                const msg = readErr instanceof Error ? readErr.message : String(readErr);
                console.error(`[codex-review] Failed to read auto-discovered diff ${autoPath}: ${msg}`);
            }
        }
    }
    return inlineDiff;
}
async function handleCodexReview(args) {
    if (!process.env.OPENAI_API_KEY) {
        throw new Error("OPENAI_API_KEY environment variable is not set. " +
            "Set it in .mcp.json env or as a system environment variable.");
    }
    if (args.build_status !== "PASS") {
        throw new Error("Build status must be PASS before submitting for review.");
    }
    // Resolve diff: inline > diff_file_path > auto-discover by slug
    const resolvedDiff = resolveDiffContent(args);
    if (resolvedDiff.length < MIN_DIFF_LENGTH) {
        throw new Error(`Diff content is empty or too short (${resolvedDiff.length} chars, minimum ${MIN_DIFF_LENGTH}). ` +
            "Provide git_diff as inline text OR set diff_file_path to the .diff file path. " +
            `Auto-discovery also checked: arch/plans/diffs/${args.slug}.diff`);
    }
    const resolvedArgs = { ...args, git_diff: resolvedDiff };
    const systemPrompt = buildSystemPrompt();
    const userPrompt = buildUserPrompt(resolvedArgs);
    // gpt-5.2-codex uses Responses API (not Chat Completions)
    const response = await openai.responses.create({
        model: MODEL,
        max_output_tokens: MAX_TOKENS,
        instructions: systemPrompt,
        input: userPrompt,
    });
    // Extract text from Responses API output
    let responseText = "No response from model";
    for (const item of response.output) {
        if (item.type === "message" && item.content) {
            for (const block of item.content) {
                if (block.type === "output_text" && block.text) {
                    responseText = block.text;
                    break;
                }
            }
        }
    }
    const tokenUsage = {
        prompt_tokens: response.usage?.input_tokens || 0,
        completion_tokens: response.usage?.output_tokens || 0,
        total_tokens: response.usage?.total_tokens || 0,
    };
    return parseCodexResponse(responseText, args.verification_questions, response.model || MODEL, tokenUsage);
}
// ─── MCP Server ───────────────────────────────────────────────
async function main() {
    const server = new Server({
        name: "mcp-codex-review",
        version: "2.1.0",
    }, {
        capabilities: {
            tools: {},
        },
    });
    // List tools
    server.setRequestHandler(ListToolsRequestSchema, async () => {
        return { tools: [CODEX_REVIEW_TOOL] };
    });
    // Handle tool calls
    server.setRequestHandler(CallToolRequestSchema, async (request) => {
        const { name, arguments: args } = request.params;
        if (name !== "codex_review") {
            return {
                content: [{ type: "text", text: `Unknown tool: ${name}` }],
                isError: true,
            };
        }
        try {
            const input = args;
            const result = await handleCodexReview(input);
            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify(result, null, 2),
                    },
                ],
            };
        }
        catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            // Categorize errors for better DevAgent handling
            let errorType = "UNKNOWN_ERROR";
            if (message.includes("API key") || message.includes("401"))
                errorType = "AUTH_ERROR";
            else if (message.includes("rate limit") || message.includes("429"))
                errorType = "RATE_LIMIT";
            else if (message.includes("timeout") || message.includes("ETIMEDOUT"))
                errorType = "TIMEOUT";
            else if (message.includes("model") || message.includes("404"))
                errorType = "MODEL_ERROR";
            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify({
                            error: true,
                            error_type: errorType,
                            message,
                            model_attempted: MODEL,
                            suggestion: errorType === "AUTH_ERROR"
                                ? "Check OPENAI_API_KEY in .mcp.json env"
                                : errorType === "RATE_LIMIT"
                                    ? "Wait a moment and retry"
                                    : errorType === "TIMEOUT"
                                        ? "API timeout - check network or try smaller diff"
                                        : errorType === "MODEL_ERROR"
                                            ? `Model '${MODEL}' may not be available - check CODEX_MODEL env`
                                            : "Check MCP server logs",
                        }, null, 2),
                    },
                ],
                isError: true,
            };
        }
    });
    // Start
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error(`MCP Codex Review v2.1 started (model: ${MODEL})`);
}
main().catch((err) => {
    console.error("Fatal error:", err);
    process.exit(1);
});
