# Convert Auto-Property to Property with Backing Field

Gives an auto-property an explicit private field and accessors that read and
write it, so the accessors can later gain behaviour. The reverse of Convert
to Auto-Property.

## Precondition

- The property is an auto-property: every accessor lacks a body, and the
  property is not abstract, extern or an interface member.
- The field name, from the `name` argument or `_camelCase` of the property,
  is free in the type.

## Transformation

- A `private` field of the property's type is added after the type's
  existing fields, or first in the type. It is `static` when the property is,
  and `readonly` when the property has no `set` accessor, since constructors
  and `init` accessors may assign a readonly field. The property's initialiser
  moves to the field.
- A get-only property becomes an expression-bodied property returning the
  field. Constructors that assigned the property, in any part of a partial
  type, assign the field instead.
- Otherwise each accessor gets an expression body, one to a line:
  `get => _name;`, `set => _name = value;` or `init => _name = value;`,
  keeping its modifiers.
- The property keeps its documentation, attributes and accessibility.

## Preserved

- Every read and write of the value, including object initialisers that use
  an `init` accessor.
- Nullable annotations.

## Limitations

- Attributes on individual accessors are not carried over.
- Attributes targeting the compiler-generated field, `[field: ...]`, are not
  moved to the new field.

## Error codes

| Code | Meaning |
|---|---|
| `not-auto-property` | the property has accessor bodies, or is abstract, extern or an interface member |
| `name-conflict` | the field name is taken |
