#!/usr/bin/env node

// Customer MSSQL MCP Server — READ-ONLY
// Connects to customer SQL Server databases for chat data extraction.
// SECURITY: No write operations. All queries are read-only.
// POLICY: This server is NOT auto-approved. Q must explicitly grant permission per use.
// PERF: export-csv uses streaming — handles millions of rows without memory pressure.

import sql from "mssql";
import fs from "fs";
import path from "path";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

const server = new Server(
  { name: "customer-mssql", version: "1.0.0" },
  { capabilities: { tools: {} } },
);

const MSSQL_HOST = process.env.MSSQL_HOST;
const MSSQL_USER = process.env.MSSQL_USER;
const MSSQL_PASSWORD = process.env.MSSQL_PASSWORD;
const MSSQL_PORT = parseInt(process.env.MSSQL_PORT || "1433", 10);

if (!MSSQL_HOST || !MSSQL_USER || !MSSQL_PASSWORD) {
  console.error("Required env vars: MSSQL_HOST, MSSQL_USER, MSSQL_PASSWORD");
  process.exit(1);
}

process.stderr.write("Customer MSSQL MCP Server starting (READ-ONLY)...\n");

const poolConfig = {
  min: 0,
  max: 4,
  idleTimeoutMillis: 60000,
  acquireTimeoutMillis: 15000,
};

function buildConfig(database) {
  const cfg = {
    server: MSSQL_HOST,
    port: MSSQL_PORT,
    user: MSSQL_USER,
    password: MSSQL_PASSWORD,
    pool: { ...poolConfig },
    options: {
      encrypt: false,
      trustServerCertificate: true,
      requestTimeout: 30000,
      connectTimeout: 15000,
    },
  };
  if (database) cfg.database = database;
  return cfg;
}

/** Pool cache keyed by database name — uses ConnectionPool, not global sql.connect() */
const pools = new Map();

async function getPool(database) {
  const key = database || "__default__";
  let pool = pools.get(key);
  if (pool?.connected) return pool;

  if (pool) {
    try { await pool.close(); } catch (_) { /* ignore */ }
    pools.delete(key);
  }

  pool = new sql.ConnectionPool(buildConfig(database));
  await pool.connect();
  pools.set(key, pool);
  return pool;
}

// ─── CSV Helpers ─────────────────────────────────────────────────────────────

function escapeCsvField(value) {
  if (value == null) return "";
  const str = String(value);
  if (str.includes(",") || str.includes('"') || str.includes("\n") || str.includes("\r")) {
    return '"' + str.replace(/"/g, '""') + '"';
  }
  return str;
}

function rowToCsvLine(row, columns) {
  return columns.map((col) => escapeCsvField(row[col])).join(",") + "\n";
}

// ─── Read-only guard ─────────────────────────────────────────────────────────

function validateReadOnly(queryText) {
  const firstWord = queryText.split(/\s/)[0].toUpperCase();
  if (firstWord !== "SELECT" && firstWord !== "WITH") {
    return `BLOCKED: Only SELECT/WITH statements allowed. Got: ${firstWord}`;
  }
  const dangerous = /\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|MERGE|GRANT|REVOKE|EXEC|EXECUTE|xp_|sp_)\b/i;
  if (dangerous.test(queryText)) {
    return "BLOCKED: Query contains prohibited write/DDL/exec keywords.";
  }
  return null;
}

// ─── Tools ───────────────────────────────────────────────────────────────────

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: "list-databases",
      description:
        "List all databases on the customer SQL Server. Use this first to discover available databases.",
      inputSchema: { type: "object", properties: {} },
    },
    {
      name: "list-tables",
      description:
        "List all user tables in a specific database with row counts.",
      inputSchema: {
        type: "object",
        properties: {
          database: {
            type: "string",
            description: "Database name to list tables from",
          },
        },
        required: ["database"],
      },
    },
    {
      name: "table-schema",
      description: "Get column definitions for a specific table.",
      inputSchema: {
        type: "object",
        properties: {
          database: { type: "string", description: "Database name" },
          table: { type: "string", description: "Table name" },
        },
        required: ["database", "table"],
      },
    },
    {
      name: "query",
      description:
        "Run a read-only SQL query against a customer database. Only SELECT/WITH statements allowed. Returns max 1000 rows. For large exports, use export-csv instead.",
      inputSchema: {
        type: "object",
        properties: {
          database: {
            type: "string",
            description: "Database name to query",
          },
          sql: {
            type: "string",
            description: "SELECT query to execute (read-only)",
          },
        },
        required: ["database", "sql"],
      },
    },
    {
      name: "export-csv",
      description:
        "Stream a read-only SQL query result directly to a CSV file on disk. Handles millions of rows via streaming — no memory limit. Returns file path and row count when done. Use this for large data exports.",
      inputSchema: {
        type: "object",
        properties: {
          database: {
            type: "string",
            description: "Database name to query",
          },
          sql: {
            type: "string",
            description: "SELECT query to execute (read-only)",
          },
          outputPath: {
            type: "string",
            description:
              "Absolute file path for the CSV output (e.g. C:/CRMs/InvektoServices/temp/chat-export/data.csv)",
          },
        },
        required: ["database", "sql", "outputPath"],
      },
    },
  ],
}));

