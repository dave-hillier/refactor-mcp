# RefactorMCP

RefactorMCP is a Model Context Protocol server that exposes Roslyn-based refactoring tools for C#.

## Usage

Run the console application directly or host it as an MCP server:

```bash
dotnet run --project RefactorMCP.ConsoleApp
```

For usage examples see [EXAMPLES.md](./EXAMPLES.md).

## Available Refactorings

- **Extract Method** – create a new method from selected code and replace the original with a call.
- **Introduce Field/Parameter/Variable** – turn expressions into new members; fails if a field already exists.
- **Convert to Static** – make instance methods static using parameters or an instance argument.
- **Move Static Method** – relocate a static method and keep a wrapper in the original class.
- **Move Instance Method** – move an instance method to another class and delegate from the source. If the moved method no longer accesses instance members, it is made static automatically.
- **Make Static Then Move** – convert an instance method to static and relocate it to another class in one step.
- **Make Field Readonly** – move initialization into constructors and mark the field readonly.
- **Transform Setter to Init** – convert property setters to init-only and initialize in constructors.
- **Safe Delete** – remove fields or variables only after dependency checks.
- **Extract Class** – create a new class from selected members and compose it with the original.
- **Inline Method** – replace calls with the method body and delete the original.

Metrics and summaries are also available via the `metrics://` and `summary://` resource schemes.

## Contributing

* Run `dotnet test` to ensure all tests pass.
* Format the code with `dotnet format` before opening a pull request.

## License

Licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).
