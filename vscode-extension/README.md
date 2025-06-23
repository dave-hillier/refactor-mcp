# Refactor MCP VS Code Extension

This extension exposes [RefactorMCP](../README.md) tools to Visual Studio Code.

## Features

- **Extract Method** – Right click a selection and run `RefactorMCP: Extract Method` to refactor the selected code using the `ExtractMethod` tool.
- **Run Tool** – Command palette entry `RefactorMCP: Run Tool` lists all available refactoring tools and executes them with JSON parameters.

### Available Tools

Output of `ListTools`:

```
cleanup-usings
clear-solution-cache
convert-to-extension-method
convert-to-static-with-instance
convert-to-static-with-parameters
inline-method
introduce-parameter
load-solution
move-instance-method
move-multiple-methods
move-static-method
safe-delete-field
safe-delete-method
safe-delete-parameter
safe-delete-variable
transform-setter-to-init
unload-solution
version
```

## Requirements

The extension requires that you open a workspace containing the `RefactorMCP.ConsoleApp` project. `dotnet` must also be available on your PATH. Use the setting `refactorMcp.dotnetPath` to override the path if necessary.

## Development

Run `npm install` and then `npm run watch` to compile the TypeScript sources. Use `F5` in VS Code to launch an Extension Development Host.
