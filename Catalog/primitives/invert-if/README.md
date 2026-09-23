# Invert If

Negates the condition of an `if` statement and swaps its branches.

## Precondition

- The caret is on an `if` statement, between `if` and the closing parenthesis
  of its condition.
- When the `if` has no `else`, it must be in a block, and one of these holds:
  - Nothing follows it, and the block is the body of a method, function or
    accessor that returns nothing, or of a loop, so that running off its end
    is an implicit `return` or `continue`.
  - Code follows it, and its branch always jumps away (returns, throws,
    breaks or continues), so that the code after it only runs when the
    condition is false.

## Transformation

- The condition is replaced by its logical negation, in its most direct form:
  `==` becomes `!=`, `<` becomes `>=`, `!x` becomes `x`, `x is T` becomes
  `x is not T`, `x is not null` becomes `x is null`, and `&&` and `||` follow
  De Morgan's laws, with parentheses added where precedence needs them.
  Anything else is negated with `!`.
- With an `else`, the branches swap. An `else if` becomes the body of the new
  `then` branch, in braces, so that the new `else` cannot attach to it.
- Without an `else`:
  - When nothing follows, the implicit exit becomes the new branch and the old
    branch follows the `if`, which introduces an early-return guard clause:
    `if (c) { A(); }` becomes `if (!c) { return; } A();`.
  - When the branch is exactly the implicit exit, such as `return;` at the end
    of a method returning nothing, the code after the `if` becomes its branch
    and the jump is dropped, which removes a guard clause.
  - Otherwise, when the code after the `if` also always jumps away, it becomes
    the branch and the old branch follows the `if`.

## Preserved

- The behaviour of every path through the statement.
- Comments inside each branch move with that branch; comments before the `if`
  stay before it.

## Limitations

- Flipping an ordering comparison is only behaviour-preserving when the values
  are totally ordered. Comparisons of floating-point values (which may be
  NaN), nullable values (which may be null) and user-defined orderings are
  negated with `!( ... )` instead. Equality is always flipped, assuming a
  user-defined `!=` is the negation of its `==`, as C# requires them in pairs.
- An `if` without an `else` in a switch section, or ending a block that is not
  a method or loop body, is refused rather than rewritten with a `goto` or a
  `break`.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `code-follows` | the `if` has no `else` and the code after it cannot become its branch, because the branch or that code can run on past the end |
| `no-implicit-exit` | the `if` has no `else`, nothing follows it, and its block does not end a method or loop body |
