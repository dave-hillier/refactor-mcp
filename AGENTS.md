# Coding Agents Guidelines

This repository implements a Model Context Protocol (MCP) server that exposes refactoring tools as **agents**. When adding new agents or modifying existing ones, follow these guidelines.

## Creating a New Agent

1. Add a new static method in the `RefactorMCP.ConsoleApp/Tools/` directory as a partial class extension of `RefactoringTools`.
2. Decorate the method with `[McpServerTool]` and include a `[Description]` for every parameter so clients can display helpful text.
3. Keep method names concise and use `CamelCase`.
4. For complex logic, extract helper functions rather than writing large methods.

## Updating Documentation

- Document the new agent in **README.md** and **docs/EXAMPLES.md**.
- Provide a short usage snippet in **docs/QUICK_REFERENCE.md**.
- Include a minimal example in the test suite under `RefactorMCP.Tests`.

## Formatting and Testing

- Run `dotnet format` to ensure consistent C# style.
- Run `dotnet test` before committing your changes.

