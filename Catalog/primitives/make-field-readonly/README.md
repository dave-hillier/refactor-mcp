# Make Field Readonly

Adds `readonly` to a field that is only assigned while an object is being
constructed, so the compiler enforces what the code already does.

## Precondition

- The field is not `const` or `volatile`.
- Every assignment, increment and `ref` or `out` use of the field, in any
  file, is in a constructor or `init` accessor of the declaring type itself,
  through `this` rather than another instance. A static field may only be
  assigned in the static constructor. An assignment in a lambda or local
  function counts as outside the constructor, since it may run later.
- The field's type is not a mutable struct declared in the solution: calls
  through a readonly field would act on a defensive copy, so a method such as
  `Add()` would silently stop changing the field.
- The field is declared on its own, since `readonly` on a shared declaration
  would apply to every field in it.

## Transformation

- `readonly` is added after the access and `static` modifiers.
- The initialiser stays on the field. A readonly field may keep one, and
  moving it into constructors would change when it runs relative to the
  constructor body.
- A field that is already readonly is left unchanged.

## Preserved

- Everything; the change only adds a constraint the code already meets.
- Comments on the field.

## Limitations

- Structs from referenced assemblies are not inspected, so a mutable struct
  from a library is not refused.

## Error codes

| Code | Meaning |
|---|---|
| `constant-field` | the field is `const` |
| `volatile-field` | the field is `volatile` |
| `mutable-struct` | the field holds a mutable struct declared in the solution |
| `multiple-declarators` | the field is declared alongside others |
| `assigned-outside-constructor` | the field is assigned somewhere other than construction |
