# Single File Mode Documentation

RefactorMCP now supports **Single File Mode** - a fast refactoring option that works without requiring a solution file. This mode provides direct syntax tree manipulation for simple refactoring operations.

For large **C#** files, the MCP-based tools remain the preferred approach because they handle extensive edits more reliably.

## Overview

Single File Mode enables quick refactoring operations on individual C# files without the overhead of loading an entire solution. This is particularly useful for:

- Standalone C# files
- Quick prototyping and experimentation  
- Simple refactoring tasks that don't require cross-file analysis
- Scenarios where solution files are not available

## Architecture Changes

### Usage
Single file mode is exposed only through the `*InSource` helper methods used by the unit tests. CLI tools always require a solution path.

### Mode Detection
The refactoring engine automatically detects which mode to use:

- **Solution Mode**: When `solutionPath` parameter is provided
- **Single File Mode**: When `solutionPath` parameter is `null` or omitted

## Implementation Details

### Single File Mode Features

1. **Direct Syntax Tree Parsing**
   ```csharp
   var sourceText = await File.ReadAllTextAsync(filePath);
   var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
   var syntaxRoot = await syntaxTree.GetRootAsync();
   ```

2. **AdhocWorkspace for Formatting**
   ```csharp
   var workspace = new AdhocWorkspace();
   var formattedRoot = Formatter.Format(newRoot, workspace);
   await File.WriteAllTextAsync(filePath, formattedRoot.ToFullString());
   ```

3. **Basic Type Inference**
   - Lightweight `CSharpCompilation` created per file
   - Enables simple type lookup for expressions
   - No cross-reference validation
4. **Syntax Tree Caching**
   - Parsed syntax trees and semantic models are cached
   - Reduces repeated parsing costs

### Supported Refactorings in Single File Mode

#### ✅ Extract Method
- Extracts selected statements into a new private method
- Works within class boundaries
- Maintains proper method signatures

#### ✅ Introduce Variable
- Creates local variables from expressions
- Infers primitive types using cached compilation
- Inserts declaration at appropriate scope

- Creates private fields from expressions
- Infers primitive types using cached compilation
- Adds field to class members
- Refactoring fails if the field name already exists

#### ✅ Make Field Readonly
- Adds `readonly` modifier to fields
- Moves initialization to constructors when present
- Handles fields with and without initializers

## Usage Examples

### Command Line Testing

```bash
# Extract Method (Single File Mode)
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method ./MyFile.cs "10:5-15:20" "ExtractedMethod"

# Extract Method (Solution Mode)  
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method ./MyFile.cs "10:5-15:20" "ExtractedMethod" ./MySolution.sln

# Introduce Variable (Single File Mode)
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-variable ./MyFile.cs "12:10-12:25" "myVariable"

# Make Field Readonly (Single File Mode)
dotnet run --project RefactorMCP.ConsoleApp -- --cli make-field-readonly ./MyFile.cs 15
```

### MCP Tool Calls

```json
{
  "name": "mcp_refactor-mcp_ExtractMethod",
  "arguments": {
    "filePath": "./Calculator.cs",
    "selectionRange": "25:13-27:50", 
    "methodName": "CalculateResult"
  }
}
```

## Limitations and Trade-offs

### Single File Mode Limitations

| Feature | Single File Mode | Solution Mode |
|---------|------------------|---------------|
| Type Information | ⚠️ Basic primitives | ✅ Full semantic types |
| Cross-file Analysis | ❌ Not supported | ✅ Full project analysis |
| Dependency Resolution | ❌ Syntax only | ✅ Complete dependency graph |
| Performance | ✅ Very fast | ⚠️ Slower (solution loading) |
| Setup Required | ✅ None | ⚠️ Requires .sln file |

### When to Use Each Mode

**Use Single File Mode for:**
- Quick syntax transformations
- Standalone file editing
- Prototyping and experimentation
- Simple local refactorings

**Use Solution Mode for:**
- Production code refactoring
- Complex type-dependent operations
- Cross-project refactoring
- When accuracy is critical

## Migration Guide

### For Existing Users

If you were using the old parameter order, update your calls:

```bash
# OLD
--cli extract-method ./MySolution.sln ./MyFile.cs "10:5-15:20" "ExtractedMethod"

# NEW (Solution Mode)  
--cli extract-method ./MyFile.cs "10:5-15:20" "ExtractedMethod" ./MySolution.sln

# NEW (Single File Mode)
--cli extract-method ./MyFile.cs "10:5-15:20" "ExtractedMethod"
```

### For MCP Clients

These helpers are primarily for unit tests and do not require a solution file.

## Technical Implementation

### Core Classes Modified

1. **`ExtractMethodWithSolution`** - Handles solution-based extraction
2. **`ExtractMethodSingleFile`** - Handles standalone file extraction  
3. **`IntroduceFieldWithSolution`** - Solution-based field introduction
4. **`IntroduceFieldSingleFile`** - Standalone field introduction
5. **`MakeFieldReadonlyWithSolution`** - Solution-based readonly conversion
6. **`MakeFieldReadonlySingleFile`** - Standalone readonly conversion

### Error Handling

Both modes provide consistent error reporting:
- Clear mode indication in success messages
- Detailed error descriptions for invalid operations
- Graceful fallback between modes

### Performance Characteristics

- **Single File Mode**: ~50-100ms for typical operations
- **Solution Mode**: ~500-2000ms (depending on solution size)
- **Memory Usage**: Single file mode uses significantly less memory

## Future Enhancements

Remaining improvements for Single File Mode:

1. **Batch Operations**: Support multiple files in single operation
2. **Configuration**: Allow customization of type inference behavior

## Contributing

When adding new refactoring operations, implement both modes:

1. Create `OperationWithSolution` method for full semantic analysis
2. Create `OperationSingleFile` method for syntax-only analysis  
3. Update main method to dispatch based on solution parameter
4. Add appropriate test cases for both modes
5. Document limitations and capabilities

Single File Mode represents a significant enhancement to RefactorMCP's flexibility and performance, enabling fast refactoring workflows while maintaining the option for full semantic analysis when needed. 
