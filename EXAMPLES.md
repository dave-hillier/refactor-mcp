# RefactorMCP Examples

This document provides comprehensive examples for all refactoring tools available in RefactorMCP. Each example shows the before/after code and the exact CLI commands to perform the refactoring.

Using the MCP tools is the preferred method for refactoring large files where manual edits become cumbersome.

## Getting Started

### Loading a Solution
Before performing any refactoring, you need to load a solution:

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test load-solution ./RefactorMCP.sln
```

### Test Mode Usage
All examples use the test mode syntax:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test <command> [arguments]
```

## 1. Extract Method

**Purpose**: Extract selected code into a new private method and replace with a method call.

### Example
**Before** (in `ExampleCode.cs` lines 21-26):
```csharp
public int Calculate(int a, int b)
{
    // This code block can be extracted into a method
    if (a < 0 || b < 0)
    {
        throw new ArgumentException("Negative numbers not allowed");
    }
    
    var result = a + b;
    numbers.Add(result);
    Console.WriteLine($"Result: {result}");
    return result;
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test extract-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "22:9-25:34" \
  "ValidateInputs"
```

**After**:
```csharp
public int Calculate(int a, int b)
{
    ValidateInputs();
    
    var result = a + b;
    numbers.Add(result);
    Console.WriteLine($"Result: {result}");
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

## 2. Introduce Field

**Purpose**: Extract an expression into a class field and replace the expression with a field reference.

### Example
**Before** (in `ExampleCode.cs` line 35):
```csharp
public double GetAverage()
{
    return numbers.Sum() / (double)numbers.Count; // This expression can become a field
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-field \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "35:16-35:58" \
  "_averageValue" \
  "private"
```

**After**:
```csharp
private double _averageValue = numbers.Sum() / (double)numbers.Count;

public double GetAverage()
{
    return _averageValue;
}
```

## 3. Introduce Variable

**Purpose**: Extract a complex expression into a local variable.

### Example
**Before** (in `ExampleCode.cs` line 41):
```csharp
public string FormatResult(int value)
{
    return $"The calculation result is: {value * 2 + 10}"; // Complex expression can become a variable
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-variable \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "41:50-41:65" \
  "processedValue"
```

**After**:
```csharp
public string FormatResult(int value)
{
    var processedValue = value * 2 + 10;
    return $"The calculation result is: {processedValue}";
}
```

## 4. Make Field Readonly

**Purpose**: Add readonly modifier to a field and move initialization to constructors.

### Example
**Before** (in `ExampleCode.cs` line 50):
```csharp
private string format = "Currency"; // This field can be made readonly

public Calculator(string op)
{
    operatorSymbol = op;
}

public void SetFormat(string newFormat)
{
    format = newFormat; // This assignment would move to constructor
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test make-field-readonly \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  50
```

**After**:
```csharp
private readonly string format;

public Calculator(string op)
{
    operatorSymbol = op;
    format = "Currency";
}

// SetFormat method would need to be removed or refactored since field is now readonly
```

## 5. Introduce Parameter

**Purpose**: Extract an expression into a new method parameter.

### Example
**Before** (in `ExampleCode.cs` line 41):
```csharp
public string FormatResult(int value)
{
    return $"The calculation result is: {value * 2 + 10}";
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-parameter \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  40 \
  "41:50-41:65" \
  "processedValue"
```

**After**:
```csharp
public string FormatResult(int value, int processedValue)
{
    return $"The calculation result is: {processedValue}";
}
```

## 6. Convert to Static with Parameters

**Purpose**: Convert an instance method to static by turning field and property usages into parameters.

### Example
**Before** (in `ExampleCode.cs` line 46):
```csharp
public string GetFormattedNumber(int number)
{
    return $"{operatorSymbol}: {number}";
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-static-with-parameters \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  46
```

**After**:
```csharp
public static string GetFormattedNumber(string operatorSymbol, int number)
{
    return $"{operatorSymbol}: {number}";
}
```

## 7. Convert to Static with Instance

**Purpose**: Convert an instance method to static and add an explicit instance parameter for member access.

### Example
**Before** (same as previous example):
```csharp
public string GetFormattedNumber(int number)
{
    return $"{operatorSymbol}: {number}";
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-static-with-instance \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  46 \
  "calculator"
```

**After**:
```csharp
public static string GetFormattedNumber(Calculator calculator, int number)
{
    return $"{calculator.operatorSymbol}: {number}";
}
```

## 8. Convert To Extension Method

**Purpose**: Transform an instance method into an extension method in a static class.

### Example
**Before** (in `ExampleCode.cs` line 46):
```csharp
public string GetFormattedNumber(int number)
{
    return $"{operatorSymbol}: {number}"; // Uses instance field
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test convert-to-extension-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  46
```

**After**:
```csharp
public static class CalculatorExtensions
{
    public static string GetFormattedNumber(this Calculator calculator, int number)
    {
        return $"{calculator.operatorSymbol}: {number}";
    }
}
```

## 9. Safe Delete Parameter

**Purpose**: Remove an unused method parameter and update call sites.

### Example
**Before** (in `ExampleCode.cs` line 74):
```csharp
public int Multiply(int x, int y, int unusedParam)
{
    return x * y; // unusedParam can be safely deleted
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test safe-delete-parameter \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  Multiply \
  unusedParam
```

**After**:
```csharp
public int Multiply(int x, int y)
{
    return x * y;
}
```

## 6. Load Solution (Utility Command)

**Purpose**: Load and validate a solution file before performing refactorings.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test load-solution "./RefactorMCP.sln"
```

**Expected Output**:
```
Successfully loaded solution 'RefactorMCP.sln' with 2 projects: RefactorMCP.ConsoleApp, RefactorMCP.Tests
```

## 9. Unload Solution (Utility Command)

**Purpose**: Remove a loaded solution from the in-memory cache.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test unload-solution "./RefactorMCP.sln"
```

**Expected Output**:
```
Unloaded solution 'RefactorMCP.sln' from cache
```

## 10. Clear Solution Cache (Utility Command)

**Purpose**: Remove all cached solutions when projects change on disk.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test clear-solution-cache
```

**Expected Output**:
```
Cleared all cached solutions
```

## 11. List Tools (Utility Command)

**Purpose**: Display all available refactoring tools and their status.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --test list-tools
```

**Output**:
```
Available refactoring tools:
load-solution - Load a solution file for refactoring operations
unload-solution - Remove a loaded solution from cache
clear-solution-cache - Clear all cached solutions
extract-method - Extract selected code into a new method
introduce-field - Create a new field from selected code
introduce-variable - Create a new variable from selected code
make-field-readonly - Make a field readonly and move initialization to constructors
introduce-parameter - Create a new parameter from selected code (TODO)
convert-to-static-with-parameters - Transform instance method to static (TODO)
convert-to-static-with-instance - Transform instance method to static with instance parameter (TODO)
move-static-method - Move a static method to another class (TODO)
move-instance-method - Move an instance method to another class (TODO)
transform-setter-to-init - Convert property setter to init-only setter (TODO)
safe-delete - Safely delete a field, parameter, or variable (TODO)

```

## Range Format

All refactoring commands that require selecting code use the range format:
```
"startLine:startColumn-endLine:endColumn"
```

- **Lines and columns are 1-based** (first line is 1, first column is 1)
- **Columns count characters**, including spaces and tabs
- **Range is inclusive** of both start and end positions

### Finding Range Coordinates

To find the correct range for your code selection:

1. **Count lines** from the top of the file (starting at 1)
2. **Count characters** from the beginning of the line (starting at 1)
3. **Include whitespace** in your character count

### Example Range Calculation

For this code:
```csharp
1:  public int Calculate(int a, int b)
2:  {
3:      if (a < 0 || b < 0)
4:      {
5:          throw new ArgumentException("Negative numbers not allowed");
6:      }
7:  }
```

To select `if (a < 0 || b < 0)` on line 3:
- **Start**: Line 3, Column 5 (after the 4 spaces of indentation)
- **End**: Line 3, Column 25 (after the closing parenthesis)
- **Range**: `"3:5-3:25"`

## Error Handling

### Common Errors

1. **File not found**:
   ```
   Error: File ./path/to/file.cs not found in solution
   ```

2. **Invalid range format**:
   ```
   Error: Invalid selection range format. Use 'startLine:startColumn-endLine:endColumn'
   ```

3. **No valid code selected**:
   ```
   Error: Selected code does not contain extractable statements
   ```

4. **Solution not found**:
   ```
   Error: Solution file not found at ./path/to/solution.sln
   ```

### Tips for Success

1. **Always load the solution first** to ensure all projects are available
2. **Use exact file paths** relative to the solution directory
3. **Double-check range coordinates** by counting carefully
4. **Test with simple selections** before trying complex refactorings
5. **Backup your code** before performing refactorings

## Advanced Usage

### Chaining Operations
You can perform multiple refactorings in sequence:

```bash
# First, extract a method
dotnet run --project RefactorMCP.ConsoleApp -- --test extract-method "./RefactorMCP.sln" "./MyFile.cs" "10:5-15:20" "ExtractedMethod"

# Then, make a field readonly
dotnet run --project RefactorMCP.ConsoleApp -- --test make-field-readonly "./RefactorMCP.sln" "./MyFile.cs" 25

# Finally, introduce a variable
dotnet run --project RefactorMCP.ConsoleApp -- --test introduce-variable "./RefactorMCP.sln" "./MyFile.cs" "30:10-30:35" "tempValue"
```

### Working with Different Projects
If your solution has multiple projects, make sure to specify the correct file path:

```bash
# For a file in the main project
dotnet run --project RefactorMCP.ConsoleApp -- --test extract-method "./RefactorMCP.sln" "./RefactorMCP.ConsoleApp/MyFile.cs" "10:5-15:20" "ExtractedMethod"

# For a file in the test project  
dotnet run --project RefactorMCP.ConsoleApp -- --test extract-method "./RefactorMCP.sln" "./RefactorMCP.Tests/TestFile.cs" "5:1-8:10" "TestMethod"
``` 