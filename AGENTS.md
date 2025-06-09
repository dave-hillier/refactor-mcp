# Coding Agents Guidelines

This repository implements a Model Context Protocol (MCP) server that exposes refactoring tools as **agents**. When adding new agents or modifying existing ones, follow these guidelines.

## Creating a New Agent

1. Add a new static method in `RefactorMCP.ConsoleApp/Program.cs` and annotate it with `[McpServerTool]`.
2. Include clear `Description` attributes for every parameter. They are used by clients for help text.
3. Keep method names concise and use `CamelCase`.
4. For complex logic, extract helper functions rather than writing large methods.

## Updating Documentation

- Document the new agent in **README.md** and **EXAMPLES.md**.
- Provide a short usage snippet in **QUICK_REFERENCE.md**.
- Include a minimal example in the test suite under `RefactorMCP.Tests`.

## Formatting and Testing

- Run `dotnet format` to ensure consistent C# style.
- Run `dotnet test` before submitting changes.

