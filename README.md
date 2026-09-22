# RefactorMCP

RefactorMCP exposes Roslyn-based refactoring tools for C# over three entry
points: a command line interface, a resident daemon that keeps a solution
loaded, and a Model Context Protocol server.

All three dispatch through the same tool dispatcher and session, so a tool
behaves the same however it is called.

## Usage

```bash
# Run a refactoring. Tool commands go to the daemon for that solution, which is
# started on demand, so only the first call pays the MSBuild load.
dotnet run --project RefactorMCP.ConsoleApp -- extract-method \
    --solution ./RefactorMCP.sln --file ./src/Foo.cs \
    --selection-range 10:5-20:6 --method-name ComputeTotal

# The same call with JSON parameters, for scripting.
dotnet run --project RefactorMCP.ConsoleApp -- --json extract-method \
    '{"solutionPath":"./RefactorMCP.sln","filePath":"./src/Foo.cs","selectionRange":"10:5-20:6","methodName":"ComputeTotal"}'

# Discover the tools, and what each one takes.
dotnet run --project RefactorMCP.ConsoleApp -- list-tools --verbose
dotnet run --project RefactorMCP.ConsoleApp -- extract-method --help

# Keep a solution loaded and serve calls from it.
dotnet run --project RefactorMCP.ConsoleApp -- serve --solution ./RefactorMCP.sln
dotnet run --project RefactorMCP.ConsoleApp -- status
dotnet run --project RefactorMCP.ConsoleApp -- stop --all

# Serve the tools over MCP on stdio.
dotnet run --project RefactorMCP.ConsoleApp -- mcp
```

Options are named after the tool's parameters (`--method-names` for
`methodNames`), a trailing `Path` may be dropped (`--file` for `filePath`), and
anything left over is positional in parameter order. `--no-daemon` runs a call
in the calling process instead. The daemon stops after ten minutes without a
request, or when asked to with `stop`.

A daemon follows the files it has loaded, so edits made in an editor are picked
up rather than refactored on top of stale text: an edited file is replaced in
place, and a change to a project file reloads the solution before the next
call.

Once built, the executable can be called directly
(`RefactorMCP.ConsoleApp/bin/Debug/net9.0/RefactorMCP.ConsoleApp`), which skips
the build check `dotnet run` performs.

For usage examples see [EXAMPLES.md](./EXAMPLES.md).

## Available Refactorings

- **Extract Method** – create a new method from selected code and replace the original with a call (expression-bodied methods are not supported).
- **Introduce Field/Parameter/Variable** – turn expressions into new members; fails if a field already exists.
- **Convert to Static** – make instance methods static using parameters or an instance argument.
- **Move Static Method** – relocate a static method and keep a wrapper in the original class.
- **Move Instance Method** – move one or more instance methods to another class and delegate from the source. If a moved method no longer accesses instance members, it is made static automatically. Provide a `methodNames` list along with optional `constructor-injections` and `parameter-injections` to control dependencies.
- **Move Multiple Methods (instance)** – move several methods and keep them as instance members of the target class. The source instance is injected via the constructor when required.
- **Move Multiple Methods (static)** – move multiple methods and convert them to static, adding a `this` parameter.
- **Make Static Then Move** – convert an instance method to static and relocate it to another class in one step.
- **Move Type to Separate File** – move a top-level type into its own file named after the type.
- **Make Field Readonly** – move initialization into constructors and mark the field readonly.
- **Transform Setter to Init** – convert property setters to init-only and initialize in constructors.
- **Constructor Injection** – convert method parameters to constructor-injected fields or properties.
- **Safe Delete** – remove fields or variables only after dependency checks.
- **Inline Method** – replace calls with the method body and delete the original.
- **Extract Decorator** – create a decorator class that delegates to an existing method.
- **Create Adapter** – generate an adapter class wrapping an existing method.
- **Add Observer** – introduce an event and raise it from a method.
- **Use Interface** – change a method parameter type to one of its implemented interfaces.
- **List Tools** – display all available refactoring tools as kebab-case names.

Metrics and summaries are also available via the `metrics://` and `summary://` resource schemes.

## Contributing

* Run `dotnet test` to ensure all tests pass.
* Format the code with `dotnet format` before opening a pull request.

## License

Licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).
