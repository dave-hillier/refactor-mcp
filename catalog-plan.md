# Plan: Refactoring catalog and fixture-based tests

The aim is a catalog of C# refactorings described as behaviour, each backed by
plain input and expected-output files, so the catalog is the specification and
the current implementation is measured against it rather than defining it.

## Why

- Current tests embed input and expected code as string literals inside xUnit
  methods. They are hard to read, hard to diff, and tied to the tool method
  signatures of today.
- There is no single place that says what refactorings exist, what each one
  promises, and what its edge cases are. The README lists tools, not
  refactorings.
- A fixture corpus lets any implementation, current or rewritten, be scored
  against the same expectations. It also serves as documentation and as
  examples for agents using the tool.

## Three tiers

The catalog separates entries by what they promise.

- **Primitives** are atomic, behaviour-preserving transformations. Each is
  implemented directly and has its own fixture directory.
- **Composites** are behaviour-preserving recipes built from primitives. Each
  README defines the recipe as a sequence of primitive steps. A composite may
  have a dedicated implementation, but its fixtures can always be run as the
  recipe, so a failure points at the step that broke.
- **Generators** add structure or change behaviour: they introduce a pattern,
  a flag or a check. Their `after/` is one reasonable design among several, so
  fixtures pin the chosen shape rather than a unique correct answer. They are
  kept separate so that everything outside this tier can promise preserved
  behaviour.

Analysis and metrics (class length, refactoring opportunities, the
`metrics://` and `summary://` resources) are not transformations and are
outside the catalog.

Each direction of a reversible refactoring is its own entry, with its own
preconditions and failure cases.

## Fixture format

One directory per case, under `Catalog/<tier>/<refactoring>/<case>/`.

```
Catalog/
  primitives/
    extract-method/
      README.md                   description of the refactoring
      simple-statements/
        case.json                 the operation and its arguments
        before/
          Sample.cs
        after/
          Sample.cs
      multiple-files/
        case.json
        before/
          Order.cs
          Customer.cs
        after/
          Order.cs
          Customer.cs
      expression-bodied-rejected/
        case.json                 expects an error, no after/ directory
        before/
          Sample.cs
  composites/
    extract-class/
      README.md                   description and recipe
      ...
  generators/
    ...
```

`case.json` describes the refactoring in terms of the catalog, not the tool
method:

```json
{
  "refactoring": "extract-method",
  "description": "Extracts two statements into a private method",
  "target": { "file": "Sample.cs", "selection": "marker" },
  "arguments": { "name": "ValidateInputs" },
  "expect": "success"
}
```

For failure cases:

```json
{
  "refactoring": "extract-method",
  "target": { "file": "Sample.cs", "selection": "marker" },
  "arguments": { "name": "Doubled" },
  "expect": "error",
  "errorCode": "expression-bodied-member",
  "errorContains": "expression-bodied"
}
```

For composites run as a recipe:

```json
{
  "refactoring": "extract-class",
  "description": "Moves the address fields and formatting into a new Address class",
  "steps": [
    { "refactoring": "create-type", "arguments": { "name": "Address", "file": "Address.cs" } },
    { "refactoring": "introduce-field", "target": { "symbol": "T:Shop.Customer" }, "arguments": { "type": "Address", "name": "_address" } },
    { "refactoring": "move-field", "target": { "symbol": "F:Shop.Customer.Street" }, "arguments": { "via": "_address" } },
    { "refactoring": "move-method", "target": { "symbol": "M:Shop.Customer.FormatAddress" }, "arguments": { "via": "_address" } }
  ],
  "expect": "success"
}
```

Project settings, where the defaults do not suit:

```json
{
  "project": { "langVersion": "12", "nullable": "enable" }
}
```

```json
{
  "projects": [
    { "name": "Core" },
    { "name": "App", "references": ["Core"] }
  ]
}
```

Each project's files live in `before/<name>/` and `after/<name>/`.

### Rules

- `before/` is a complete, compilable set of files. The runner generates
  project files from `case.json`, so cases never carry them. The default is a
  single `net9.0` library project on the latest language version with
  nullable and implicit usings disabled, so fixtures state every `using`. The
  generated solution must carry configuration sections; without them the
  workspace loads documents but resolves no references.
- `after/` contains every file that should exist afterwards. A file present in
  `before/` and absent from `after/` must have been deleted. A file present
  only in `after/` must have been created.
- The actual output must compile with no diagnostics beyond those `before/`
  already had. A textual match that does not compile is a failure.
- Comparison is whitespace-normalised on line endings only. Indentation and
  blank lines are part of the expectation.
- Error cases assert a stable, kebab-case `errorCode`. The adapter maps each
  code to the message fragment the current tool reports. `errorContains` is
  optional and checks the message for a further detail.

### Targeting

- **Symbols** use Roslyn documentation comment IDs, for example
  `M:Shop.Order.Total(System.Int32)` or `F:Shop.Order._items`. This
  disambiguates overloads and is resolved with `DocumentationCommentId`.
  Symbol targeting is preferred wherever the refactoring acts on a
  declaration.
