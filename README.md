# Claude Code Discord Controller

A Discord bot that manages multiple Claude Code sessions remotely via Discord (desktop, web, and mobile).

Run independent Claude Code sessions per channel, with tool use approval/denial via Discord buttons.

> **[Korean documentation (한국어)](README.kr.md)**

## Features

- 📱 Remote control Claude Code from Discord (desktop/web/mobile)
- 🔀 Independent sessions per channel (project directory mapping)
- ✅ Tool use approve/deny via Discord button UI
- ❓ Interactive question UI (selectable options + custom text input)
- ⏹️ Stop button for instant cancellation during progress
- 📎 File attachments support (images, documents, code files)
- 🔄 Session resume/delete/new (persist across bot restarts, last conversation preview)
- ⏱️ Real-time progress display (tool usage, elapsed time)
- 🔒 User whitelist, rate limiting, path security, duplicate instance prevention

## Tech Stack

| Category | Technology |
|----------|------------|
| Runtime | Node.js 20+, TypeScript |
| Discord | discord.js v14 |
| AI | @anthropic-ai/claude-agent-sdk |
| DB | better-sqlite3 (SQLite) |
| Validation | zod v4 |
| Build | tsup (ESM) |
| Test | vitest |

## Installation

```bash
git clone git@github.com:chadingTV/claudecode-discord.git
cd claudecode-discord

# Auto install (Node.js, Claude Code CLI, npm packages)
./install.sh        # macOS / Linux
install.bat         # Windows

# Or manual install
npm install
cp .env.example .env
npm run dev
```

For Discord bot creation, environment variables, Windows setup, and Claude Code installation,
see the full setup guide at **[SETUP.md](SETUP.md)**.

## Project Structure

```
claudecode-discord/
├── install.sh              # macOS/Linux auto-installer
├── install.bat             # Windows auto-installer
├── .env.example            # Environment variable template
├── src/
│   ├── index.ts            # Entry point
│   ├── bot/
│   │   ├── client.ts       # Discord bot init & events
│   │   ├── commands/       # Slash commands
│   │   │   ├── register.ts
│   │   │   ├── unregister.ts
│   │   │   ├── status.ts
│   │   │   ├── stop.ts
│   │   │   ├── auto-approve.ts
│   │   │   ├── sessions.ts
│   │   │   ├── last.ts
│   │   │   └── clear-sessions.ts
│   │   └── handlers/       # Event handlers
│   │       ├── message.ts
│   │       └── interaction.ts
│   ├── claude/
│   │   ├── session-manager.ts   # Session lifecycle
│   │   └── output-formatter.ts  # Discord output formatting
│   ├── db/
│   │   ├── database.ts     # SQLite init & queries
│   │   └── types.ts
│   ├── security/
│   │   └── guard.ts        # Auth, rate limit
│   └── utils/
│       └── config.ts       # Env var validation (zod)
├── SETUP.md                # Detailed setup guide
├── package.json
└── tsconfig.json
```

## Usage

| Command | Description | Example |
|---------|-------------|---------|
| `/register <folder>` | Link current channel to a project | `/register my-project` |
| `/unregister` | Unlink channel | |
| `/status` | Check all session statuses | |
| `/stop` | Stop current channel's session | |
| `/auto-approve on\|off` | Toggle auto-approval | `/auto-approve on` |
| `/sessions` | List sessions to resume or delete | |
| `/last` | Show the last Claude response from current session | |
| `/clear-sessions` | Delete all session files for the project | |

The `/register` path is resolved relative to the `BASE_PROJECT_DIR` set in your `.env` file.
For example, if `BASE_PROJECT_DIR=/Users/you/projects`, then `/register my-project` maps to `/Users/you/projects/my-project`. Absolute paths also work: `/register path:/Users/you/other/project`.

Send a **regular message** in a registered channel and Claude will respond.
Attach images, documents, or code files and Claude can read and analyze them.

### In-Progress Controls

- **⏹️ Stop** button on progress messages for instant cancellation
- Sending a new message while busy shows "previous task in progress" notice
- `/stop` slash command also available

## Architecture

```
[Mobile Discord] ←→ [Discord Bot] ←→ [Session Manager] ←→ [Claude Agent SDK]
                          ↕
                     [SQLite DB]
```

- Independent sessions per channel (project directory mapping)
- Claude Agent SDK runs Claude Code as subprocess (shares existing auth)
- Tool use approval via Discord buttons (auto-approve mode supported)
- Streaming responses edited every 1.5s into Discord messages
- Heartbeat progress display every 15s until text output begins
- Markdown code blocks preserved across message splits

## Session States

| State | Meaning |
|-------|---------|
| 🟢 online | Claude is working |
| 🟡 waiting | Waiting for tool use approval |
| ⚪ idle | Task complete, waiting for input |
| 🔴 offline | No session |

## Security

### Zero External Attack Surface

This bot **does not open any HTTP servers, ports, or API endpoints.** It connects to Discord via an outbound WebSocket — there is no inbound listener, so there is no network path for external attackers to reach this bot.

```
Typical web server:  External → [Port open, waiting] → Receives requests  (inbound)
This bot:            Bot → [Connects to Discord] → Receives events         (outbound only)
```

### Self-Hosted Architecture

The bot runs entirely on your own PC/server. No external servers involved, and no data leaves your machine except through Discord and the Anthropic API (which uses your own Claude Code login session).

### Access Control

- `ALLOWED_USER_IDS` whitelist-based authentication — all messages and commands from unregistered users are ignored
- Discord servers are private by default (no access without invite link)
- Per-minute request rate limiting

### Execution Protection

- Tool use default: file modifications, command execution, etc. **require user approval each time** (Discord buttons)
- Path traversal (`..`) blocked
- File attachments: executable files (.exe, .bat, etc.) blocked, 25MB size limit

### Precautions

- The `.env` file contains your bot token — **never share it publicly.** If compromised, immediately Reset Token in Discord Developer Portal
- `auto-approve` mode is convenient but may allow Claude to perform unintended actions — use only on trusted projects

## Development

```bash
npm run dev          # Dev mode (tsx)
npm run build        # Production build (tsup)
npm start            # Run built files
npm test             # Tests (vitest)
npm run test:watch   # Test watch mode
```

## License

[MIT License](LICENSE) - Free to use, modify, and distribute commercially. Attribution required: include the original copyright notice and link to [this repository](https://github.com/chadingTV/claudecode-discord).
