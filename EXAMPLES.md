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
A solution is loaded on demand by the first tool that needs it. Loading it
explicitly starts a fresh session, which clears cached data and the record of
moved methods:

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
When the target class lives in another file, pass `--target-file`; without it the method is added to the file it came from.
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

**Purpose**: Move several methods at once. Use `move-multiple-methods-static` to convert
them to static with a `this` parameter, or `move-multiple-methods-instance` to keep them
as instance methods with the source instance injected through the constructor.

### Example
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json move-multiple-methods-static \
  '{"solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/ExampleCode.cs","sourceClass":"Helper","methodNames":["A","B"],"targetClass":"Target"}'
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

## 14. Safe Delete Method

**Purpose**: Remove an unused method and update call sites.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli safe-delete-method \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  FormatUserLegacy
```

## 15. Safe Delete Variable

**Purpose**: Remove a local variable using a line range.

### Example
**Command**:
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli safe-delete-variable \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  "12:9-12:31"
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

**Output**: one kebab-case tool name per line, e.g. `add-observer`, `extract-method`,
`load-solution`. `refactor list-tools` prints the same names with their descriptions.

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
dotnet run --project RefactorMCP.ConsoleApp -- --cli analyze-refactoring-opportunities "./RefactorMCP.sln" "./RefactorMCP.Tests/ExampleCode.cs"
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
dotnet run --project RefactorMCP.ConsoleApp -- --cli convert-to-constructor-injection \
  "./RefactorMCP.sln" \
  "./RefactorMCP.Tests/ExampleCode.cs" \
  '[{"methodName":"Add","parameterName":"a"},{"methodName":"Multiply","parameterName":"b"}]'
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

<!-- End of Conditionals. -->

### Loops and expressions

<!-- Loops and expressions: examples for this group's tools go below this line. -->

<!-- End of Loops and expressions. -->

### Naming and housekeeping

<!-- Naming and housekeeping: examples for this group's tools go below this line. -->

<!-- End of Naming and housekeeping. -->

### Composites

<!-- Composites: examples for this group's tools go below this line. -->

<!-- End of Composites. -->

### Generators

<!-- Generators: examples for this group's tools go below this line. -->

<!-- End of Generators. -->

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
The returned text begins with `// summary://...`, replaces each block body with
`{}`, and leaves expression-bodied members as they are — an arrow clause has to
hold an expression, so there is no placeholder that parses in its place.

## Tool Call Log

Recording the calls made in a session is opt in, because a plain command line
call should leave nothing behind. Set `REFACTOR_MCP_LOG=1` to append to
`<solution directory>/.refactor-mcp/tool-call-log-<timestamp>-<pid>.jsonl`, or
set `REFACTOR_MCP_LOG` to a file path to choose the file yourself:

```bash
REFACTOR_MCP_LOG=1 dotnet run --project RefactorMCP.ConsoleApp -- --json cleanup-usings '{"solutionPath":"./RefactorMCP.sln","filePath":"./RefactorMCP.Tests/ExampleCode.cs"}'
```

With `REFACTOR_MCP_LOG` unset nothing is written.
