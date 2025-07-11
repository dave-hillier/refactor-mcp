# RefactorMCP Examples

This document provides comprehensive examples for all refactoring tools available in RefactorMCP. Each example shows the before/after code and the JSON command needed to perform the refactoring.

Using the MCP tools is the preferred method for refactoring large C# files where manual edits become cumbersome.

## Getting Started

### Loading a Solution
Before performing any refactoring, you need to load a solution. This also clears any cached data so each load starts a fresh session:

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json load-solution '{"solutionPath":"./RefactorMCP.sln"}'
```

### JSON Example
```json
{"tool":"load-solution","solutionPath":"./RefactorMCP.sln"}
```

### JSON Mode Usage
All examples use JSON parameters:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json ToolName '{"param":"value"}'
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
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-method \
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

## 6. Convert to Static with Parameters

**Purpose**: Convert an instance method to static by turning field and property usages into parameters.

### Example
**Before** (in `ExampleCode.cs` line 46):
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
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  GetFormattedNumber
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
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-static-with-instance \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  GetFormattedNumber \
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

## 9. Move Static Method

**Purpose**: Move a static method to another class.

### Example
**Before** (in `ExampleCode.cs` line 63):
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
  FormatCurrency \
  MathUtilities
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
The original method remains in `ExampleCode.cs` as a wrapper that forwards to `MathUtilities.FormatCurrency`.
Running `move-static-method` again on this wrapper will now fail. Use `inline-method` if you want to remove it.

## 10. Move Instance Method

**Purpose**: Move an instance method to another class while leaving a wrapper behind. Protected override methods cannot be moved and will result in an error.

### Example
**Before** (in `ExampleCode.cs` line 69):
```csharp
public void LogOperation(string operation)
{
    Console.WriteLine($"[{DateTime.Now}] {operation}");
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-instance-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  Calculator \
  LogOperation \
  --constructor-injections this \
  Logger \
  
```

**After**:
```csharp
public class Calculator
{
    private readonly Logger _logger = new Logger();

    public void LogOperation(string operation)
    {
        _logger.LogOperation(operation);
    }
}

public class Logger
{
    public void Log(string message)
    {
        Console.WriteLine($"[LOG] {message}");
    }

    public static void LogOperation(string operation)
    {
        Console.WriteLine($"[{DateTime.Now}] {operation}");
    }
}
```
The original method in `Calculator` now delegates to the static `Logger.LogOperation` method, preserving existing call sites.
If you run `move-instance-method` again on this wrapper, an error will be reported. Use `inline-method` to remove the wrapper if desired.
When a moved method references private fields from its original class, those values are passed as additional parameters.

## 10. Make Static Then Move

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

## 10. Move Multiple Methods

**Purpose**: Move several methods at once, ordered by dependencies.

### Example
**Before**:
```csharp
class Helper
{
    public void A() { B(); }
    public void B() { Console.WriteLine("B"); }
}

class Target { }
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-multiple-methods-instance \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  Helper \
  "A,B" \
  Target \
  "./Target.cs"
```

### Cross-file Example
Move methods to a separate file using the `targetFile` property or by passing a default path:

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-multiple-methods-instance \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  Helper \
  A \
  Target \
  "./Target.cs"
```

### Static Parameter Injection
Move the same methods but convert them to static members with an explicit `this` parameter:

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli move-multiple-methods-static \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  Helper \
  "A,B" \
  Target
```

**After**:
```csharp
class Helper
{
    private readonly Target _target = new Target();

    public void A()
    {
        _target.A();
    }

    public void B()
    {
        _target.B();
    }
}

class Target
{
    public void B()
    {
        Console.WriteLine("B");
    }

    public void A()
    {
        B();
    }
}
```
Each moved method in `Helper` now delegates to the corresponding method on `Target`, preserving the original public interface.
Because an access field didn't exist, the refactoring introduced a private readonly field named `_target` automatically.

## 11. Batch Move Methods

**Purpose**: Move several methods at once using a JSON description. This supersedes the older move commands.

### Example
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli batch-move-methods \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "[{\"SourceClass\":\"Helper\",\"Method\":\"A\",\"TargetClass\":\"Target\",\"AccessMember\":\"t\"}]"
```

## 12. Move Type to Separate File

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

## 12. Inline Method

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
## 11. Safe Delete Parameter

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
dotnet run --project RefactorMCP.ConsoleApp -- --cli safe-delete-parameter \
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

## 12. Transform Setter to Init

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

## 13. Safe Delete Field

**Purpose**: Remove an unused field from a class.

### Example
**Before** (in `ExampleCode.cs` line 88):
```csharp
private int deprecatedCounter = 0; // Not used anywhere
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli safe-delete-field \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  deprecatedCounter
```

**After**:
```csharp
// Field 'deprecatedCounter' removed from Calculator class
```

## 12. Cleanup Usings

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

## 6. Load Solution (Utility Command)

**Purpose**: Clear previous caches, reset move history, and load a solution file before performing refactorings.

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

## 9. Unload Solution (Utility Command)

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

## 10. Clear Solution Cache (Utility Command)

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

## Reset Move History (Utility Command)

**Purpose**: Allow previously moved methods to be moved again in the same session. Loading a solution automatically clears this history.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli reset-move-history
```

**Expected Output**:
```
Cleared move history
```

### Failed Move Example
A failed move does not record the method:
```json
{"tool":"move-instance-method","solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/ExampleCode.cs","sourceClass":"Wrong","methodNames":["LogOperation"],"targetClass":"Logger"}
```
Running the command again with the correct `sourceClass` succeeds.

## 11. List Tools (Utility Command)

**Purpose**: Display all available refactoring tools and their status.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json ListTools '{}'
```

**Output**:
```
Available refactoring tools:
load-solution - Start a new session and load a solution file
unload-solution - Remove a loaded solution from cache
clear-solution-cache - Clear all cached solutions
extract-method - Extract selected code into a new method
introduce-field - Create a new field from selected code
introduce-variable - Create a new variable from selected code
make-field-readonly - Make a field readonly and move initialization to constructors
introduce-parameter - Create a new parameter from selected code
convert-to-static-with-parameters - Transform instance method to static
convert-to-static-with-instance - Transform instance method to static with instance parameter
move-static-method - Move a static method to another class
move-instance-method - Move an instance method to another class
move-multiple-methods-instance - Move several methods and keep them as instance methods
move-multiple-methods-static - Move several methods and convert them to static with a `this` parameter
transform-setter-to-init - Convert property setter to init-only setter
safe-delete - Safely delete a field, parameter, or variable

```

## 12. Version Info (Utility Command)

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

## 13. Analyze Refactoring Opportunities

**Purpose**: Prompt the server to inspect a file for smells such as long methods, long parameter lists, large classes, or unused members.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli analyze-refactoring-opportunities "./RefactorMCP.Tests/ExampleCode.cs" "./RefactorMCP.sln"
```

**Expected Output**:
```
Suggestions:
- Method 'UnusedHelper' appears unused -> safe-delete-method
- Field 'deprecatedCounter' appears unused -> safe-delete-field
```

## 14. List Class Lengths

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

## 15. Extract Interface

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


## 16. Rename Symbol

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

## 17. Feature Flag Refactor

**Purpose**: Replace `features.IsEnabled(flag)` checks with strategy classes.

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
    _coolFeatureStrategy.Apply();
}
```
## 18. Extract Decorator

**Purpose**: Generate a decorator class that delegates to an existing method.

### Example
**Before**:
```csharp
public class Greeter
{
    public void Greet(string name)
    {
        Console.WriteLine($"Hello {name}");
    }
}
```
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli extract-decorator \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/Decorator.cs" \
  Greeter \
  Greet
```
**After**:
```csharp
public class GreeterDecorator
{
    private readonly Greeter _inner;
    public GreeterDecorator(Greeter inner) { _inner = inner; }
    public void Greet(string name) { _inner.Greet(name); }
}
```

## 19. Create Adapter

**Purpose**: Create an adapter class wrapping an existing method.

### Example
**Before**:
```csharp
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
dotnet run --project RefactorMCP.ConsoleApp -- --cli create-adapter \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/Adapter.cs" \
  LegacyLogger \
  Write \
  LoggerAdapter
```
**After**:
```csharp
public class LoggerAdapter
{
    private readonly LegacyLogger _inner;
    public LoggerAdapter(LegacyLogger inner) { _inner = inner; }
    public void Adapt(string message) { _inner.Write(message); }
}
```

## 20. Add Observer

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

## 21. Constructor Injection

**Purpose**: Convert one or more method parameters to constructor-injected fields.

### Example
**Before**:
```csharp

class C
{
    int Add(int a)
    {
        return a + 1;
    }

    int Multiply(int b)
    {
        return b * 2;
    }

    void Call()
    {
        Add(1);
        Multiply(2);
    }
}
```

**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli constructor-injection \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "Add:a;Multiply:b"
```

**After**:
```csharp
class C
{
    private readonly int _a;
    private readonly int _b;

    public C(int a, int b)
    {
        _a = a;
        _b = b;
    }

    int Add()
    {
        return _a + 1;
    }

    int Multiply()
    {
        return _b * 2;
    }

    void Call()
    {
        Add();
        Multiply();
    }
}
```

## 22. Use Interface

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
dotnet run --project RefactorMCP.ConsoleApp -- --cli make-field-readonly "./RefactorMCP.sln" "./MyFile.cs" 25

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

### File-Scoped Namespace Example
When a tool needs to create a new file, the namespace uses the file-scoped style:

```json
{"tool":"move-static-method","solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/ExampleCode.cs","methodName":"Add","targetClass":"MathHelpers","targetFilePath":"./RefactorMCP.Tests/MathHelpers.cs"}
```

### Overloaded Methods Example
`move-multiple-methods-static` now works when the source class contains overloaded methods:

```json
{"tool":"move-multiple-methods-static","solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/ExampleCode.cs","sourceClass":"Helper","methodNames":["A","A"],"targetClass":"Target","targetFilePath":"./Target.cs"}
```

### JSON Example
Provide `methodNames` as a list (this property is required):

```json
{"tool":"move-instance-method","solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/ExampleCode.cs","sourceClass":"Calculator","methodNames":["LogOperation"],"targetClass":"Logger"}
```

### Interface/Base Member Example
Inherited members are automatically qualified when moved:

```json
{"tool":"move-instance-method","solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/ExampleCode.cs","sourceClass":"Derived","methodNames":["PrintName"],"targetClass":"Target"}
```

### Automatic Static Conversion
When a moved instance method has no dependencies on instance members, it is made static automatically.

## Metrics Resource

Metrics can be queried using the resource scheme:

```
metrics://RefactorMCP.Tests/ExampleCode.cs/Calculator.Calculate
```
This URI returns metrics for the `Calculate` method. Omitting the method name
returns metrics for the whole class, and specifying only the file gives all
classes and methods.

Metrics are cached in `.refactor-mcp/metrics/` once a solution is loaded. The path mirrors the solution's folder structure. For example after running `load-solution` on `RefactorMCP.sln` metrics for `RefactorMCP.Tests/ExampleCode.cs` are written to:

```text
.refactor-mcp/metrics/RefactorMCP.Tests/ExampleCode.cs.json
```

## Summary Resource

Retrieve a file with method bodies omitted using the `summary://` scheme:

```
summary://RefactorMCP.Tests/ExampleCode.cs
```
The returned text begins with `// summary://...` and shows each method body as `// ...`.

## Playback Log

After each tool invocation in JSON mode (after running `load-solution`), the parameters are appended to a session log such as `.refactor-mcp/tool-call-log-YYYYMMDDHHMMSS.jsonl`. Replay them with:

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli play-log ./.refactor-mcp/tool-call-log-YYYYMMDDHHMMSS.jsonl
```

### JSON Logging Example
Invoking tools in JSON mode is also recorded once `load-solution` has been run:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json cleanup-usings '{"solutionPath":"./RefactorMCP.sln","documentPath":"./RefactorMCP.Tests/ExampleCode.cs"}'
```
