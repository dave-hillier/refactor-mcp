# RefactorMCP Examples

This directory contains comprehensive, realistic examples demonstrating each refactoring tool available in RefactorMCP. Each example shows:

- **Before**: The original code with the issue
- **After**: The refactored code
- **Tool Usage**: The exact command to perform the refactoring
- **Benefits**: Why this refactoring improves the code

## Quick Reference

| Tool | Category | Description |
|------|----------|-------------|
| `extract-method` | Method Transformation | Extract code block into a new method |
| `inline-method` | Method Transformation | Replace method calls with the method body |
| `introduce-variable` | Introduction | Extract expression into a named variable |
| `introduce-field` | Introduction | Extract expression into a class field |
| `introduce-parameter` | Introduction | Convert hardcoded value to method parameter |
| `move-instance-method` | Method Moving | Move instance method to another class |
| `move-static-method` | Method Moving | Move static method to another class |
| `move-type-to-file` | Method Moving | Move a type to its own file |
| `convert-to-static-with-instance` | Conversion | Make method static with instance parameter |
| `convert-to-static-with-parameters` | Conversion | Make method static with member parameters |
| `convert-to-extension-method` | Conversion | Convert to extension method |
| `constructor-injection` | Conversion | Convert parameters to injected fields |
| `use-interface` | Conversion | Change parameter type to interface |
| `safe-delete-field` | Safe Deletion | Remove field if no references |
| `safe-delete-method` | Safe Deletion | Remove method if no callers |
| `safe-delete-parameter` | Safe Deletion | Remove parameter if unused |
| `safe-delete-variable` | Safe Deletion | Remove variable if unused |
| `extract-interface` | Design Patterns | Create interface from class members |
| `extract-decorator` | Design Patterns | Create decorator wrapper class |
| `create-adapter` | Design Patterns | Create adapter for incompatible interface |
| `analyze-refactoring-opportunities` | Analysis | Find code smells and suggestions |
| `class-length-metrics` | Analysis | Measure class sizes |
| `cleanup-usings` | Analysis | Remove unused using directives |
| `rename-symbol` | Utility | Rename symbol across solution |
| `feature-flag-refactor` | Utility | Convert feature flags to strategy pattern |

## Example Categories

### [Method Transformation](./MethodTransformation/)
- **[Extract Method](./MethodTransformation/ExtractMethod.md)**: Breaking long methods into smaller, focused units
- **[Inline Method](./MethodTransformation/InlineMethod.md)**: Removing unnecessary method indirection

### [Introduction](./Introduction/)
- **[Introduce Variable](./Introduction/IntroduceVariable.md)**: Naming complex expressions for clarity
- **[Introduce Field](./Introduction/IntroduceField.md)**: Extracting repeated values to class fields
- **[Introduce Parameter](./Introduction/IntroduceParameter.md)**: Making methods more flexible

### [Method Moving](./MethodMoving/)
- **[Move Instance Method](./MethodMoving/MoveInstanceMethod.md)**: Relocating methods to better homes
- **[Move Static Method](./MethodMoving/MoveStaticMethod.md)**: Organizing static utilities
- **[Move Type to File](./MethodMoving/MoveTypeToFile.md)**: One type per file convention

### [Conversion](./Conversion/)
- **[Convert to Static](./Conversion/ConvertToStatic.md)**: Creating pure functions from instance methods
- **[Convert to Extension Method](./Conversion/ConvertToExtensionMethod.md)**: Adding fluent APIs
- **[Constructor Injection](./Conversion/ConstructorInjection.md)**: Implementing dependency injection
- **[Use Interface](./Conversion/UseInterface.md)**: Programming to abstractions

### [Safe Deletion](./SafeDeletion/)
- **[Safe Delete Examples](./SafeDeletion/SafeDeleteExamples.md)**: Removing dead code safely

