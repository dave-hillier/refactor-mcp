# Split If

Splits an `if` whose condition is joined by `&&` or `||` into two `if`
statements, the reverse of Merge Nested If and of Consolidate Conditional
Expression.

## Precondition

- The caret is on an `if` statement.
- The condition, ignoring enclosing parentheses, is an `&&` or an `||`.
- For `&&`, the `if` has no `else`: splitting would need the `else` on both
  the outer and the inner `if`, duplicating it.

## Transformation

The condition is split at its first operator, so `a && b && c` splits into `a`
and `b && c`.

- `if (a && b) S` becomes `if (a) { if (b) S }`.
- `if (a || b) S else E` becomes `if (a) S else if (b) S else E`, duplicating
  `S`. This always preserves behaviour: `b` is only evaluated when `a` is
  false, and `S` runs at most once.
- When there is no `else` and `S` always jumps away (it returns, throws,
  breaks or continues), the second `if` need not be an `else`:
  `if (a || b) { return; }` becomes `if (a) { return; }` followed by
  `if (b) { return; }`. Control only reaches the second `if` when `a` is false,
  exactly when `b` was evaluated before.

## Preserved

- Behaviour, including the order and short-circuiting of the conditions.
- Pattern variables declared by the first operand stay in scope in the second
  and in the body.
- Comments before the `if` stay before the first `if`.

## Limitations

- The split is always at the first operator; to split elsewhere, split
  repeatedly or add parentheses first.
- Splitting on `||` duplicates the body. Consolidate it again with Extract
  Method if it is long.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `not-splittable` | the condition is not joined by `&&` or `||` |
| `and-with-else` | the condition is joined by `&&` and the `if` has an `else` |
