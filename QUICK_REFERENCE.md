# RefactorMCP Quick Reference

## Resources

- `metrics://<file path>/[ClassName].[MethodName]` - retrieve metrics for a scope.
- `summary://<file path>` - get the file with method bodies replaced by `// ...`.

### Example

Request a summary of `ExampleCode.cs`:

```json
{"role":"tool","name":"summary://RefactorMCP.Tests/ExampleCode.cs"}
```
