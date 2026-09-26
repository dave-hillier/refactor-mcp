# RefactorMCP Examples

This document provides comprehensive examples for all refactoring tools available in RefactorMCP. Each example shows the before/after code and the command needed to perform the refactoring.

Using the MCP tools is the preferred method for refactoring large C# files where manual edits become cumbersome.

## Getting Started

Tools are named in kebab-case and every tool takes the solution and the file it
should work on:

```bash
refactor extract-method --solution ./RefactorMCP.sln --file ./src/Foo.cs \
    --selection-range 10:5-20:6 --method-name ComputeTotal
```

Options are named after the tool's parameters, so `--selection-range` is
`selectionRange`; a trailing `Path` may be dropped, so `--file` is `filePath`.
Any argument without an option is taken positionally in parameter order, which
is what the `--cli` form in the examples below uses:

```bash
refactor --cli extract-method ./RefactorMCP.sln ./src/Foo.cs 10:5-20:6 ComputeTotal
```

Every example also works with a JSON object, which is easier to generate from
another program:

```bash
refactor --json extract-method \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","selectionRange":"10:5-20:6","methodName":"ComputeTotal"}'
```

Run `refactor list-tools --verbose` for the tool list, and
`refactor <tool> --help` for one tool's options. Steps that need the same
solution repeatedly are faster if a daemon holds it: see
[README.md](./README.md).

Most examples below show only the arguments that matter for the tool being
demonstrated; fill in the rest the same way.

### Loading a Solution
A solution is loaded on demand by the first tool that needs it, so every tool
takes `solutionPath` and `load-solution` is optional. Each call names its
solution, which keeps it independent of what was loaded earlier: it still works
after the server restarts, and with several solutions loaded at once.
Loading a solution explicitly starts a fresh session, clearing every loaded
solution and cached data, and lists the solution's projects:

```bash
refactor load-solution --solution ./RefactorMCP.sln
```

## 1. Extract Method

**Purpose**: Extract selected code into a new private method and replace with a method call.
**Note**: Expression-bodied methods are not supported for extraction.

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
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "22:9-25:13" \
  "ValidateInputs"
```

**JSON Example**:
```json
{
  "tool": "extract-method",
  "solutionPath": "./RefactorMCP.sln",
  "filePath": "./RefactorMCP.Tests/ExampleCode.cs",
  "selectionRange": "22:9-25:13",
  "methodName": "ValidateInputs"
}
```

**After**:
```csharp
public int Calculate(int a, int b)
{
    ValidateInputs(a, b);
    var result = a + b;
    numbers.Add(result);
    Console.WriteLine($"Result: {result}");
    return result;
}

private void ValidateInputs(int a, int b)
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
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-field \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "35:16-35:58" \
  "_averageValue" \
  "private"
```
If a field named `_averageValue` already exists on the `Calculator` class, the command will fail with an error.

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
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-variable \
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
dotnet run --project RefactorMCP.ConsoleApp -- --cli make-field-readonly \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  format
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
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-parameter \
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

## 6. Convert To Extension Method

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
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-extension-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  GetFormattedNumber
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

## 7. Make Static Then Move

**Purpose**: Convert an instance method to static with an explicit instance parameter and move it to another class.

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
dotnet run --project RefactorMCP.ConsoleApp -- --cli make-static-then-move \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  GetFormattedNumber \
  MathUtilities \
  calculator
```

**After**:
```csharp
public class Calculator
{
    public string GetFormattedNumber(int number)
    {
        return MathUtilities.GetFormattedNumber(this, number);
    }
}

public class MathUtilities
{
    public static string GetFormattedNumber(Calculator calculator, int number)
    {
        return $"{calculator.operatorSymbol}: {number}";
    }
}
```
The wrapper in `Calculator` preserves call sites while the actual logic moves to `MathUtilities`.

## 8. Move Type to Separate File

**Purpose**: Move a top-level type into its own file named after the type. Works for classes, interfaces, structs, records, enums and delegates.

### Example
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

## 9. Inline Method

**Purpose**: Replace method calls with the method body and remove the original method.

### Example
**Before** (in `InlineSample.cs`):
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
  "./RefactorMCP.sln" \
  "./InlineSample.cs" \
  Helper
```

**After**:
```csharp
public void Call()
{
    Console.WriteLine("Hi");
    Console.WriteLine("Done");
}
```
## 10. Transform Setter to Init

**Purpose**: Convert a property setter to an init-only setter.

### Example
**Before** (in `ExampleCode.cs` line 60):
```csharp
public string Name { get; set; } = "Default Calculator";
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli transform-setter-to-init \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  Name
```

**After**:
```csharp
public string Name { get; init; } = "Default Calculator";
```

## 11. Cleanup Usings

**Purpose**: Remove unused using directives from a file.

### Example
**Before** (in `CleanupSample.cs`):
```csharp
using System;
using System.Text;

