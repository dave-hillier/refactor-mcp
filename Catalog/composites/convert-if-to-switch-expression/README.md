# Convert If to Switch Expression

Turns an `if` / `else if` chain that compares one value with constants or
patterns, and whose branches each produce a value, into a `switch` expression
that is returned or assigned.

## Recipe

1. Convert If Chain to Switch Statement, with the caret on the first `if`:
   `{ "refactoring": "convert-if-chain-to-switch", "target": { "file": "Shipping.cs", "caret": "marker" } }`.
2. Convert Switch Statement to Switch Expression, with the caret on the
   `switch` keyword, which starts where the `if` did:
   `{ "refactoring": "convert-switch-statement-to-expression", "target": { "file": "Shipping.cs", "range": "7:13-7:13" } }`.

A later step cannot use a marker, so it gives the caret as a `range` whose
start is the caret.

## Target

The first `if` of the chain, by caret: `"target": { "file": "Shipping.cs", "caret": "marker" }`.

## Precondition

Both steps' preconditions hold for the chain:

- The chain has at least two cases, counting a final `else`, and every
  condition tests the same value, without side effects, as a constant
  comparison, a pattern test, an `||` of those, or one of those followed by
  `&& condition`.
- No branch contains a `break` of an enclosing loop.
- Every branch is one of `return value;`, `variable = value;` (the same
  variable in every such branch) or `throw exception;`, and the branches
  either all return or all assign, apart from those that throw.
- A chain without a final `else` returns in its branches and is followed
  directly by a `return` or `throw`, which becomes the discard arm. Otherwise
  the switch expression would throw for a value no branch matches, where the
  chain does nothing.

## Transformation

- The chain becomes `return x switch { ... };` or `variable = x switch { ... };`.
- Each branch becomes an arm, in order: `x == c` becomes `c`, `x is T t`
  becomes `T t`, `a || b` becomes `a or b`, `x is T t && condition` becomes
  `T t when condition`, and `throw e;` becomes `throw e`.
- The final `else` becomes the `_` arm, as does a `return` or `throw` after a
  chain without one, which is removed.
- Arms are written one per line, with a trailing comma after the last.

## Preserved

- Behaviour: patterns are tried in the order of the chain, and the `_` arm only
  applies when no condition held.
- A comment before the chain stays before the statement. Comments inside a
  branch are written above its arm, and a comment at the end of a branch's
  statement stays at the end of the arm.

## Limitations

- A declaration immediately before an assigning chain is not merged into the
  assignment; combine them with Join Declaration and Assignment.
- Chains of separate `if` statements that each return are not collected; only
  `else if` chains are.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `too-few-cases` | the chain has fewer than two cases |
| `different-values` | the conditions do not all test the same value |
| `unsupported-condition` | a condition is not a constant comparison or a pattern test that an arm can express |
| `side-effects` | the tested value has side effects |
| `contains-break` | a branch contains a `break` of an enclosing loop |
| `unsupported-section` | a branch does something other than return a value, assign one variable or throw |
| `different-targets` | the branches assign different variables |
| `no-default` | there is no final `else` and no `return` after the chain to become the discard arm |
