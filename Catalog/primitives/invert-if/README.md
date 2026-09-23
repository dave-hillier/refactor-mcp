# Invert If

Negates the condition of an `if` statement and swaps its branches.

## Precondition

- The caret is on an `if` statement.
- When the `if` has no `else`, the statement must be the last in its block, or
  be followed only by code that the `then` branch always jumps past, so that the
  rest of the block can become the new `then` branch.

## Transformation

- The condition is replaced by its logical negation, simplified where the
  negation has a direct form: `>` becomes `<=`, `==` becomes `!=`, `!x`
  becomes `x`, and `&&` and `||` follow De Morgan's laws.
- The `then` and `else` branches are swapped.
- Without an `else`, the code after the `if` becomes the new `then` branch and
  the old `then` branch follows the `if`, which is how early-return guard
  clauses are introduced.

## Preserved

- The behaviour of every path through the statement.
- Comments attached to each branch move with that branch.

## Limitations

- Negating a comparison is not behaviour-preserving for floating-point values
  that may be NaN; such conditions are negated with `!( ... )` instead.
