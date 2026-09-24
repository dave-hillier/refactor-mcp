# RefactorMCP

RefactorMCP exposes Roslyn-based refactoring tools for C# over three entry
points: a command line interface, a resident daemon that keeps a solution
loaded, and a Model Context Protocol server.

All three dispatch through the same tool dispatcher and session, so a tool
behaves the same however it is called.

## Install

RefactorMCP is published to NuGet as a .NET tool. It needs a .NET SDK (9.0 or
later) on the machine, since solutions are loaded through MSBuild.

```bash
dotnet tool install --global RefactorMCP
```

This puts `refactor-mcp` on the path. To serve it to an MCP client, register
the command `refactor-mcp` with the argument `mcp`.

The examples below run from source with `dotnet run --project
RefactorMCP.ConsoleApp --`; with the tool installed, `refactor-mcp` takes its
place.

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
- **Make Method Static** – make an instance method static, passing the instance or the members it reads, and update every call.
- **Move Member** – move a method, field or property to another type: an instance member through a field, property or parameter of the target type, a static member to the named type.
- **Move Multiple Methods** – move several methods of a class in one step, each method before the ones that call it.
- **Make Static Then Move** – convert an instance method to static and relocate it to another class in one step.
- **Move Type to Separate File** – move a top-level type into its own file named after the type.
- **Make Field Readonly** – move initialization into constructors and mark the field readonly.
- **Transform Setter to Init** – convert property setters to init-only and initialize in constructors.
- **Inject Constructor Dependency** – turn an object a method constructs for itself into a dependency the constructor receives.
- **Safe Delete** – remove an unused member, type or local, refusing when anything refers to it.
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

## Releasing

Pushing a tag named `v<version>` (for example `v0.2.0`) runs the Publish
workflow: it builds and tests at that version, pushes the package to NuGet
using the `NUGET_API_KEY` repository secret, and creates a GitHub release with
the package attached.

To try the package locally first:

```bash
dotnet pack RefactorMCP.ConsoleApp -c Release -o artifacts
dotnet tool install --global RefactorMCP --add-source ./artifacts
```

## License

Licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).
