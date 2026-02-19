#!/usr/bin/env node

// ============================================
//  Invekto Server Ops MCP Server v1.0
//  SSH/SFTP tools for production server management
//  Tools: server-exec, server-upload, server-download,
//         server-logs, server-status, server-deploy, server-health
// ============================================

import { Client } from "ssh2";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import {
  readFileSync,
  readdirSync,
  statSync,
  existsSync,
  unlinkSync,
} from "fs";
import { join, relative, dirname } from "path";
import { execSync } from "child_process";
import { tmpdir } from "os";

// ── Configuration ──────────────────────────────────────────────
const SSH_HOST = process.env.SSH_HOST || "services.invekto.com";
const SSH_PORT = parseInt(process.env.SSH_PORT || "22", 10);
const SSH_USER = process.env.SSH_USER || "Administrator";
const SSH_PASSWORD = process.env.SSH_PASSWORD;
const SSH_PRIVATE_KEY_PATH = process.env.SSH_PRIVATE_KEY_PATH;
const SERVER_BASE = process.env.SERVER_BASE_PATH || "C:\\Invekto";
const NSSM_PATH = process.env.NSSM_PATH || "C:\\nssm.exe";

// ── Service Definitions ────────────────────────────────────────
const SERVICES = {
  Backend:           { nssm: "InvektoBackend",           port: 5000, path: "Backend" },
  ChatAnalysis:      { nssm: "InvektoChatAnalysis",      port: 7101, path: "ChatAnalysis" },
  Appointments:      { nssm: "InvektoAppointments",      port: 7102, path: "Appointments" },
  Knowledge:         { nssm: "InvektoKnowledge",         port: 7104, path: "Knowledge" },
  AgentAI:           { nssm: "InvektoAgentAI",           port: 7105, path: "AgentAI" },
  Integrations:      { nssm: "InvektoIntegrations",      port: 7106, path: "Integrations" },
  Outbound:          { nssm: "InvektoOutbound",          port: 7107, path: "Outbound" },
  Automation:        { nssm: "InvektoAutomation",        port: 7108, path: "Automation" },
  WhatsAppAnalytics: { nssm: "InvektoWhatsAppAnalytics", port: 7109, path: "WhatsAppAnalytics" },
  Marketing:         { nssm: "InvektoMarketing",         port: 7112, path: "Marketing" },
};
const SERVICE_NAMES = Object.keys(SERVICES);

// ── SSH Connection Manager ─────────────────────────────────────
let sshClient = null;
let sshReady = false;

function getSSHConfig() {
  const config = {
    host: SSH_HOST,
    port: SSH_PORT,
    username: SSH_USER,
    readyTimeout: 15000,
    keepaliveInterval: 30000,
    keepaliveCountMax: 3,
  };
  if (SSH_PRIVATE_KEY_PATH) {
    config.privateKey = readFileSync(SSH_PRIVATE_KEY_PATH);
  } else if (SSH_PASSWORD) {
    config.password = SSH_PASSWORD;
  } else {
    throw new Error("SSH_PASSWORD or SSH_PRIVATE_KEY_PATH must be set");
  }
  return config;
}

function ensureConnection() {
  return new Promise((resolve, reject) => {
    if (sshClient && sshReady) return resolve(sshClient);

    if (sshClient) {
      try { sshClient.end(); } catch { /* ignore */ }
    }

    const conn = new Client();
    conn.on("ready", () => {
      sshClient = conn;
      sshReady = true;
      process.stderr.write(`[invekto-ops] SSH connected: ${SSH_USER}@${SSH_HOST}:${SSH_PORT}\n`);
      resolve(conn);
    });
    conn.on("error", (err) => {
      sshReady = false;
      reject(new Error(`SSH connection failed: ${err.message}`));
    });
    conn.on("close", () => {
      sshReady = false;
    });
    conn.connect(getSSHConfig());
  });
}

// ── SSH Helpers ────────────────────────────────────────────────

/** Encode a PowerShell script as Base64 for -EncodedCommand (avoids all quoting issues) */
function encodePSCommand(script) {
  const encoded = Buffer.from(script, "utf16le").toString("base64");
  return `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand ${encoded}`;
}

