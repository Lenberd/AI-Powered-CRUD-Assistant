# AI-Powered CRUD Assistant — Task Manager API

ASP.NET Core 8 Web API + ADO.NET (SQL Server/LocalDB, no ORM) + Google Gemini function calling.
Users type plain English; Gemini decides which task action applies; the C# backend validates and
executes it. The AI never touches the database directly.

## How to run

1. Requires: .NET 8 SDK, SQL Server/LocalDB, a Gemini API key ([aistudio.google.com/apikey](https://aistudio.google.com/apikey)).
2. Connection string is already set in `appsettings.json` for LocalDB — the app auto-creates the
   database and `dbo.Tasks` table on startup, no manual migration needed.
3. Set your API key (don't put it in appsettings.json):
   ```bash
   cd TaskAssistant.Mvc
   dotnet user-secrets init
   dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY"
   ```
4. Run it: `dotnet run`, then open the printed URL.

## AI provider

**Google Gemini** (`gemini-3.6-flash`, model id configurable via `Gemini:Model`), called via its
REST `generateContent` endpoint with native function calling
(`toolConfig.functionCallingConfig.mode = "ANY"` forces a structured function call every time).
Chosen for the generous free tier and first-class tool calling with no SDK needed.

## Architecture

```
Browser → AssistantController → AssistantOrchestrator → ITaskService → ITaskRepository (ADO.NET) → SQL Server
                                        │
                                        └──▶ IAiAssistantClient (GeminiAiClient) — only ever
                                             returns a proposed function name + args. It has no
                                             reference to the database layer at all.
```

- `Data/` — `SqlTaskRepository`: 100% ADO.NET, every query parameterized.
- `Services/` — `TaskService`: validation + title-resolution logic, used by both the plain
  REST controller and the AI orchestrator.
- `Assistant/` — `AssistantToolCatalog` (tool schemas: `create_task`, `update_task`,
  `delete_task`, `get_task`, `list_tasks`, `reject_request`), `GeminiAiClient` (talks to Gemini
  only), `AssistantOrchestrator` (the bridge that validates and executes a proposed call),
  `ConversationStore` (multi-turn context).
- `Controllers/TasksController.cs` — baseline CRUD, no AI.
- `Controllers/AssistantController.cs` — `POST /api/assistant`.

## Bonus features implemented

- **Multi-turn context** — a `conversationId` threads recent turns back into Gemini, so "actually
  make that due tomorrow" resolves without repeating the task title.
- **Delete confirmation** — `delete_task` never runs immediately; it returns
  `confirmation_required` first, and a second confirmed request (no AI round-trip) executes it.
- **Audit logging** — every AI-proposed call, accepted or rejected, is logged server-side
  (`ILogger`) and also shown live in the UI's sidebar audit log.

## Error handling (Section 5)

| Case | What happens |
|---|---|
| Off-topic message | Gemini calls `reject_request(reason)` → `{status: "rejected"}` |
| Task doesn't exist | `TaskNotFoundException` → `{status: "not_found"}`, HTTP 200 |
| Ambiguous title match | 2+ matches → `{status: "ambiguous", candidates: [...]}`, never guesses |
| Missing/invalid AI args | Backend re-validates independently of the AI → `{status: "invalid_arguments"}` |
| AI times out / unavailable | Wrapped as `AiUnavailableException` → HTTP 503, friendly message |
| SQL-injection-shaped input | Every query uses `SqlParameter` — value is always data, never SQL text |

Any other unexpected exception is caught at the controller and returns `{status: "error"}` (HTTP 500) instead of crashing.

## Short answer questions

**1. Why shouldn't the AI have direct DB/SQL access?**
Its output is untrusted input, not trusted code. Direct SQL access means a bad or manipulated
prompt could drop tables, run unbounded deletes, or leak data. Limiting it to a fixed set of typed
functions means the worst it can do is propose the wrong call — which the backend still validates,
resolves, and (for deletes) confirms before anything happens.

**2. What's required vs. optional in `create_task`?**
Only `title` is required — a task without one isn't a task. `dueDate` and `isDone` are optional,
matching the entity's own defaults (`IsDone = false`, `DueDate = null`), and the backend
re-validates the title itself rather than trusting the AI's schema conformance.

**3. What would you add with more time?**
Auth + rate limiting on `/api/assistant` — right now anyone who can reach it can spend Gemini
quota and mutate any task with no per-user scoping. I'd also persist the audit log to a table
instead of just `ILogger`.

## Example interactions

**1. SQL injection attempt** — `{"message":"Create task Robert'); DROP TABLE Tasks;--"}` →
`{"status":"success","action":"create_task","result":"Created task 7: \"Robert'); DROP TABLE Tasks;--\"."}`
— title stored byte-for-byte, `Tasks` table untouched.

**2. Not-found error** — `{"message":"delete all"}` →
`{"status":"not_found","action":"delete_task","result":"No task matching \"all\" was found."}`

**3. Ambiguous reference** — `{"message":"Delete the report task"}` (4 tasks contain "report") →
`{"status":"ambiguous","action":"delete_task","candidates":[...4 tasks...]}`

Reproduce with curl:
```bash
curl -X POST http://localhost:5299/api/assistant -H "Content-Type: application/json" \
  -d '{"message":"Mark task 999 as done"}'
```

Baseline CRUD: `curl http://localhost:5299/api/tasks`
