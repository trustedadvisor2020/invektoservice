#!/usr/bin/env node
/**
 * MCP Idea Consultant Server v1.0
 *
 * SaaS idea consulting via OpenAI gpt-5.2-pro thinking model.
 * Provides multi-perspective analysis for InvektoServices feature ideas.
 * Uses reasoning effort (medium/high/xhigh) mapped from analysis depth.
 *
 * Q: 2026-02-24
 */

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import type { Tool } from "@modelcontextprotocol/sdk/types.js";
import OpenAI from "openai";

import { buildSystemPrompt, buildUserPrompt } from "./prompt.js";
import type {
  IdeaConsultInput,
  IdeaConsultResult,
  AnalysisDepth,
  TokenUsage,
} from "./types.js";

// ─── Configuration ────────────────────────────────────────────

const MODEL = process.env.IDEA_MODEL || "gpt-5.2-pro";
const MAX_TOKENS = parseInt(process.env.IDEA_MAX_TOKENS || "32768", 10);

// Map analysis depth to reasoning effort
// "xhigh" is valid for gpt-5.2-pro but SDK types may lag behind API
const REASONING_EFFORT: Record<AnalysisDepth, string> = {
  quick: "medium",
  standard: "high",
  deep: "xhigh",
};

// ─── OpenAI Client ────────────────────────────────────────────

const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY,
});

// ─── Tool Definition ──────────────────────────────────────────

const IDEA_CONSULT_TOOL: Tool = {
  name: "idea_consult",
  description: `SaaS idea consulting via OpenAI gpt-5.2-pro thinking model.
Analyzes feature ideas from multiple expert perspectives (SaaS growth, architecture, UX, business, product).
Returns structured analysis with feasibility score, action items, risks, and open questions.
Supports iterative refinement via previous_feedback parameter.
Uses reasoning effort levels: quick=medium, standard=high, deep=xhigh.`,
  inputSchema: {
    type: "object" as const,
    properties: {
      idea: {
        type: "string",
        description:
          "The core idea or feature description to analyze. Be as detailed as possible.",
      },
      context: {
        type: "string",
        description:
          "Project-specific context: which service, current state, why this idea, target users.",
      },
      focus_areas: {
        type: "array",
        items: {
          type: "string",
          enum: [
            "feasibility",
            "architecture",
            "monetization",
            "user_experience",
            "competitive_analysis",
            "implementation_plan",
            "risk_assessment",
            "scalability",
            "market_fit",
            "mvp_definition",
          ],
        },
        description:
          "Areas to focus the analysis on. Pick 2-5 for best results.",
      },
      iteration: {
        type: "number",
        description:
          "Current iteration number (0 = first analysis, 1+ = refinement based on feedback).",
      },
      previous_feedback: {
        type: "string",
        description:
          "User feedback from the previous iteration. Used to refine and redirect the analysis.",
      },
      constraints: {
        type: "string",
        description:
          "Known constraints: budget, timeline, team size, tech limitations, etc.",
      },
      depth: {
        type: "string",
        enum: ["quick", "standard", "deep"],
        description:
          "Analysis depth mapped to reasoning effort: 'quick'=medium, 'standard'=high, 'deep'=xhigh.",
      },
    },
    required: ["idea", "context", "focus_areas", "iteration", "depth"],
  },
};

// ─── Response Parser ──────────────────────────────────────────

function parseConsultResponse(
  text: string,
  modelUsed: string,
  tokenUsage: TokenUsage,
): IdeaConsultResult {
  // Strip markdown code fences if present
  let cleaned = text.trim();
  if (cleaned.startsWith("```json")) {
    cleaned = cleaned.slice(7);
  } else if (cleaned.startsWith("```")) {
    cleaned = cleaned.slice(3);
  }
  if (cleaned.endsWith("```")) {
    cleaned = cleaned.slice(0, -3);
  }
  cleaned = cleaned.trim();

  try {
    const parsed = JSON.parse(cleaned);
    return {
      analysis: parsed.analysis || "No analysis provided",
      perspectives: parsed.perspectives || [],
      action_items: parsed.action_items || [],
      risks: parsed.risks || [],
      open_questions: parsed.open_questions || [],
      feasibility_score: parsed.feasibility_score || 0,
      implementation_complexity: parsed.implementation_complexity || "medium",
      thinking_tokens: tokenUsage.reasoning_tokens,
      token_usage: tokenUsage,
      model_used: modelUsed,
    };
  } catch {
    // If JSON parsing fails, return the raw text as analysis
    return {
      analysis: text,
      perspectives: [],
      action_items: [],
      risks: [],
      open_questions: [],
      feasibility_score: 0,
      implementation_complexity: "medium",
      thinking_tokens: tokenUsage.reasoning_tokens,
      token_usage: tokenUsage,
      model_used: modelUsed,
    };
  }
}