/** Execute a command on the remote server via SSH */
function sshExec(conn, command, timeoutMs = 30000) {
  return new Promise((resolve, reject) => {
    let settled = false;
    const timer = setTimeout(() => {
      if (!settled) {
        settled = true;
        reject(new Error(`Command timed out after ${timeoutMs}ms`));
      }
    }, timeoutMs);

    conn.exec(command, (err, stream) => {
      if (err) {
        clearTimeout(timer);
        if (!settled) { settled = true; reject(err); }
        return;
      }

      let stdout = "", stderr = "";
      stream.on("data", (d) => { stdout += d.toString(); });
      stream.stderr.on("data", (d) => { stderr += d.toString(); });
      stream.on("close", (code) => {
        clearTimeout(timer);
        if (!settled) {
          settled = true;
          resolve({ stdout: stdout.trim(), stderr: stderr.trim(), code });
        }
      });
      stream.on("error", (streamErr) => {
        clearTimeout(timer);
        if (!settled) { settled = true; reject(streamErr); }
      });
    });
  });
}

/** Execute a PowerShell script on the remote server (auto-encodes) */
function psExec(conn, script, timeoutMs = 30000) {
  return sshExec(conn, encodePSCommand(script), timeoutMs);
}

/** Get SFTP session from SSH connection */
function getSFTP(conn) {
  return new Promise((resolve, reject) => {
    conn.sftp((err, sftp) => {
      if (err) return reject(err);
      resolve(sftp);
    });
  });
}

/** Upload a single file via SFTP */
function sftpUpload(sftp, localPath, remotePath) {
  return new Promise((resolve, reject) => {
    sftp.fastPut(localPath, remotePath, (err) => {
      if (err) return reject(new Error(`Upload failed ${remotePath}: ${err.message}`));
      resolve();
    });
  });
}

/** Download a file via SFTP and return as Buffer */
function sftpDownload(sftp, remotePath) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    const stream = sftp.createReadStream(remotePath);
    stream.on("data", (chunk) => chunks.push(chunk));
    stream.on("end", () => resolve(Buffer.concat(chunks)));
    stream.on("error", (err) => reject(new Error(`Download failed ${remotePath}: ${err.message}`)));
  });
}

/** Create remote directory (ignores "already exists" errors) */
function sftpMkdir(sftp, remotePath) {
  return new Promise((resolve) => {
    sftp.mkdir(remotePath, () => resolve());
  });
}

/** Recursively list all files in a local directory */
function getFilesRecursive(dir, base = dir) {
  const results = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const fullPath = join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...getFilesRecursive(fullPath, base));
    } else if (entry.isFile()) {
      results.push({ local: fullPath, relative: relative(base, fullPath) });
    }
  }
  return results;
}

// ── MCP Server Setup ───────────────────────────────────────────
const mcpServer = new Server(
  { name: "invekto-ops", version: "1.0.0" },
  { capabilities: { tools: {} } },
);

