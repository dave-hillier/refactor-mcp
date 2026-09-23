# Extract Method

Moves a run of statements, or a single expression, out of a method body into
a new private method, and replaces them with a call to it.

## Precondition

- The selection lies inside a block-bodied method and covers at least one
  whole statement, or exactly one expression that is not a statement of its
  own. Statements the selection touches are extracted whole.
- An expression has a value whose type can be written, and is not assigned
  to or passed by `ref` or `out`.
- No local declared inside the selection is used after it. Such a local would
  be left without a declaration.
- No local or parameter assigned inside the selection is read after it, or
  before it in an enclosing loop. The assignment would change a copy in the
  new method and be lost.
- In a `void` method, the statements return only at their end.
- The class has no member with the name, or has a method of that name whose
  body is the selected code (see below).

## Transformation

- The new method is placed after the containing method, with the given name,
  and is `private`. It is `static` when the containing method is.
- Statements are taken from the innermost block or switch section that holds
  the whole selection, so a selection inside the block of an `if` or loop
  extracts statements of that block. A statement an `if`, `else` or loop runs
  without braces is extracted on its own and replaced by the call.
- An expression becomes a method returning it, with the expression's type,
  and the call takes its place. An expression over several lines keeps the
  indentation of its later lines relative to the statement it is in.
- A `return;`, `break;` or `continue;` ending the selection stays at the call
  site, after the call.
- When the name is that of a method the class already has, whose body is the
  selected code with each of its parameters standing for an expression in
  it, no method is created: the selection becomes a call of that method,
  passing those expressions. The method is not generic, takes its parameters
  by value, returns the selected expression's type or, for statements,
  nothing, and is static when the containing method is. The expressions have
  no side effects, and every other name means the same in both.
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
- Comments and blank lines inside the extracted statements or expression.
- Comments and blank lines outside the selection: a comment above the first
  selected statement stays at the call site, and a blank line after the
  selection stays after the call.

## Limitations

- Expression-bodied methods are refused rather than converted to a block body.
- Locals assigned inside the selection and read afterwards are not returned as
  out values; the extraction is refused instead.
- A selection that spans several blocks extracts whole statements of the
  innermost block holding it. Statements inside a lambda or local function
  are not extracted on their own.
- A `void` method's statements that return before their end are refused
  rather than turned into a method whose result says whether to return.
- Only the selected occurrence of an expression is replaced, not other equal
  expressions.
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
| `returns-early` | the statements of a `void` method return before their end |
| `name-conflict` | the class already has a member with the name, and no method of that name has the selected code as its body |
| `no-value` | the selected expression has no value a method could return |
| `assigned-expression` | the selected expression is assigned to |
