# Replace Temp with Query

Replaces a local variable that holds the result of an expression with a
private method that computes it, and calls the method wherever the local was
read.

## Recipe

1. `extract-method` on the local's initializer, selected with markers, with
   `name` the query's name. The initializer is a single expression, so the new
   method returns its value and the declaration now calls it.
2. `inline-local-variable` on the local, targeted by the containing method's
   symbol with `arguments.local` naming it. Each read becomes the call and the
   declaration goes.

```json
"steps": [
  { "refactoring": "extract-method", "target": { "file": "Order.cs", "selection": "marker" }, "arguments": { "name": "BasePrice" } },
  { "refactoring": "inline-local-variable", "target": { "symbol": "M:Shop.Order.Price" }, "arguments": { "local": "basePrice" } }
]
```

Inline Local Variable treats a call as a side effect, so the recipe only
completes when the local is read once. The dedicated implementation checks
the extracted expression rather than the call, and so also replaces a local
read several times.

## Arguments

| Argument | Meaning |
|---|---|
| `name` | the name of the query method |

The target is the local, by a caret on its declaration or on any read of it,
or in a later step of a composite by the containing method's symbol with
`arguments.local` naming it.

## Precondition

- The local is declared with an initializer by a local declaration statement
  in a block, in the body of a class's method. It is not a `using`
  declaration or a `ref` local.
- The local is never written after its declaration, and is read at least
  once.
- When it is read more than once, the initializer has no side effects: no
  call, object creation, assignment, increment or `await`.
- No local, parameter or field the initializer reads is assigned between the
  declaration and a read.
- The class has no member with the query's name.

## Transformation

- A `private` method with the given name is added after the containing
  method, returning the initializer. It is `static` when the containing
  method is, and takes the locals and parameters the initializer reads, in
  the order it first reads them.
- The query returns the local's declared type, so a conversion the
  declaration made, such as `double total = count * weight;` from `int`
  operands, is still made.
- Each read of the local becomes a call of the query, and the declaration is
  removed.

## Preserved

- The value at every read, since nothing the initializer reads changes before
  it.
- Comments above the declaration stay in place, above the statement that
  followed it; comments inside the initializer move into the query.

## Limitations

- The query is evaluated at each read instead of once. Calls inside the
  initializer are refused when the local is read more than once, but state a
  called method reads is not checked for changes between the declaration and
  a single read.
- Locals in constructors, accessors, local functions and lambdas, and in
  structs and records, are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `no-initializer` | the local has no initializer to become the query |
| `assigned-after-declaration` | the local is written after its declaration |
| `initializer-has-side-effects` | the initializer has side effects and the local is read more than once |
| `initializer-inputs-change` | a variable the initializer reads is assigned before a read |
| `never-used` | the local is never read |
| `name-conflict` | the class already has a member with the query's name |
| `not-in-method` | the local is not in the body of a class's method |
| `not-a-local` | the target is not a local variable |