mcpServer.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: "server-exec",
      description:
        "Execute a PowerShell command on the production server via SSH. " +
        "REQUIRES Q's approval for destructive commands.",
      inputSchema: {
        type: "object",
        properties: {
          command: {
            type: "string",
            description: "PowerShell command/script to execute on the server",
          },
          timeout: {
            type: "number",
            description: "Timeout in ms (default: 30000, max: 120000)",
          },
        },
        required: ["command"],
      },
    },
    {
      name: "server-upload",
      description: "Upload a single file from dev PC to the production server via SFTP.",
      inputSchema: {
        type: "object",
        properties: {
          localPath: {
            type: "string",
            description: "Absolute file path on dev PC",
          },
          remotePath: {
            type: "string",
            description:
              "Absolute path on server (e.g. C:\\Invekto\\scripts\\deploy-watcher.ps1)",
          },
        },
        required: ["localPath", "remotePath"],
      },
    },
    {
      name: "server-download",
      description:
        "Download and read a file from the production server. Returns content as text.",
      inputSchema: {
        type: "object",
        properties: {
          remotePath: {
            type: "string",
            description: "Absolute path on server",
          },
          encoding: {
            type: "string",
            enum: ["utf8", "base64"],
            description: "Output encoding (default: utf8)",
          },
        },
        required: ["remotePath"],
      },
    },
    {
      name: "server-logs",
      description:
        "Read service logs from the production server. Tail last N lines with optional text search.",
      inputSchema: {
        type: "object",
        properties: {
          service: {
            type: "string",
            enum: SERVICE_NAMES,
            description: "Service name",
          },
          logType: {
            type: "string",
            enum: ["stdout", "stderr"],
            description: "Log type (default: stderr)",
          },
          lines: {
            type: "number",
            description: "Number of lines (default: 50, max: 500)",
          },
          search: {
            type: "string",
            description: "Filter lines containing this text",
          },
        },
        required: ["service"],
      },
    },
    {
      name: "server-status",
      description: "Check NSSM service status. Returns running/stopped state for one or all services.",
      inputSchema: {
        type: "object",
        properties: {
          service: {
            type: "string",
            enum: [...SERVICE_NAMES, "all"],
            description: "Service name or 'all'",
          },
        },
        required: ["service"],
      },
    },
    {
      name: "server-deploy",
      description:
        "Deploy a service: stop service → zip & upload → extract → start → health check. " +
        "Preserves appsettings.Production.json. REQUIRES Q's approval.",
      inputSchema: {
        type: "object",
        properties: {
          service: {
            type: "string",
            enum: SERVICE_NAMES,
            description: "Service to deploy",
          },
          localPath: {
            type: "string",
            description:
              "Path to dotnet publish output on dev PC (e.g. deploy_output/Backend)",
          },
          skipHealthCheck: {
            type: "boolean",
            description: "Skip post-deploy health check (default: false)",
          },
        },
        required: ["service", "localPath"],
      },
    },
    {
      name: "server-health",
      description:
        "HTTP health check for services. Calls /health on localhost from the server.",
      inputSchema: {
        type: "object",
        properties: {
          service: {
            type: "string",
            enum: [...SERVICE_NAMES, "all"],
            description: "Service name or 'all'",
          },
        },
        required: ["service"],
      },
    },
  ],
}));

// ── Tool Router ────────────────────────────────────────────────
mcpServer.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  const conn = await ensureConnection();
  try {
    switch (name) {
      case "server-exec":
        return await handleExec(conn, args);
      case "server-upload":
        return await handleUpload(conn, args);
      case "server-download":
        return await handleDownload(conn, args);
      case "server-logs":
        return await handleLogs(conn, args);
      case "server-status":
        return await handleStatus(conn, args);
      case "server-deploy":
        return await handleDeploy(conn, args);
      case "server-health":
        return await handleHealth(conn, args);
      default:
        throw new Error(`Unknown tool: ${name}`);
    }
  } catch (err) {
    if (
      err.message?.includes("SSH") ||
      err.message?.includes("Not connected") ||
      err.message?.includes("ECONNRESET")
    ) {
      sshReady = false;
    }
    return {
      content: [{ type: "text", text: `ERROR: ${err.message}` }],
      isError: true,
    };
  }
});

// ── Tool Handlers ──────────────────────────────────────────────

async function handleExec(conn, args) {
  const timeout = Math.min(args.timeout || 30000, 120000);
  const result = await psExec(conn, args.command, timeout);

  let output = "";
  if (result.stdout) output += result.stdout;
  if (result.stderr) output += (output ? "\n--- STDERR ---\n" : "") + result.stderr;
  if (!output) output = `(no output) exit code: ${result.code}`;

  return { content: [{ type: "text", text: output }] };
}

async function handleUpload(conn, args) {
  if (!existsSync(args.localPath)) {
    throw new Error(`Local file not found: ${args.localPath}`);
  }

  const sftp = await getSFTP(conn);
  const remotePath = args.remotePath.replace(/\\/g, "/");

  // Ensure remote directory exists
  const remoteDir = remotePath.substring(0, remotePath.lastIndexOf("/"));
  if (remoteDir) {
    const parts = remoteDir.split("/").filter(Boolean);
    let current = "";
    for (const part of parts) {
      current += (current.endsWith("/") ? "" : "/") + part;
      if (current.length <= 2 && current.includes(":")) continue; // Skip drive letter
      await sftpMkdir(sftp, current);
    }
  }

  await sftpUpload(sftp, args.localPath, remotePath);

  const stats = statSync(args.localPath);
  const sizeKB = (stats.size / 1024).toFixed(1);
  return {
    content: [
      { type: "text", text: `OK: ${args.localPath} → ${args.remotePath} (${sizeKB} KB)` },
    ],
  };
}

