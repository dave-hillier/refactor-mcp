# Coding Agents Guidelines

This repository implements a Model Context Protocol (MCP) server that exposes **C#** refactoring tools as **agents**. When adding new agents or modifying existing ones, follow these guidelines.

## Creating a New Agent

1. Add a new static class in the `RefactorMCP.ConsoleApp/Tools/` directory and decorate it with `[McpServerToolType]`.
2. Inside that class, add a static method decorated with `[McpServerTool]` and include a `[Description]` for every parameter so clients can display helpful text.
3. Keep method names concise and use `CamelCase`.
4. For complex logic, extract helper functions rather than writing large methods.

## Updating Documentation

- Document the new agent in **README.md** and **EXAMPLES.md**.
- Provide a short usage snippet in **QUICK_REFERENCE.md**.
- Include a minimal example in the test suite under `RefactorMCP.Tests`.

## Formatting and Testing

- Run `dotnet format` to ensure consistent C# style.
- Run `dotnet test` before committing your changes.

