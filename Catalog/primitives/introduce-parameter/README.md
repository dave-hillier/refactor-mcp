# Introduce Parameter

Turns an expression in a method body into a new parameter. The expression is
replaced by the parameter, and every call passes the expression instead,
computed from that call's own arguments.

## Arguments

| Argument | Meaning |
|---|---|
| `name` | the name of the new parameter |

The target is a selection covering exactly one expression inside a method or
constructor body.

## Precondition

- The selection is a whole expression, give or take surrounding whitespace.
- The expression reads nothing a caller cannot see: no locals, no lambda or
  local function parameters, no `this`, and no instance members of the
  containing type. It may read the method's parameters, constants, static
  members and members of other objects.
- The expression's type does not use one of the method's type parameters.
- The name is not already used in the method by a parameter or local, nor by a
  member the body could mean by it.
- The result compiles.

## Transformation

- The selected occurrence of the expression becomes a reference to the new
  parameter. Other equal expressions are left alone.
- The parameter is declared with the expression's type, after the existing
  required parameters and before any optional or params parameters.
- Every call passes the expression, with each parameter it read replaced by
  the argument that call passes for it, parenthesised where precedence needs
  it. A parameter the call leaves to its default is replaced by the default.
  An extension method called in reduced form uses its receiver.
- The signature change is carried out as by Change Signature, so overrides,
  interface members and their calls change too.

## Preserved

- The value the method computes, as long as the expression gives the same
  value when evaluated at the call instead of in the body.
- Comments and layout around the expression and the calls.

## Limitations

- The expression is evaluated before the call rather than where it was, so
  side effects and timing move with it; an expression reading a parameter the
  body assigns first would see the original value.
- Names in the expression are copied as written, so a call in another
  namespace may need a `using` the expression's file had.
- Calls can end up with expressions a person would simplify, such as
  `"sam" ?? "anonymous"` (`nullable-expression`).
- Instance members are not passed through the call's receiver; the
  refactoring refuses instead.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-expression` | the selection is not exactly one expression |
| `references-local` | the expression reads a local, which callers cannot see |
| `references-instance-member` | the expression reads `this` or an instance member of the type |
| `references-type-parameter` | the expression uses one of the method's type parameters |
| `name-conflict` | the name is already used in the method |
