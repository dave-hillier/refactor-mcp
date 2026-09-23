# Introduce Field

Stores the value of a selected expression in a new field of the containing
type and uses the field in its place. A second form adds a field of a named
type to a type, which composite recipes such as Extract Class use to hold the
object that members move to.

## From an expression

`"target": { "file": ..., "selection": "marker" }` with `"arguments": { "name": "_field" }`.

### Precondition

- The selection is exactly one expression, which produces a value and is not
  assigned to.
- No member of the containing type already has the name.
- The expression's type does not use a type parameter of the containing
  method, which a field could not name.
- Unless the expression is constant, it is inside a statement in a block, and
  that statement evaluates it exactly once each time it runs: it is not on the
  right of `&&`, `||` or `??`, in a branch of `?:`, after `?.`, in a switch
  expression arm, a lambda or a loop condition.

### Transformation

- A constant expression becomes a `private readonly` field initialised where
  it is declared, `static` when the expression is in a static member.
- Any other expression becomes a `private` field without an initialiser. An
  assignment of the expression to the field is inserted immediately before the
  statement that contained it, and takes over the comments above that
  statement.
- Only the selected occurrence is replaced.
- The field is placed after the type's existing fields, or first in the type,
  followed by a blank line, when it has none.
- Its type is written as code at the expression would write it. With nullable
  reference types enabled, a reference-type field assigned in a member is
  declared nullable, because it is null until the member runs.

### Preserved

- The value the statement computes, and the order in which the statement
  evaluates its parts.
- Comments above the statement and trailing comments on its line.

## Adding a field of a type

`"target": { "symbol": "T:Shop.Customer" }` with
`"arguments": { "type": "Address", "name": "_address" }`.

- The field is added to the targeted type, after its existing fields.
- When the named type is a non-abstract class with an accessible parameterless
  constructor, the field is `private readonly` and initialised with a new
  instance, so code moved behind it has an object to run against. Otherwise it
  is declared `private` without an initialiser, to be assigned by a later step.
- The type name must resolve from inside the targeted type, and the field name
  must be free.

## Limitations

- Other occurrences of the same expression are not replaced.
- A non-constant expression in an expression-bodied member is refused rather
  than converting the member to a block body.
- A non-constant expression is re-evaluated each time the member runs and
  stored in the field; the refactoring does not move the evaluation into a
  constructor.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-expression` | the selection is not exactly one expression |
| `no-value` | the expression produces no value, such as a call to a `void` method |
| `assigned-expression` | the expression is assigned to or passed by `ref` or `out` |
| `name-conflict` | the type already has a member with the name |
| `method-type-parameter` | the expression's type uses a type parameter of the method |
| `conditionally-evaluated` | the statement may evaluate the expression other than exactly once |
| `expression-bodied-member` | a non-constant expression is in an expression-bodied member |
| `not-in-block` | the statement containing the expression is not in a block |
| `unknown-type` | the type named for a new field does not resolve |
