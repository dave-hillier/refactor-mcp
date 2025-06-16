# RefactorMCP

A Model Context Protocol (MCP) server providing automated refactoring tools for C# code transformation using Roslyn. Using these tools through MCP is the preferred way to refactor, especially when working with large files.

## Features

- **Solution Mode**: Full semantic analysis with cross-project dependencies
- **Single File Helpers**: In-memory transformations used for unit tests
- **Comprehensive Refactoring Tools**: Extract methods, introduce variables/fields, make fields readonly, convert methods to extension methods, and more
- **Analysis Prompt**: Inspect code for long methods, large classes, long parameter lists, unused methods or fields
- **Class Length Metrics**: List classes in a solution with their line counts
- **MCP Compatible**: Works with any MCP-compatible client
- **Preferred for Large Files**: Invoking these tools via MCP is recommended for large code files
- **VS Code Extension**: Invoke refactoring tools directly from the editor
## Refactoring Tools

Below is a quick reference of all tool classes provided by RefactorMCP. Each tool is decorated with `[McpServerToolType]` and some also include `[McpServerPromptType]` for prompt-based actions.

- **AnalyzeRefactoringOpportunitiesTool** `[McpServerToolType, McpServerPromptType]`  
  Analyze a C# file for refactoring opportunities like long methods or unused code.
- **ClassLengthMetricsTool** `[McpServerToolType, McpServerPromptType]`  
  List all classes in the solution with their line counts.
- **CleanupUsingsTool** `[McpServerToolType]`  
  Remove unused using directives from a C# file (preferred for large C# file refactoring).
- **MetricsResource** `[McpServerResourceType]`
  Read metrics using `metrics://` URIs for directories, files, classes or methods.
- **ConvertToExtensionMethodTool** `[McpServerToolType]`  
  Convert an instance method to an extension method in a static class.
