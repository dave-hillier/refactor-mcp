# Encapsulate Field

Makes a field private behind a property that reads and writes it, and points
code outside the declaring type at the property.

## Precondition

- The target is a field that is not `const`, declared on its own rather than
  alongside other fields.
- Code outside the declaring type does not pass the field by `ref`, `out` or
  `in`, which a property cannot be.
- The property name, and the field's new name when it needs one, are free in
  the type.

## Transformation

- The property is named by the `name` argument, or from the field: `_title`
  and `m_title` give `Title`, `title` gives `Title`.
- When the field already has the property's name, as a public field
  `Quantity` does, the field is renamed to `_quantity`.
- The field becomes `private`, keeping `static`, `readonly` and its other
  modifiers and its initialiser.
- The property has the field's accessibility, or is `public` when the field
  was private, is `static` when the field is, and is placed after the type's
  fields. It reads and writes the field through expression-bodied accessors,
  one to a line; a readonly field gets a get-only expression-bodied property.
- References outside the declaring type, including subclasses and other
  files, use the property. References inside it, including nested types and
  other parts of a partial type, keep using the field.
- The field's documentation comment moves to the property; ordinary comments
  stay with the field.

## Preserved

- Every read and write, including compound assignments and increments, which
  work the same through the property.
- Nullable annotations on the field's type, which the property carries.

## Limitations

- Code outside the solution that used a public field is not updated, and
  becomes a binary-breaking change for compiled consumers.
- Uses inside the type are not redirected to the property; a later
  refactoring can do that when the property gains behaviour.

## Error codes

| Code | Meaning |
|---|---|
| `constant-field` | the field is `const` |
| `multiple-declarators` | the field is declared alongside others |
| `name-conflict` | the property name, or the field's new name, is taken |
| `passed-by-reference` | code outside the type passes the field by reference |
