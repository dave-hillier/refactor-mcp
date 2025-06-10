# RefactorMCP Quick Reference

Using these tools through the MCP interface is the preferred approach for refactoring, especially when dealing with large files.

## Basic Commands

```bash
# Help
dotnet run --project RefactorMCP.ConsoleApp -- --test

# List all tools
dotnet run --project RefactorMCP.ConsoleApp -- --test list-tools

# Load solution (always do this first)
dotnet run --project RefactorMCP.ConsoleApp -- --test load-solution ./RefactorMCP.sln
# Unload solution when done
dotnet run --project RefactorMCP.ConsoleApp -- --test unload-solution ./RefactorMCP.sln
# Clear all cached solutions
dotnet run --project RefactorMCP.ConsoleApp -- --test clear-solution-cache
```

## Refactoring Commands

### Extract Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test extract-method \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  "startLine:startCol-endLine:endCol" \
  "MethodName"
```

### Introduce Field
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-field \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  "startLine:startCol-endLine:endCol" \
  "fieldName" \
  "private"
```

### Introduce Variable
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-variable \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  "startLine:startCol-endLine:endCol" \
  "variableName"
```

### Make Field Readonly
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test make-field-readonly \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  fieldName
```

### Convert To Extension Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-extension-method \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName
```

### Introduce Parameter
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-parameter \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName \
  "startLine:startCol-endLine:endCol" \
  "parameterName"
```

### Convert to Static with Parameters
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-static-with-parameters \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName
```

### Convert to Static with Instance
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-static-with-instance \
  "./RefactorMCP.sln" \
  "./path/to/file.cs" \
  methodName \
  "instanceName"
### Convert To Extension Method
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-extension-method \
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
dotnet run --project RefactorMCP.ConsoleApp -- --test extract-method \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" "22:9-25:10" "ValidateInputs"

# Create field from calculation
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-field \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" "35:16-35:58" "_averageValue" "private"

# Extract complex expression to variable
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-variable \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" "41:50-41:65" "processedValue"

# Make format field readonly
dotnet run --project RefactorMCP.ConsoleApp -- --test make-field-readonly \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" format

# Convert method to extension
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-extension-method \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" GetFormattedNumber

# Introduce parameter from expression
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-parameter \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" Calculate "41:50-41:65" "processedValue"

# Convert method to static with parameters
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-static-with-parameters \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" GetFormattedNumber

# Convert method to static with instance
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-static-with-instance \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" GetFormattedNumber "calculator"
# Convert method to extension
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-extension-method \
  "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs" GetFormattedNumber
```

## Common Errors

| Error | Solution |
|-------|----------|
| File not found | Check file path relative to solution |
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
