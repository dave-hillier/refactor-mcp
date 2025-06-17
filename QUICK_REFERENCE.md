# RefactorMCP Quick Reference

<<<<<<< codex/update-documentation-and-add-unit-test-for-summary-resource
## Resources

- `metrics://<file path>/[ClassName].[MethodName]` - retrieve metrics for a scope.
- `summary://<file path>` - get the file with method bodies replaced by `// ...`.

### Example

Request a summary of `ExampleCode.cs`:

```json
{"role":"tool","name":"summary://RefactorMCP.Tests/ExampleCode.cs"}
=======
Using these tools through the MCP interface is the preferred approach for refactoring **C# code**.

## Basic Commands
```bash
# List all tools
dotnet run --project RefactorMCP.ConsoleApp -- --cli list-tools
# Load solution
dotnet run --project RefactorMCP.ConsoleApp -- --cli load-solution ./RefactorMCP.sln
# Unload solution
dotnet run --project RefactorMCP.ConsoleApp -- --cli unload-solution ./RefactorMCP.sln
# Clear all cached solutions
dotnet run --project RefactorMCP.ConsoleApp -- --cli clear-solution-cache
# Reset moved method tracking
dotnet run --project RefactorMCP.ConsoleApp -- --cli reset-move-history
```

## Reset Move History
Clears the internal record of moved methods so a method can be moved again without errors.

```bash
dotnet run --project RefactorMCP.ConsoleApp -- --cli reset-move-history
>>>>>>> main
```
