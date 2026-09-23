# Extract Method

Moves a run of statements out of a method body into a new private method, and
replaces them with a call to it.

## Precondition

- The selection lies inside a block-bodied method and covers at least one
  whole statement. Statements the selection touches are extracted whole.
- No local declared inside the selection is used after it. Such a local would
  be left without a declaration.
- No local or parameter assigned inside the selection is read after it. The
  assignment would change a copy in the new method and be lost.

## Transformation

- The new method is placed after the containing method, with the given name,
  and is `private`. It is `static` when the containing method is.
- Parameters and locals the statements read are passed as parameters, in the
  order they are first used. A parameter keeps its nullable annotation; a
  `var` local that flow analysis knows is not null is passed as non-nullable.
- Type parameters of the containing method that the new method needs are
  declared on it with their constraints. The call site names them only when
  the arguments cannot infer them.
- When the statements return from the containing method on some paths, the new
  method returns a nullable result and the call site returns it when present,
  otherwise execution continues after the call.

## Preserved

- The behaviour of the containing method on every path.
- Comments and blank lines inside the extracted statements.
- Comments and blank lines outside the selection: a comment above the first
  selected statement stays at the call site, and a blank line after the
  selection stays after the call.

## Limitations

- Expression-bodied methods are refused rather than converted to a block body.
- Locals assigned inside the selection and read afterwards are not returned as
  out values; the extraction is refused instead.
- Only statements directly in the method body are extracted; a selection
  inside a nested block extracts the whole enclosing statement.
- Extraction from constructors, accessors and local functions is refused.
- The new method is added to a class; structs and records are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `expression-bodied-member` | the selection is in an expression-bodied method |
| `declared-local-used-after` | the selection declares a local used after it |
| `assigned-local-used-after` | the selection assigns a local or parameter read after it |
| `not-in-method` | the selection is not inside a method |
| `no-statements-selected` | the selection covers no whole statement |
