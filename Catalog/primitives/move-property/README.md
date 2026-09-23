# Move Property

Moves a property to another type. An instance property moves onto the type of
a field or property of its class (the "via") and is reached through it; a
static property moves to a named type.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the property, such as `P:Shop.Customer.Street` |
| `via` | for an instance property: the field or property to move through |
| `to` | the target type: required for a static property; for an instance property, the one field or property of that type is used |

## Precondition

- The property is not an indexer, and is not virtual, abstract, an override or
  an interface implementation.
- An instance property's accessors use nothing of its class except the via,
  since a property cannot take the instance as a parameter. An auto-property
  qualifies.
- The target and via meet the same conditions as for Move Field.
- Every use can access the via, and none sets the property in an object
  initializer or uses null-conditional access.

## Transformation

- The property, with its accessors, initializer, documentation and comments,
  is appended to the target type. A private property becomes `internal`.
- In its accessors, uses of the via become `this`: `_address.City` becomes
  `City`.
- Every use goes through the via (`Street` becomes `Address.Street`,
  `customer.Street` becomes `customer.Address.Street`), or, for a static
  property, names the target type.

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
