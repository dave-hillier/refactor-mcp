# Remove Redundant Else

Removes the `else` of an `if` whose branch always jumps away, so the `else`'s
statements follow the `if`: `if (a) { return A(); } else { B(); }` becomes
`if (a) { return A(); }` followed by `B();`.

## Target

The `if`, by caret: `"target": { "file": "Payroll.cs", "caret": "marker" }`.

## Precondition

- The caret is on an `if` statement, between `if` and the closing parenthesis
  of its condition, and the `if` has an `else`.
- The `if` is a statement of a block or switch section, so statements can
  follow it. An `else if` is the `else` of the `if` before it, not such a
  statement.
- The `if`'s branch always jumps away: it returns, throws, breaks, continues
  or otherwise never reaches its end. The `else`'s statements then only run
  when the condition is false, wherever they are.
- No local the `else` declares is declared elsewhere in the block it joins,
  including in the `if`'s own branch, since the two would then share a scope.

## Transformation

- The `else` is removed from the `if`, and its statements follow the `if` in
  the block, the first set apart from it by a blank line. An `else` without
  braces contributes its one statement; an `else if` becomes an `if` of its
  own, with the rest of its chain.
- The condition and the branch are unchanged.

## Preserved

- The behaviour of every path through the statement.
- Comments above the `else` keyword and inside the `else` move with its
  statements, above the first of them; blank lines between them are kept.

## Limitations

- An `else` whose statements declare a local that is also declared elsewhere
  in the enclosing block is refused even when the two would not actually
  clash, such as one in a nested block of the `else`.
- An `if` with no `else` whose branch jumps away is left alone; there is
  nothing to remove.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `no-else` | the `if` has no `else` |
| `not-in-block` | the `if` is not a statement of a block or switch section, such as an `else if` |
| `branch-falls-through` | the `if`'s branch can run on past its end |
| `name-conflict` | a local the `else` declares is declared elsewhere in the block it would join |
