# Consolidate Duplicate Conditional Fragments

Moves statements that every branch of a conditional starts or ends with out of
the conditional, so they are written once.

## Recipe

Move the statements common to every branch out of the conditional: those
every branch ends with go after it, and those every branch starts with go
before it.

No primitive moves a statement out of a conditional, so the recipe has no
primitive steps and every case runs the dedicated implementation.

## Target

The first `if` of the chain, by caret: `"target": { "file": "Deal.cs", "caret": "marker" }`.

## Precondition

- The caret is on an `if` statement in a block or switch section, not on an
  `else if`.
- The chain ends with an `else`, so that some branch always runs. Without one,
  a moved statement would also run when no branch does.
- Every branch starts or ends with the same statement, compared as code
  regardless of comments and layout.
- A statement moved after the conditional uses no local that its branch
  declares.
- A statement moved before the conditional runs before the conditions rather
  than after them, so the conditions have no side effects, read no local the
  statement assigns, and, when the statement may change other state (it calls
  a method, creates an object, or assigns a field or property), read no field,
  property or method.
- The result compiles.

## Transformation

- The longest run of statements every branch ends with is removed from each
  branch and written once after the conditional, set apart from it by a blank
  line.
- The longest run of the remaining statements every branch starts with is
  removed from each branch and written once before the conditional, which is
  then set apart from them by a blank line.
- A branch left with nothing keeps its empty braces.

## Preserved

- The behaviour of every path: each moved statement runs exactly when it ran
  in whichever branch was taken.
- The comments of the first branch's statements move with them; the other
  branches' copies go with their comments.

## Limitations

- `switch` statements are not covered, only `if` / `else` chains.
- A branch left empty is not removed, and the condition is not inverted to
  drop an empty `then` branch.
- A call in a moved-before statement is assumed to change state, so a
  condition that reads a field or property keeps it in the branches even when
  the call would not affect it.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `not-in-block` | the `if` is not a statement of a block or switch section, such as an `else if` |
| `no-final-else` | the chain does not end with an `else` |
| `no-common-fragments` | the branches neither start nor end with the same statement |
| `uses-branch-local` | a statement every branch ends with uses a local its branch declares |
| `condition-depends-on-fragment` | the conditions depend on what a statement every branch starts with does |