- **Selections** use markers in the source: `/*[*/` opens and `/*]*/` closes
  the selection. `"selection": "marker"` tells the runner to find them. The
  runner strips markers from `before/` before loading, so they never reach
  the refactoring or the output. Markers survive edits to the file where line
  and column numbers would not.
- **Positions** use `/*^*/` for a caret, for refactorings that act on the
  token under the cursor.
- Explicit `"range": "5:9-6:30"` is allowed as a fallback, with 1-based lines
  and columns and an exclusive end, matching the tools.

## Runner

- A single xUnit theory discovers every `case.json` and runs it. Test names
  are `<tier>/<refactoring>/<case>` so failures point at a directory.
- The runner copies `before/` into a temp directory, strips markers, generates
  and restores project files, applies the refactoring or each step in turn
  through a small adapter that maps catalog names and arguments onto the
  current tools, compiles the result, then diffs against `after/`.
- The adapter invokes tools through `ToolDispatcher` by name, the same path the
  CLI and daemon use, so a case exercises the tool as a client would.
- A case marked `unimplemented` still has its `before/` compiled, so backlog
  fixtures are valid code before anything implements them.
- Cases the implementation does not yet satisfy, whether the refactoring is
  missing or an existing tool falls short of the case, are marked
  `"status": "unimplemented"` in `case.json` and reported as skipped, not
  failed. The skipped count is the backlog. An `unimplemented` case that
  passes fails the run with a message to remove the status, so the backlog
  never overstates what is missing.
- Setting `CATALOG_UPDATE=1` rewrites `after/` from actual output for
  reviewing new behaviour. It is never set in CI.
- The runner is the only place the tool method signatures appear. Renaming or
  reshaping a tool changes one adapter entry.
- Formatting is part of the expectation, so an SDK upgrade that changes
  Roslyn's formatter can change many fixtures at once. Those upgrades are
  reviewed with `CATALOG_UPDATE=1` and a diff, as a single change.

## Catalog

Each entry gets a `README.md` stating the precondition, the transformation,
what must be preserved, and the known limitations. Composite READMEs also
state the recipe. The list below is the intended scope regardless of what is
implemented today.

### Primitives

#### Methods and locals

- Extract Method
- Inline Method
- Extract Local Variable (Introduce Variable)
- Inline Local Variable
- Split Temporary Variable
- Split Declaration and Assignment
- Join Declaration and Assignment
- Convert Local to Field
- Convert Method to Local Function
- Convert Local Function to Method
- Convert Method to Expression-Bodied
- Convert Expression-Bodied to Block Body
- Convert Lambda to Method Group
- Convert Method Group to Lambda

#### Signatures

- Change Signature (add, remove and reorder parameters, updating every call
  site)
- Introduce Parameter (from an expression in the body)
- Inline Parameter (when every call site passes the same value)
- Remove Unused Parameter
- Add Default Value to Parameter
- Use Named Arguments at Call Site
- Change Return Type
- Change Accessibility

#### Fields, properties and constants

- Introduce Field (from an expression)
- Inline Field
- Introduce Constant
- Inline Constant
- Encapsulate Field (field to property with backing field)
- Convert Property to Auto-Property
- Convert Auto-Property to Property with Backing Field
- Convert Method to Property
- Convert Property to Methods
- Make Field Readonly
- Convert Setter to Init-Only
- Encapsulate Collection (expose a read-only view, add and remove methods)

#### Moving members and types

- Move Instance Method (with and without a delegating stub)
- Move Static Method
- Move Field
- Move Property
- Move Member to Another Partial File
- Move Type to File
- Move Type to Namespace
- Sync Namespace with Folder
- Rename File to Match Type
- Make Method Static (with an option for passing instance members as
  parameters or the instance itself as an argument)
- Make Method Instance
- Convert Static Method to Extension Method
- Convert Extension Method to Static Method

#### Types and hierarchy

- Create Type
- Change Base Type
- Pull Up Field
- Pull Up Method
- Pull Up Constructor Body
- Push Down Field
- Push Down Method
- Extract Interface
- Change Type / Use Base Type Where Possible (for a variable, parameter,
  field or return type)
- Introduce Generic Type Parameter
- Make Type Partial
- Merge Partial Declarations
- Convert Class to Record
- Convert Record to Class
- Convert Tuple to Named Type
- Convert Anonymous Type to Class
- Convert Class to Primary Constructor
- Convert Primary Constructor to Class Constructor
- Replace Constructor with Factory Method

#### Conditionals

- Invert If
- Merge Nested If
- Split If (on `&&` or `||`)
- Invert Boolean (method, property or field, updating every use)
- Convert If Chain to Switch Statement
- Convert Switch Statement to Switch Expression
- Convert Switch Expression to Switch Statement
- Use Pattern Matching (replace type check and cast with `is` or `switch`
  patterns)

#### Loops and expressions

- Convert For to Foreach
- Convert Foreach to For
- Convert Foreach to LINQ
- Convert LINQ to Foreach
- Convert String Concatenation to Interpolation
- Introduce Using Declaration

