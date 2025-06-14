# RefactorMCP Quick Reference

Using these tools through the MCP interface is the preferred approach for refactoring **C# code**, especially when dealing with large files.

## Basic Commands

```bash
# Help
dotnet run --project RefactorMCP.ConsoleApp -- --cli

# List all tools
dotnet run --project RefactorMCP.ConsoleApp -- --cli list-tools

# Load solution
dotnet run --project RefactorMCP.ConsoleApp -- --cli load-solution ./RefactorMCP.sln
# Unload solution when done
dotnet run --project RefactorMCP.ConsoleApp -- --cli unload-solution ./RefactorMCP.sln
# Clear all cached solutions
dotnet run --project RefactorMCP.ConsoleApp -- --cli clear-solution-cache
# Show version information
dotnet run --project RefactorMCP.ConsoleApp -- --cli version
```

```bash
# JSON mode example
dotnet run --project RefactorMCP.ConsoleApp -- --json ToolName '{"param":"value"}'
```

## Refactoring Commands

### Extract Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  "startLine:startCol-endLine:endCol" \
  "MethodName"
```

### Introduce Field
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-field \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  "startLine:startCol-endLine:endCol" \
"fieldName" \
"private"
```
The field name must not already exist on the target type.

### Introduce Variable
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-variable \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  "startLine:startCol-endLine:endCol" \
  "variableName"
```

### Make Field Readonly
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli make-field-readonly \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  fieldName
```

### Convert To Extension Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-extension-method \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName
```
The original method remains and calls the extension method so the interface stays the same.
The original method remains and calls the extension method so the interface stays the same.

### Analyze Refactoring Opportunities
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli analyze-refactoring-opportunities \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "./RefactorMCP.sln"
```
Prompt the server to analyze the file and suggest possible refactorings such as extract-method or safe-delete-method.

### List Class Lengths
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli list-class-lengths \
  "./RefactorMCP.sln"
```
Show each class in the solution with its line count for complexity insight.

### Safe Delete Parameter
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli safe-delete-parameter \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  MethodName \
  parameterName
```

### Introduce Parameter
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-parameter \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName \
  "startLine:startCol-endLine:endCol" \
  "parameterName"
```

### Convert to Static with Parameters
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-static-with-parameters \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName
```

### Convert to Static with Instance
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-static-with-instance \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName \
  "instanceName"
```

### Move Static Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-static-method \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName \
  TargetClass \
  "./optional/target.cs"
```
Leaves a delegating method in the original class so existing calls still work.

### Inline Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli inline-method \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName
```

### Cleanup Usings
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli cleanup-usings \
  "./RefactorMCP.sln" \
  "./path/to/file.cs"
```

### Rename Symbol
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli rename-symbol \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  OldName \
  NewName \
  10 \
  5
```

### Move Instance Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-instance-method \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  "SourceClass" \
  "MethodA,MethodB" \
  "TargetClass" \
  "memberName" \
  "field" \
  "./optional/target.cs"
```
Newly added access fields are readonly and existing members are reused if present.
Each moved method leaves a wrapper that calls the new implementation.

### Move Multiple Methods
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-multiple-methods \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  SourceClass \
  "Foo,Bar" \
  TargetClass \
  memberName field \
  "./optional/Target.cs"
```
Wrapper methods remain in the source class, delegating to their moved versions.

### Batch Move Methods
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli batch-move-methods \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  "[{\"SourceClass\":\"Foo\",\"Method\":\"Bar\",\"TargetClass\":\"Target\"}]"
```

### Move To Separate File
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-to-separate-file \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  ClassName
```

### Convert To Extension Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-extension-method \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName
```

## Range Format

`"startLine:startColumn-endLine:endColumn"`

- **1-based indexing** (first line = 1, first column = 1)
- **Inclusive ranges** (includes both start and end)
- **Count all characters** including spaces and tabs

## Example File

Test with: `./RefactorMCP.Tests/ExampleCode.cs`

```csharp
// Line 22-25: Extract Method example
if (a < 0 || b < 0)
{
    throw new ArgumentException("Negative numbers not allowed");
}

// Line 35: Introduce Field example  
return numbers.Sum() / (double)numbers.Count;

// Line 41: Introduce Variable example
return $"The calculation result is: {value * 2 + 10}";

// Line 46: Convert To Static example
return $"{operatorSymbol}: {number}";

// Line 50: Make Field Readonly example
private string format = "Currency";
```

## Quick Test Examples

```bash
# Extract validation logic into method
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" "22:9-25:10" "ValidateInputs"

# Create field from calculation
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-field \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" "35:16-35:58" "_averageValue" "private"

# Extract complex expression to variable
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-variable \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" "41:50-41:65" "processedValue"

# Make format field readonly
dotnet run --project RefactorMCP.ConsoleApp -- --cli make-field-readonly \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" format

# Convert method to extension
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-extension-method \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" GetFormattedNumber

# Introduce parameter from expression
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-parameter \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" Calculate "41:50-41:65" "processedValue"

# Convert method to static with parameters
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-static-with-parameters \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" GetFormattedNumber

# Convert method to static with instance
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-static-with-instance \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" GetFormattedNumber "calculator"
# Move static method to MathUtilities
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-static-method \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" FormatCurrency MathUtilities
# The original method stays as a wrapper
# Convert method to extension
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-extension-method \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" GetFormattedNumber
```

## Common Errors

| Error | Solution |
|-------|----------|
| File not found | Check file path relative to solution. The error lists the current working directory |
| Invalid range | Verify 1-based line:column format |
| No extractable code | Select complete statements/expressions |
| Solution not loaded | Run load-solution command first |

## Tips

1. **Always load solution first**
2. **Use exact paths** relative to solution directory  
3. **Count carefully** for range coordinates
4. **Test simple cases** before complex ones
5. **Backup code** before refactoring
6. **Clear cache** if projects change on disk