public class CleanupSample
{
    public void Say() => Console.WriteLine("Hi");
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli cleanup-usings \
  "./RefactorMCP.sln" \
  "./CleanupSample.cs"
```

**After**:
```csharp
using System;

public class CleanupSample
{
    public void Say() => Console.WriteLine("Hi");
}
```

## 12. Load Solution (Utility Command)

**Purpose**: Clear previous caches and load a solution file before performing refactorings.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli load-solution "./RefactorMCP.sln"
```
```json
{"tool":"load-solution","solutionPath":"./RefactorMCP.sln"}
```

**Expected Output**:
```
Successfully loaded solution 'RefactorMCP.sln' with 2 projects: RefactorMCP.ConsoleApp, RefactorMCP.Tests
```

## 13. Unload Solution (Utility Command)

**Purpose**: Remove a loaded solution from the in-memory cache.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli unload-solution "./RefactorMCP.sln"
```

**Expected Output**:
```
Unloaded solution 'RefactorMCP.sln' from cache
```

## 14. Clear Solution Cache (Utility Command)

**Purpose**: Remove all cached solutions when projects change on disk.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli clear-solution-cache
```

**Expected Output**:
```
Cleared all cached solutions
```

## 15. List Tools (Utility Command)

**Purpose**: Display all available refactoring tools and their status.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json ListTools '{}'
```

**Output**: one kebab-case tool name per line, e.g. `add-observer`, `extract-method`,
`load-solution`. `refactor list-tools` prints the same names with their descriptions.

## 16. Version Info (Utility Command)

**Purpose**: Display the current build version and timestamp.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli version
```

**Expected Output**:
```
Version: 1.0.0.0 (Build 2024-01-01 00:00:00Z)
```

## 17. Analyze Refactoring Opportunities

**Purpose**: Prompt the server to inspect a file for smells such as long methods, long parameter lists, large classes, or unused members.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli analyze-refactoring-opportunities "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs"
```

**Expected Output**:
```
Suggestions:
- Method 'UnusedHelper' appears unused -> safe-delete-member
- Field 'deprecatedCounter' appears unused -> safe-delete-member
```

## 18. List Class Lengths

**Purpose**: Display each class in the solution with its number of lines as a simple complexity metric.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli list-class-lengths "./RefactorMCP.sln"
```

**Expected Output**:
```
Class lengths:
Calculator - 82 lines
MathUtilities - 4 lines
Logger - 8 lines
```

## Find References, Implementations, Overrides and Callers

**Purpose**: Answer where a symbol is used and what builds on it, without
changing any code. Each tool names the symbol by `filePath` and name, like
`rename-symbol`: the file may declare the symbol or just use it, and `line`
(with `column` for the exact token) chooses between symbols of the same name.
A name that could mean several symbols is refused with the candidates listed.
Locations are relative to the solution's directory.

- `find-references` lists every reference, grouped by file with the line of
  code at each. Writes, implicit uses, and uses through an interface or base
  member are marked. `includeDeclarations` adds the declarations, and
  `maxResults` (200 by default) caps a long list.
- `find-implementations` lists the types implementing an interface, or the
  members implementing an interface member.
- `find-overrides` lists the overrides of a virtual, abstract or override
  member, at every level below it.
- `find-callers` lists the members that call a method or use a property or
  event, grouped by caller. Calls made through an interface or base member are
  marked.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json find-references \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Counter.cs","symbolName":"Count"}'

dotnet run --project RefactorMCP.ConsoleApp -- --json find-implementations \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/IShape.cs","symbolName":"IShape"}'

dotnet run --project RefactorMCP.ConsoleApp -- --json find-overrides \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Animal.cs","memberName":"Speak"}'

dotnet run --project RefactorMCP.ConsoleApp -- --json find-callers \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Service.cs","memberName":"Log","line":3}'
```

**Expected Output** (`find-references`):
```
3 references to property Counter.Count in 2 files
src/Counter.cs
  5:26  [write] public void Bump() { Count++; }
src/Report.cs
  9:17  var total = counter.Count;
  14:9  [write] counter.Count = 0;
```

**Expected Output** (`find-callers`):
```
3 calls to method Service.Log(string) from 2 callers
method Service.A()
  src/Service.cs:4:23  public void A() { Log("a"); Log("b"); }
  src/Service.cs:4:33  public void A() { Log("a"); Log("b"); }
method Worker.Run(Service)
  src/Worker.cs:8:13  service.Log("run");
```

## 19. Extract Interface

**Purpose**: Generate an interface from specific class members.

### Example
**Before**:
```csharp
public class Person
{
    public string Name { get; set; }
    public void Greet() { Console.WriteLine(Name); }
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-interface \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  Person \
  "Name,Greet" \
  "./IPerson.cs"
```

**After**:
```csharp
public interface IPerson
{
    string Name { get; set; }
    void Greet();
}

public class Person : IPerson
{
    public string Name { get; set; }
    public void Greet() { Console.WriteLine(Name); }
}
```


## 20. Rename Symbol

**Purpose**: Rename a field or method across the entire file.

### Example
**Before** (excerpt from `ExampleCode.cs`):
```csharp
private List<int> numbers = new List<int>();

// ...
numbers.Add(result);
return numbers.Sum() / (double)numbers.Count;
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli rename-symbol \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  numbers \
  values
```

**File Diff**:
```diff
-    private List<int> numbers = new List<int>();
+    private List<int> values = new List<int>();
@@
-    numbers.Add(result);
+    values.Add(result);
@@
-    return numbers.Sum() / (double)numbers.Count;
+    return values.Sum() / (double)values.Count;
```

**After**:
```csharp
private List<int> values = new List<int>();

// ...
values.Add(result);
return values.Sum() / (double)values.Count;
```

## 21. Feature Flag Refactor

**Purpose**: Replace a `features.IsEnabled(flag)` check with strategy classes chosen by a property that checks the flag.

### Example
**Before**:
```csharp
public void DoWork()
{
    if (featureFlags.IsEnabled("CoolFeature"))
    {
        Console.WriteLine("New path");
    }
    else
    {
        Console.WriteLine("Old path");
    }
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli feature-flag-refactor \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/FeatureFlag.cs" \
  CoolFeature
```

**After**:
```csharp
public void DoWork()
{
    CoolFeature.Apply();
}

private ICoolFeatureStrategy CoolFeature => featureFlags.IsEnabled("CoolFeature") ? new CoolFeatureStrategy() : new NoCoolFeatureStrategy();
```

`CoolFeatureStrategy` and `NoCoolFeatureStrategy` implement `ICoolFeatureStrategy`, and their `Apply` methods hold the two branches.
## 22. Extract Decorator

**Purpose**: Generate a decorator that implements an interface and forwards every member to a wrapped instance.

### Example
**Before**:
```csharp
public interface IGreeter
{
    void Greet(string name);
}

public class Greeter : IGreeter
{
    public void Greet(string name)
    {
        Console.WriteLine($"Hello {name}");
    }
}
```
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-decorator '{"solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/Decorator.cs","typeName":"Greeter"}'
```
**After** (in `GreeterDecorator.cs`):
```csharp
public class GreeterDecorator : IGreeter
{
    private readonly IGreeter _inner;

    public GreeterDecorator(IGreeter inner)
    {
        _inner = inner;
    }

    public void Greet(string name) => _inner.Greet(name);
}
```

## 23. Create Adapter

**Purpose**: Implement an interface over an existing class by forwarding to its members.

### Example
**Before**:
```csharp
public interface ILogger
{
    void Log(string message);
}

public class LegacyLogger
{
    public void Write(string message)
    {
        Console.WriteLine(message);
    }
}
```
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json create-adapter '{"solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/Adapter.cs","className":"LegacyLogger","interfaceName":"ILogger","adapterName":"LegacyLoggerAdapter","memberMap":"Log:Write"}'
```
**After** (in `LegacyLoggerAdapter.cs`):
```csharp
public class LegacyLoggerAdapter : ILogger
{
    private readonly LegacyLogger _adaptee;

    public LegacyLoggerAdapter(LegacyLogger adaptee)
    {
        _adaptee = adaptee;
    }

    public void Log(string message) => _adaptee.Write(message);
}
```

## 24. Add Observer

**Purpose**: Add an event and raise it within a method.

### Example
**Before**:
```csharp
public class Counter
{
    private int _value;
    public void Update(int value)
    {
        _value = value;
    }
}
```
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli add-observer \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/Observer.cs" \
  Counter \
  Update \
  Updated
```
**After**:
```csharp
public event Action<int> Updated;
public void Update(int value)
{
    _value = value;
    Updated?.Invoke(value);
}
```

## 25. Use Interface

**Purpose**: Change a method parameter type to an implemented interface when only interface members are used.

### Example
**Before**:
```csharp
public interface IWriter { void Write(string value); }
public class FileWriter : IWriter { public void Write(string value) { } }
public class C
{
    public void DoWork(FileWriter writer)
    {
        writer.Write("hi");
    }
}
```
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli use-interface \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/Writer.cs" \
  DoWork \
  writer \
  IWriter
```
**After**:
```csharp
public void DoWork(IWriter writer)
{
    writer.Write("hi");
}
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
   Error: File ./path/to/file.cs not found in solution (current dir: /your/working/dir)
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
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method "./RefactorMCP.sln" "./MyFile.cs" "10:5-15:20" "ExtractedMethod"

# Then, make a field readonly
dotnet run --project RefactorMCP.ConsoleApp -- --cli make-field-readonly "./RefactorMCP.sln" "./MyFile.cs" "_cachedTotal"

# Finally, introduce a variable
dotnet run --project RefactorMCP.ConsoleApp -- --cli introduce-variable "./RefactorMCP.sln" "./MyFile.cs" "30:10-30:35" "tempValue"
```

### Working with Different Projects
If your solution has multiple projects, make sure to specify the correct file path:

```bash
# For a file in the main project
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method "./RefactorMCP.sln" "./RefactorMCP.ConsoleApp/MyFile.cs" "10:5-15:20" "ExtractedMethod"

# For a file in the test project  
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method "./RefactorMCP.sln" "./RefactorMCP.Tests/TestFile.cs" "5:1-8:10" "TestMethod"
```

## Catalog Refactorings

Tools added for the refactoring catalog in `Catalog/`, grouped as the catalog
groups them. Each tool's fixtures are the fuller specification.

### Methods and locals

<!-- Methods and locals: examples for this group's tools go below this line. -->

The tools that act on one local take the line and column of its name, on its
declaration or on any use of it.

**Inline Local Variable**: replace every use of `total` with its initializer
and remove the declaration.

```bash
refactor --json inline-local-variable \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":13}'
```

**Split Temporary Variable**: the local reassigned on line 15 gets a new
local, `area`, from that assignment on. The position is the assigned name.

```bash
refactor --json split-temporary-variable \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":15,"column":9,"name":"area"}'
```

**Split Declaration and Assignment**: `var total = a + b;` becomes
`int total;` followed by `total = a + b;`.

```bash
refactor --json split-declaration-and-assignment \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":13}'
```

**Join Declaration and Assignment**: `int total;` moves down to its first
assignment and becomes `int total = a + b;`.

```bash
refactor --json join-declaration-and-assignment \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":13}'
```

**Convert Local to Field**: promote `total` to a private field `_total`. The
name is optional and defaults to the local's.

```bash
refactor --json convert-local-to-field \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":13,"name":"_total"}'
```

**Inline Method** takes an optional `line`, the line of the method's
declaration, to choose between overloads:

```bash
refactor --json inline-method \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","methodName":"Format","line":20}'
```

<!-- End of Methods and locals. -->

### Method conversions

<!-- Method conversions: examples for this group's tools go below this line. -->

**Convert Method to Local Function**: move the private method `WithTax`, used
only by one member, into that member. `line` chooses between overloads.

```bash
refactor --json convert-method-to-local-function \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","methodName":"WithTax","line":20}'
```

**Convert Local Function to Method**: the local function named, or called, at
line 18, column 13 becomes a private method; the variables it captures become
parameters. The name is optional and defaults to the local function's.

```bash
refactor --json convert-local-function-to-method \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":18,"column":13,"name":"ApplyTax"}'
```

The body conversions take the position of the member's name, or of the `get`
or `set` keyword for one accessor.

**Convert to Expression Body**: `{ return _a + _b; }` becomes `=> _a + _b;`.

```bash
refactor --json convert-to-expression-body \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":16}'
```

**Convert to Block Body**: `=> _a + _b;` becomes `{ return _a + _b; }`.

```bash
refactor --json convert-to-block-body \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":16}'
```

**Convert Lambda to Method Group**: `numbers.Select(n => Format(n))` becomes
`numbers.Select(Format)`. The position is anywhere inside the lambda.

```bash
refactor --json convert-lambda-to-method-group \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":14,"column":31}'
```

**Convert Method Group to Lambda**: `numbers.Select(Format)` becomes
`numbers.Select(number => Format(number))`. The position is the method's name.

```bash
refactor --json convert-method-group-to-lambda \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":14,"column":31}'
```

<!-- End of Method conversions. -->

### Signatures

<!-- Signatures: examples for this group's tools go below this line. -->

These tools find a method by its name in a file; `line`, any line of its
declaration, chooses between overloads. A constructor is named by its type.
Overrides, interface members and their implementations change with the
method, and every call in the solution is updated. Each tool refuses a change
that would not compile.

#### Change Signature

`parameters` is the whole new list in order. Existing parameters are named; a
new one gives its `type` and the `value` existing calls pass, a `default`, or
both. Leaving a parameter out removes it.

```bash
refactor --json change-signature '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Pricing.cs","methodName":"Discount","parameters":[{"name":"percent"},{"name":"price"},{"name":"rounding","type":"int","value":"2"}]}'
```

```csharp
// before
public decimal Discount(decimal price, int percent) { ... }
var sale = Discount(price, 10);

// after
public decimal Discount(int percent, decimal price, int rounding) { ... }
var sale = Discount(10, price, 2);
```

#### Inline Parameter

When every call passes the same constant, the body uses the constant and the
parameter goes.

```bash
refactor --json inline-parameter '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Greeting.cs","methodName":"Greet","parameterName":"punctuation"}'
```

#### Remove Unused Parameter

Removes a parameter no body in the family reads, refusing when an argument a
call passes for it may have side effects.

```bash
refactor --json remove-unused-parameter '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Report.cs","methodName":"Title","parameterName":"width"}'
```

#### Add Parameter Default Value

Gives a parameter a default; `removeFromCallSites` drops arguments that pass
the same value.

```bash
refactor --json add-parameter-default-value '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Catalogue.cs","methodName":"Page","parameterName":"size","value":"20","removeFromCallSites":true}'
```

```csharp
// before
public string Page(int number, int size) { ... }
catalogue.Page(1, 20) + catalogue.Page(2, 50)

// after
public string Page(int number, int size = 20) { ... }
catalogue.Page(1) + catalogue.Page(2, 50)
```

#### Use Named Arguments

Names the arguments of the call at `line` and `column`.

```bash
refactor --json use-named-arguments '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Shipping.cs","line":12,"column":16}'
```

```csharp
// before
return Quote(1.5m, "EU", true);

// after
return Quote(weight: 1.5m, region: "EU", express: true);
```

#### Change Return Type

Changes the return type when the body and every caller still compile, and
every call around the result binds to the same overload as before.

```bash
refactor --json change-return-type '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Catalogue.cs","methodName":"Names","returnType":"IEnumerable<string>"}'
```

#### Change Accessibility

Changes the accessibility of a type or member, refusing when a reference,
override or interface implementation would break, or a call would bind to a
different overload.

```bash
refactor --json change-accessibility '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Order.cs","memberName":"Tax","accessibility":"internal"}'
```

<!-- End of Signatures. -->

### Fields, properties and constants

<!-- Fields, properties and constants: examples for this group's tools go below this line. -->

Tools that take a member name also accept it qualified by its type, such as
`Order.Quantity`, to choose between types declared in the same file.

#### Introduce Field of a Type

`introduce-field` with `fieldType` adds a field of that type to the type
containing the selection, instead of introducing one from an expression. A
class with an accessible parameterless constructor gets a readonly field
holding a new instance.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-field '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Customer.cs","selectionRange":"3:18-3:26","fieldName":"_address","fieldType":"Address"}'
```

#### Introduce Constant

Replaces a selected literal or constant expression with a private constant;
`replaceAll` also replaces every other occurrence of the same value in the
type.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-constant '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Session.cs","selectionRange":"7:30-7:32","constantName":"SecondsPerMinute","replaceAll":true}'
```

#### Inline Constant

Replaces every use of a constant with its value, qualified and parenthesised
as each use needs, and removes the constant.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json inline-constant '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Limits.cs","constantName":"MaxItems"}'
```

#### Inline Field

Replaces every read of a field assigned only by a side-effect-free
initialiser with that initialiser, and removes the field.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json inline-field '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Greeter.cs","fieldName":"_greeting"}'
```

#### Encapsulate Field

Makes a field private behind a property; code outside the type uses the
property. `propertyName` is optional.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json encapsulate-field '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Order.cs","fieldName":"Quantity"}'
```

#### Convert to Auto-Property

Replaces a property that only reads and writes a private field with an
auto-property, and removes the field.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-auto-property '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Basket.cs","propertyName":"Count"}'
```

#### Convert Auto-Property to Backing Field

Gives an auto-property a private backing field and accessors that read and
write it. `fieldName` is optional.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-auto-property-to-backing-field '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Customer.cs","propertyName":"Name","fieldName":"_name"}'
```

#### Convert Method to Property

Turns a parameterless method that returns a value into a get-only property,
with its overrides, and each call into a read. `GetTotal` becomes `Total`
unless `propertyName` is given.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-method-to-property '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Order.cs","methodName":"GetTotal"}'
```

#### Convert Property to Methods

Replaces a property with `Get` and `Set` methods and turns every read and
write into a call.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-property-to-methods '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Order.cs","propertyName":"Quantity"}'
```

#### Encapsulate Collection

Exposes a private `List<T>` field as a read-only list, adds `Add` and
`Remove` methods, and redirects callers that added or removed through the
property. `elementName` is optional.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json encapsulate-collection '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Order.cs","fieldName":"_tags","elementName":"Tag"}'
```

<!-- End of Fields, properties and constants. -->

### Moving members and types

<!-- Moving members and types: examples for this group's tools go below this line. -->

`move-member` moves a method, field or property. An instance member moves
through a field, property or parameter of the target type (`via`), which
becomes `this` in the target; a static member moves to `targetType`, created
as a static class if it does not exist. A moved method leaves a delegating
stub unless `keepStub` is false, in which case every call is updated.
`kind` optionally refuses a member of another kind than expected.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-member '{"solutionPath":"./Shop.sln","filePath":"./Shop/Customer.cs","memberName":"Label","via":"_address","keepStub":false}'
dotnet run --project RefactorMCP.ConsoleApp -- --json move-member '{"solutionPath":"./Shop.sln","filePath":"./Shop/Order.cs","memberName":"Vat","targetType":"TaxRules","kind":"static-method"}'
```

`move-member-to-partial-file` moves a member of a partial type into the part
declared in another file, creating the file with a new part if needed.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-member-to-partial-file '{"solutionPath":"./Shop.sln","filePath":"./Shop/Order.cs","memberName":"Discount","targetFilePath":"./Shop/Order.Pricing.cs"}'
```

`move-type-to-namespace` changes a type's namespace and updates qualified
names and usings across the solution; `sync-namespace-with-folder` sets a
file's namespace from the project's root namespace and its folders.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-type-to-namespace '{"solutionPath":"./Shop.sln","filePath":"./Shop/Invoice.cs","typeName":"Invoice","targetNamespace":"Shop.Billing"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json sync-namespace-with-folder '{"solutionPath":"./Shop.sln","filePath":"./Shop/Billing/Invoice.cs"}'
```

`rename-file-to-match-type` renames a file after the single top-level type it
declares.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json rename-file-to-match-type '{"solutionPath":"./Shop.sln","filePath":"./Shop/Ledger.cs"}'
```

`make-method-static` makes an instance method static, passing the instance
(`"pass":"instance"`, the default) or the members it reads
(`"pass":"parameters"`), and updates every call; `make-method-instance` turns
a static method taking its own type back into an instance method.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json make-method-static '{"solutionPath":"./Shop.sln","filePath":"./Shop/Order.cs","methodName":"Describe","pass":"parameters"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json make-method-instance '{"solutionPath":"./Shop.sln","filePath":"./Shop/Order.cs","methodName":"Discounted"}'
```

`convert-to-extension-method` also converts a static method of a static class
in place, adding `this` to its first parameter and rewriting calls to the
extension form; `convert-extension-method-to-static` does the reverse.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-extension-method '{"solutionPath":"./Shop.sln","filePath":"./Shop/Text.cs","methodName":"Shout"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-extension-method-to-static '{"solutionPath":"./Shop.sln","filePath":"./Shop/Text.cs","methodName":"Shout"}'
```


<!-- End of Moving members and types. -->

### Types and hierarchy

<!-- Types and hierarchy: examples for this group's tools go below this line. -->

#### Create Type

Creates an empty class, interface, record or struct, in a new file or at the
end of an existing one. The namespace defaults to the one the file, or the
other files in its folder, already use.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json create-type \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Address.cs","name":"Address","kind":"class","baseType":"Entity"}'
```

#### Change Base Type

Sets, replaces or removes (leave out `newBaseType`) a class's base class,
refusing when the class or its callers rely on the old one.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json change-base-type \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Manager.cs","className":"Manager","newBaseType":"Employee"}'
```

#### Pull Up Field and Pull Up Method

Moves a member into the base class. Identical copies in the other subclasses
are removed; `makeAbstract` declares a method abstract in the base class and
makes the subclasses' versions overrides. `line` picks one overload.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json pull-up-field \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Manager.cs","className":"Manager","fieldName":"_name"}'

dotnet run --project RefactorMCP.ConsoleApp -- --json pull-up-method \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Manager.cs","className":"Manager","methodName":"Bonus","makeAbstract":true}'
```

#### Pull Up Constructor Body

Moves the leading statements of a constructor that only set up the base class
into a base constructor, and chains to it with `base(...)`.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json pull-up-constructor-body \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Manager.cs","className":"Manager","line":12}'
```

#### Push Down Field and Push Down Method

Moves a member into the subclasses that use it, or every subclass when none
does. An abstract method is removed and its overrides become ordinary methods.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json push-down-field \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Employee.cs","className":"Employee","fieldName":"Quota"}'

dotnet run --project RefactorMCP.ConsoleApp -- --json push-down-method \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Employee.cs","className":"Employee","methodName":"QuotaReport"}'
```

#### Extract Interface

Declares the named members, or every public instance member when
`memberList` is empty, in a new interface and makes the class implement it.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-interface \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Order.cs","className":"Order","memberList":"Total,Add","interfaceFilePath":"./src/IOrder.cs","interfaceName":"IOrder"}'
```

#### Change Type

Changes the declared type of a local, parameter (`parameterName` with the
method's name), field, property or method return value, refusing when a use
would stop compiling or reach a different member. `line` and `column` pick
between declarations with the same name.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json change-type \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Report.cs","name":"Print","parameterName":"writer","newType":"IWriter"}'
```

#### Introduce Generic Type Parameter

Replaces a concrete type in a class, or in a method when `methodName` is
given, with a new type parameter, constrained as the uses need. Every other
use of the class or method passes the old type.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-generic-type-parameter \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Box.cs","className":"Box","typeToReplace":"Invoice","typeParameterName":"T"}'
```

<!-- End of Types and hierarchy. -->

### Type conversions

<!-- Type conversions: examples for this group's tools go below this line. -->

These tools find a type by its name in a file; `line`, any line of its
declaration, chooses between types of the same name. Each tool refuses a
change that would not compile, or that would change behaviour the solution
can observe.

#### Make Type Partial

```bash
refactor --json make-type-partial '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Invoice.cs","typeName":"Invoice"}'
```

#### Merge Partial Declarations

Moves every part's members, modifiers, base types and needed `using`
directives into the declaration in `filePath`, deleting files left empty.

```bash
refactor --json merge-partial-declarations '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Basket.cs","typeName":"Basket"}'
```

#### Convert Class to Record

Positional when the constructor only assigns get-only properties. Refused when
the solution compares, hashes or prints instances, where a record would behave
differently.

```bash
refactor --json convert-class-to-record '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Point.cs","typeName":"Point"}'
```

```csharp
// before
public sealed class Point
{
    public Point(int x, int y) { X = x; Y = y; }
    public int X { get; }
    public int Y { get; }
}

// after
public sealed record Point(int X, int Y);
```

#### Convert Record to Class

Writes out the constructor, properties and `Deconstruct` of a positional
record, and the `Equals`, `GetHashCode`, `ToString`, `==` and `!=` the record
provided.

```bash
refactor --json convert-record-to-class '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Point.cs","typeName":"Point"}'
```

#### Convert Tuple to Named Type

Replaces the tuple in the return type, or in `parameterName`, with a
`readonly record struct` (`kind` `class` gives a `sealed record`), updating
returned or passed literals and element accesses.

```bash
refactor --json convert-tuple-to-named-type '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Stats.cs","methodName":"Range","typeName":"MinMax"}'
```

```csharp
// before
public static (int min, int max) Range(int[] values) { ... return (min, max); }
var width = Stats.Range(values).max;

// after
public static MinMax Range(int[] values) { ... return new MinMax(min, max); }
var width = Stats.Range(values).Max;
public readonly record struct MinMax(int Min, int Max);
```

#### Convert Anonymous Type to Class

Declares a class with the same properties, equality and `ToString` as the
anonymous type at `line` and `column`, and constructs it throughout the
containing member.

```bash
refactor --json convert-anonymous-type-to-class '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Report.cs","line":7,"column":20,"className":"Line"}'
```

#### Convert to Primary Constructor

```bash
refactor --json convert-to-primary-constructor '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/OrderService.cs","typeName":"OrderService"}'
```

```csharp
// before
public class OrderService
{
    private readonly IRepository _repository;
    public OrderService(IRepository repository) { _repository = repository; }
    public void Place(Order order) => _repository.Save(order);
}

// after
public class OrderService(IRepository repository)
{
    public void Place(Order order) => repository.Save(order);
}
```

#### Convert Primary Constructor to Constructor

Captured parameters become private fields named `_parameter`.

```bash
refactor --json convert-primary-constructor-to-constructor '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/OrderService.cs","typeName":"OrderService"}'
```

#### Replace Constructor with Factory Method

`line` picks the constructor; `methodName` defaults to `Create` and
`accessibility`, the constructor's new accessibility, to `private`.

```bash
refactor --json replace-constructor-with-factory-method '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Order.cs","typeName":"Order","line":5,"methodName":"Create"}'
```

```csharp
// before
var order = new Order("tea", 2);

// after
var order = Order.Create("tea", 2);
```

<!-- End of Type conversions. -->

### Conditionals

<!-- Conditionals: examples for this group's tools go below this line. -->

The tools that act on a statement take the line and column of its keyword:
`if` or `switch`, or anywhere on a switch expression before its opening
brace.

#### Invert If

Negates the condition and swaps the branches. Without an `else`, an `if` that
ends a method or loop body becomes an early `return` or `continue`, and an
early return becomes an `if` around the code after it.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json invert-if '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Shipping.cs","line":10,"column":13}'
```

#### Merge Nested If

Joins an `if` whose only statement is another `if` into one `if` on both
conditions.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json merge-nested-if '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Shipping.cs","line":9,"column":13}'
```

#### Split If

Splits an `if` on its first `&&` into nested ifs, or on its first `||` into
two ifs with the same body.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json split-if '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Validator.cs","line":8,"column":13}'
```

#### Invert Boolean

Renames a `bool` field, property, method or local and negates every value it
is given and every use, so `IsEnabled` can become `IsDisabled`. The position
is the symbol's declaration or a use of it.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json invert-boolean '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Feature.cs","line":7,"column":21,"newName":"IsDisabled"}'
```

#### Convert If Chain to Switch

Turns an `if` / `else if` chain comparing one value with constants or patterns
into a switch statement.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-if-chain-to-switch '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Shipping.cs","line":9,"column":13}'
```

#### Convert Switch Statement to Expression

Turns a switch statement whose sections all return, or all assign one
variable, into a switch expression.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-switch-statement-to-expression '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Shipping.cs","line":7,"column":13}'
```

#### Convert Switch Expression to Statement

Turns a switch expression that is returned, assigned or initialises a local
into a switch statement.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-switch-expression-to-statement '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Shipping.cs","line":7,"column":25}'
```

#### Use Pattern Matching

Replaces `x is T` followed by casts `(T)x`, or `x as T` followed by a null
check, with a declaration pattern. `name` optionally names the variable.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json use-pattern-matching '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Shapes.cs","line":14,"column":13,"name":"circle"}'
```

#### Consolidate Duplicate Conditional Fragments

Moves statements every branch ends with after the conditional, and statements
every branch starts with before it.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json consolidate-duplicate-conditional-fragments '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Deal.cs","line":9,"column":13}'
```

<!-- End of Conditionals. -->

### Loops and expressions

<!-- Loops and expressions: examples for this group's tools go below this line. -->

These tools act on the loop, statement or expression under a caret: any line
and column inside it.

**Convert For to Foreach**: `for (int i = 0; i < orders.Count; i++)` that only
reads `orders[i]` becomes `foreach (Order order in orders)`. The optional
`name` names the element.

```bash
refactor --json convert-for-to-foreach \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":9}'
```

**Convert Foreach to For**: `foreach (var order in orders)` becomes an index
loop over `orders.Count` that starts with `var order = orders[i];`. The
optional `name` names the index.

```bash
refactor --json convert-foreach-to-for \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":9,"name":"index"}'
```

**Convert Foreach to LINQ**: a loop that adds the matching elements to a new
list, sums, counts or looks for a match becomes `Where`, `Select` and `ToList`,
`Sum`, `Count` or `Any`.

```bash
refactor --json convert-foreach-to-linq \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":13,"column":9}'
```

**Convert LINQ to Foreach**: `var names = users.Where(...).Select(...).ToList();`
becomes a list filled by a `foreach`. The optional `name` names the local that
holds a returned query's result.

```bash
refactor --json convert-linq-to-foreach \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":21}'
```

**Convert String Concatenation to Interpolation**: `"Total: " + count + " items"`
becomes `$"Total: {count} items"`.

```bash
refactor --json convert-concatenation-to-interpolation \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":20}'
```

**Introduce Using Declaration**: `using (var reader = ...) { ... }` at the end
of its block becomes `using var reader = ...;` followed by the block's
statements.

```bash
refactor --json introduce-using-declaration \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","line":12,"column":9}'
```

<!-- End of Loops and expressions. -->

### Naming and housekeeping

<!-- Naming and housekeeping: examples for this group's tools go below this line. -->

#### Rename

Renames any symbol and every reference to it, including overrides, interface
implementations, named arguments and documentation comments. A top-level type
whose file is named after it has its file renamed too. `line` and `column`
pick the symbol when the name is ambiguous; a rename that would clash with
existing code is refused.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json rename-symbol \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Customer.cs","oldName":"Customer","newName":"Client","line":3,"column":18}'
```

#### Introduce Type Alias and Inline Type Alias

`introduce-type-alias` declares a `using` alias for the type named at a line
and column and uses it wherever the file names that type.
`inline-type-alias` writes the type back in place of every use of an alias,
in every file for a global alias, and removes the directive.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-type-alias \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Inventory.cs","line":7,"column":26,"aliasName":"StockIndex"}'

dotnet run --project RefactorMCP.ConsoleApp -- --json inline-type-alias \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Inventory.cs","aliasName":"StockIndex"}'
```

#### Safe Delete Member, Type and Local

Each deletes a declaration only when nothing depends on it.
`safe-delete-member` takes a method, property, field or event, with `line` to
pick an overload, and refuses overrides and interface implementations.
`safe-delete-type` also deletes a file the type was alone in.
`safe-delete-local` keeps an initializer with side effects as a statement.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-member \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Order.cs","memberName":"Legacy","line":12}'

dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-type \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/LegacyPricing.cs","typeName":"LegacyPricing"}'

dotnet run --project RefactorMCP.ConsoleApp -- --json safe-delete-local \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Sample.cs","line":8,"column":13}'
```

#### Cleanup Usings

Removes the using directives nothing in the file needs, leaving the rest of the
file as written.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json cleanup-usings \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Sample.cs"}'
```

#### Convert to File-Scoped Namespace and Convert to Block Namespace

Switches a file between `namespace Shop { ... }` and `namespace Shop;`,
shifting the code inside by one level of indentation.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-file-scoped-namespace \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Order.cs"}'

dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-block-namespace \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Order.cs"}'
```

<!-- End of Naming and housekeeping. -->

### Composites

<!-- Composites: examples for this group's tools go below this line. -->

Composite refactorings run a sequence of primitive refactorings as one call.
Those built from primitives name the step that refused in their error, and put
every file back as it was.

`extract-class` creates a class, gives the class a field holding an instance
of it, and moves the named fields, properties and methods through that field.
`extract-superclass` creates a base class between a class and its old base
class and pulls the named fields and methods up into it.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-class '{"solutionPath":"./Shop.sln","filePath":"./Shop/Customer.cs","className":"Customer","newClassName":"Address","memberNames":["_street","_town","FormatAddress"],"fieldName":"_address"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-superclass '{"solutionPath":"./Staff.sln","filePath":"./Staff/Manager.cs","className":"Manager","superclassName":"Employee","memberNames":["_name","Badge"]}'
```

`introduce-interface-for-dependency` extracts an interface from the class a
field, property or parameter holds and declares the dependency as the
interface; for a field, the constructor parameters stored in it change too.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-interface-for-dependency '{"solutionPath":"./Shop.sln","filePath":"./Shop/Report.cs","name":"_writer","interfaceName":"IWriter","memberNames":["Write"]}'
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-interface-for-dependency '{"solutionPath":"./Shop.sln","filePath":"./Shop/Report.cs","name":"Print","parameterName":"writer","interfaceName":"IWriter"}'
```

`make-static-then-move` makes an instance method static, taking the instance
as a parameter, and moves it to another class; `move-multiple-methods` moves
several methods, callees first, through a field (`via`) or to a type
(`targetType`). Both keep delegating stubs unless told not to.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json make-static-then-move '{"solutionPath":"./Shop.sln","filePath":"./Shop/Order.cs","methodName":"Describe","targetClass":"Receipts","keepStub":false}'
dotnet run --project RefactorMCP.ConsoleApp -- --json move-multiple-methods '{"solutionPath":"./Shop.sln","filePath":"./Shop/Customer.cs","className":"Customer","methodNames":["Label","Street"],"via":"_address"}'
```

`replace-method-with-method-object` moves a method's body into a new class
whose fields hold the instance, the parameters and the locals; the method
creates one for each call and runs it.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-method-with-method-object '{"solutionPath":"./Shop.sln","filePath":"./Shop/Order.cs","methodName":"Price","className":"PriceCalculation"}'
```

`hide-delegate` gives a class a member forwarding to a member of an object it
holds, and repoints clients: `person.Department.Manager` becomes
`person.Manager`.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json hide-delegate '{"solutionPath":"./Staff.sln","filePath":"./Staff/Person.cs","delegateName":"Department","memberName":"Manager"}'
```

`replace-inheritance-with-delegation` turns a base class into a field,
forwarding the inherited members other code uses;
`replace-delegation-with-inheritance` goes the other way, deriving from the
class of a field the class created and removing the members that only
forwarded to it.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-inheritance-with-delegation '{"solutionPath":"./Shop.sln","filePath":"./Shop/Stack.cs","className":"Stack"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-delegation-with-inheritance '{"solutionPath":"./Staff.sln","filePath":"./Staff/Employee.cs","className":"Employee","fieldName":"_person"}'
```

#### Conditionals and removal

Each of these composites runs its recipe as one tool call, and refuses without
changing anything when any part of it would fail. The conditional ones take
the line and column of the first `if`; the others take the class by file and
name.

##### Convert If to Switch Expression

Turns an `if` / `else if` chain comparing one value, whose branches each
return a value or assign one variable, into a switch expression.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-if-to-switch-expression '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Shipping.cs","line":7,"column":13}'
```

##### Replace Nested Conditional with Guard Clauses

Flattens nested ifs into early returns or continues, and removes an `else`
after a branch that always jumps away, repeatedly.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-nested-conditional-with-guard-clauses '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Shipping.cs","line":16,"column":13}'
```

##### Consolidate Conditional Expression

Joins nested ifs with `&&`, and consecutive ifs or `else if` branches with the
same body with `||`. `methodName` extracts the combined condition into a
method.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json consolidate-conditional-expression '{"solutionPath":"./RefactorMCP.sln","filePath":"./Staff/Disability.cs","line":12,"column":13,"methodName":"IsNotEligible"}'
```

##### Remove Middle Man

Removes the members of a class that only delegate through `via`, and makes
their callers use the delegate directly.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json remove-middle-man '{"solutionPath":"./RefactorMCP.sln","filePath":"./Company/Person.cs","className":"Person","via":"Department"}'
```

##### Inline Class

Moves a class's members into the one class that holds and creates an instance
of it, and deletes it.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json inline-class '{"solutionPath":"./RefactorMCP.sln","filePath":"./Shop/Address.cs","className":"Address"}'
```

##### Collapse Hierarchy

Merges a subclass into its base class, or, with `into`, a base class into its
only subclass, and deletes the class removed.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json collapse-hierarchy '{"solutionPath":"./RefactorMCP.sln","filePath":"./Drawing/Shape.cs","className":"Shape","into":"Circle"}'
```

**Replace Temp with Query**: the local `basePrice`, named at line 18, column
21, becomes a private method `BasePrice()` called wherever the local was read.

```bash
refactor --json replace-temp-with-query \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Order.cs","line":18,"column":21,"queryName":"BasePrice"}'
```

**Decompose Conditional**: the `if` statement whose `if` keyword is at line 24,
column 13 calls `NotSummer(date)` for its condition and `WinterCharge` and
`SummerCharge` for its branches. `elseName` is optional; without it the else
branch stays as it is.

```bash
refactor --json decompose-conditional \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Tariff.cs","line":24,"column":13,"conditionName":"NotSummer","thenName":"WinterCharge","elseName":"SummerCharge"}'
```

**Introduce Parameter Object**: `TotalBetween(DateTime start, DateTime end)`
becomes `TotalBetween(DateRange range)`, with `DateRange` declared as a
`readonly record struct`, or a `sealed record` with `"kind":"class"`, and
every call passing `new DateRange(...)`.

```bash
refactor --json introduce-parameter-object \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Account.cs","methodName":"TotalBetween","parameters":["start","end"],"typeName":"DateRange","parameterName":"range"}'
```

**Preserve Whole Object**: calls such as
`plan.WithinRange(room.Range.Low, room.Range.High)` pass `room.Range`
instead, and the method reads `range.Low` and `range.High`.

```bash
refactor --json preserve-whole-object \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/HeatingPlan.cs","methodName":"WithinRange","parameters":["low","high"],"parameterName":"range"}'
```

**Separate Query from Modifier**: `Withdraw` becomes `Debit`, which changes
the balance, and `Balance`, which returns it; each caller calls both.

```bash
refactor --json separate-query-from-modifier \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Account.cs","methodName":"Withdraw","queryName":"Balance","modifierName":"Debit"}'
```

**Parameterise Method**: `TenPercentRaise()` and `FivePercentRaise()`, which
differ only in a literal, become `Raise(decimal factor)`, and their callers
pass `1.10m` and `1.05m`.

```bash
refactor --json parameterise-method \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Employee.cs","methods":["TenPercentRaise","FivePercentRaise"],"name":"Raise","parameterNames":["factor"]}'
```

**Replace Parameter with Explicit Methods**: the branches of `SetValue` for
`"height"` and `"width"` become `SetHeight` and `SetWidth`, and calls passing
those constants call them directly.

```bash
refactor --json replace-parameter-with-explicit-methods \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Box.cs","methodName":"SetValue","parameterName":"name","methods":[{"value":"\"height\"","name":"SetHeight"},{"value":"\"width\"","name":"SetWidth"}]}'
```

**Constructor Injection**: the `mailer` a method constructs at line 17 becomes
a constructor parameter kept in `_mailer`, and every construction of the class
passes a new one. `parameterName` and `fieldName` are optional.

```bash
refactor --json inject-constructor-dependency \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/OrderService.cs","line":17,"column":17}'
```

**Convert to Async**: `Available`, which reads a task's `Result`, awaits it and
returns `Task<int>`; its callers await it and become async in turn, and a
caller that cannot be async, such as a constructor, blocks on the task.

```bash
refactor --json convert-to-async \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Inventory.cs","methodName":"Available"}'
```

Change Signature's `replacements` let the composites above remove a parameter
the body still reads, replacing each read with an expression:

```bash
refactor --json change-signature \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Grid.cs","methodName":"DistanceFromOrigin","parameters":[{"name":"point","type":"Point","value":"new Point(3, 4)"}],"replacements":{"x":"point.X","y":"point.Y"}}'
```

<!-- End of Composites. -->

### Generators

<!-- Generators: examples for this group's tools go below this line. -->

Generators add structure or change behaviour, so each pins one design; the
catalog README for each refactoring under `Catalog/generators/` describes it.

`add-null-checks` guards a method's or constructor's reference-type
parameters with `ArgumentNullException.ThrowIfNull`, skipping those already
guarded, annotated nullable or defaulting to null. A constructor is named by
its type; `line` picks an overload.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json add-null-checks '{"solutionPath":"./Shop.sln","filePath":"./Shop/Printer.cs","methodName":"Print"}'
```