#### Naming and housekeeping

- Rename (any symbol, with all references, including the file when renaming
  a type whose file matches)
- Introduce Type Alias
- Inline Type Alias
- Safe Delete Member
- Safe Delete Type
- Safe Delete Local
- Cleanup Usings
- Convert Namespace to File-Scoped
- Convert File-Scoped Namespace to Block

### Composites

Each recipe is the default. An implementation may be smarter, but the recipe
defines the expected result.

- **Extract Class**: Create Type, Introduce Field of the new type, Move Field
  and Move Instance Method through it.
- **Inline Class**: Move Field and Move Instance Method into the using class,
  Safe Delete Type.
- **Extract Superclass**: Create Type, Change Base Type, Pull Up Field and
  Pull Up Method.
- **Collapse Hierarchy**: Pull Up or Push Down every member, retarget
  references, Safe Delete Type.
- **Replace Method with Method Object**: Create Type, Convert Local to Field
  and move parameters to fields, Move Instance Method.
- **Replace Temp with Query**: Extract Method on the initialiser, Inline Local
  Variable.
- **Decompose Conditional**: Extract Method on the condition and on each
  branch.
- **Consolidate Conditional Expression**: merge conditions with identical
  bodies, Extract Method on the combined condition.
- **Consolidate Duplicate Conditional Fragments**: move statements common to
  every branch out of the conditional.
- **Replace Nested Conditional with Guard Clauses**: Invert If and early
  return, repeated.
- **Convert If to Switch Expression**: Convert If Chain to Switch Statement,
  Convert Switch Statement to Switch Expression.
- **Hide Delegate**: Extract Method on the delegate call, Move Instance Method
  onto the server, repoint callers.
- **Remove Middle Man**: Inline Method at each delegating call site, Safe
  Delete Member.
- **Introduce Parameter Object**: Create Type, Change Signature to take it,
  rewrite parameter uses.
- **Preserve Whole Object**: Change Signature to take the source object,
  rewrite parameter uses as member access.
- **Parameterise Method**: Change Signature on one method to add the varying
  value, redirect the similar methods, Inline Method.
- **Replace Parameter with Explicit Methods**: Extract Method per value,
  redirect call sites that pass a constant.
- **Separate Query from Modifier**: Extract Method for the query, Extract
  Method for the modifier, redirect callers.
- **Replace Inheritance with Delegation**: Introduce Field of the base type,
  delegate each used inherited member, Change Base Type.
- **Replace Delegation with Inheritance**: Change Base Type, Inline Method
  for each delegating member, Inline Field.
- **Introduce Interface for Dependency**: Extract Interface, Change Type on
  the field or parameter.
- **Constructor Injection**: Change Signature on the constructor to add the
  dependency, Introduce Field, replace the local construction.
- **Make Static then Move**: Make Method Static, Move Static Method.
- **Move Multiple Methods**: Move Instance Method or Move Static Method per
  method, in dependency order.
- **Convert to Async**: Change Return Type to `Task`, await the calls inside,
  then repeat for each caller up to a boundary.

### Generators

- Introduce Null Object
- Replace Conditional with Polymorphism
- Replace Type Code with Enum
- Replace Type Code with Subclasses
- Replace Array with Object
- Replace Error Code with Exception
- Extract Decorator
- Create Adapter
- Add Observer / Event
- Feature Flag Wrapping
- Add Null Checks
- Convert to Nullable-Aware

## Case coverage guidance

For each primitive, aim for at least:

- One minimal happy path.
- One case exercising trivia: comments, regions, pragma directives and blank
  lines adjacent to the edit.
- One case with references in another file, to prove solution-wide edits.
- One case for each documented precondition failure.
- One case with generics, one with overloads, one with inheritance, where the
  refactoring interacts with them.
- One case with `#nullable enable` and nullable annotations.
- One case across projects, where the refactoring can reach another project.

For each composite, aim for one recipe case and one case where a step's
precondition fails, proving the composite reports the failing step and leaves
the files unchanged.

## Work items

1. Define the `case.json` schema and write the runner with a hand-written
   adapter for one existing primitive. Prove the discovery, marker stripping,
   project generation, compile check and diff loop on Extract Method.
2. Add step execution and prove it on Make Static then Move, which already has
   both primitives implemented.
3. Port the existing string-literal tests for implemented tools into fixture
   directories. Delete a literal test once its fixture version passes. Tests
   of tool-level behaviour rather than refactoring meaning, such as argument
   validation, solution loading and the MCP tool surface, stay as xUnit tests.
4. Write `README.md` for every catalog entry, including unimplemented ones.
5. Author fixture cases for unimplemented refactorings and mark them
   `unimplemented`. Each becomes a ready-made acceptance test.
6. Add a summary test or script that prints implemented, unimplemented and
   failing counts per tier and group. Use it to prioritise the backlog,
   primitives before the composites that depend on them.
7. Generate `EXAMPLES.md` from the catalog so documentation and tests cannot
   drift, and retire the hand-maintained example verification tests it
   replaces.
