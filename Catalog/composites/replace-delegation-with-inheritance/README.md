# Replace Delegation with Inheritance

For a class that holds an object of another class and forwards most of its
members to it: the class derives from the other class instead, the members
that only forwarded are removed so the inherited ones take their place, and
the field goes.

## Recipe

1. `change-base-type` the class to the field's class:
   `"target": { "symbol": "T:Staff.Employee" }, "arguments": { "to": "Person" }`.
2. `inline-method` each forwarding method: `"target": { "symbol": "M:Staff.Employee.LastName" }`.
3. `inline-field` the field: `"target": { "symbol": "F:Staff.Employee._person" }`.

This recipe cannot finish. Inline Method puts the forwarding method's body,
`_person.LastName()`, into its callers, and refuses when a caller outside the
class cannot reach the private field. Inline Field refuses a field whose
initialiser creates an object. Neither is what the refactoring needs: a
forwarding member should be removed so callers reach the inherited member,
and the field should become the instance itself, which no primitive does. The
recipe case is kept, marked unimplemented, with the dedicated implementation's
result as its `after/`, until primitives for those steps exist.

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