`convert-to-nullable-aware` adds `#nullable enable` to one file and annotates
the declarations its nullable warnings point to, refusing when a warning such
as a possible null dereference remains.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json convert-to-nullable-aware '{"solutionPath":"./Shop.sln","filePath":"./Shop/Directory.cs"}'
```

`add-observer` declares `public event Action<...> <eventName>` before a void
method and raises it with the method's parameters at the end and before each
return.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json add-observer '{"solutionPath":"./Shop.sln","filePath":"./Shop/Counter.cs","className":"Counter","methodName":"Update","eventName":"Updated"}'
```

`feature-flag-refactor` moves the branches of the one `if (x.IsEnabled("Flag"))`
in a file into `FlagStrategy` and `NoFlagStrategy` classes, selected by a
private `Flag` property that checks the flag on each call.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json feature-flag-refactor '{"solutionPath":"./Shop.sln","filePath":"./Shop/Checkout.cs","flagName":"NewCheckout"}'
```

`introduce-null-object` generates `Null<Interface>`, a sealed class with a
static `Instance`, for the interface a field is typed as. Null assignments to
the field use it, and the field's null checks are removed, with the null
object returning the fallback values those checks used.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json introduce-null-object '{"solutionPath":"./Shop.sln","filePath":"./Shop/Order.cs","fieldName":"_logger"}'
```

