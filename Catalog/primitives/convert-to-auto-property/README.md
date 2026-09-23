# Convert to Auto-Property

Replaces a property whose accessors only read and write a private field with
an auto-property, and removes the field.

## Precondition

- The property's getter only returns a field, as `_f` or `this._f`, whether
  written as `get { return _f; }`, `get => _f;` or an expression body.
- Each `set` or `init` accessor only assigns `value` to that same field.
- The field is private, declared in the same type, of the same type and
  equally static.
- Nothing passes the field by `ref`, `out` or `in`.
- The property is not already an auto-property.

## Transformation

- The accessors lose their bodies and sit on one line, `{ get; set; }`,
  keeping their modifiers and attributes.
- The field's initialiser becomes the property's initialiser.
- Other uses of the field, in any part of a partial type, use the property
  instead; `this._f` becomes `this.Name`.
- A get-only property keeps no setter when only constructors write the field,
  since constructors may assign a get-only auto-property. When the type writes
  the field anywhere else, the property gets `private set`.
- The field is removed with its comments; the property keeps its own.

## Preserved

- Every read and write of the value, and the property's accessibility and
  accessor modifiers.
- Nullable annotations, including on a property of a type parameter.

## Limitations

- Comments inside the accessor bodies are removed with the bodies.
- Assignments in constructors run through the property rather than the field,
  which is observable only if a derived type overrides the property.

## Error codes

| Code | Meaning |
|---|---|
| `no-backing-field` | the property is already an auto-property |
| `not-trivial` | an accessor does more than read or write a single field of the property's type |
| `backing-field-not-private` | the field is visible outside the type |
| `field-passed-by-reference` | the field is passed by reference |
