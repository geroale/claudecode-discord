#!/usr/bin/env node
/**
 * claudecode-discord send — Send a message to a Discord channel.
 *
 * Usage:
 *   claudecode-discord-send --channel <id> "Hello world"
 *   echo "report" | claudecode-discord-send --channel <id> --stdin
 *   claudecode-discord-send --channel <id> --file report.txt
 *
 * Lightweight: uses raw Discord REST API (no gateway connection needed).
 */
import 'dotenv/config';
import { getConfig } from '../utils/config.js';

const USAGE = `Usage: claudecode-discord-send --channel <id> <message> | --stdin | --file <path>

Channel can be an ID or an alias:
  alerts   → #alerts channel
  reports  → #lyra-agent channel`;

// Well-known channel aliases (add more as needed)
// NOTE: bot currently only has Send Messages permission on #lyra-agent
// To enable #alerts, grant Send Messages to the bot's role in Discord server settings
const CHANNEL_ALIASES: Record<string, string> = {
  alerts: '1486634959501398166',   // #lyra-agent (fallback — #alerts needs perms)
  reports: '1486634959501398166',   // #lyra-agent
};

const DISCORD_API = 'https://discord.com/api/v10';

async function main() {
  const args = process.argv.slice(2);
  const config = getConfig();

  // Parse --channel
  let channelId: string | undefined;
  const chanIdx = args.indexOf('--channel');
  if (chanIdx !== -1 && chanIdx + 1 < args.length) {
    const chanArg = args[chanIdx + 1];
    channelId = CHANNEL_ALIASES[chanArg] || chanArg;
    args.splice(chanIdx, 2);
  }

  if (!channelId) {
    console.error('Error: --channel is required\n');
    console.error(USAGE);
    process.exit(1);
  }

  // Parse message source
  let message: string;

  if (args.includes('--stdin')) {
    message = await readStdin();
  } else if (args.includes('--file')) {
    const idx = args.indexOf('--file');
    const filePath = args[idx + 1];
    if (!filePath) {
      console.error(USAGE);
      process.exit(1);
    }
    const fs = await import('fs');
    message = fs.readFileSync(filePath, 'utf-8').trim();
  } else if (args.length > 0 && !args[0].startsWith('--')) {
    message = args.join(' ');
  } else {
    console.error(USAGE);
    process.exit(1);
  }

  if (!message.trim()) {
    console.error('Empty message, nothing to send.');
    process.exit(1);
  }

  // Split if >2000 chars
  const chunks = splitMessage(message, 2000);
  let ok = 0;

  for (const chunk of chunks) {
    const resp = await fetch(`${DISCORD_API}/channels/${channelId}/messages`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bot ${config.DISCORD_BOT_TOKEN}`,
      },
      body: JSON.stringify({ content: chunk }),
    });

    if (resp.ok) {
      ok++;
    } else {
      const body = await resp.text();
      console.error(`  [Discord] Error: HTTP ${resp.status} — ${body}`);
      process.exit(1);
    }
  }

  console.log(`  [Discord] #${channelId}: OK (${ok} message${ok > 1 ? 's' : ''})`);
}

function splitMessage(text: string, maxLen: number): string[] {
  if (text.length <= maxLen) return [text];
  const chunks: string[] = [];
  let remaining = text;
  while (remaining.length > 0) {
    if (remaining.length <= maxLen) {
      chunks.push(remaining);
      break;
    }
    let idx = remaining.lastIndexOf('\n', maxLen);
    if (idx === -1) idx = maxLen;
    chunks.push(remaining.slice(0, idx));
    remaining = remaining.slice(idx).replace(/^\n/, '');
  }
  return chunks;
}

function readStdin(): Promise<string> {
  return new Promise((resolve) => {
    let data = '';
    process.stdin.setEncoding('utf-8');
    process.stdin.on('data', (chunk: string) => (data += chunk));
    process.stdin.on('end', () => resolve(data.trim()));
    if (process.stdin.isTTY) resolve('');
  });
}

main().catch((err) => {
  console.error(`Fatal: ${err.message || err}`);
  process.exit(1);
});