`replace-error-code-with-exception` makes a method returning an `int` code
(0 for success) or a `bool` (true for success) return `void` and throw
`exceptionType` (default `InvalidOperationException`) on failure; callers that
tested the result in an `if` get a `try`/`catch` instead.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-error-code-with-exception '{"solutionPath":"./Shop.sln","filePath":"./Shop/Account.cs","methodName":"Withdraw","exceptionType":"InvalidOperationException"}'
```

`replace-array-with-object` replaces an array field or local, named by the
position of its name, with a new class that has one property per index, and
rewrites its creations and constant-index accesses.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-array-with-object '{"solutionPath":"./Shop.sln","filePath":"./Shop/Report.cs","line":7,"column":22,"className":"Performance","memberNames":["Club","Wins"]}'
```

`replace-type-code-with-enum` replaces int or string constants used as a type
code with a public enum in `<enumName>.cs` beside the type, retargets every
reference, and retypes the fields, properties, parameters, locals and return
types the codes flow into. `replace-type-code-with-subclasses` turns an enum
field into a sealed subclass per enum member: the class becomes abstract, the
field an abstract property, and a static `Create` factory maps a code to its
subclass. `replace-conditional-with-polymorphism` moves each case of a switch
or if chain on a type-code property, or on a parameter's type, into an
override in the matching subclass.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-type-code-with-enum '{"solutionPath":"./Staff.sln","filePath":"./Staff/Employee.cs","typeName":"Employee","constantNames":["Engineer","Salesman","Manager"],"enumName":"EmployeeType"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-type-code-with-subclasses '{"solutionPath":"./Staff.sln","filePath":"./Staff/Employee.cs","fieldName":"_type"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-conditional-with-polymorphism '{"solutionPath":"./Shapes.sln","filePath":"./Shapes/Geometry.cs","methodName":"Area"}'
```

`extract-decorator` generates a class that implements an interface, wraps an
instance of it and forwards every member, in a new file beside the target. The
target is the interface, or a class implementing exactly one; `decoratorName`
defaults to the interface name without its `I`, plus `Decorator`.
`create-adapter` implements an interface over an existing class, forwarding
each member to the class member named in `memberMap`, or to one with the same
name. Members with no counterpart throw `NotImplementedException`.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-decorator '{"solutionPath":"./Shop.sln","filePath":"./Shop/Greeter.cs","typeName":"Greeter"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json create-adapter '{"solutionPath":"./Shop.sln","filePath":"./Shop/LegacyLogger.cs","className":"LegacyLogger","interfaceName":"ILogger","adapterName":"LegacyLoggerAdapter","memberMap":"Log:Write"}'
```

<!-- End of Generators. -->

### Recipe primitives

<!-- Recipe primitives: examples for this group's tools go below this line. -->

#### Add Delegating Member

Adds to a class a member forwarding to the member of the same name of one of
its fields, or of its base class when `via` is `base`. `memberName` is the
member's name, `this` for an indexer, or its documentation comment id when
overloads share the name. A member forwarding to the base class is declared
`new`, and the class's own uses of the member it hides are written with
`base`.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json add-delegating-member \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Order.cs","className":"Order","memberName":"Greeting","via":"_customer"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json add-delegating-member \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Stack.cs","className":"Stack","memberName":"Count","via":"base"}'
```

#### Remove Delegating Member

Removes a member that only forwards, through `base`, to the inherited member
it hides or overrides, so callers reach the inherited member. `line` chooses
between overloads; an indexer is named `this`.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json remove-delegating-member \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Employee.cs","className":"Employee","memberName":"LastName"}'
```

