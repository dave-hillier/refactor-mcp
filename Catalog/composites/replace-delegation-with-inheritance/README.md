# Replace Delegation with Inheritance

For a class that holds an object of another class and forwards most of its
members to it: the class derives from the other class instead, the members
that only forwarded are removed so the inherited ones take their place, and
the field goes.

## Recipe

1. `change-base-type` the class to the field's class:
   `"target": { "symbol": "T:Staff.Employee" }, "arguments": { "to": "Person" }`.
2. `replace-field-uses-with-base`, so uses of the field's members reach the
   inherited members and the field goes:
   `"target": { "symbol": "T:Staff.Employee" }, "arguments": { "field": "_person" }`.
   A forwarding member now reads `get => base.Name;` or
   `return base.LastName();`.
3. `remove-delegating-member` for each member that now only forwards to the
   inherited one: `"target": { "symbol": "P:Staff.Employee.Name" }`, then
   `"target": { "symbol": "M:Staff.Employee.LastName" }`. Uses written with
   `base` only because the removed member hid the inherited one become plain
   uses.

The plan's recipe, Change Base Type, Inline Method for each delegating
member, Inline Field, cannot preserve behaviour: Inline Method would put the
private field into callers outside the class, and a field holding an object
is not inlined. Turning the field into the instance itself first, then
removing the members that forward to the inherited ones, does the same job
one safe step at a time.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `field` | yes | The private field holding the object the class delegates to |

The target is the class, by symbol: `"target": { "symbol": "T:Staff.Employee" }`.

## Precondition

- The target is a single, non-static class declaration, not a record, that
  derives only from `object`.
- The field's class is a class that is neither sealed nor static.
- The field is a private instance field, initialised with a new instance of
  its own class created without arguments or an object initializer, and
  declared on its own.
- The field is never assigned after its initialiser, and every use of it is
  followed by a member access (`_person.Name`), so no code holds the object
  itself.
- No member the class keeps shares a name with an accessible member of the
  field's class, which it would hide.
- The result compiles.

## Transformation

- The field's class becomes the class's base class, first in its base list.
- A member of the class is removed when all it does is forward to the member
  of the field with the same name, signature and accessibility: a method
  whose body is `_person.M(a, b)` passing its own parameters in order, or a
  property whose getter reads `_person.P` and whose setter, if any, assigns
  `_person.P = value`.
- Every other use of the field becomes a use of the instance:
  `_person.Greeting("x")` becomes `Greeting("x")`, keeping `this.` only where
  a local or parameter would otherwise hide the member.
- The field is removed with its comments.

## Preserved

- Every call of a removed forwarding member, which now reaches the inherited
  member it forwarded to.
- The behaviour of the class's other members, given that the field's object
  was only ever used through its members.

## Limitations

- Names in the class that bound to something else, such as an extension
  method or a type, can bind to a newly inherited member of the same name;
  this is not checked.
- Other code can now convert the class to its new base class; nothing relied
  on that before, so it changes no existing behaviour.
- A field whose object needs constructor arguments is refused rather than
  turned into a `base(...)` call.

## Error codes

| Code | Meaning |
|---|---|
| `field-not-found` | the class has no field of that name |
| `not-a-class` | the target is not a single, non-static class declaration |
| `has-base-class` | the class already derives from another class |
| `invalid-base-type` | the field's class is sealed, static or not a class |
| `field-not-private` | the field is not a private instance field |
| `not-created-by-field` | the field is not initialised with a new instance of its class |
| `field-assigned` | the field is assigned after its initialiser |
| `field-escapes` | the field is used as a value rather than through its members |
| `hides-base-member` | a member the class keeps would hide a member of the new base class |
| `breaks-compilation` | the result would not compile |
