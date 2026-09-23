# Move Field

Moves a field to another type. An instance field moves onto the type of
another field or property of its class (the "via") and is reached through it;
a static field or constant moves to a named type.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the field, such as `F:Shop.Customer._street` |
| `via` | for an instance field: the field or property to move through, such as `_address` |
| `to` | the target type: required for a static field or constant; for an instance field, the one field or property of that type is used |

Composite recipes use the `via` form, for example
`{ "refactoring": "move-field", "target": { "symbol": "F:Shop.Customer.Street" }, "arguments": { "via": "_address" } }`.

## Precondition

- An instance field has a via: a field or property of its class whose type is
  a class declared in the solution, other than the field's own class. With
  `to`, exactly one field or property has that type.
- A static field or constant has a target type declared in the solution.
- The target has no member with the field's name.
- An instance field that is `readonly` is not assigned outside its
  initializer, since the assignment could not go through the via.
- Every use of an instance field can access the via, and none sets it in an
  object initializer or uses null-conditional access.

## Transformation

- The field, with its initializer and the comments above it, is appended to
  the target type. A private field becomes `internal` so its old class can
  still reach it. A field declared alongside others is split out of its
  declaration.
- Every use of an instance field goes through the via: `_street` becomes
  `_address._street`, `this._street` becomes `this._address._street`, and
  `customer.Postcode` in another file becomes `customer.Address.Postcode`.
- Every use of a static field or constant names the target type:
  `DefaultQuantity` becomes `Defaults.DefaultQuantity`.

## Preserved

- The field's type, nullable annotation, modifiers other than accessibility,
  and initializer.
- Reads and writes of the field, through the via.

## Limitations

- The via must hold the same object whenever the field is used. The move does
  not check that the via is assigned before the field is first used, for
  example in a constructor that sets the field before the via.
- The field is always appended at the end of the target type.

## Error codes

| Code | Meaning |
|---|---|
| `no-reference-to-target` | no field or property of the class has the `to` type |
| `ambiguous-target` | several fields or properties have the `to` type |
| `via-not-found` | no field or property has the via's name |
| `static-member-via` | a static field was given a via; name the target type instead |
| `target-not-in-source` | the target type is not declared in the solution |
| `target-not-class` | the via's type is not a class |
| `same-type` | the target is the field's own class |
| `member-exists` | the target already has a member with the field's name |
| `readonly-assigned` | a `readonly` instance field is assigned outside its initializer |
| `object-initializer` | an object initializer sets the field |
| `via-not-accessible` | a use of the field cannot access the via |
| `conditional-access` | a use of the field goes through null-conditional access |
