# Inline Field

Replaces every read of a field that is only ever assigned by its initialiser
with that initialiser, across the solution, and removes the field.

## Precondition

- The field has an initialiser.
- Nothing assigns the field, increments it or passes it by `ref` or `out`,
  including constructors.
- Evaluating the initialiser again at each use gives the same value with no
  side effects: it is built from constants, literals, `default`, `typeof`,
  static readonly or constant fields, and built-in operators and casts. An
  initialiser that creates an object or calls a method is refused, because
  each use would get a new object or a new call.
- No use of the field is inside `nameof`.

## Transformation

- Each read, including `this._field` and `Type.Field`, is replaced by the
  initialiser, qualified and parenthesised as the use needs, as Inline
  Constant does.
- The field is removed with its comments, and the blank lines around it close
  up. A field declared alongside others is removed from the declaration only.

## Preserved

- Every value the code reads, given the precondition.
- Nullable annotations on the code that used the field.

## Limitations

- A static readonly field read from another static initialiser of its own
  type could observe the field before it was initialised; inlining gives the
  initialised value instead.
- Code outside the solution that uses a public field is not updated.

## Error codes

| Code | Meaning |
|---|---|
| `no-initializer` | the field has no initialiser |
| `written-after-initialization` | the field is assigned somewhere other than its initialiser |
| `initializer-not-inlinable` | the initialiser could give a different value or have side effects when evaluated again |
| `used-in-nameof` | a use of the field is inside `nameof` |