// ─── Handlers ────────────────────────────────────────────────────────────────

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  // ── list-databases ──
  if (name === "list-databases") {
    const pool = await getPool(null);
    const result = await pool.request().query(
      `SELECT name, database_id, create_date
       FROM sys.databases
       WHERE name NOT IN ('master','tempdb','model','msdb')
       ORDER BY name`,
    );
    return {
      content: [
        { type: "text", text: JSON.stringify(result.recordset, null, 2) },
      ],
    };
  }

  // ── list-tables ──
  if (name === "list-tables") {
    const pool = await getPool(args.database);
    const result = await pool.request().query(
      `SELECT
         s.name AS [schema],
         t.name AS [table],
         p.rows AS row_count
       FROM sys.tables t
       JOIN sys.schemas s ON t.schema_id = s.schema_id
       JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
       ORDER BY s.name, t.name`,
    );
    return {
      content: [
        { type: "text", text: JSON.stringify(result.recordset, null, 2) },
      ],
    };
  }

  // ── table-schema ──
  if (name === "table-schema") {
    const pool = await getPool(args.database);
    const req = pool.request();
    req.input("tableName", sql.NVarChar, args.table);
    const result = await req.query(
      `SELECT
         c.COLUMN_NAME,
         c.DATA_TYPE,
         c.CHARACTER_MAXIMUM_LENGTH,
         c.IS_NULLABLE,
         c.COLUMN_DEFAULT
       FROM INFORMATION_SCHEMA.COLUMNS c
       WHERE c.TABLE_NAME = @tableName
       ORDER BY c.ORDINAL_POSITION`,
    );
    return {
      content: [
        { type: "text", text: JSON.stringify(result.recordset, null, 2) },
      ],
    };
  }

  // ── query (max 1000 rows, in-memory) ──
  if (name === "query") {
    const queryText = (args.sql || "").trim();
    const blocked = validateReadOnly(queryText);
    if (blocked) {
      return { content: [{ type: "text", text: blocked }], isError: true };
    }

    const pool = await getPool(args.database);
    const req = pool.request();
    req.timeout = 30000;
    const result = await req.query(queryText);

    const rows = result.recordset || [];
    const truncated = rows.length > 1000;
    const output = truncated ? rows.slice(0, 1000) : rows;

    let text = JSON.stringify(output, null, 2);
    if (truncated) {
      text += `\n\n--- Truncated: showing 1000 of ${rows.length} rows ---`;
    }
    text = `${output.length} row(s) returned\n${text}`;

    return { content: [{ type: "text", text }] };
  }

  // ── export-csv (streaming, unlimited rows) ──
  if (name === "export-csv") {
    const queryText = (args.sql || "").trim();
    const blocked = validateReadOnly(queryText);
    if (blocked) {
      return { content: [{ type: "text", text: blocked }], isError: true };
    }

    const outputPath = args.outputPath;
    if (!outputPath) {
      return {
        content: [{ type: "text", text: "outputPath is required" }],
        isError: true,
      };
    }

    // Ensure output directory exists
    const dir = path.dirname(outputPath);
    fs.mkdirSync(dir, { recursive: true });

    const pool = await getPool(args.database);

    // Use streaming for memory-efficient large exports
    const req = pool.request();
    req.stream = true;
    req.timeout = 600000; // 10 min for large exports

    const writeStream = fs.createWriteStream(outputPath, { encoding: "utf8" });
    // UTF-8 BOM for Excel compatibility
    writeStream.write("\uFEFF");

    let rowCount = 0;
    let columns = null;
    let headerWritten = false;
    const startTime = Date.now();

    return new Promise((resolve, reject) => {
      req.on("recordset", (cols) => {
        columns = Object.keys(cols);
        if (!headerWritten) {
          writeStream.write(columns.join(",") + "\n");
          headerWritten = true;
        }
      });

      req.on("row", (row) => {
        rowCount++;
        const line = rowToCsvLine(row, columns);
        const canContinue = writeStream.write(line);

        // Backpressure: pause SQL stream if write buffer is full
        if (!canContinue) {
          req.pause();
          writeStream.once("drain", () => req.resume());
        }

        // Progress log every 100k rows
        if (rowCount % 100000 === 0) {
          process.stderr.write(`  export-csv: ${rowCount.toLocaleString()} rows written...\n`);
        }
      });

      req.on("error", (err) => {
        writeStream.end();
        resolve({
          content: [
            { type: "text", text: `ERROR during export: ${err.message}` },
          ],
          isError: true,
        });
      });

      req.on("done", () => {
        writeStream.end(() => {
          const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);
          const fileSize = fs.statSync(outputPath).size;
          const sizeMB = (fileSize / (1024 * 1024)).toFixed(2);

          const summary = [
            `CSV export complete.`,
            `  File: ${outputPath}`,
            `  Rows: ${rowCount.toLocaleString()}`,
            `  Size: ${sizeMB} MB`,
            `  Time: ${elapsed}s`,
          ].join("\n");

          process.stderr.write(`  export-csv: done — ${rowCount.toLocaleString()} rows, ${sizeMB} MB, ${elapsed}s\n`);

          resolve({ content: [{ type: "text", text: summary }] });
        });
      });

      req.query(queryText);
    });
  }

  throw new Error(`Unknown tool: ${name}`);
});

// ─── Cleanup ─────────────────────────────────────────────────────────────────

async function closePools() {
  const closing = [];
  for (const pool of pools.values()) {
    closing.push(pool.close().catch(() => {}));
  }
  await Promise.all(closing);
  pools.clear();
}

process.on("SIGINT", async () => {
  await closePools();
  process.exit(0);
});

process.on("SIGTERM", async () => {
  await closePools();
  process.exit(0);
});

// ─── Start ───────────────────────────────────────────────────────────────────

async function runServer() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

runServer().catch((err) => {
  console.error(err);
  process.exit(1);
});
