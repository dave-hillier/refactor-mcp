# Make Method Static

Turns an instance method into a static method of the same class, and updates
every call across the solution to pass what the method used to take from the
instance.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the method, such as `M:Shop.Order.Describe(System.String)` |
| `pass` | `instance` (the default) or `parameters` |
| `name` | optional, with `pass: instance`: the instance parameter's name (default: the type name in camel case) |

With `pass: instance` the method takes the instance as a new first parameter.
With `pass: parameters` each instance field or property it reads becomes a new
parameter instead, named after the member in camel case, in the order the
members are first used, ahead of the existing parameters.

## Precondition

- The method is an instance method of a class, and is not virtual, abstract,
  an override or an interface implementation.
- It does not call through `base`.
- The new parameter names are not already used by a parameter or local.
- No call uses null-conditional access (`order?.Describe()`), and, when the
  signature changes, the method is not used as a method group.

With `pass: parameters`, additionally:

- The method reads instance fields and properties only: it assigns none,
  calls no instance method and does not use `this` on its own.
- Every member it reads is accessible at every call site.
- Calls made on an instance name it simply (a local, parameter, field or
  `this`), so the instance can be read once per parameter.

## Transformation

- `static` is added after the accessibility modifiers.
- With `pass: instance`, uses of instance members become `order.Member`, and
  `this` becomes `order`. A method that uses no instance member gains no
  parameter.
- With `pass: parameters`, uses of each member become its parameter.
- Each call `order.Describe(x)` becomes `Order.Describe(order, x)`, or
  `Order.Describe(order.Total, order.Currency, x)`; a call inside the class
  on `this` becomes `Describe(this, x)` or `Describe(Total, Currency, x)`. A
  call on a constructed generic type names it: `Box<int>.Describe(box)`.

## Preserved

- The result of every call, and the comments and documentation of the method.
- Nullable annotations of the members passed as parameters.

## Limitations

- Structs are refused, since passing a copy of the instance would lose writes.
- A recursive method cannot pass its members as parameters.

## Error codes

| Code | Meaning |
|---|---|
| `already-static` | the method is already static |
| `polymorphic-method` | the method is virtual, abstract, an override or implements an interface |
| `not-a-class` | the method belongs to a struct, record struct or interface |
| `uses-base` | the method calls through `base` |
| `assigns-instance-member` | with `parameters`, the method assigns an instance member |
| `uses-instance` | with `parameters`, the method calls an instance method or uses `this` |
| `inaccessible-member` | with `parameters`, a member is not accessible at a call site |
| `complex-receiver` | with `parameters`, a call is made on an expression that is not simple |
| `method-group` | the method is used as a method group and its signature changes |
| `conditional-access` | a call uses null-conditional access |
| `name-conflict` | a new parameter's name is already taken |