- **ConvertToStaticWithInstanceTool** `[McpServerToolType]`  
  Transform instance method to static by adding instance parameter (preferred for large C# file refactoring).
- **ConvertToStaticWithParametersTool** `[McpServerToolType]`  
  Transform instance method to static by converting dependencies to parameters (preferred for large C# file refactoring).
- **ExtractInterfaceTool** `[McpServerToolType]`  
  Extract a simple interface from a class.
- **ExtractMethodTool** `[McpServerToolType]`  
  Extract a code block into a new method (preferred for large C# file refactoring).
- **InlineMethodTool** `[McpServerToolType]`  
  Inline a method and remove its declaration (preferred for large C# file refactoring).
- **IntroduceFieldTool** `[McpServerToolType]`  
  Introduce a new field from selected expression (preferred for large C# file refactoring).
- **IntroduceParameterTool** `[McpServerToolType]`  
  Create a new parameter from selected code (preferred for large C# file refactoring).
- **IntroduceVariableTool** `[McpServerToolType]`  
  Introduce a new variable from selected expression (preferred for large C# file refactoring).
- **LoadSolutionTool** `[McpServerToolType]`  
  Load a solution file for refactoring operations and set the current directory to the solution directory.
- **MakeFieldReadonlyTool** `[McpServerToolType]`  
  Make a field readonly if assigned only during initialization (preferred for large C# file refactoring).
- **MoveClassToFileTool** `[McpServerToolType]`  
  Move a class to a separate file with the same name.
- **MoveMethodsTool** `[McpServerToolType]`
  Move a static or instance method to another class (preferred for large C# file refactoring).
  If the same method is moved again in one run, an error is raised. Use `InlineMethodTool` to remove the wrapper.
- **MoveMultipleMethodsTool** `[McpServerToolType]`  
  Move multiple methods from a source class to a target class, automatically ordering by dependencies.
- **RenameSymbolTool** `[McpServerToolType]`  
  Rename a symbol across the solution using Roslyn.
- **SafeDeleteTool** `[McpServerToolType]`  
  Safely delete an unused field (preferred for large C# file refactoring).
- **TransformSetterToInitTool** `[McpServerToolType]`  
  Convert property setter to init-only setter (preferred for large C# file refactoring).
- **UnloadSolutionTool** `[McpServerToolType]`  
  Unload a solution and remove it from the cache.
- **VersionTool** `[McpServerToolType]`  
  Show the current version and build timestamp.

## Solution Mode

All CLI tools require an absolute path to a solution file. The working directory is set from this path so relative file references resolve correctly. Single file helper methods are available only inside the unit tests.

**Single file mode is suitable for:**
- Extract Method (within same class)
- Introduce Variable/Field (local scope)
- Make Field Readonly (single file)
- Basic syntax transformations

**Use solution mode for:**
- Move Method operations
- Move class to separate file
- Convert to Static (requires dependency analysis)
- Convert to Extension Method (for instance methods)
- Safe Delete (requires usage analysis)
- Any refactoring requiring cross-references

### Solution Cache
Loaded solutions are cached in memory for faster access. Use `unload-solution` to remove a single entry or `clear-solution-cache` to reset the cache when projects change on disk.

## Installation

```bash
git clone https://github.com/yourusername/RefactorMCP.git
cd RefactorMCP
dotnet build
```

## VS Code Extension Development

To work on the VS Code extension located in `vscode-extension`:

1. Install the npm dependencies:
   ```bash
   cd vscode-extension
   npm install
   ```
2. Start the TypeScript compiler in watch mode:
   ```bash
   npm run watch
   ```
3. In VS Code, press `F5` (or choose **Run > Start Debugging**) to launch an Extension Development Host that loads the extension for testing.

## Usage

### Command Line Testing

## Technology Stack

- **Microsoft.CodeAnalysis.CSharp** (4.14.0) - C# syntax analysis
- **Microsoft.CodeAnalysis.CSharp.Workspaces** (4.14.0) - Workspace management
- **Microsoft.CodeAnalysis.Workspaces.MSBuild** (4.14.0) - Solution loading
- **ModelContextProtocol** (0.2.0-preview.3) - MCP server implementation

## MCP Configuration

To use RefactorMCP with MCP-compatible clients (like Claude Desktop, Continue, or other AI assistants), add it to your `mcp.json` configuration file.

### Location of mcp.json

The `mcp.json` file location depends on your operating system:

- **macOS**: `~/Library/Application Support/Claude/mcp.json`
- **Windows**: `%APPDATA%\Claude\mcp.json`
- **Linux**: `~/.config/claude/mcp.json`

### Configuration Format

Add the following configuration to your `mcp.json` file:

```json
{
  "mcpServers": {
    "refactor-mcp": {
      "command": "dotnet",
      "args": [
        "run"
      ],
      "cwd": "/path/to/RefactorMCP/RefactorMCP.ConsoleApp"
    }
  }
}
```

### Example Configuration

Replace the paths with your actual installation directory:

```json
{
  "mcpServers": {
    "refactor-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/RefactorMCP/RefactorMCP.ConsoleApp"
      ],
      "cwd": "/path/to/RefactorMCP"
    }
  }
}
```

### Alternative: Using Published Binary

If you prefer to publish the application as a standalone executable:

1. **Publish the application**:
   ```bash
   dotnet publish RefactorMCP.ConsoleApp -c Release -o ./publish
   ```

2. **Update mcp.json to use the executable**:
   ```json
   {
     "mcpServers": {
       "refactor-mcp": {
         "command": "/absolute/path/to/RefactorMCP/publish/RefactorMCP.ConsoleApp"
       }
     }
   }
   ```

### Verification

After configuring, restart your MCP client. The RefactorMCP tools should be available with the following capabilities:

- `extract_method` - Extract code blocks into new methods
- `introduce_field` - Create fields from expressions
- `introduce_variable` - Create variables from expressions
- `make_field_readonly` - Convert fields to readonly
- `convert_to_extension_method` - Transform instance methods into extension methods while leaving a wrapper call in the original class
- `convert_to_static` - Transform methods to static
- `move_method` - Relocate methods between classes. A delegating wrapper remains so existing callers continue to compile
- `safe_delete` - Remove unused code safely
- `transform_property` - Convert setters to init-only

## Usage

### MCP Server Mode (Default)

Run as an MCP server for integration with AI assistants:

```bash
dotnet run --project RefactorMCP.ConsoleApp
```

### CLI Mode

Use the `--cli` flag for direct command-line testing:

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli <command> [arguments]
```

### JSON Mode

Pass parameters as a JSON object using `--json`:

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json ToolName '{"param":"value"}'
```

#### Available Test Commands

- `list-tools` - Show all available refactoring tools
- `load-solution <solutionPath>` - Load a solution file and set the working directory
- `extract-method <solutionPath> <filePath> <range> <methodName>` - Extract code into method
- `introduce-field <solutionPath> <filePath> <range> <fieldName> [accessModifier]` - Create field from expression. Fails if a field with the same name already exists
- `introduce-variable <solutionPath> <filePath> <range> <variableName>` - Create variable from expression
- `make-field-readonly <solutionPath> <filePath> <fieldLine>` - Make field readonly
- `introduce-parameter <solutionPath> <filePath> <methodLine> <range> <parameterName>` - Create parameter from expression
- `convert-to-static-with-parameters <solutionPath> <filePath> <methodLine>` - Convert instance method to static with parameters
- `convert-to-static-with-instance <solutionPath> <filePath> <methodLine> [instanceName]` - Convert instance method to static with explicit instance
- `move-static-method <solutionPath> <filePath> <methodName> <targetClass> [targetFilePath]` - Move a static method to another class
 - `move-instance-method <solutionPath> <filePath> <sourceClass> <methodNames> <targetClass> <accessMember> [targetFilePath]` - Move one or more instance methods (comma separated names) to another class. Newly created access fields are marked `readonly` and won't duplicate existing members
 - `move-multiple-methods <solutionPath> <filePath> <sourceClass> <methodNames> <targetClass> <accessMember> [targetFilePath]` - Move multiple methods from one class to another. Accepts comma separated `methodNames`. If the access member doesn't exist, a private readonly field is added. The older JSON form is still supported for backward compatibility
- `batch-move-methods <solutionPath> <filePath> <operationsJson> [defaultTargetFile]` - Move many methods in a single batch using JSON operations
- `move-multiple-methods <solutionPath> <filePath> <sourceClass> <methodNames> <targetClass> <accessMember> [targetFilePath]` - Move multiple methods from one class to another. Accepts comma separated `methodNames`.
- Each method move is described by a `MoveOperation` object with these properties:
  `SourceClass`, `Method`, `TargetClass`, `AccessMember`, `AccessMemberType`,
  `IsStatic`, and an optional `TargetFile`.
- `cleanup-usings [solutionPath] <filePath>` - Remove unused using directives
 - `move-static-method <solutionPath> <filePath> <methodName> <targetClass> [targetFilePath]` - Move a static method to another class. A placeholder wrapper is left behind to delegate to the new location. Moving the same method again in one run results in an error; use `inline-method` to remove the wrapper.
 - `move-instance-method <solutionPath> <filePath> <sourceClass> <methodNames> <targetClass> <accessMember> [targetFilePath]` - Move one or more instance methods to another class. Wrapper methods remain in the original class so existing callers continue to work. Repeating the command on the wrapper now errors; run `inline-method` if you no longer need it. Private field values used by the method are passed as additional parameters
 - `move-multiple-methods <solutionPath> <filePath> <sourceClass> <methodNames> <targetClass> <accessMember> [targetFilePath]` - Move multiple methods from one class to another. Each method leaves a delegating wrapper behind. Accepts comma separated `methodNames`. If the access member doesn't exist, a private readonly field is added. The older JSON form is still supported for backward compatibility
- `cleanup-usings [solutionPath] <filePath>` - Remove unused using directives
- `rename-symbol <solutionPath> <filePath> <oldName> <newName> [line] [column]` - Rename a symbol across the solution
- `extract-interface <solutionPath> <filePath> <className> <memberNames> <interfacePath>` - Generate an interface from specified members
- `version` - Show build version and timestamp
- `analyze-refactoring-opportunities <solutionPath> <filePath>` - Prompt for refactoring suggestions (long methods, long parameter lists, unused code)
- `list-class-lengths <solutionPath>` - Prompt for class names and line counts

#### Quick Start Example

```bash
# List available tools
dotnet run --project RefactorMCP.ConsoleApp -- --cli list-tools

# Load a solution
dotnet run --project RefactorMCP.ConsoleApp -- --cli load-solution ./RefactorMCP.sln

# Extract a method (example range)
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "22:9-25:34" \
  "ValidateInputs"
```

```bash
# Convert instance method to extension
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-extension-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  GetFormattedNumber
```

The original instance method remains in the class as a thin wrapper that
invokes the generated extension method, ensuring existing call sites keep
working.

### Metrics Cache

Metrics results are cached in `codeMetricsCache.json` under the current working directory. Repeated requests with the same
solution and file paths return the cached JSON without recomputing. Delete this file to reset the cache.

### Metrics Resource

Metrics can also be accessed via the `metrics://` resource scheme. Use a URI like
`metrics://<file path>/[ClassName].[MethodName]` to retrieve metrics for a
specific scope. Supplying only a directory lists all classes. Specifying a file
returns metrics for all classes and methods, adding a class name narrows to that
class, and appending a method name yields metrics just for that method.

## Range Format

Code selections use the format: `"startLine:startColumn-endLine:endColumn"`

- **1-based indexing** (first line/column = 1)
- **Inclusive ranges** (includes start and end positions)
- **Character counting** includes spaces and tabs

Example: `"10:5-15:20"` selects from line 10, column 5 to line 15, column 20.

## Examples

### 1. Extract Method

**Before**:
```csharp
public int Calculate(int a, int b)
{
    if (a < 0 || b < 0)
    {
        throw new ArgumentException("Negative numbers not allowed");
    }
    
    var result = a + b;
    return result;
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method \
  "./RefactorMCP.sln" "./MyFile.cs" "3:5-6:6" "ValidateInputs"
```

**After**:
```csharp
public int Calculate(int a, int b)
{
    ValidateInputs();
    
    var result = a + b;
    return result;
}

private void ValidateInputs()
{
    if (a < 0 || b < 0)
    {
        throw new ArgumentException("Negative numbers not allowed");
    }
}
```

### 2. Introduce Field

**Before**:
```csharp
public double GetAverage()
{
    return numbers.Sum() / (double)numbers.Count;
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-field \
  "./RefactorMCP.sln" "./MyFile.cs" "3:12-3:54" "_averageValue" "private"
```
The specified field name must be unique within the class. If a field with the
same name already exists, the tool returns an error.

**After**:
```csharp
private double _averageValue = numbers.Sum() / (double)numbers.Count;

public double GetAverage()
{
    return _averageValue;
}
```

### 3. Introduce Parameter

**Before**:
```csharp
public string FormatResult(int value)
{
    return $"The calculation result is: {value * 2 + 10}";
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-parameter \
  "./RefactorMCP.sln" "./MyFile.cs" 40 "41:50-41:65" "processedValue"
```

**After**:
```csharp
public string FormatResult(int value, int processedValue)
{
    return $"The calculation result is: {processedValue}";
}
```

### 4. Convert to Static with Parameters

**Before**:
```csharp
private string _operatorSymbol;

public string GetFormattedNumber(int number)
{
    return $"{_operatorSymbol}: {number}";
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-static-with-parameters \
  "./RefactorMCP.sln" "./MyFile.cs" GetFormattedNumber
```

**After**:
```csharp
public static string GetFormattedNumber(string operatorSymbol, int number)
{
    return $"{operatorSymbol}: {number}";
}
```

### 5. Convert to Static with Instance

**Before**:
```csharp
public string GetFormattedNumber(int number)
{
    return $"{operatorSymbol}: {number}";
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-static-with-instance \
  "./RefactorMCP.sln" "./MyFile.cs" GetFormattedNumber "calculator"
```

**After**:
```csharp
public static string GetFormattedNumber(Calculator calculator, int number)
{
    return $"{calculator.operatorSymbol}: {number}";
}
```

### 6. Safe Delete Parameter

**Before**:
```csharp
public int Multiply(int x, int y, int unusedParam)
{
    return x * y;
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli safe-delete-parameter \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "Multiply" \
  "unusedParam"
```

**After**:
```csharp
public int Multiply(int x, int y)
{
    return x * y;
}
```

### 7. Move Static Method

**Before**:
```csharp
public static string FormatCurrency(decimal amount)
{
    return $"${amount:F2}";
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-static-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "FormatCurrency" \
  "MathUtilities"
```

**After**:
```csharp
public class MathUtilities
{
    public static string FormatCurrency(decimal amount)
    {
        return $"${amount:F2}";
    }
}
```
The original `FormatCurrency` method remains in the source file as a wrapper that calls `MathUtilities.FormatCurrency`.

### 8. Inline Method

**Before**:
```csharp
private void Helper()
{
    Console.WriteLine("Hi");
}

public void Call()
{
    Helper();
    Console.WriteLine("Done");
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli inline-method \
  "./RefactorMCP.sln" "./MyFile.cs" Helper
```

**After**:
```csharp
public void Call()
{
    Console.WriteLine("Hi");
    Console.WriteLine("Done");
}
```

### 9. Cleanup Usings

**Before**:
```csharp
using System;
using System.Text;

public class Sample
{
    public void Say() => Console.WriteLine("Hi");
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli cleanup-usings \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs"
```

**After**:
```csharp
using System;

public class Sample
{
    public void Say() => Console.WriteLine("Hi");
}
```

### 10. Move Class to Separate File

**Before**:
```csharp
public class Logger
{
    public void Log(string message)
    {
        Console.WriteLine($"[LOG] {message}");
    }
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-to-separate-file \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  Logger
```

**After**:
```csharp
// Logger.cs
public class Logger
{
    public void Log(string message)
    {
        Console.WriteLine($"[LOG] {message}");
    }
}
```

### 11. Rename Symbol

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli rename-symbol \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  numbers \
  values
```

## Complete Examples

See [EXAMPLES.md](./EXAMPLES.md) for comprehensive examples of all refactoring tools, including:

- Detailed before/after code samples
- Exact command-line usage
- Range calculation guides
- Error handling tips
- Advanced usage patterns

The examples use the sample code in [RefactorMCP.Tests/ExampleCode.cs](./RefactorMCP.Tests/ExampleCode.cs).

## Architecture

### Core Components

- **MSBuildWorkspace**: Loads and manages solution/project files
- **Roslyn SyntaxTree**: Analyzes and manipulates C# code structure
- **SemanticModel**: Provides type information and symbol resolution
- **Formatter**: Ensures consistent code formatting after transformations

### Refactoring Pipeline

1. **Load Solution**: Parse .sln file and create workspace
2. **Find Document**: Locate target file within the solution
3. **Parse Selection**: Convert range format to syntax tree positions
4. **Analyze Code**: Use semantic model for type and dependency analysis
5. **Transform Syntax**: Apply refactoring using Roslyn APIs
6. **Format Output**: Apply consistent formatting
7. **Write Changes**: Save transformed code back to file

## Development

### Adding New Refactorings

1. Create a new static class in `RefactorMCP.ConsoleApp/Tools/` and decorate it with `[McpServerToolType]`
2. Add your refactoring method to that class with the `[McpServerTool]` attribute
3. Implement using Roslyn SyntaxFactory and SyntaxNode manipulation
4. Add a test command handler in `RunTestMode` switch statement
5. Update documentation and examples

### Testing

```bash
# Run existing tests
dotnet test

*Note: Avoid using `--no-build` on the first run unless the project has already been built, or tests may fail with an "invalid argument" error.*

# Test specific refactoring tool
dotnet run --project RefactorMCP.ConsoleApp -- --cli <tool-name> [args]

# Load test solution for debugging
dotnet run --project RefactorMCP.ConsoleApp -- --cli load-solution ./RefactorMCP.sln
```

## Error Handling

Common error scenarios and solutions:

- **File not found**: Ensure file paths are relative to the solution directory. The error message now includes the current working directory for reference.
- **Invalid range**: Check 1-based line/column indexing
- **No extractable code**: Verify selection contains valid statements/expressions
- **Solution load failure**: Check .sln file path and project references

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Update documentation and examples
5. Submit a pull request

## License

This project is licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).
See the [LICENSE](./LICENSE) file for full license text.

## Support

For issues and questions:
- Check [EXAMPLES.md](./EXAMPLES.md) for usage guidance
- Review error messages for specific guidance
- Test with simple cases before complex refactorings 
