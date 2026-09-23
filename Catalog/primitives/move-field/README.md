# Move Field

Moves a field to another type. An instance field moves onto the type of
another field or property of its class (the "via") and is reached through it,
or into the class that holds its class (`into`), whose uses went through that
holder; a static field or constant moves to a named type.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the field, such as `F:Shop.Customer._street` |
| `via` | for an instance field: the field or property to move through, such as `_address` |
| `to` | the target type: required for a static field or constant; for an instance field, the one field or property of that type is used |
| `into` | instead of `via` or `to`, for an instance field: the class that holds the field's class in a field or property, such as `Customer` |

Composite recipes use the `via` form, for example
`{ "refactoring": "move-field", "target": { "symbol": "F:Shop.Customer.Street" }, "arguments": { "via": "_address" } }`,
and the `into` form to move a field back towards the class that holds its own,
as in `{ "refactoring": "move-field", "target": { "symbol": "F:Shop.Address._country" }, "arguments": { "into": "Customer" } }`.

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

With `into`:

- The member is an instance member. The `into` class, declared in the
  solution, has exactly one field or property whose type is the member's
  class: the holder.
- The holder is an instance field, or a get-only auto-property, that creates
  its object in its initializer and is never assigned. Each instance of the
  holding class then has exactly one instance of the member's class, from the
  start, whose state the member can take over.
- Every use of the member goes through the holder: `_address.Street`,
  `this._address.Street` or `customer.Address.Street`. A use inside its own
  class, or through any other instance, would have no holder to go through.
- The holding class has no member with the member's name.

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
- With `into`, the field keeps its accessibility and goes after the holding
  class's last field, and every use through the holder becomes direct:
  `_address.city` becomes `city`, or `this.city` where a local or parameter of
  that name hides it; `this._address.city` becomes `this.city`; and
  `customer.Address.city` becomes `customer.city`.

## Preserved

- The field's type, nullable annotation, modifiers other than accessibility,
  and initializer.
- Reads and writes of the field, through the via.

## Limitations

- The via must hold the same object whenever the field is used. The move does
  not check that the via is assigned before the field is first used, for
  example in a constructor that sets the field before the via.
- The field is always appended at the end of the target type, except with
  `into`.
- With `into`, the field's initializer runs among the holding class's field
  initializers rather than when the holder creates its object.

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
| `holder-not-found` | with `into`, the class has no field or property of the member's class |
| `several-holders` | with `into`, the class has several fields or properties of the member's class |
| `holder-not-created` | with `into`, the holder is static, does not create its object in its initializer, or is assigned |
| `used-outside-holder` | with `into`, a use of the member does not go through the holder |
