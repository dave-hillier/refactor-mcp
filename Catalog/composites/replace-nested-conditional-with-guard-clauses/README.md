# Replace Nested Conditional with Guard Clauses

Flattens nested conditionals so that each special case is checked by a guard
clause that leaves early, and the normal path runs unindented after them.

## Recipe

Starting with the outermost `if` and repeating on the `if` that each step
leaves last, one of these steps, chosen by the shape of the `if`:

- It has an `else`, and its branch always jumps away: Remove Redundant Else,
  so the `else`'s statements follow it.
  `{ "refactoring": "remove-redundant-else", "target": { "file": "Payroll.cs", "caret": "marker" } }`
- It has an `else` that always jumps away, and its branch does not: Invert If,
  which swaps the branches, then Remove Redundant Else on the same `if`.
  `{ "refactoring": "invert-if", "target": { "file": "Shipping.cs", "caret": "marker" } }`,
  `{ "refactoring": "remove-redundant-else", "target": { "file": "Shipping.cs", "range": "9:13-9:13" } }`
- It has no `else` and ends a method or loop body: Invert If, which turns it
  into an early `return` or `continue` with the old branch after it.
  `{ "refactoring": "invert-if", "target": { "file": "Shipping.cs", "range": "22:13-22:13" } }`

A later step cannot use a marker, so it gives the caret as a `range` whose
start is the caret, on the `if` as the previous steps left it.

## Target

The outermost `if`, by caret: `"target": { "file": "Shipping.cs", "caret": "marker" }`.

## Precondition

- The caret is on an `if` statement in a block.
- The `if` can become a guard clause, in one of these ways:
  - It has an `else`, and its branch always jumps away (returns, throws,
    breaks or continues).
  - It has an `else` that always jumps away, and its branch does not.
  - It has no `else`, nothing follows it, and its block is the body of a
    method, function or accessor returning nothing, or of a loop.

## Transformation

Starting with the `if` at the caret:

- When its branch always jumps away, the `else` is removed and its statements
  follow the `if`: `if (a) { return A(); } else { return B(); }` becomes
  `if (a) { return A(); }` followed by `return B();`. An `else if` becomes an
  `if` of its own.
- When only the `else` always jumps away, the condition is negated, the
  `else` becomes the branch, and the old branch follows the `if`.
- When it has no `else` and ends a method or loop body, the condition is
  negated, the branch becomes `return;` or `continue;`, and the old branch
  follows the `if`, as Invert If does.

The statements that now follow the guard clause are set apart from it by a
blank line. When the last of them is an `if`, it is flattened the same way,
until no nesting that can become a guard clause is left.

Conditions are negated as Invert If negates them: comparisons flip where that
is exact, `!x` becomes `x`, and `&&` and `||` follow De Morgan's laws.

## Preserved

- The behaviour of every path through the statement.
- Comments inside each branch move with that branch; a comment above a nested
  `if` stays above it.

## Limitations

- Only the last statement of what a guard clause guarded is flattened further.
- Branches that assign a result variable returned after the conditional are not
  turned into returns; the nesting is kept.
- A value-returning method whose nested `if` has no `else` has no implicit
  exit, so that `if` is left as it is.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `not-in-block` | the `if` is not a statement of a block, such as an `else if` |
| `no-guard-clause` | the `if` has no `else` after a branch that always jumps away, and does not end a method or loop body |
