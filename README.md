# RefactorMCP

RefactorMCP is a Model Context Protocol server that exposes Roslyn based refactoring tools for C# code. It can run as a standalone MCP server or as a simple command line application.

## Available Tools

The project includes the following refactorings and helpers:

- AnalyzeRefactoringOpportunities
- ClassLengthMetrics
- CleanupUsings
- ConvertToExtensionMethod
- ConvertToStaticWithInstance
- ConvertToStaticWithParameters
- ExtractInterface
- ExtractMethod
- InlineMethod
- IntroduceField
- IntroduceParameter
- IntroduceVariable
- LoadSolution / UnloadSolution
- MakeFieldReadonly
- MoveClassToFile
- MoveMethods
- MoveMultipleMethods
- RenameSymbol
- SafeDelete
- TransformSetterToInit
- Version

Metrics and code summaries are also available via the `metrics://` and `summary://` resource schemes. After loading a solution, metrics are cached under `.refactor-mcp/metrics/` mirroring the project structure so they can be served directly from disk.

## Usage

Run the console application directly or start it as an MCP server to integrate with other clients:

```bash
dotnet run --project RefactorMCP.ConsoleApp
```

For examples of each tool see [EXAMPLES.md](./EXAMPLES.md).

## Development

Build and test with the standard .NET tooling:

```bash
dotnet test
```

Formatting uses `dotnet format`.

## License

Licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).
