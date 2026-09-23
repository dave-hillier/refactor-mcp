# Inline Constant

Replaces every use of a constant field, across the solution, with its value
and removes the constant.

## Precondition

- The target is a `const` field.

## Transformation

- Each use, including a qualified one such as `Limits.MaxItems`, is replaced
  by the constant's initialiser.
- Names in the initialiser are qualified as each use needs them: `Base * 2`
  declared in `Limits` becomes `Limits.Base * 2` in another type.
- The value is parenthesised only where the use binds more tightly than the
  initialiser, as in `total / (Limits.Base * 2)`.
- A constant declared alone is removed with its comments, and the blank lines
  around it close up. A constant declared alongside others is removed from
  the declaration, which keeps the rest.

## Preserved

- Every value the code computes.
- Comments at each use.

## Limitations

- A use inside `nameof` is replaced like any other, which does not compile.
- Code outside the solution that uses a public constant is not updated.
- Preprocessor directives in the constant's leading trivia are removed with
  it.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-constant` | the targeted field is not `const` |
