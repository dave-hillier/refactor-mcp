# Convert Method to Property

Turns a parameterless method that returns a value, such as `GetTotal()`, into
a get-only property, and every call into a property read.

## Precondition

- The target is a parameterless method that returns a value and is not
  generic or `async`.
- It should be free of side effects and cheap, as a property read is expected
  to be. The refactoring cannot prove this; choosing the method is the
  caller's judgement.
- It is not an override; convert the method it overrides, which converts every
  override with it.
- It does not implement an interface member.
- Every use calls it. A method group, such as a delegate conversion or
  `nameof`, is refused.
- The property name is free in the declaring type and in every type that
  overrides the method.

## Transformation

- The property is named by the `name` argument, or by the method name without
  a leading `Get`: `GetTotal` becomes `Total`, `Describe` stays `Describe`.
- An expression body, or a block that only returns a value and has no
  comments, becomes an expression-bodied property. Any other block becomes
  the `get` accessor. An abstract method becomes `{ get; }`.
- Modifiers, attributes and documentation are kept, so a virtual method gives
  a virtual property. Every override becomes an overriding property.
- Every call, in any file, becomes a read: `order.GetTotal()` becomes
  `order.Total`, `order?.GetTotal()` becomes `order?.Total` and
  `base.GetLabel()` becomes `base.Label`.
- Only the parameterless overload is converted; other overloads and their
  calls are left alone.

## Preserved

- The value every call site receives, given the precondition.
- Comments inside a block body.

## Limitations

- Code outside the solution that calls the method is not updated.

## Error codes

| Code | Meaning |
|---|---|
| `has-parameters` | the method, and every overload of its name, takes parameters |
| `returns-void` | the method returns nothing |
| `generic-method` | the method has type parameters |
| `async-method` | the method is `async` |
| `is-override` | the method overrides another |
| `implements-interface` | the method implements an interface member |
| `used-as-method-group` | a use of the method does not call it |
| `name-conflict` | the property name is taken in the type or an overriding type |
