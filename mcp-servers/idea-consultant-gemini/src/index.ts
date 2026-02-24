#!/usr/bin/env node
/**
 * MCP Idea Consultant (Gemini) Server v1.0
 *
 * SaaS idea consulting via Google Gemini 3.1 Pro thinking model.
 * Mirror of idea-consultant (OpenAI) with identical response format.
 * Uses thinkingLevel (low/medium/high) mapped from analysis depth.
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
import { GoogleGenAI, ThinkingLevel } from "@google/genai";

import { buildSystemPrompt, buildUserPrompt } from "./prompt.js";
import type {
  IdeaConsultInput,
  IdeaConsultResult,
  AnalysisDepth,
  TokenUsage,
} from "./types.js";

// ─── Configuration ────────────────────────────────────────────

const MODEL = process.env.GEMINI_MODEL || "gemini-3.1-pro-preview";
const MAX_TOKENS = parseInt(process.env.GEMINI_MAX_TOKENS || "32768", 10);

// Map analysis depth to Gemini thinking level enum
const THINKING_LEVEL: Record<AnalysisDepth, ThinkingLevel> = {
  quick: ThinkingLevel.LOW,
  standard: ThinkingLevel.MEDIUM,
  deep: ThinkingLevel.HIGH,
};

// ─── Gemini Client ───────────────────────────────────────────

const genai = new GoogleGenAI({
  apiKey: process.env.GEMINI_API_KEY,
});

// ─── Tool Definition ──────────────────────────────────────────

const IDEA_CONSULT_GEMINI_TOOL: Tool = {
  name: "idea_consult_gemini",
  description: `SaaS idea consulting via Google Gemini 3.1 Pro thinking model.
Analyzes feature ideas from multiple expert perspectives (SaaS growth, architecture, UX, business, product).
Returns structured analysis with feasibility score, action items, risks, and open questions.
Supports iterative refinement via previous_feedback parameter.
Uses thinking levels: quick=low, standard=medium, deep=high.`,
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
          "Analysis depth mapped to thinking level: 'quick'=low, 'standard'=medium, 'deep'=high.",
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
  if (!process.env.GEMINI_API_KEY) {
    throw new Error(
      "GEMINI_API_KEY environment variable is not set. " +
        "Set it in .mcp.json env or as a system environment variable.",
    );
  }

  const systemPrompt = buildSystemPrompt();
  const userPrompt = buildUserPrompt(args);
  const thinkingLevel = THINKING_LEVEL[args.depth];

  console.error(
    `[idea-consultant-gemini] Calling ${MODEL} (iteration: ${args.iteration}, depth: ${args.depth} -> thinking_level: ${thinkingLevel}, focus: ${args.focus_areas.join(", ")})`,
  );

  const response = await genai.models.generateContent({
    model: MODEL,
    contents: userPrompt,
    config: {
      maxOutputTokens: MAX_TOKENS,
      systemInstruction: systemPrompt,
      thinkingConfig: {
        thinkingLevel: thinkingLevel,
        includeThoughts: true,
      },
    },
  });

  // Extract text from response (skip thought parts)
  let responseText = "No response from model";
  const candidates = response.candidates;
  if (candidates && candidates.length > 0) {
    const parts = candidates[0].content?.parts;
    if (parts) {
      for (const part of parts) {
        // Skip thought/reasoning parts, take the actual answer
        if (part.text && !(part as Record<string, unknown>).thought) {
          responseText = part.text;
        }
      }
    }
  }

  // Extract token usage from usageMetadata
  const usage = response.usageMetadata;
  const thoughtsTokenCount = (usage as Record<string, number> | undefined)?.thoughtsTokenCount ?? 0;

  const tokenUsage: TokenUsage = {
    input_tokens: usage?.promptTokenCount || 0,
    output_tokens: usage?.candidatesTokenCount || 0,
    reasoning_tokens: thoughtsTokenCount,
    total_tokens: usage?.totalTokenCount || 0,
  };

  console.error(
    `[idea-consultant-gemini] Response received. Tokens: ${tokenUsage.total_tokens} (thinking: ${tokenUsage.reasoning_tokens})`,
  );

  return parseConsultResponse(
    responseText,
    MODEL,
    tokenUsage,
  );
}

// ─── MCP Server ───────────────────────────────────────────────

async function main() {
  const server = new Server(
    {
      name: "mcp-idea-consultant-gemini",
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
    return { tools: [IDEA_CONSULT_GEMINI_TOOL] };
  });

  // Handle tool calls
  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;

    if (name !== "idea_consult_gemini") {
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
      if (message.includes("API key") || message.includes("401") || message.includes("403"))
        errorType = "AUTH_ERROR";
      else if (message.includes("rate limit") || message.includes("429") || message.includes("RESOURCE_EXHAUSTED"))
        errorType = "RATE_LIMIT";
      else if (message.includes("timeout") || message.includes("DEADLINE_EXCEEDED"))
        errorType = "TIMEOUT";
      else if (message.includes("model") || message.includes("404") || message.includes("NOT_FOUND"))
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
                    ? "Check GEMINI_API_KEY in .mcp.json env - get one from https://aistudio.google.com/apikey"
                    : errorType === "RATE_LIMIT"
                      ? "Wait and retry - Gemini 3.1 Pro preview may have lower rate limits"
                      : errorType === "TIMEOUT"
                        ? "Gemini thinking can take time - reduce depth to 'quick' or 'standard'"
                        : errorType === "MODEL_ERROR"
                          ? `Model '${MODEL}' may not be available - check GEMINI_MODEL env or try 'gemini-2.5-pro'`
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
  console.error(`MCP Idea Consultant (Gemini) v1.0 started (model: ${MODEL})`);
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