// ─── Tool Handler ─────────────────────────────────────────────

async function handleIdeaConsult(
  args: IdeaConsultInput,
): Promise<IdeaConsultResult> {
  if (!process.env.OPENAI_API_KEY) {
    throw new Error(
      "OPENAI_API_KEY environment variable is not set. " +
        "Set it in .mcp.json env or as a system environment variable.",
    );
  }

  const systemPrompt = buildSystemPrompt();
  const userPrompt = buildUserPrompt(args);
  const effort = REASONING_EFFORT[args.depth];

  console.error(
    `[idea-consultant] Calling ${MODEL} (iteration: ${args.iteration}, depth: ${args.depth} -> reasoning_effort: ${effort}, focus: ${args.focus_areas.join(", ")})`,
  );

  // gpt-5.2-pro uses Responses API with reasoning effort
  const response = await openai.responses.create({
    model: MODEL,
    max_output_tokens: MAX_TOKENS,
    instructions: systemPrompt,
    input: userPrompt,
    reasoning: {
      effort: effort as "medium" | "high",
    },
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

  const reasoningTokens =
    (response.usage as Record<string, number> | undefined)?.reasoning_tokens ?? 0;

  const tokenUsage: TokenUsage = {
    input_tokens: response.usage?.input_tokens || 0,
    output_tokens: response.usage?.output_tokens || 0,
    reasoning_tokens: reasoningTokens,
    total_tokens: response.usage?.total_tokens || 0,
  };

  console.error(
    `[idea-consultant] Response received. Tokens: ${tokenUsage.total_tokens} (reasoning: ${tokenUsage.reasoning_tokens})`,
  );

  return parseConsultResponse(
    responseText,
    response.model || MODEL,
    tokenUsage,
  );
}

// ─── MCP Server ───────────────────────────────────────────────

async function main() {
  const server = new Server(
    {
      name: "mcp-idea-consultant",
      version: "1.0.0",
    },
    {
      capabilities: {
        tools: {},
      },
    },
  );

  // List tools
  server.setRequestHandler(ListToolsRequestSchema, async () => {
    return { tools: [IDEA_CONSULT_TOOL] };
  });

  // Handle tool calls
  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;

    if (name !== "idea_consult") {
      return {
        content: [{ type: "text", text: `Unknown tool: ${name}` }],
        isError: true,
      };
    }

    try {
      const input = args as unknown as IdeaConsultInput;
      const result = await handleIdeaConsult(input);

      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(result, null, 2),
          },
        ],
      };
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);

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
            text: JSON.stringify(
              {
                error: true,
                error_type: errorType,
                message,
                model_attempted: MODEL,
                suggestion:
                  errorType === "AUTH_ERROR"
                    ? "Check OPENAI_API_KEY in .mcp.json env"
                    : errorType === "RATE_LIMIT"
                      ? "Wait and retry - gpt-5.2-pro has lower rate limits"
                      : errorType === "TIMEOUT"
                        ? "gpt-5.2-pro thinking can take minutes - reduce depth to 'quick' or 'standard'"
                        : errorType === "MODEL_ERROR"
                          ? `Model '${MODEL}' may not be available - check IDEA_MODEL env`
                          : "Check MCP server logs",
              },
              null,
              2,
            ),
          },
        ],
        isError: true,
      };
    }
  });

  // Start
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error(`MCP Idea Consultant v1.0 started (model: ${MODEL})`);
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
