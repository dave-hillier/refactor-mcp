# Refactoring MCP Server Contribution Guide

## 1. Big Picture

| Layer                          | What It Does                                                                       | Where To Learn More                                     |
| ------------------------------ | ---------------------------------------------------------------------------------- | ------------------------------------------------------- |
| **MCP Server**                 | Hosts “tools”, “resources”, and “prompts” that the client (human or LLM) can call. | MCP C# SDK README → *Getting Started (Server)* section. |
| **Refactoring Logic (Roslyn)** | Analysers, code‑fixes and syntax transforms that change or inspect code safely.    | Roslyn SDK quick‑starts and samples.                    |

MCP gives us the protocol surface; Roslyn gives us the ability to *understand* and *rewrite* C#; everything else is standard .NET hosting.

---

## 2. Key MCP Concepts (30‑second refresher)

| Concept                              | What It Represents                                                                | Typical Use in This Repo                    |
| ------------------------------------ | --------------------------------------------------------------------------------- | ------------------------------------------- |
| **Tool** (`[McpServerTool]`)         | A *side‑effect‑free* operation that *returns* something (e.g., “Extract Method”). | Main refactoring entry‑points.              |
| **Resource** (`[McpServerResource]`) | Read‑only contextual data, addressable via URI templates.                         | Metrics, dependency graphs.                 |
| **Prompt** (`[McpServerPrompt]`)     | A templated `ChatMessage` that instructs an LLM.                                  | Enforce architectural or style constraints. |

> **Tip:** One class → one concept. Mixing tools and resources in the same type hurts discoverability.

---

## 3. Fast Path: Creating a New Refactoring Tool

1. **Decorate** the class with `[McpServerToolType]` and the method with `[McpServerTool]`.
2. **Keep the surface minimal**

   * One or two domain parameters (string name, Location span, etc.).
   * Accept `CancellationToken` as last parameter.
   * Prefer returning *strongly‑typed* objects; strings are fine for simple success/error.
3. **Delegate to Roslyn helpers**—don’t re‑invent tree traversal.
4. **Add an Xunit test** beside the implementation; use the `AdhocWorkspace` pattern shown in the Roslyn samples.

---

## 4. Roslyn SDK Best Practices

* **Immutable first** – every tree, compilation and symbol is immutable; create new trees instead of mutating existing ones.
* **Cancellation everywhere** – pass the `CancellationToken` you receive to every Roslyn call to avoid UI freezes.
* **Prefer `SyntaxGenerator` over manual trivia handling** for readability.
* **Use *incremental* analyzers** (`IncrementalGenerator`, `IncrementalValuesProvider`) when performance matters.
* **Unit‑test refactorings and analyzers** with the `Microsoft.CodeAnalysis.Testing` framework; follow the pattern in the “Write your first analyzer” tutorial.
* **Separate analysis from transformation**:

  * Use `SyntaxWalker` / analyzers to **find** code.
  * Use `SyntaxRewriter` or generators to **rewrite** the code.
  * This keeps diagnostics fast and only allocates when a fix is actually applied.
* **Reuse Workspaces** – long‑lived `AdhocWorkspace` instances reuse metadata references and reduce allocations.
* **Avoid `.Result`** – Roslyn APIs are async‑first; blocking can deadlock Visual Studio.

---

## 5. Error Handling (Thin Version)

| Situation                                        | How To Respond                                             |
| ------------------------------------------------ | ---------------------------------------------------------- |
| Business rule violated (e.g., method name empty) | `return "Message…";`                                       |
| Protocol violation (e.g., JSON schema mismatch)  | `throw new McpException("…", McpErrorCode.InvalidParams);` |
| Unexpected failure                               | `throw new McpException("…", McpErrorCode.InternalError);` |

Keep messages actionable: *what happened*, *how to fix*.

---

## 6. Documentation & Quality Checklist

* **Docs** – add a short entry to `README.md` and a runnable JSON example to `EXAMPLES.md`.
* **Build** - `dotnet build` you'll need this if you're not running tests 
* **Tests** – run `dotnet test` from the root with no further parameters must pass green before commit. 
* **Formatting** – run `dotnet format` before pushing; zero warnings.
* **Dependencies** – stick to the main MCP and Roslyn packages; no extra NuGets unless justified.

---

### Further Reading

* MCP API reference (`ModelContextProtocol.Server` namespace).
* Roslyn “Get started with syntax transformation” quick‑start for advanced tree rewrites.

---

## Cross-project type move qualification protocol

For each early production use of `move-type-to-project`:

1. Run `refactor-mcp move-type-to-project ... --dry-run true --no-daemon` before the write.
2. Confirm source/target project identities, project-reference changes, namespace, all partial declarations and ABI/forwarder choice. Use the exact same arguments for the write, omitting `--dry-run true`.
3. On `DEFECT_REPORT_REQUIRED`: **immediately surface the failure in the operating ChatGPT thread**, including stage, error, source/destination, rollback status and saved JSON path. Preserve the JSON.
4. On RH, load `defectReportPayload` from the JSON, and submit with **`semantic plan defect-report create --payload $payload`**, where `$payload` is `ConvertTo-Json -Depth 25 -Compress` of the report's `defectReportPayload`; read it back with `ttd show <returned-ref>`.
5. Put the canonical DefectReport reference visibly in the **same thread**. If Recorder fails, show **DEFECT REPORT SUBMISSION FAILED** with its output, and retain the JSON for retry.

The report stores source/tool commits, environment, all paths, intended action, failed and successful steps, planned project-reference edits, compilation diagnostics, stack traces, and byte-exact rollback status. Retain verbose reporting for the initial several real work cycles; simplify only after qualification evidence supports doing so.

Avoid applying a failed move manually before inspecting its rollback status. Keep test files and controlled fixtures outside production project roots. Use the RH Zellij tunnel for shell/build execution; close each finished pane to reclaim RH resources.
