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
the command `refactor-mcp` with the argument `mcp`, for example:

```json
{
  "mcpServers": {
    "refactor-mcp": { "command": "refactor-mcp", "args": ["mcp"] }
  }
}
```

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
request, or when asked to with `stop`; `serve --idle-timeout` changes that
(`30s`, `1h`, or `0` to never expire).

A daemon follows the files it has loaded, so edits made in an editor are picked
up rather than refactored on top of stale text: an edited file is replaced in
place, and a change to a project file reloads the solution before the next
call.

Once built, the executable can be called directly
(`RefactorMCP.ConsoleApp/bin/Debug/net9.0/RefactorMCP.ConsoleApp`), which skips
the build check `dotnet run` performs.

For usage examples see [EXAMPLES.md](./EXAMPLES.md).

## Tools

`list-tools --verbose` prints every tool with what it takes; the groups below
are a map of what is there. Each tool refuses, and leaves the files unchanged,
when it cannot keep the code's behaviour.

**Solution and session** – `load-solution`, `unload-solution`,
`clear-solution-cache`, `list-tools`, `version`. Solutions may be `.sln` or
`.slnx` files.

**Navigation and analysis** – `find-references`, `find-implementations`,
`find-overrides`, `find-callers`, `analyze-refactoring-opportunities`,
`list-class-lengths`.

**Renaming and signatures** – `rename-symbol`, `change-signature`,
`add-parameter-default-value`, `remove-unused-parameter`, `inline-parameter`,
`introduce-parameter`, `introduce-parameter-object`, `preserve-whole-object`,
`parameterise-method`, `replace-parameter-with-explicit-methods`,
`redirect-calls-with-constant-argument`, `use-named-arguments`,
`change-accessibility`, `change-type`, `change-return-type`, `use-interface`,
`introduce-generic-type-parameter`.

**Extracting and inlining** – `extract-method`, `inline-method`,
`introduce-variable`, `inline-local-variable`, `introduce-field`,
`inline-field`, `introduce-constant`, `inline-constant`,
`replace-expression-with-field`, `replace-temp-with-query`,
`split-temporary-variable`, `split-declaration-and-assignment`,
`join-declaration-and-assignment`, `convert-local-to-field`,
`introduce-type-alias`, `inline-type-alias`.

**Moving** – `move-member`, `move-multiple-methods`, `make-static-then-move`,
`make-method-static`, `make-method-instance`, `move-to-separate-file`,
`move-type-to-namespace`, `move-member-to-partial-file`, `make-type-partial`,
`merge-partial-declarations`, `rename-file-to-match-type`,
`sync-namespace-with-folder`, `convert-to-file-scoped-namespace`,
`convert-to-block-namespace`, `cleanup-usings`, `create-type`.

**Classes and hierarchies** – `extract-class`, `inline-class`,
`extract-interface`, `extract-superclass`, `collapse-hierarchy`,
`change-base-type`, `pull-up-field`, `pull-up-method`,
`pull-up-constructor-body`, `push-down-field`, `push-down-method`,
`hide-delegate`, `remove-middle-man`, `replace-inheritance-with-delegation`,
`replace-delegation-with-inheritance`, `add-delegating-member`,
`remove-delegating-member`, `replace-base-uses-with-field`,
`replace-field-uses-with-base`, `introduce-interface-for-dependency`,
`inject-constructor-dependency`,
`initialize-field-from-constructor-parameter`,
`replace-constructor-with-factory-method`,
`replace-method-with-method-object`.

**Fields and properties** – `encapsulate-field`, `encapsulate-collection`,
`make-field-readonly`, `transform-setter-to-init`, `convert-to-auto-property`,
`convert-auto-property-to-backing-field`, `convert-method-to-property`,
`convert-property-to-methods`.

**Conditionals** – `invert-if`, `invert-boolean`, `split-if`,
`merge-nested-if`, `merge-sibling-ifs`, `remove-redundant-else`,
`decompose-conditional`, `consolidate-conditional-expression`,
`consolidate-duplicate-conditional-fragments`,
`replace-nested-conditional-with-guard-clauses`, `convert-if-chain-to-switch`,
`convert-if-to-switch-expression`, `convert-switch-statement-to-expression`,
`convert-switch-expression-to-statement`, `use-pattern-matching`,
`separate-query-from-modifier`, `feature-flag-refactor`.

**Language conversions** – `convert-to-expression-body`,
`convert-to-block-body`, `convert-to-primary-constructor`,
`convert-primary-constructor-to-constructor`, `convert-class-to-record`,
`convert-record-to-class`, `convert-anonymous-type-to-class`,
`convert-tuple-to-named-type`, `convert-to-extension-method`,
`convert-extension-method-to-static`, `convert-local-function-to-method`,
`convert-method-to-local-function`, `convert-lambda-to-method-group`,
`convert-method-group-to-lambda`, `convert-for-to-foreach`,
`convert-foreach-to-for`, `convert-foreach-to-linq`, `convert-linq-to-foreach`,
`convert-concatenation-to-interpolation`, `introduce-using-declaration`,
`make-method-async`, `convert-to-async`.

**Generators** – `add-null-checks`, `convert-to-nullable-aware`,
`introduce-null-object`, `extract-decorator`, `create-adapter`,
`add-observer`, `replace-array-with-object`,
`replace-conditional-with-polymorphism`, `replace-error-code-with-exception`,
`replace-type-code-with-enum`, `replace-type-code-with-subclasses`.

**Deleting** – `safe-delete-member`, `safe-delete-type`, `safe-delete-local`.

Several of these are composites: they run a sequence of the smaller tools as a
recipe, so each step is checked the way the tool on its own would be.

The MCP server also serves two resources: `metrics://` for code metrics of a
file, and `summary://` for a file with its method bodies omitted.

## Refactoring catalog

[`Catalog/`](./Catalog/README.md) specifies each refactoring as behaviour, with
before and after fixtures for every case, grouped into primitives, composites
and generators. The test suite runs every case against the tools, and the
[catalog site](https://dave-hillier.github.io/refactor-mcp/) renders them as
diffs.

## State and logging

Metrics caches are kept under `~/.refactor-mcp`, one folder per solution, so
nothing is written into the repository being refactored. Set
`REFACTOR_MCP_HOME` to use another folder.

Tool calls are not logged unless `REFACTOR_MCP_LOG` is set, either to a file
path or to `1` to log into the solution's folder under `~/.refactor-mcp`.

## Repository layout

| Path | Contents |
|---|---|
| `RefactorMCP.ConsoleApp/` | The tools, the CLI, the daemon and the MCP server |
| `RefactorMCP.Tests/` | xUnit tests, including the catalog runner |
| `Catalog/` | The refactoring specification and its fixtures |
| `site/` | The generator for the catalog site |
| `Examples/`, `Demos/` | Worked examples and an end-to-end e-commerce demo |
| `vscode-extension/` | A VS Code extension that calls the command line |

## Contributing

* Run `dotnet test` to ensure all tests pass. The catalog alone runs with
  `dotnet test --filter "FullyQualifiedName~CatalogTests"`.
* A new refactoring starts as a catalog case; see
  [Catalog/README.md](./Catalog/README.md).
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
