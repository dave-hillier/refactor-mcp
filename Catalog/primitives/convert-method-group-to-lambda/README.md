# Convert Method Group to Lambda

Replaces a method group used as a delegate, such as `Format`, with a lambda
calling it, such as `number => Format(number)`. The reverse of Convert Lambda
to Method Group.

## Target

The method group, by a caret on the method's name.

## Precondition

- The name is a method group converted to a delegate type, not a call.
- The receiver, if any, has the same value whenever the delegate could run:
  no receiver, `this`, `base`, a type, a readonly field of such a receiver,
  or a local or value parameter never assigned after its declaration. A
  method group evaluates its receiver once; a lambda evaluates it on every
  call.
- As a lambda, the expression calls the same method, converts to the same
  delegate type, and leaves the overload of any call it is passed to
  unchanged, with no new compile errors. A method group with a natural type,
  as in `var format = Format;`, has no lambda without parameter types to
  replace it.

## Transformation

- The lambda has one parameter per parameter of the delegate, named after the
  method's parameters, and calls the method with them in order, keeping the
  receiver and any type arguments written on the group.
- A name already used by a local, a parameter or the receiver where the lambda
  goes gets a number: `number` becomes `number1`.
- A single parameter is written without parentheses and a delegate without
  parameters gets `()`.
- When a parameter is `ref`, `out` or `in`, every parameter is written with
  its type and the call passes each with its ref kind.

## Preserved

- Behaviour: the delegate calls the same method with the same arguments.
- Comments before and after the method group.

## Limitations

- A readonly field is treated as stable even while a constructor is still
  assigning it.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-method-group` | the caret is not on a method group converted to a delegate |
| `unstable-receiver` | the receiver could change between creating the delegate and calling it |
| `resolution-changes` | the lambda would bind to a different method, delegate type or overload |
