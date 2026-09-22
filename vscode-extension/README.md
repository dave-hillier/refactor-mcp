# Refactor MCP VS Code Extension

This extension exposes [RefactorMCP](../README.md) tools to Visual Studio Code.

## Features

- **Extract Method** – Right click a selection and run `RefactorMCP: Extract Method` to refactor the selected code using the `ExtractMethod` tool.
- **Run Tool** – Command palette entry `RefactorMCP: Run Tool` lists all available refactoring tools and executes them with JSON parameters.

## Requirements

The extension runs the `RefactorMCP.ConsoleApp` command line, so build it first
with `dotnet build` in the workspace. It looks for the Debug build under
`RefactorMCP.ConsoleApp/bin/Debug/net9.0`, and falls back to running
`RefactorMCP.ConsoleApp.dll` with `dotnet` when only the dll is there. Use the
settings `refactorMcp.executablePath` and `refactorMcp.dotnetPath` to point at
another build.

Run `RefactorMCP: Run Tool` and pick `list-tools` to see what the build offers.

## Development

Run `npm install` and then `npm run watch` to compile the TypeScript sources. Use `F5` in VS Code to launch an Extension Development Host.
