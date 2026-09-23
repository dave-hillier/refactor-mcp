# Move Property

Moves a property to another type. An instance property moves onto the type of
a field or property of its class (the "via") and is reached through it, or
into the class that holds its class (`into`), whose uses went through that
holder; a static property moves to a named type.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the property, such as `P:Shop.Customer.Street` |
| `via` | for an instance property: the field or property to move through |
| `to` | the target type: required for a static property; for an instance property, the one field or property of that type is used |
| `into` | instead of `via` or `to`, for an instance property: the class that holds the property's class in a field or property, such as `Customer` |

## Precondition

- The property is not an indexer, and is not virtual, abstract, an override or
  an interface implementation.
- An instance property's accessors use nothing of its class except the via,
  since a property cannot take the instance as a parameter. An auto-property
  qualifies.
- The target and via meet the same conditions as for Move Field.
- Every use can access the via, and none sets the property in an object
  initializer or uses null-conditional access.

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
- The accessors may use other members of the property's class; they reach
  them through the holder.

## Transformation

- The property, with its accessors, initializer, documentation and comments,
  is appended to the target type. A private property becomes `internal`.
- In its accessors, uses of the via become `this`: `_address.City` becomes
  `City`.
- Every use goes through the via (`Street` becomes `Address.Street`,
  `customer.Street` becomes `customer.Address.Street`), or, for a static
  property, names the target type.
- With `into`, the property keeps its accessibility and is appended to the
  holding class. Its accessors reach the rest of its old class through the
  holder (`Number` becomes `Address.Number`), and every use through the holder
  becomes direct: `customer.Address.Full` becomes `customer.Full`.

## Preserved

- Reads and writes of the property, through the via.
- The property's type, accessors and initializer.

## Limitations

- A property with a backing field in its class must have the field moved
  first; its accessors use that field, so the move is refused.
- The via must hold the same object whenever the property is used.

## Error codes

| Code | Meaning |
|---|---|
| `no-reference-to-target` | no field or property of the class has the `to` type |
| `ambiguous-target` | several fields or properties have the `to` type |
| `via-not-found` | no field or property has the via's name |
| `static-member-via` | a static property was given a via; name the target type instead |
| `target-not-in-source` | the target type is not declared in the solution |
| `target-not-class` | the via's type is not a class |
| `same-type` | the target is the property's own class |
| `member-exists` | the target already has a member with the property's name |
| `polymorphic-method` | the property is virtual, abstract, an override or implements an interface |
| `uses-source-members` | the accessors use other members of the property's class |
| `object-initializer` | an object initializer sets the property |
| `via-not-accessible` | a use of the property cannot access the via |
| `conditional-access` | a use goes through null-conditional access |
| `holder-not-found` | with `into`, the class has no field or property of the member's class |
| `several-holders` | with `into`, the class has several fields or properties of the member's class |
| `holder-not-created` | with `into`, the holder is static, does not create its object in its initializer, or is assigned |
| `used-outside-holder` | with `into`, a use of the member does not go through the holder |
