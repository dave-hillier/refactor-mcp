# Split Temporary Variable

Gives a local that is reassigned for an unrelated purpose a new local from
that reassignment on. The assignment becomes the declaration of a new local,
and the uses after it refer to the new local. Applied once per reassignment,
it leaves one local per purpose.

## Target

The reassignment, by a caret on the assigned local in `x = e;`. The argument
`name` is the new local's name.

## Precondition

- The caret is on the left of a plain assignment statement to a local that is
  declared by a local declaration statement.
- The assignment is in the same block as the declaration, so it runs before
  every statement after it in that block and no later code can read the
  earlier value.
- The assigned value does not read the local, so it is unrelated to the old
  value. A compound assignment such as `x += 1` reads it.
- The local is not captured by a lambda or local function, which could read it
  after the assignment.
- `name` is not visible at the assignment and is not declared as a local or
  parameter anywhere in the member.

## Transformation

- The assignment becomes a declaration of `name` with the same value. An
  explicitly typed local's type is repeated; a `var` local's new local is
  `var` when the value has the local's type by itself, and is declared with
  the local's type otherwise.
- Every use of the local after the assignment, including later assignments,
  refers to `name`. Uses before it keep the original local.

## Preserved

- Behaviour: each use reads the same value it did.
- Comments around the assignment, and the nullable annotation of an explicit
  type.

## Limitations

- Splits at one assignment at a time. A local with three purposes takes two
  applications.
- Locals written through `ref` aliases are not detected.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-assignment` | the caret is not on the target of a plain assignment statement |
| `assignment-reads-variable` | the assignment reads the local, or is a compound assignment |
| `assignment-in-nested-block` | the assignment is in a block nested inside the declaring one |
| `captured-variable` | the local is captured by a lambda or local function |
| `name-conflict` | `name` is already declared or visible where the new local would be |
| `not-a-local` | the target is not a local variable |
