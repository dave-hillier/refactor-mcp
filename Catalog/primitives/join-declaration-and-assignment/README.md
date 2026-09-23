# Join Declaration and Assignment

Joins a local declaration that has no initializer with the first assignment to
the local: `T x;` followed by `x = e;` becomes `T x = e;`. The reverse of Split
Declaration and Assignment.

## Target

The local, by a caret on its declaration or on any use of it. In a later step
of a composite, the containing member's symbol with `arguments.local` naming
the local.

## Precondition

- The local is declared by a local declaration statement in a block or switch
  section, without an initializer, and the statement declares only that
  local.
- The first statement after the declaration that mentions the local is a
  plain assignment to it (`x = e;`) in the same block, and `e` does not read
  the local. An assignment inside a nested block, a compound assignment, or a
  use as an `out` or `ref` argument does not qualify.

## Transformation

- The declaration moves down to the assignment and takes its value. The type
  is kept as written.
- Statements between the declaration and the assignment stay where they were;
  none of them mentions the local.

## Preserved

- Behaviour: the value is assigned at the same point, and the local is not
  used before it.
- Comments above the declaration move with it, below the assignment's own
  leading blank lines and comments. A comment at the end of the assignment's
  line stays; a comment at the end of the declaration's line is kept when the
  assignment has none.
- A blank line before the declaration stays before the statement that now
  follows its old place.

## Limitations

- The local's type is not changed to `var`.
- Locals declared by `for` or `using` statements are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `has-initializer` | the declaration already has a value |
| `first-use-not-assignment` | the first use of the local is not a plain assignment in the same block |
| `never-assigned` | nothing after the declaration uses the local |
| `multiple-declarators` | the statement declares several locals |
| `not-a-local` | the target is not a local variable |
