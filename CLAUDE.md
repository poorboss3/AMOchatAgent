# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Running the Projects

This repo contains two independent .NET 8 APIs and a standalone HTML frontend. They must run concurrently for the system to work.

```bash
# Terminal 1 — Mock third-party API (must start first, runs on :5001)
cd AMOchatAgent.MockApi && dotnet run

# Terminal 2 — Main agent API (runs on :5000 by default)
cd AMOchatAgent.Api && dotnet run

# Frontend — open directly in browser (no server needed)
open frontend/index.html
```

Build only (no run):
```bash
dotnet build AMOchatAgent.Api/AMOchatAgent.Api.csproj
dotnet build AMOchatAgent.MockApi/AMOchatAgent.MockApi.csproj
```

Before running the main API, set a real LLM API key in `AMOchatAgent.Api/appsettings.json` under the active provider.

## Switching LLM Providers

Change `Llm.ActiveProvider` in `AMOchatAgent.Api/appsettings.json`. Supported values: `OpenAI`, `DeepSeek`, `Qwen`, `AzureOpenAI`. Each provider has its own config block under `Llm.Providers`. All providers use the OpenAI-compatible chat completions format; Azure uses a different URL path (`/openai/deployments/{deployment}/chat/completions?api-version=...`).

## Architecture

### Request Flow

```
frontend/index.html
  → POST /api/chat (or /api/chat/stream for SSE)
    → AgentService (tool-calling loop, max Session:MaxAttempts iterations)
      → LlmServiceFactory → OpenAiCompatibleLlmService → LLM API
      → ITool.ExecuteAsync() → MockApi HTTP calls
```

### Key Design: Tool Calling Loop (`AgentService`)

`AgentService` is the core orchestrator. On each user message it runs a `while` loop:
1. Calls LLM with the full `ConversationContext.Messages` history + all registered tool definitions
2. If the LLM responds with `tool_calls`: executes each tool, appends `role: tool` messages, loops again
3. If the LLM responds with plain text (`finish_reason: stop`): returns to the caller

Conversation context is stored in `IMemoryCache` keyed by `sessionId` with sliding expiration. The system prompt is a static string inside `AgentService`.

### Adding a New Third-Party API Integration

1. **MockApi side**: add a controller in `AMOchatAgent.MockApi/Controllers/` — all responses return HTTP 200 with `{ success: bool, errorCode?, message? }` shape even for business errors.
2. **Tool side**: create `AMOchatAgent.Api/Tools/YourTool.cs` implementing `ITool` (3 properties + `ExecuteAsync`). The `ToToolDefinition()` default interface method handles the LLM wire format automatically.
3. **Register**: add `builder.Services.AddScoped<ITool, YourTool>();` in `AMOchatAgent.Api/Program.cs`. No other changes needed — `AgentService` injects `IEnumerable<ITool>` and sends all tools to the LLM on every turn.

### MockApi Validation Rules (for testing multi-turn flows)

- Phone: must match `^1[3-9]\d{9}$`
- Products: P001 (iPhone 16 ¥6999), P002 (小米14 ¥3999), P003 (AirPods Pro ¥1799), P004 (充电宝 ¥99)
- Quantity: 1–10, cannot exceed stock
- Address: minimum 10 characters
- KYC gate: orders > ¥5000 require even last digit in phone number (odd = `KYC_REQUIRED` error)
- Order storage: in-memory `ConcurrentDictionary`, cleared on restart; only `pending` orders can be cancelled

### Frontend (`frontend/index.html`)

Single self-contained file with no build step. `const API_BASE` at the top of the script block sets the backend URL. Toggle `const USE_STREAM = true/false` to switch between SSE and regular POST. Session ID is a UUID generated on load and stored in `sessionStorage`.

## Project Conventions

- All HTTP responses from MockApi use HTTP 200 with a `success` boolean — never 4xx for business validation errors. Tools must parse `success` from the JSON string result.
- `System.Text.Json` throughout — no Newtonsoft.
- LLM models use `[JsonPropertyName]` attributes for snake_case wire format.
- Tools return raw JSON strings (not objects) from `ExecuteAsync` — these are inserted directly as `role: tool` message content.
