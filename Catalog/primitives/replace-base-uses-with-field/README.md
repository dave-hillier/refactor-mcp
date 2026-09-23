# Replace Base Uses with Field

For a class that derives from another and holds a new instance of it in a
private field: the class's own uses of the members it inherits go through the
field instead, so nothing uses the part of the instance its base class makes
up. Replace Inheritance with Delegation uses it just before removing the base
class.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `field` | yes | The private field holding an instance of the base class |

The target is the class, by symbol: `"target": { "symbol": "T:Shop.Stack" }`.

## Precondition

- The target is a class deriving from a class other than `object`.
- The field is a private instance field of exactly the base class, type
  arguments included, initialised with a new instance created without
  arguments or an object initializer, and not used anywhere yet.
- The class's constructors pass no arguments to the base class, so the field's
  object and the base class part start out alike.
- Neither the class nor a subclass overrides a member of the base class,
  which the base class would call where the field's object would not.
- The class uses no protected member of the base class, which the field
  cannot reach.
- No code outside the class, subclasses included, uses an inherited member
  through an instance of the class, explicitly or implicitly (a `foreach`
  calling `GetEnumerator`), and no code converts an instance of the class to
  the base class or an interface the base class implements. Such code would
  stop seeing what the class adds. Add Delegating Member gives the class its
  own members for those uses first.

## Transformation

- Inside the class, every use of an inherited instance member through an
  implicit `this`, `this` or `base` goes through the field: `Add(value)`
  becomes `_list.Add(value)`, `this[Count - 1]` becomes
  `_list[_list.Count - 1]`, `base.Count` becomes `_list.Count`.
- Where a local or parameter shares the field's name, the field is written
  `this._list`.

## Preserved

- The behaviour of every member: the field's object holds what the base class
  part held, since both started alike and only the class reached either.
- The base class and every member the class declares.
- Comments and layout.

## Limitations

- Inherited static members are left as they are.
- Members of `object` the base class overrides, such as `ToString`, still run
  on the base class part, which no longer changes; code calling them on the
  class is not checked.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-class` | the target is not a class |
| `no-base-class` | the class derives only from `object` |
| `field-not-found` | the class has no field of that name |
| `field-type-not-base` | the field's type is not the base class |
| `field-not-private` | the field is not a private instance field |
| `not-created-by-field` | the field is not initialised with a new instance of the base class made without arguments |
| `field-in-use` | the field is already used |
| `base-constructed-with-arguments` | a constructor passes arguments to the base class |
| `overrides-base-member` | the class or a subclass overrides a member of the base class |
| `uses-protected-member` | the class uses a protected member of the base class |
| `base-members-in-use` | code outside the class uses an inherited member through an instance of it |
| `base-conversion-in-use` | code converts the class to its base class or one of the base class's interfaces |