async function handleDownload(conn, args) {
  const sftp = await getSFTP(conn);
  const remotePath = args.remotePath.replace(/\\/g, "/");
  const encoding = args.encoding || "utf8";

  const buffer = await sftpDownload(sftp, remotePath);
  const text =
    encoding === "base64"
      ? buffer.toString("base64")
      : buffer.toString("utf8");

  return { content: [{ type: "text", text }] };
}

async function handleLogs(conn, args) {
  const svc = SERVICES[args.service];
  if (!svc) throw new Error(`Unknown service: ${args.service}`);

  const logType = args.logType || "stderr";
  const lines = Math.min(args.lines || 50, 500);
  const logFile = `${SERVER_BASE}\\${svc.path}\\logs\\service-${logType}.log`;

  let script = `Get-Content '${logFile}' -Tail ${lines} -ErrorAction SilentlyContinue`;
  if (args.search) {
    // Escape single quotes in search term
    const safe = args.search.replace(/'/g, "''");
    script += ` | Select-String -SimpleMatch '${safe}'`;
  }

  const result = await psExec(conn, script, 15000);
  const output = result.stdout || `(empty — no log at ${logFile})`;

  return {
    content: [
      {
        type: "text",
        text: `--- ${args.service} ${logType} (last ${lines} lines) ---\n${output}`,
      },
    ],
  };
}

async function handleStatus(conn, args) {
  if (args.service === "all") {
    const lines = [];
    for (const [name, svc] of Object.entries(SERVICES)) {
      const res = await sshExec(conn, `"${NSSM_PATH}" status ${svc.nssm}`, 5000);
      lines.push(`${name.padEnd(20)} ${res.stdout || "UNKNOWN"}`);
    }
    return { content: [{ type: "text", text: lines.join("\n") }] };
  }

  const svc = SERVICES[args.service];
  if (!svc) throw new Error(`Unknown service: ${args.service}`);

  const res = await sshExec(conn, `"${NSSM_PATH}" status ${svc.nssm}`, 5000);
  return { content: [{ type: "text", text: `${args.service}: ${res.stdout || "UNKNOWN"}` }] };
}

async function handleDeploy(conn, args) {
  const svc = SERVICES[args.service];
  if (!svc) throw new Error(`Unknown service: ${args.service}`);

  const localPath = args.localPath.replace(/\//g, "\\");
  const remoteCurrent = `${SERVER_BASE}\\${svc.path}\\current`;
  const log = [];

  // Validate local path
  if (!existsSync(localPath)) {
    throw new Error(`Local path not found: ${localPath}`);
  }

  const localFiles = getFilesRecursive(localPath);
  if (localFiles.length === 0) {
    throw new Error(`No files in ${localPath}`);
  }
  log.push(`[1/6] Found ${localFiles.length} files to deploy`);

  // Step 1: Stop service
  log.push(`[2/6] Stopping ${svc.nssm}...`);
  await sshExec(conn, `"${NSSM_PATH}" stop ${svc.nssm}`, 15000);
  await sleep(3000);
  log.push("  Service stopped");

  try {
    // Step 2: Backup appsettings.Production.json
    log.push("[3/6] Backing up config, cleaning directory...");
    await psExec(
      conn,
      `
        $current = '${remoteCurrent}'
        $bak = '${SERVER_BASE}\\${svc.path}\\appsettings.Production.json.bak'
        $cfg = Join-Path $current 'appsettings.Production.json'
        if (Test-Path $cfg) { Copy-Item $cfg $bak -Force; Write-Host 'Config backed up' }
        if (Test-Path $current) {
          Get-ChildItem $current | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
          Write-Host 'Directory cleaned'
        }
      `,
      30000,
    );

    // Step 3: Zip locally, upload, extract on server
    log.push("[4/6] Zipping and uploading...");
    const zipName = `deploy-${svc.path}-${Date.now()}.zip`;
    const localZip = join(tmpdir(), zipName);
    const remoteZip = `${SERVER_BASE}\\${svc.path}\\${zipName}`;

    // Zip locally using PowerShell
    execSync(
      `powershell -NoProfile -Command "Compress-Archive -Path '${localPath}\\*' -DestinationPath '${localZip}' -Force"`,
      { timeout: 60000 },
    );

    const zipSizeKB = (statSync(localZip).size / 1024).toFixed(0);
    log.push(`  Zip created: ${zipSizeKB} KB`);

    // Upload zip via SFTP
    const sftp = await getSFTP(conn);
    await sftpUpload(sftp, localZip, remoteZip.replace(/\\/g, "/"));
    log.push("  Zip uploaded");

    // Clean up local zip
    try { unlinkSync(localZip); } catch { /* ignore */ }

    // Extract on server
    await psExec(
      conn,
      `Expand-Archive -Path '${remoteZip}' -DestinationPath '${remoteCurrent}' -Force`,
      60000,
    );
    log.push("  Extracted on server");

    // Remove remote zip
    await psExec(conn, `Remove-Item '${remoteZip}' -Force -ErrorAction SilentlyContinue`, 5000);

    // Step 4: Restore appsettings.Production.json
    log.push("[5/6] Restoring config...");
    const restoreResult = await psExec(
      conn,
      `
        $bak = '${SERVER_BASE}\\${svc.path}\\appsettings.Production.json.bak'
        $cfg = '${remoteCurrent}\\appsettings.Production.json'
        if (Test-Path $bak) {
          Copy-Item $bak $cfg -Force
          Remove-Item $bak -Force
          Write-Host 'Config restored'
        } else {
          Write-Host 'WARNING: No config backup found'
        }
      `,
      10000,
    );
    log.push(`  ${restoreResult.stdout || "done"}`);

    // Step 5: Start service
    log.push(`[6/6] Starting ${svc.nssm}...`);
    await sshExec(conn, `"${NSSM_PATH}" start ${svc.nssm}`, 15000);

    // Health check
    if (!args.skipHealthCheck) {
      await sleep(5000);
      const healthScript = `
        try {
          $r = Invoke-RestMethod "http://localhost:${svc.port}/health" -TimeoutSec 10
          "HEALTHY: $r"
        } catch {
          "UNHEALTHY: $($_.Exception.Message)"
        }
      `;
      const health = await psExec(conn, healthScript, 15000);
      log.push(`  Health: ${health.stdout || "no response"}`);
    }

    log.push(`\nDEPLOY ${args.service} COMPLETE`);
  } catch (err) {
    // Recovery: try to restart service with whatever files are there
    log.push(`\nERROR during deploy: ${err.message}`);
    log.push("Attempting service restart for recovery...");
    try {
      // Restore config backup if it exists
      await psExec(
        conn,
        `
          $bak = '${SERVER_BASE}\\${svc.path}\\appsettings.Production.json.bak'
          $cfg = '${remoteCurrent}\\appsettings.Production.json'
          if (Test-Path $bak) { Copy-Item $bak $cfg -Force }
        `,
        5000,
      );
      await sshExec(conn, `"${NSSM_PATH}" start ${svc.nssm}`, 15000);
      log.push("Service restarted (may have old or partial files)");
    } catch (recoverErr) {
      log.push(`CRITICAL: Recovery failed — ${recoverErr.message}`);
    }
  }

  return { content: [{ type: "text", text: log.join("\n") }] };
}

async function handleHealth(conn, args) {
  const check = async (name, svc) => {
    const script = `
      try {
        $r = Invoke-RestMethod "http://localhost:${svc.port}/health" -TimeoutSec 5
        "OK — $r"
      } catch {
        "FAIL — $($_.Exception.Message)"
      }
    `;
    const res = await psExec(conn, script, 10000);
    return `${name.padEnd(20)} :${svc.port}  ${res.stdout || "FAIL — no response"}`;
  };

  if (args.service === "all") {
    const results = [];
    for (const [name, svc] of Object.entries(SERVICES)) {
      results.push(await check(name, svc));
    }
    return { content: [{ type: "text", text: results.join("\n") }] };
  }

  const svc = SERVICES[args.service];
  if (!svc) throw new Error(`Unknown service: ${args.service}`);
  return { content: [{ type: "text", text: await check(args.service, svc) }] };
}

// ── Utility ────────────────────────────────────────────────────
function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// ── Start Server ───────────────────────────────────────────────
async function main() {
  process.stderr.write("[invekto-ops] Starting...\n");
  process.stderr.write(`[invekto-ops] Target: ${SSH_USER}@${SSH_HOST}:${SSH_PORT}\n`);
  process.stderr.write(`[invekto-ops] Base path: ${SERVER_BASE}\n`);

  const transport = new StdioServerTransport();
  await mcpServer.connect(transport);
}

main().catch((err) => {
  process.stderr.write(`[invekto-ops] Fatal: ${err.message}\n`);
  process.exit(1);
});
