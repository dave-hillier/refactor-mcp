# Replace Field Uses with Base

For a class that derives from another and also holds a new instance of it in
a private field: uses of the field's members become uses of the members the
class inherits, and the field is removed, so the instance itself takes the
field's place. Replace Delegation with Inheritance uses it once the class
derives from the field's class.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `field` | yes | The private field holding an instance of the base class |

The target is the class, by symbol: `"target": { "symbol": "T:Staff.Employee" }`.

## Precondition

- The target is a class deriving from a class other than `object`.
- The field is a private instance field of exactly the base class, type
  arguments included, initialised with a new instance created without
  arguments or an object initializer.
- The class's constructors pass no arguments to the base class, so the
  instance's base class part and the field's object start out alike.
- Neither the class nor a subclass overrides a member of the base class,
  which the base class would call where the field's object would not.
- The field is never assigned after its initialiser, and every use of it is
  `_person.Member`, `_person[...]` or the same with `this.`, so no code holds
  the field's object itself.
- Nothing uses the base class part yet: no code uses an inherited member
  through the class, inside or outside it, and no code converts an instance
  of the class to the base class or an interface the base class implements.

## Transformation

- Each `_person.M` becomes `M` where that reaches the same member, `this.M`
  where a local or parameter named `M` would otherwise be reached, and
  `base.M` where a member of the class hides the inherited one, including
  inside that member (`get => _person.Name;` becomes `get => base.Name;`).
  `_items[i]` becomes `this[i]` or `base[i]` likewise.
- The field is removed with the comments above it; directives such as
  `#region` stay.

## Preserved

- The behaviour of every member: the base class part now holds what the field
  held, since both started alike and only the field was ever used.
- Every member the class declares, and every caller of them.

## Limitations

- Members of `object` the base class overrides, such as `ToString`, now see
  the state the field held; code calling them on the class is not checked.
- A field whose object needs constructor arguments is refused rather than
  turned into a `base(...)` call.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-class` | the target is not a class |
| `no-base-class` | the class derives only from `object` |
| `field-not-found` | the class has no field of that name |
| `field-type-not-base` | the field's type is not the base class |
| `field-not-private` | the field is not a private instance field |
| `not-created-by-field` | the field is not initialised with a new instance of the base class made without arguments |
| `base-constructed-with-arguments` | a constructor passes arguments to the base class |
| `overrides-base-member` | the class or a subclass overrides a member of the base class |
| `field-assigned` | the field is assigned after its initialiser |
| `field-escapes` | the field is used as a value rather than through its members |
| `base-members-in-use` | code already uses an inherited member through the class |
| `base-conversion-in-use` | code converts the class to its base class or one of the base class's interfaces |
