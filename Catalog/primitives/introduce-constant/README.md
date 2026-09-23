# Introduce Constant

Replaces a selected literal or constant expression with a named constant of
the containing type, and optionally every other occurrence of the same value
in that type. The catalog's answer to a magic number.

## Precondition

- The selection is exactly one expression whose value is known at compile
  time, other than `null`.
- The expression does not use a local constant, which a member of the type
  could not see.
- No member of the containing type already has the name.

## Transformation

- A `private const` of the expression's type is declared with the expression
  as its value, with redundant outer parentheses removed. It is placed after
  the type's existing fields and constants, or first in the type, followed by
  a blank line, when it has none.
- The selected expression is replaced by the constant's name.
- With `"replaceAll": true`, every expression in the containing type that is
  written the same way and has the same type is replaced too. `60` matches
  `60` but not `60.0`, `600` or the text inside `"60 per minute"`.

## Preserved

- Every value the code computes, since the constant has exactly the value of
  the expression it replaces.
- Comments around the replaced expression.

## Limitations

- Occurrences are matched by how they are written, so `60` does not match
  `0x3C` or `30 * 2`.
- Only the type declaration containing the selection is searched; other parts
  of a partial type are not.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-expression` | the selection is not exactly one expression |
| `not-constant` | the expression's value is not known at compile time |
| `references-local` | the expression uses a local constant |
| `name-conflict` | the type already has a member with the name |
