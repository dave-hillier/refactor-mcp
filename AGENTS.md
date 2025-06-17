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
