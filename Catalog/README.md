# Refactoring catalog

Each refactoring the project aims to support is described here as behaviour,
with fixtures that show its input and expected output. The catalog is the
specification; the implementation is measured against it.

## Layout

```
Catalog/
  case.schema.json
  <tier>/                  primitives, composites or generators
    <refactoring>/
      README.md            precondition, transformation, what is preserved, limitations
      <case>/
        case.json          the operation, its target and arguments
        before/            a complete, compilable set of source files
        after/             every file that should exist afterwards
```

A file in `before/` and absent from `after/` must have been deleted; a file
only in `after/` must have been created. Error cases have no `after/`: the
refactoring must refuse and leave every file unchanged.

Fixtures never carry project files. The runner generates a `net9.0` library
with nullable and implicit usings disabled, so every `using` a file needs is
written in it. `project` in `case.json` changes the language version or the
nullable context, and `projects` splits the files into several projects, each
in `before/<name>/`.

## Targeting

Mark the code a refactoring acts on in the `before/` source. The runner
removes the markers before the refactoring sees the file.

| Marker | Meaning | `case.json` |
|---|---|---|
| `/*[*/ ... /*]*/` | a selection | `"target": { "file": "Sample.cs", "selection": "marker" }` |
| `/*^*/` | a caret before the next character | `"target": { "file": "Sample.cs", "caret": "marker" }` |

Declarations are targeted by documentation comment id, such as
`"symbol": "M:Shop.Order.Total(System.Int32)"`, which tells overloads apart.
`"range": "5:9-6:30"` (1-based, end exclusive) is a fallback.

Markers describe `before/`, so in a composite only the first step can use
them; later steps target by symbol.

## Errors

A refusal is named by a stable `errorCode` in kebab case. The runner's adapter
maps each code to the message the current implementation reports, so rewording
a message changes the adapter, not the fixtures. `errorContains` can check a
detail of the message as well.

## Running

```bash
dotnet test --filter "FullyQualifiedName~CatalogTests"
```

To run some of the catalog, set `CATALOG_FILTER` to one or more id prefixes,
separated by commas:

```bash
CATALOG_FILTER=primitives/extract-method,primitives/invert-if dotnet test --filter "FullyQualifiedName~CatalogTests"
```

Each case is reported as `<tier>/<refactoring>/<case>`. A case whose
`case.json` has `"status": "unimplemented"` is skipped while it fails and fails
once it passes, so the skipped count is the backlog.

Cases with the same project settings share one MSBuild restore and load per
run, and each case runs against an in-memory solution built from what MSBuild
resolved. To restore and load every case through MSBuild, as a client of the
tools would, set `CATALOG_MSBUILD=1`; it is much slower.

To accept new behaviour, rewrite `after/` from the actual output and review the
change as a diff before committing:

```bash
CATALOG_UPDATE=1 dotnet test --filter "FullyQualifiedName~CatalogTests"
git diff Catalog/
```

## Adding a case

1. Create `<tier>/<refactoring>/<case>/before/` with the smallest code that
   shows the behaviour, and mark the target.
2. Write `case.json`, referencing `../../../case.schema.json` as `$schema` if
   your editor validates against it.
3. Write `after/` by hand when specifying new behaviour. Use update mode only
   to capture behaviour you have already decided is right.
4. If no mapping in `RefactorMCP.Tests/Catalog/Mappings/` covers the
   refactoring, mark the case `unimplemented`. A mapping class implements
   `ICatalogMappings` and is discovered automatically, one class per catalog
   group.

## Website

`site/build.py` renders the catalog as a static website: an index of every
refactoring, and a page for each with its README and every case as a diff from
`before/` to `after/`. The `Catalog site` workflow builds it on each change
and publishes it to GitHub Pages from `main`. To preview it locally:

```bash
python3 site/build.py --out _site
python3 -m http.server -d _site
```
