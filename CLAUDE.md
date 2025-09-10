# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RefactorMCP is a Model Context Protocol server that exposes Roslyn-based refactoring tools for C#. It provides extensive code transformation capabilities through both CLI and MCP server modes.

## Architecture

### Core Components

- **RefactorMCP.ConsoleApp**: Main application hosting refactoring tools and MCP server
  - `Tools/`: Individual refactoring tool implementations (40+ tools)
  - `SyntaxRewriters/`: Roslyn syntax tree rewriting components
  - `SyntaxWalkers/`: Code analysis and traversal utilities
  - `Move/`: Specialized method moving logic

- **RefactorMCP.Tests**: Comprehensive xUnit test suite
  - Each tool has corresponding test coverage
  - Tests organized by feature area

### Key Technologies
- .NET 9.0
- Roslyn (Microsoft.CodeAnalysis) for C# code analysis and transformation
- xUnit for testing
- Model Context Protocol for server functionality

## Development Commands

### Build
```bash
dotnet build
```

### Run Tests
```bash
# Run all tests
dotnet test

# Run tests with filter
dotnet test --filter "FullyQualifiedName~MoveInstanceMethod"

# Run a specific test
dotnet test --filter "FullyQualifiedName=RefactorMCP.Tests.Tools.MoveInstanceMethodTests.Move_MethodWithNamedArguments_Success"
```

### Run Application
```bash
# Run MCP server
dotnet run --project RefactorMCP.ConsoleApp

# Run with JSON mode
dotnet run --project RefactorMCP.ConsoleApp -- --json <tool-name> '<json-params>'

# Example: Load solution
dotnet run --project RefactorMCP.ConsoleApp -- --json load-solution '{"solutionPath":"./RefactorMCP.sln"}'
```

### Format Code
```bash
dotnet format
```

## Working with Refactoring Tools

### Tool Naming Convention
- Tools use kebab-case names (e.g., `extract-method`, `move-instance-method`)
- Each tool has a corresponding class in `RefactorMCP.ConsoleApp/Tools/`

### Adding New Tools
1. Create tool class in `RefactorMCP.ConsoleApp/Tools/`
2. Implement the refactoring logic using Roslyn APIs
3. Add corresponding tests in `RefactorMCP.Tests/Tools/` or `RefactorMCP.Tests/ToolsNew/`
4. Register the tool in the appropriate location for MCP discovery

### Testing Patterns
- Tests use `TestBase` for common setup
- Each test typically:
  1. Creates test code as a string
  2. Applies the refactoring
  3. Asserts the transformed code matches expected output
  4. Tests both success and failure cases

## Important Patterns

### Roslyn Usage
- Solution and workspace management via `MSBuildWorkspace`
- Syntax tree transformations using `CSharpSyntaxRewriter` derivatives
- Semantic model analysis for type and symbol information

### Method Moving Logic
- Complex dependency tracking for moved methods
- Automatic static conversion when instance members aren't accessed
- Constructor and parameter injection for dependencies
- Support for overloaded methods and inheritance hierarchies

### Resource Schemes
- `metrics://` - Code metrics analysis
- `summary://` - File summaries with method bodies omitted

## Common Development Tasks

### Debug a Specific Refactoring
1. Identify the tool class (e.g., `MoveInstanceMethodTool`)
2. Find corresponding test file
3. Run specific test or create minimal reproduction
4. Use debugger breakpoints in tool implementation

### Test File Generation
- Test outputs are written to `TestOutput/` directory
- These generated files are excluded from compilation