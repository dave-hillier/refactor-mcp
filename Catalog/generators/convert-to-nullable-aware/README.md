# Convert to Nullable-Aware

Enables nullable reference types in one file and adds the `?` annotations
its code shows it needs, so the file compiles without new warnings. Files are
converted one at a time, so a project can move to nullable gradually.

This is a generator: the annotations are the ones this file's flow requires,
chosen by following the compiler's warnings, not the only possible set; a
person might instead initialise a field or add a null check. The `after/`
fixtures pin that choice. Run-time behaviour does not change; the annotations
only inform the compiler.

The target is the file: `"target": { "file": "Directory.cs" }`.

## Precondition

- The file has no `#nullable` directive and its project does not already
  enable nullable reference types.
- Every nullable warning that enabling produces can be answered by
  annotating a declaration in the file (see below).

## Transformation

- `#nullable enable` becomes the first line, followed by a blank line and the
  file as it was, including any leading comment.
- The file is compiled and each new warning is answered by annotating one
  declaration with `?`, repeating until no warning remains:

  | Warning | Annotated |
  |---|---|
  | CS8600, CS8601, CS8625: a possibly null value assigned or initialised | the local, field, property or parameter it is stored in |
  | CS8604, CS8625: a possibly null argument | the parameter it is passed to |
  | CS8603: a possibly null return | the method's, property's or local function's return type |
  | CS8618: a member left unset by the constructor | the field, property or event |
  | CS8765, CS8767: a parameter less nullable than the member it overrides or implements | the parameter |

  So a null followed through a return, a local and an argument annotates
  each declaration in turn.
- Only reference types and unconstrained type parameters (`T?`) are
  annotated, and only when written explicitly: `var` locals are already
  inferred nullable.

## Preserved

- Behaviour, and every other file: callers in files that are still
  nullable-oblivious see no warnings and are not changed.

## Limitations

- Annotations are only added, never removed, and `!`, null checks and
  `[NotNull]`-style attributes are never introduced.
- A field declaration naming several variables is annotated as a whole.
- Warnings about generic type arguments (`List<string>` holding null),
  delegates, or members declared in other files are not answered, so the
  conversion is refused.

## Error codes

| Code | Meaning |
|---|---|
| `already-nullable-aware` | the file has a `#nullable` directive or its project enables nullable |
| `unresolved-warning` | a warning remains that no annotation fixes, such as CS8602, a dereference of a possibly null value |
