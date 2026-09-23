# Introduce Interface for Dependency

Makes a class depend on an interface instead of a concrete class: the
interface is extracted from the concrete class, and the field, property or
parameter holding the dependency is declared as the interface. Callers keep
passing the concrete class, which now implements the interface.

## Recipe

1. `extract-interface` from the dependency's class:
   `"target": { "symbol": "T:Shop.FileWriter" }, "arguments": { "name": "IWriter", "members": ["Write"] }`.
2. `change-type` of the field, property or parameter to the interface:
   `"target": { "symbol": "F:Shop.Report._writer" }, "arguments": { "to": "IWriter" }`.
3. For a field or property, `change-type` of each constructor parameter stored
   in it: `"target": { "symbol": "M:Shop.Report.#ctor(Shop.FileWriter)" }, "arguments": { "parameter": "writer", "to": "IWriter" }`.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | yes | The interface's name |
| `parameter` | no | With a method or constructor target, the parameter holding the dependency |
| `members` | no | The members the interface declares; defaults to every public instance member of the class |
| `file` | no | The interface's file, relative to the solution; defaults to `<name>.cs` beside the class |

The target is the field or property by symbol, `"target": { "symbol": "F:Shop.Report._writer" }`,
or a method or constructor by symbol with `parameter`.

## Precondition

- The dependency's declared type is a class declared in the solution.
- Extract Interface's precondition: the named members exist and are public
  instance members, and no type has the interface's name.
- Change Type's precondition for each declaration: every use of it needs only
  what the interface declares, and reaches the same member through it.

## Transformation

- The interface is created beside the class, declaring the chosen members with
  their documentation comments, and the class implements it.
- The field, property or parameter is declared as the interface, keeping its
  nullable annotation.
- For a field or property, each constructor parameter of the same type that
  the constructor assigns straight to it (`_writer = writer;`) is declared as
  the interface too.

## Preserved

- Every call: the members used through the dependency are the class's own
  implementations of the interface.
- Callers, which still construct and pass the concrete class.

## Limitations

- Only constructor parameters assigned directly to the field or property
  change with it; a parameter passed through a local or another method keeps
  the concrete type.
- An existing interface is not reused; the interface is always new.

## Error codes

| Code | Meaning |
|---|---|
| `type-not-in-source` | the dependency's type is not a class declared in the solution |
| `parameter-not-found` | the method has no parameter of that name |
| `member-not-found` | a named member does not exist in the class (step 1) |
| `type-already-exists` | a type has the interface's name (step 1) |
| `incompatible-use` | a use of the dependency needs a member the interface lacks |
| `changes-overload` | a use would reach a different overload through the interface |

A refusal in any step leaves every file as it was, and names the step that
refused.
