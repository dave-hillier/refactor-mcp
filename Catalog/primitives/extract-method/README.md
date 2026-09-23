# Extract Method

Moves a run of statements out of a method body into a new private method, and
replaces them with a call to it. A selection that is exactly an expression
extracts the expression instead, into a method that returns its value.

## Precondition

For statements:

- The selection lies inside a block-bodied method and covers at least one
  whole statement. Statements the selection touches are extracted whole.
- No local declared inside the selection is used after it. Such a local would
  be left without a declaration.
- No local or parameter assigned inside the selection is read after it. The
  assignment would change a copy in the new method and be lost.

For an expression:

- The selection, ignoring whitespace around it, is exactly an expression in a
  method, block-bodied or expression-bodied. The whole expression of an
  expression statement, such as a call ending in `;`, is a statement, and a
  type or the name after a `.` is not an expression to extract.
- The expression is a value: it has a type other than `void`, and it is read,
  not assigned to or passed by `ref` or `out`.
- It assigns no local or parameter, which the new method would only change a
  copy of, and no variable it declares, such as a pattern variable, is used
  after it.
- It does not `await`.

## Transformation

For statements:

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

For an expression:

- The new method is placed after the containing method, with the given name.
  It is `private`, `static` when the containing method is, and its body is
  `return expression;`. Its return type is the expression's type; in a
  nullable context a reference type is nullable only when the value may be
  null there.
- Parameters, type parameters and the call's type arguments follow the same
  rules as for statements. The call replaces the expression where it was:
  `if (age >= 65 || member)` becomes `if (IsExempt(age, member))`.
- An expression over several lines keeps the indentation of its later lines
  relative to the statement it is in.

## Preserved

- The behaviour of the containing method on every path.
- Comments and blank lines inside the extracted statements or expression.
- Comments and blank lines outside the selection: a comment above the first
  selected statement stays at the call site, and a blank line after the
  selection stays after the call.

## Limitations

- Statements cannot be selected in an expression-bodied method; a selection
  there that is not a whole expression is refused rather than the method
  being converted to a block body.
- Locals assigned inside the selection and read afterwards are not returned as
  out values; the extraction is refused instead.
- Only statements directly in the method body are extracted; a selection
  inside a nested block extracts the whole enclosing statement.
- Extraction from constructors and accessors is refused, and so is extracting
  statements from a local function; an expression in a local function or
  lambda inside a method can be extracted.
- An expression that awaits is refused rather than extracted into an async
  method.
- The new method is added to a class; structs and records are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `expression-bodied-member` | the selection is in an expression-bodied method and is not a whole expression |
| `declared-local-used-after` | the selection declares a local, or the expression a variable, used after it |
| `assigned-local-used-after` | the selection assigns a local or parameter read after it |
| `not-in-method` | the selection is not inside a method |
| `no-statements-selected` | the selection covers no whole statement |
| `not-a-value` | the selected expression has no type, or is assigned to |
| `expression-assigns-local` | the selected expression assigns a local or parameter |
| `expression-awaits` | the selected expression awaits |
