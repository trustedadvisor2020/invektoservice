#!/usr/bin/env node

// Invekto PostgreSQL MCP Server
// Based on @zeddotdev/postgres-context-server v0.1.7 (SQL injection patched)
// Added: "execute" tool for DDL/DML with Q's permission (Claude Code will prompt user)
// query = read-only (safe), execute = read-write (requires user approval)

import pg from "pg";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListResourcesRequestSchema,
  ListPromptsRequestSchema,
  ListToolsRequestSchema,
  ReadResourceRequestSchema,
  GetPromptRequestSchema,
  CompleteRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

const server = new Server(
  {
    name: "invekto-postgres",
    version: "1.0.0",
  },
  {
    capabilities: {
      resources: {},
      tools: {},
      prompts: {},
      completions: {},
    },
  },
);

const databaseUrl = process.env.DATABASE_URL;
if (databaseUrl == null || databaseUrl.trim().length === 0) {
  console.error("Please provide a DATABASE_URL environment variable");
  process.exit(1);
}

const resourceBaseUrl = new URL(databaseUrl);
resourceBaseUrl.protocol = "postgres:";
resourceBaseUrl.password = "";

process.stderr.write("Invekto PostgreSQL MCP Server starting...\n");

const pool = new pg.Pool({
  connectionString: databaseUrl,
});

const SCHEMA_PATH = "schema";
const SCHEMA_PROMPT_NAME = "pg-schema";
const ALL_TABLES = "all-tables";

server.setRequestHandler(ListResourcesRequestSchema, async () => {
  const client = await pool.connect();
  try {
    const result = await client.query(
      "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'",
    );
    return {
      resources: result.rows.map((row) => ({
        uri: new URL(`${row.table_name}/${SCHEMA_PATH}`, resourceBaseUrl).href,
        mimeType: "application/json",
        name: `"${row.table_name}" database schema`,
      })),
    };
  } finally {
    client.release();
  }
});

server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
  const resourceUrl = new URL(request.params.uri);
  const pathComponents = resourceUrl.pathname.split("/");
  const schema = pathComponents.pop();
  const tableName = pathComponents.pop();

  if (schema !== SCHEMA_PATH) {
    throw new Error("Invalid resource URI");
  }

  const client = await pool.connect();
  try {
    const result = await client.query(
      "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = $1",
      [tableName],
    );
    return {
      contents: [
        {
          uri: request.params.uri,
          mimeType: "application/json",
          text: JSON.stringify(result.rows, null, 2),
        },
      ],
    };
  } finally {
    client.release();
  }
});

server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: "pg-schema",
        description: "Returns the schema for a Postgres database.",
        inputSchema: {
          type: "object",
          properties: {
            mode: {
              type: "string",
              enum: ["all", "specific"],
              description: "Mode of schema retrieval",
            },
            tableName: {
              type: "string",
              description:
                "Name of the specific table (required if mode is 'specific')",
            },
          },
          required: ["mode"],
          if: {
            properties: { mode: { const: "specific" } },
          },
          then: {
            required: ["tableName"],
          },
        },
      },
      {
        name: "query",
        description: "Run a read-only SQL query",
        inputSchema: {
          type: "object",
          properties: {
            sql: { type: "string" },
          },
        },
      },
      {
        name: "execute",
        description: "Execute a read-write SQL statement (CREATE, INSERT, UPDATE, DELETE, ALTER). REQUIRES Q's explicit permission. Use for DDL/DML operations only.",
        inputSchema: {
          type: "object",
          properties: {
            sql: {
              type: "string",
              description: "SQL statement to execute (DDL or DML)",
            },
          },
          required: ["sql"],
        },
      },
    ],
  };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  // pg-schema tool
  if (request.params.name === "pg-schema") {
    const mode = request.params.arguments?.mode;
    const tableName = (() => {
      switch (mode) {
        case "specific": {
          const tn = request.params.arguments?.tableName;
          if (typeof tn !== "string" || tn.length === 0) {
            throw new Error(`Invalid tableName: ${tn}`);
          }
          return tn;
        }
        case "all":
          return ALL_TABLES;
        default:
          throw new Error(`Invalid mode: ${mode}`);
      }
    })();

    const client = await pool.connect();
    try {
      const sql = await getSchema(client, tableName);
      return { content: [{ type: "text", text: sql }] };
    } finally {
      client.release();
    }
  }

  // query tool (read-only)
  if (request.params.name === "query") {
    const sql = request.params.arguments?.sql;
    const client = await pool.connect();
    try {
      await client.query("BEGIN TRANSACTION READ ONLY");
      const result = await client.query({
        name: "sandboxed-statement",
        text: sql,
        values: [],
      });
      return {
        content: [
          { type: "text", text: JSON.stringify(result.rows, undefined, 2) },
        ],
      };
    } catch (error) {
      throw error;
    } finally {
      client
        .query("ROLLBACK")
        .catch((err) => console.warn("Could not roll back transaction:", err));
      client.release(true);
    }
  }

  // execute tool (read-write, requires Q's permission via Claude Code approval prompt)
  if (request.params.name === "execute") {
    const sql = request.params.arguments?.sql;
    if (typeof sql !== "string" || sql.trim().length === 0) {
      throw new Error("SQL statement is required");
    }

    const client = await pool.connect();
    try {
      await client.query("BEGIN");
      const result = await client.query(sql);
      await client.query("COMMIT");

      const rowCount = result.rowCount ?? 0;
      const command = result.command ?? "UNKNOWN";
      const summary = `${command} OK — ${rowCount} row(s) affected`;

      // Return rows if SELECT-like, otherwise summary
      if (result.rows && result.rows.length > 0) {
        return {
          content: [
            { type: "text", text: `${summary}\n${JSON.stringify(result.rows, undefined, 2)}` },
          ],
        };
      }

      return { content: [{ type: "text", text: summary }] };
    } catch (error) {
      await client.query("ROLLBACK").catch((err) =>
        console.warn("Could not roll back transaction:", err),
      );
      throw error;
    } finally {
      client.release(true);
    }
  }

  throw new Error("Tool not found");
});

