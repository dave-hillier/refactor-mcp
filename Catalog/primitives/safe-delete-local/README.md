# Safe Delete Local

Deletes a local variable that nothing uses, keeping whatever its initializer
did beyond producing a value.

## Precondition

- The caret is on a local declared by a local declaration statement.
- Nothing reads or assigns the local after its declaration.
- The local is not a `using` declaration, whose disposal at the end of the
  scope is behaviour.

## Transformation

- A declaration whose initializer has no side effects is removed. Comments
  above it stay, above the statement that follows.
- An initializer with side effects, one that calls a method, creates an
  object, assigns, awaits or indexes, is kept:
  - as a statement of its own when C# allows one, so `var loaded = Load();`
    becomes `Load();`;
  - otherwise as a discard, so `var total = Load() + 1;` becomes
    `_ = Load() + 1;`.
- A local declared alongside others loses just its declarator. When its
  initializer is kept, the declaration is split around it so every
  initializer still runs in the order written:
  `int first = Next(), second = Next(), third = Next();` becomes three
  statements.

## Preserved

- The order and number of evaluations of every initializer.
- Comments above the declaration.

## Limitations

- Property reads are treated as free of side effects, so
  `var count = list.Count;` is removed outright.
- A local that is only written, never read, is refused rather than deleted
  with its assignments.
- Locals declared by `for`, `foreach`, `using` statements, patterns and `out`
  arguments are not offered.

## Error codes

| Code | Meaning |
|---|---|
| `local-referenced` | the local is read or assigned after its declaration |
| `using-declaration` | the local is a `using` declaration |
| `not-a-local` | the caret is not on a local variable |
