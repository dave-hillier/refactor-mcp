# Introduce Null Object

Generates a class that implements a field's interface by doing nothing, makes
the field hold it instead of null, and removes the field's null checks across
the solution.

This is a generator: it adds a type and changes what runs, so the fixtures pin
one chosen design rather than the only correct answer.

## Arguments

None. The target is the field, by symbol:
`"target": { "symbol": "F:Shop.Order._logger" }`.

## Precondition

- The field's type is an interface. A field typed as a class is refused;
  Extract Interface comes first.
- The field is checked for null at least once, and every check has one of the
  shapes listed under Transformation.
- Where checks fall back to a value for the same member, they agree on it, and
  that value is a constant.
- The interface's namespace has no type with the null object's name, and its
  folder no file with that name.
- The result compiles.

## Transformation

The generated type:

- Is named after the interface without its leading `I` and prefixed with
  `Null`: `ILogger` gives `NullLogger`. A generic interface gives a generic
  class with the same type parameters and constraints.
- Is a `public sealed class` in a new file named after it, in the interface's
  folder and namespace, following the namespace style (block or file-scoped)
  of the interface's file. When the interface is not in source, the field's
  file is used instead.
- Has a single instance, `public static readonly NullLogger Instance`, and a
  private constructor.
- Implements every abstract member of the interface and of the interfaces it
  extends, publicly and in declaration order:
  - `void` methods have empty bodies; `out` parameters are set to `default`.
  - A member whose checks fell back to a value returns that value. Otherwise
    arrays, `IEnumerable<T>`, `IReadOnlyCollection<T>` and `IReadOnlyList<T>`
    return `Array.Empty<T>()`, and everything else returns `default`
    (`default!` for a non-nullable reference type when nullable analysis is
    enabled).
  - Get-only properties are expression-bodied; a setter does nothing. Events
    have empty `add` and `remove` accessors.

The field and its uses:

- `if (_f != null) { ... }` and `if (_f is not null) ...` without an `else`
  become the statements they guarded, keeping the comments above the `if`.
- `_f?.M();` as a statement becomes `_f.M();`.
- `_f?.M() ?? fallback`, `_f != null ? _f.M() : fallback` and
  `_f == null ? fallback : _f.M()` become `_f.M()`, and M returns `fallback`
  in the null object. The same applies to properties.
- Assigning `null` assigns `NullLogger.Instance`. Assigning any other value
  that may be null assigns `value ?? NullLogger.Instance`; a new object, `this`
  or the field itself is left alone.
- A field without an initialiser that some constructor leaves unassigned is
  initialised to `NullLogger.Instance`.
- A nullable field type, `ILogger?`, becomes `ILogger`.

## Behaviour changes

- The field is never null, so code that read it without a check and would have
  thrown `NullReferenceException` now calls the null object.
- Members nobody checked return `default` (or an empty sequence) from the null
  object instead of throwing.

## Preserved

- The behaviour of every removed check: the guarded calls did nothing when the
  field was null, and the fallbacks become the null object's return values.
- Comments next to the removed checks.

## Limitations

- Checks with an `else`, guards that return early (`if (_f == null) return;`),
  `_f ?? other`, `_f ??= other` and null comparisons in other expressions are
  refused rather than rewritten.
- Fallbacks must be constants; one computed from local state is refused.
- Only a field is supported, not a property, parameter or local.
- Passing the field by `ref` or `out` is not treated as an assignment, so a
  method that sets it to null through the reference is not caught.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-interface` | the field's type is not an interface |
| `no-null-checks` | the field is never checked for null |
| `unsupported-null-check` | a null check has a shape the null object cannot replace, or falls back to a non-constant value |
| `inconsistent-fallbacks` | two checks fall back to different values for the same member |
| `type-already-exists` | the null object's name is already taken |
| `breaks-compilation` | the result would not compile |