server.setRequestHandler(CompleteRequestSchema, async (request) => {
  if (request.params.ref.name === SCHEMA_PROMPT_NAME) {
    const tableNameQuery = request.params.argument.value;
    const alreadyHasArg = /\S*\s/.test(tableNameQuery);
    if (alreadyHasArg) {
      return { completion: { values: [] } };
    }
    const client = await pool.connect();
    try {
      const result = await client.query(
        "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'",
      );
      return {
        completion: { values: [ALL_TABLES, ...result.rows.map((r) => r.table_name)] },
      };
    } finally {
      client.release();
    }
  }
  throw new Error("unknown prompt");
});

server.setRequestHandler(ListPromptsRequestSchema, async () => {
  return {
    prompts: [
      {
        name: SCHEMA_PROMPT_NAME,
        description: "Retrieve the schema for a given table in the postgres database",
        arguments: [
          { name: "tableName", description: "the table to describe", required: true },
        ],
      },
    ],
  };
});

server.setRequestHandler(GetPromptRequestSchema, async (request) => {
  if (request.params.name === SCHEMA_PROMPT_NAME) {
    const tableName = request.params.arguments?.tableName;
    if (typeof tableName !== "string" || tableName.length === 0) {
      throw new Error(`Invalid tableName: ${tableName}`);
    }
    const client = await pool.connect();
    try {
      const sql = await getSchema(client, tableName);
      return {
        description: tableName === ALL_TABLES ? "all table schemas" : `${tableName} schema`,
        messages: [{ role: "user", content: { type: "text", text: sql } }],
      };
    } finally {
      client.release();
    }
  }
  throw new Error(`Prompt '${request.params.name}' not implemented`);
});

async function getSchema(client, tableNameOrAll) {
  const select =
    "SELECT column_name, data_type, is_nullable, column_default, table_name FROM information_schema.columns";

  let result;
  if (tableNameOrAll === ALL_TABLES) {
    result = await client.query(
      `${select} WHERE table_schema NOT IN ('pg_catalog', 'information_schema')`,
    );
  } else {
    result = await client.query(`${select} WHERE table_name = $1`, [tableNameOrAll]);
  }

  const allTableNames = Array.from(
    new Set(result.rows.map((row) => row.table_name).sort()),
  );

  let sql = "```sql\n";
  for (let i = 0, len = allTableNames.length; i < len; i++) {
    const tableName = allTableNames[i];
    if (i > 0) sql += "\n";
    sql += [
      `create table "${tableName}" (`,
      result.rows
        .filter((row) => row.table_name === tableName)
        .map((row) => {
          const notNull = row.is_nullable === "NO" ? "" : " not null";
          const defaultValue =
            row.column_default != null ? ` default ${row.column_default}` : "";
          return `    "${row.column_name}" ${row.data_type}${notNull}${defaultValue}`;
        })
        .join(",\n"),
      ");",
    ].join("\n");
    sql += "\n";
  }
  sql += "```";
  return sql;
}

async function runServer() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

runServer().catch((error) => {
  console.error(error);
  process.exit(1);
});
