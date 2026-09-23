# Merge Nested If

Joins an `if` whose only statement is another `if` into a single `if` on
both conditions: `if (a) { if (b) { S } }` becomes `if (a && b) { S }`.

## Precondition

- The caret is on the outer `if` statement.
- The outer `if` has no `else`: it would otherwise also run when `a` holds and
  `b` does not.
- The outer `if`'s body is the inner `if` and nothing else, with or without
  braces.
- The inner `if` has no `else`: it would otherwise also run when `a` does not
  hold.

## Transformation

- The conditions are joined with `&&`, outer first, so they are evaluated in
  the same order and `b` is still only evaluated when `a` holds. A condition
  that binds more loosely than `&&`, such as one using `||`, is parenthesised.
- The inner `if`'s body becomes the body of the merged `if`.

## Preserved

- Behaviour, including short-circuiting: `b` is evaluated exactly when it was.
- Pattern variables declared by the outer condition stay in scope in the
  inner condition and the body.
- Comments above the outer `if`; comments above the inner `if` move to the top
  of the merged body, inside the braces they were in.

## Limitations

- Only a directly nested `if` is merged; an inner `if` inside a nested block
  or after a declaration is not.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `outer-has-else` | the outer `if` has an `else` |
| `inner-has-else` | the inner `if` has an `else` |
| `not-only-statement` | the outer `if`'s body has statements besides the inner `if` |
| `no-inner-if` | the outer `if`'s body is not an `if` statement |