#### Replace Base Uses with Field and Replace Field Uses with Base

For a class holding a new instance of its base class in a private field,
`replace-base-uses-with-field` makes the class reach its inherited members
through the field, so its base class part is unused and the base class can be
removed. `replace-field-uses-with-base` goes the other way: uses of the
field's members reach the inherited members, and the field is removed.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-base-uses-with-field \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Stack.cs","className":"Stack","fieldName":"_list"}'
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-field-uses-with-base \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Employee.cs","className":"Employee","fieldName":"_person"}'
```

<!-- End of Recipe primitives. -->

### Recipe gaps

<!-- Recipe gaps: examples for this group's tools go below this line. -->

Primitives that give composite recipes a step of their own. Like the other
tools that act on a statement, they take the line and column of the `if`
keyword.

#### Remove Redundant Else

Removes the `else` of an `if` whose branch always returns, throws, breaks or
continues, so the `else`'s statements follow the `if`; an `else if` becomes an
`if` of its own.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json remove-redundant-else '{"solutionPath":"./RefactorMCP.sln","filePath":"./Staff/Payroll.cs","line":11,"column":13}'
```

#### Merge Sibling Ifs

Joins an `if` with the `else if` or `if` statement after it when both have the
same body, into one `if` on both conditions joined by `||`. A following `if`
statement must share a body that always jumps away.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json merge-sibling-ifs '{"solutionPath":"./RefactorMCP.sln","filePath":"./Staff/Disability.cs","line":12,"column":13}'
```

<!-- End of Recipe gaps. -->

### Method recipe primitives

<!-- Method recipe primitives: examples for this group's tools go below this line. -->

These primitives are steps in the recipes of Replace Parameter with Explicit
Methods, Constructor Injection and Convert to Async; the catalog README for
each under `Catalog/primitives/` states its preconditions.

`redirect-calls-with-constant-argument` makes calls of `SetValue` that pass
`"height"` for `name` call `SetHeight`, which `SetValue` runs for that value,
passing the arguments `SetHeight` takes.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json redirect-calls-with-constant-argument '{"solutionPath":"./Shapes.sln","filePath":"./Shapes/Box.cs","methodName":"SetValue","parameterName":"name","value":"\"height\"","targetMethodName":"SetHeight"}'
```

