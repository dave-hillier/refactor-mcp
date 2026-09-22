# Plan: CLI with a resident solution daemon

## Status

All nine work items below are implemented. Both open questions are settled: one
daemon per solution, and tool call logging stays (opt-in, without the playback
path it used to have). The test failures this work inherited are tracked
separately in `failing-tests-plan.md`.

## Goal

Make RefactorMCP usable as a plain command line tool without paying the
solution load cost on every invocation. The Roslyn `Solution` must be loaded
once and reused across many commands, with the MCP server becoming an optional
front end rather than the primary one.

## Current state

- Every refactoring is a static method on a `[McpServerToolType]` class that
  takes a `solutionPath` and goes through `RefactoringHelpers.GetOrLoadSolution`.
- Loaded solutions live in static `MemoryCache` fields in `RefactoringHelpers`.
  The cache exists only for the lifetime of the process.
- `Program.cs` already has a `--json <Tool> '<params>'` mode that dispatches to
  the tool methods by reflection. The VS Code extension uses only this mode.
- In `--json` mode each call is a new process, so the cache is always cold and
  `LoadSolution` is effectively a no-op. Only the long-lived MCP stdio process
  benefits from the cache.
- Measured on this repository: process start is about 0.2s, process start plus
  MSBuild workspace open is about 1.2s. Larger solutions will take far longer.

## Target architecture

```
refactor <tool> [args]        thin client, exits immediately
        |
        | unix socket / named pipe, keyed by solution path
        v
refactor serve <solution>     resident daemon, owns the loaded Solution
        |
        v
   existing tool classes      unchanged static methods
```

- The client looks for a running daemon for the requested solution. If none is
  found it spawns one detached, waits for it to signal ready, then sends the
  request. Users never manage the daemon by hand.
- The daemon exits after a configurable idle period.
- The wire protocol is JSON-RPC over the socket. MCP over stdio becomes one
  more transport over the same dispatcher.

## Work items

Each item is independently shippable and ordered so that earlier items reduce
the risk of later ones.

### 1. Fix CLI tool name dispatch

Problem: docs and `ListTools` use kebab-case names (`load-solution`) but the
dispatcher in `Program.cs` and the playback path in `ToolCallLogger.cs` match on
the raw method name (`LoadSolution`). The documented commands fail with
"Unknown tool".

- Accept kebab-case, PascalCase and case-insensitive matches.
- Include `[McpServerPrompt]` methods in the lookup so `ListClassLengths` and
  similar are reachable from the CLI.
- Add a test that every documented tool name in `EXAMPLES.md` resolves.

### 2. Extract a single `ToolDispatcher`

`Program.RunJsonMode` and `ToolCallLogger.InvokeTool` duplicate reflection,
parameter binding and `ConvertInput`. Move this into one class in
`Infrastructure/` with:

- `Resolve(string name) -> MethodInfo?`
- `InvokeAsync(string name, IReadOnlyDictionary<string, JsonElement> args) -> Task<string>`
- A `ListTools()` that returns name, description and parameter schema, so the
  client can print help without loading Roslyn.

Both the CLI and the daemon will use this. Tests cover parameter binding for
string, int, bool, string arrays and JSON objects.

### 3. Replace static caches with a `SolutionSession`

Problem: three static `MemoryCache` fields plus `MoveMethodTool` move history
are process-global. A daemon serving more than one solution, or reloading a
solution, needs isolation.

- Introduce `SolutionSession` owning the `MSBuildWorkspace`, the current
  `Solution`, the syntax tree and semantic model caches, and move history.
- Introduce `SessionRegistry` keyed by normalised solution path.
- `GetOrLoadSolution`, `UpdateSolutionCache`, `ClearAllCaches` and
  `UnloadSolution` become thin wrappers over the registry so tool code does
  not change in this step.
- Move `MSBuildLocator.RegisterDefaults()` to process start. It must run before
  any `Microsoft.Build` type is loaded and lazy registration is fragile.
- Remove `Directory.SetCurrentDirectory` from the load path. Resolve relative
  file paths against the solution directory explicitly via the session.

### 4. Fix logging side effects

