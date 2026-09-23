# Convert Property to Methods

Replaces a property with a `Get` method and, when it has a setter, a `Set`
method, and turns every read and write into a call.

## Precondition

- The property is not virtual, abstract, an override, an interface member or
  an interface implementation, since its whole hierarchy would have to change.
- It has no `init` accessor, which object initialisers use and no method can
  stand in for.
- `Get<Name>` and `Set<Name>`, and for an auto-property `_<name>`, are free in
  the type.
- Every write can become a call: it is not in an object initialiser, and the
  value of an assignment or increment is not used.
- A compound assignment or increment reads the property as well as writing
  it, so its receiver must be a plain name, such as `order` or `this.order`,
  rather than a call that would run twice.
- No use is inside `nameof`, whose text would change.

## Transformation

- The getter becomes `<Type> Get<Name>()` and the setter
  `void Set<Name>(<Type> value)`, in the property's place, separated by a
  blank line. Expression bodies stay expression bodies and block bodies stay
  blocks, laid out as method bodies.
- An auto-property gets a private backing field `_<name>` after the type's
  other fields, holding the property's initialiser. It is `readonly` when the
  property has no setter, and constructors that assigned such a property
  assign the field instead.
- Each method keeps the property's modifiers, with the accessor's
  accessibility where the accessor narrows it, so `private set` gives a
  private `Set` method. The getter keeps the property's documentation and
  attributes.
- A read `x.Name` becomes `x.GetName()`. An assignment `x.Name = v` becomes
  `x.SetName(v)`. `x.Name += v` becomes `x.SetName(x.GetName() + v)`, and
  `x.Name++` becomes `x.SetName(x.GetName() + 1)`.

## Preserved

- Every value read and written, given the precondition.
- Comments inside accessor bodies.

## Limitations

- `??=` has no single-call equivalent and is refused.
- Code outside the solution that uses the property is not updated.

## Error codes

| Code | Meaning |
|---|---|
| `in-hierarchy` | the property is virtual, abstract, an override or part of an interface |
| `init-accessor` | the property has an `init` accessor |
| `name-conflict` | a method or field name the conversion needs is taken |
| `used-in-object-initializer` | an object initialiser sets the property |
| `assignment-used-as-value` | the value of an assignment to the property is used |
| `increment-used-as-value` | the value of an increment or decrement of the property is used |
| `unsupported-assignment` | a compound assignment, such as `??=`, has no call equivalent |
| `receiver-evaluated-twice` | a compound write's receiver is not a plain name |
| `used-in-nameof` | a use is inside `nameof` |
