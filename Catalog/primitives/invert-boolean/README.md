# Invert Boolean

Inverts the meaning of a `bool` field, property, method or local: renames it,
gives it the negation of every value it was given, and negates every use, so
that `IsEnabled` can become `IsDisabled` without changing behaviour.

## Precondition

- The target is a field, a property (not an indexer), a method or a local
  whose type is `bool`. A `bool?` is refused, since negation keeps its null.
- It is not virtual, abstract, an override, an interface member or an
  interface implementation: the rest of its hierarchy would have to be
  inverted too.
- A property has no setter or `init` accessor with a body, which would have
  to store the negation of the value it is given.
- It is never passed by `ref` or `out`, where what is written through the
  reference cannot be negated.
- A method is always called, never used as a method group, whose eventual
  callers cannot be negated.
- An assignment to it is not used as a value.
- A local is declared by a local declaration statement.
- The new name is not already used by a member of the type, or, for a local,
  in its method.

## Transformation

- The symbol is renamed, with every reference, in every file.
- Every value it is given is negated: a field's or local's initialiser, an
  auto-property's initialiser, each `return` of a method or getter and an
  expression body, and the right-hand side of every assignment, including in
  object initialisers. Negation uses the same rules as Invert If: comparisons
  flip, `!x` becomes `x`, and `&&` and `||` follow De Morgan's laws.
- A field or auto-property without an initialiser defaulted to `false`, so it
  is initialised to `true`.
- `x &= v` becomes `x |= !v`, and `x |= v` becomes `x &= !v`; `x ^= v` is
  unchanged, since `!a ^ b` is `!(a ^ b)`.
- Every read becomes its negation: `!x` becomes `x`, and any other read `x`
  becomes `!x`, parenthesised where precedence needs it.
- A reference in `nameof` is renamed but not negated.

## Preserved

- Behaviour: at every point the new symbol holds the negation of what the old
  one held, and every read sees the value it saw before.
- Comments on the declaration and around every use.

## Limitations

- A local without an initialiser is left without one; each assignment to it is
  negated.
- Parameters are not inverted, since every caller would have to pass the
  negation.
- Negating a read can leave `!x == false` and similar forms that a person
  would simplify further.

## Arguments

| Argument | Meaning |
|---|---|
| `name` | the new name for the inverted symbol |

## Error codes

| Code | Meaning |
|---|---|
| `unsupported-symbol` | the target is not a field, property, method or local |
| `not-boolean` | the symbol's type is not `bool` |
| `in-hierarchy` | the symbol is virtual, abstract, an override or part of an interface |
| `setter-with-body` | the property's setter has a body |
| `passed-by-reference` | the symbol is passed by `ref` or `out` |
| `used-as-method-group` | the method is used without being called |
| `assignment-used-as-value` | the value of an assignment to the symbol is used |
| `name-conflict` | the new name is already in use |