`initialize-field-from-constructor-parameter` assigns `_mailer`, which
nothing uses yet, from the constructor's `mailer` parameter. A constructor is
named by its class; `line` picks one.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json initialize-field-from-constructor-parameter '{"solutionPath":"./Shop.sln","filePath":"./Shop/OrderService.cs","className":"OrderService","fieldName":"_mailer","parameterName":"mailer"}'
```

`replace-expression-with-field` replaces an expression with a readonly field
when every constructor assigns the field from a parameter and every
construction passes an equivalent expression for it. Give the expression by
`selectionRange`, or give `memberName` and `expression` to replace every
occurrence in a member.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json replace-expression-with-field '{"solutionPath":"./Shop.sln","filePath":"./Shop/OrderService.cs","fieldName":"_mailer","memberName":"Place","expression":"new Mailer(\"smtp.example.com\")"}'
```

`make-method-async` makes `Available`, which reads a task's `Result`, await
it and return `Task<int>`. Calls in async methods await it; every other
caller blocks with `.GetAwaiter().GetResult()`.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json make-method-async '{"solutionPath":"./Stock.sln","filePath":"./Stock/Inventory.cs","methodName":"Available"}'
```

<!-- End of Method recipe primitives. -->

## Metrics Resource

Metrics can be queried using the resource scheme:

```
metrics://RefactorMCP.Tests/ExampleCode.cs/Calculator.Calculate
```
This URI returns metrics for the `Calculate` method. Omitting the method name
returns metrics for the whole class, and specifying only the file gives all
classes and methods.

Metrics are cached under your home directory, in `~/.refactor-mcp/<solution>-<hash>/metrics/`, so nothing is written into the repository you are working on. The hash comes from the solution's full path and keeps two checkouts of the same solution apart. Set `REFACTOR_MCP_HOME` to use a different root than `~/.refactor-mcp`. The path below the metrics folder mirrors the solution's folder structure. For example metrics for `RefactorMCP.Tests/ExampleCode.cs` in `RefactorMCP.sln` are written to:

```text
~/.refactor-mcp/RefactorMCP-<hash>/metrics/RefactorMCP.Tests/ExampleCode.json
```

## Summary Resource

Retrieve a file with method bodies omitted using the `summary://` scheme:

```
summary://RefactorMCP.Tests/ExampleCode.cs
```
The returned text begins with `// summary://...`, replaces each block body with
`{}`, and leaves expression-bodied members as they are — an arrow clause has to
hold an expression, so there is no placeholder that parses in its place.

## Tool Call Log

Recording the calls made in a session is opt in, because a plain command line
call should leave nothing behind. Set `REFACTOR_MCP_LOG=1` to append to
`~/.refactor-mcp/<solution>-<hash>/tool-call-log-<timestamp>-<pid>.jsonl`, or
set `REFACTOR_MCP_LOG` to a file path to choose the file yourself:

```bash
REFACTOR_MCP_LOG=1 dotnet run --project RefactorMCP.ConsoleApp -- --json cleanup-usings '{"solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/ExampleCode.cs"}'
```

With `REFACTOR_MCP_LOG` unset nothing is written.
