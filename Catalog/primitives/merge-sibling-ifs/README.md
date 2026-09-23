# Merge Sibling Ifs

Joins an `if` with the `if` after it when both have the same body, into one
`if` on both conditions: `if (a) { S } else if (b) { S }` and
`if (a) { return; } if (b) { return; }` become `if (a || b) { S }` and
`if (a || b) { return; }`. The counterpart of Merge Nested If, which joins
with `&&`.

## Target

The first `if`, by caret: `"target": { "file": "Disability.cs", "caret": "marker" }`.
The merged `if` stays where the first one was, so repeating the step on the
same caret joins a longer run one condition at a time.

## Precondition

- The caret is on an `if` statement, between `if` and the closing parenthesis
  of its condition.
- The `if` is followed by one of these, with the same body:
  - an `else if`, when the `if` has an `else`;
  - an `if` statement directly after it in the same block or switch section,
    when the `if` has no `else`. That `if` may have an `else` of its own.
- Bodies are the same when they are the same code; comments and layout do
  not count.
- For an `if` statement after it, the shared body always jumps away (returns,
  throws, breaks or continues). The second `if` then only runs when the first
  condition was false, exactly as `||` evaluates it; a body that can fall
  through would run once for each condition that holds.
- Neither condition declares a pattern or `out` variable, which would be
  unassigned when the other condition holds.

## Transformation

- The conditions are joined with `||`, first then second, parenthesised only
  where precedence requires it: an operand joined by `&&` needs none, a
  conditional or assignment does.
- The first body is kept. The second `if` is removed; its `else`, if any,
  becomes the merged `if`'s `else`, so an `else if` chain continues after the
  merged branch.

## Preserved

- Behaviour: the conditions are evaluated in the same order and short-circuit
  as before, and the body runs exactly when it did.
- Comments above the first `if`, and comments above the second `if`, which
  follow them above the merged `if`. Comments in the first body.

## Limitations

- Only the next `if` is merged; a longer run takes one step per condition.
- Comments inside the second body are dropped with it.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `no-sibling-if` | the `if` is not followed by an `else if` or `if` statement with the same body |
| `body-falls-through` | the `if` statement after it shares a body that can fall through |
| `declares-variable` | a condition declares a pattern or `out` variable |