- Tool call logs are written to `tool-call-log.jsonl` in the current directory
  whenever a tool runs without a prior `LoadSolution` in the same process.
  Logging should be scoped to the session and written under
  `<solution dir>/.refactor-mcp/`.
- Make logging opt-in or at minimum quiet by default for CLI use.

### 5. Add the `serve` command and socket transport

- `refactor serve --solution <path> [--idle-timeout 10m]`.
- Listens on a Unix domain socket on macOS and Linux and a named pipe on
  Windows. Socket path derives from a hash of the solution path under the
  user's temp or runtime directory.
- Writes a small pid/socket discovery file next to the socket so clients can
  find it and detect stale entries.
- JSON-RPC request shape mirrors the existing `--json` mode:
  `{ "tool": "extract-method", "params": { ... } }`.
- Responses carry either `result` or `error`. Errors keep the existing
  `Error: ...` message text so current consumers keep working.
- Concurrency: one request at a time per session. Roslyn `Solution` is
  immutable but the tools write files and replace the cached solution, so
  serialise mutations through the session.
- Idle timer resets on each request. On expiry the daemon disposes the
  session and exits.

### 6. Make the CLI auto-connect and auto-spawn

- Every tool command tries the daemon first. If the socket is missing or the
  connection is refused, remove stale discovery files, spawn
  `refactor serve` detached, poll for ready, then send.
- `refactor status` lists running daemons and their loaded solutions.
- `refactor stop [--solution <path> | --all]` shuts daemons down.
- `--no-daemon` runs the tool in-process for debugging and CI.
- Update the VS Code extension to call the CLI directly instead of
  `dotnet run`, which currently pays a build check on every invocation.

### 7. Keep the cached solution fresh

Problem: the cache never invalidates. Edits made by an editor or another
process leave the daemon operating on stale text.

- Watch the solution directory with `FileSystemWatcher`.
- On a `.cs` change, apply `Solution.WithDocumentText` for that document.
  Cheap and incremental.
- On `.csproj`, `.sln`, `Directory.Build.props` or `global.json` change, mark
  the session for full reload on the next request.
- Tools that write files already update the cache; make sure the watcher
  ignores those writes to avoid double work.

### 8. Make MCP an optional front end

- Register the MCP server only when invoked with `mcp` or no arguments, using
  the same `ToolDispatcher` and `SessionRegistry`.
- Keep the `[McpServerTool]` and `[Description]` attributes as the single
  source of tool metadata for both CLI help and MCP schema.
- Document the three entry points in `README.md`: `mcp`, `serve`, and direct
  tool commands.

### 9. Command line surface

Replace `--json <Tool> '<json>'` with a conventional interface while keeping
the JSON form for scripting:

```
refactor extract-method --solution ./App.sln --file src/Foo.cs \
    --range 10:5-20:6 --name ComputeTotal
refactor --json extract-method '{"solutionPath": "...", ...}'
refactor list-tools
refactor serve --solution ./App.sln
refactor status
refactor stop --all
```

Generate `--flag` names from the tool method parameters so no per-tool code is
needed. Use `System.CommandLine` or a small hand-rolled parser; avoid pulling
in a framework if the reflection-driven approach stays simple.

## Testing

- Unit tests for `ToolDispatcher` name resolution and parameter binding.
- Unit tests for `SessionRegistry` load, reuse, unload and reload.
- Integration test that starts `serve` on a temp socket, sends a request,
  confirms the second request does not reload the solution, and confirms idle
  shutdown.
- Integration test for the client auto-spawn path.
- Existing tool tests continue to pass unchanged through the wrapper helpers
  in step 3.

## Related

A separate stream for a refactoring catalog with fixture-based tests is in
`catalog-plan.md`.

## Out of scope

- Changing any refactoring behaviour.
- Multi-user or remote daemons. The socket is local to the user.
- Replacing MSBuildWorkspace with a faster loader.

## Open questions

- Should the daemon serve multiple solutions from one process, or one daemon
  per solution? One per solution is simpler to reason about and lets the OS
  reclaim memory on idle exit. Start there.
- Whether to keep tool call logging and playback at all once the CLI is the
  primary interface, since shell history serves the same purpose.
