# Safe Delete Type

Deletes a class, struct, record, interface, enum or delegate that nothing uses,
and the file it was alone in.

## Precondition

- Nothing outside the type's own declarations refers to it, in any project of
  the solution: not as a variable, parameter or return type, not in a base
  list, not by `new`, `typeof` or `nameof`.
- Nothing outside refers to its members either. Extension methods are called
  without naming their class, so a static class whose extension methods are in
  use is not deleted.

## Transformation

- Every declaration of the type is removed with its documentation comment:
  each part of a partial type, wherever it is.
- A file left with nothing declared in it is deleted, whatever using
  directives it had. A file that declares other types keeps them, one blank
  line apart as before.
- A namespace block left empty is removed with it.
- A nested type is removed from its containing type like any member.

## Preserved

- Every other type, including a type of the same name with a different number
  of type parameters.
- Preprocessor directives around the declaration.

## Limitations

- Uses by reflection, serialization or string names are not found.
- A file that keeps only assembly attributes or global using directives is kept.

## Error codes

| Code | Meaning |
|---|---|
| `type-referenced` | code outside the type refers to it or to one of its members |