### [Design Patterns](./DesignPatterns/)
- **[Extract Interface](./DesignPatterns/ExtractInterface.md)**: Defining contracts for testability
- **[Extract Decorator](./DesignPatterns/Decorator.md)**: Adding cross-cutting concerns
- **[Create Adapter](./DesignPatterns/Adapter.md)**: Integrating incompatible interfaces

### [Analysis](./Analysis/)
- **[Analysis Tools](./Analysis/AnalysisTools.md)**: Finding issues and cleaning up code

## Running the Examples

### Prerequisites
1. .NET 9.0 SDK installed
2. RefactorMCP built: `dotnet build`

### Using the Tools

#### Solution Mode (Recommended)
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json <tool-name> '{
    "solutionPath": "path/to/your.sln",
    "filePath": "path/to/file.cs",
    ...additional parameters...
}'
```

#### Single File Mode
```bash
dotnet run --project RefactorMCP.ConsoleApp -- --json <tool-name> '{
    "filePath": "path/to/file.cs",
    ...additional parameters...
}'
```

### Common Parameters

| Parameter | Description |
|-----------|-------------|
| `solutionPath` | Path to .sln file for solution-wide refactoring |
| `filePath` | Path to the C# file to refactor |
| `methodName` | Name of the method to refactor |
| `startLine` / `endLine` | Line range for selection-based refactorings |
| `targetClassName` | Target class name for move operations |
| `targetFilePath` | Target file path for move operations |

## Best Practices

### When to Refactor

1. **Before adding features**: Clean up the area you're about to modify
2. **After tests pass**: Refactor with confidence when tests are green
3. **Small steps**: Make one refactoring at a time, test, commit
4. **Code review feedback**: Address structural issues found in review

### Refactoring Workflow

```
1. Ensure tests pass
2. Identify the smell or improvement opportunity
3. Choose the appropriate refactoring
4. Apply the refactoring
5. Run tests
6. Review the changes
7. Commit with clear message
```

### Safety Tips

- **Always have tests**: Refactoring without tests is risky
- **Use version control**: Commit before refactoring
- **Review the diff**: Verify the changes are what you expected
- **Run the build**: Ensure compilation succeeds after refactoring

## Example Scenarios

### Scenario 1: Cleaning Up a God Class

```bash
# 1. Analyze the class
dotnet run -- --json class-length-metrics '{"solutionPath": "MyApp.sln"}'

# 2. Find opportunities
dotnet run -- --json analyze-refactoring-opportunities '{"filePath": "GodClass.cs"}'

# 3. Extract interface for testing
dotnet run -- --json extract-interface '{...}'

# 4. Move related methods to new class
dotnet run -- --json move-instance-method '{...}'

# 5. Clean up unused code
dotnet run -- --json safe-delete-method '{...}'
```

### Scenario 2: Preparing for Dependency Injection

```bash
# 1. Convert concrete types to interfaces
dotnet run -- --json use-interface '{...}'

# 2. Inject dependencies through constructor
dotnet run -- --json constructor-injection '{...}'

# 3. Extract interface if needed
dotnet run -- --json extract-interface '{...}'
```

### Scenario 3: Breaking Up a Long Method

```bash
# 1. Analyze the method
dotnet run -- --json analyze-refactoring-opportunities '{"filePath": "LongMethod.cs"}'

# 2. Introduce variables for clarity
dotnet run -- --json introduce-variable '{...}'

# 3. Extract logical sections
dotnet run -- --json extract-method '{...}'

# 4. Clean up
dotnet run -- --json cleanup-usings '{...}'
```

## Contributing

Found an issue with an example? Want to add a new one?

1. Fork the repository
2. Create a feature branch
3. Add or update examples in the `Examples/` directory
4. Submit a pull request

## Additional Resources

- [Martin Fowler's Refactoring Catalog](https://refactoring.com/catalog/)
- [Roslyn Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Code Smells](https://refactoring.guru/refactoring/smells)
