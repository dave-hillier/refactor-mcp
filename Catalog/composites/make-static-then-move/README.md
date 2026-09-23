# Make Static then Move

Moves an instance method to another class as a static method that takes the
instance as a parameter. Useful for moving a method out of a large class
when it has no field or parameter of the target type to move through.

## Recipe

1. `make-method-static` the method, passing the instance:
   `"target": { "symbol": "M:Shop.Order.Describe(System.String)" }`.
2. `move-static-method` the now static method to the target class:
   `"target": { "symbol": "M:Shop.Order.Describe(Shop.Order,System.String)" }, "arguments": { "to": "Receipts" }`.

The second step's symbol has the instance as its first parameter; a method
that used no instance member gains no parameter and keeps its id.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `to` | yes | The target class: a simple or namespace-qualified name. It is created as a static class when it does not exist |
| `name` | no | The instance parameter's name; defaults to the class name in camel case |
| `stub` | no | `true` (the default) leaves a static method in the old class that delegates to the moved one; `false` points every call at the target |
| `file` | no | The file for a target class that has to be created; defaults to `<to>.cs` beside the class |

The target is the method, by symbol.

## Precondition

- Make Method Static's precondition: the method is an instance method of a
  class, not virtual, abstract, an override or an interface implementation,
  does not call through `base`, and is not called through null-conditional
  access or used as a method group.
- Move Static Method's precondition: the target is a class or struct declared
  in the solution, or a free name for a new one, other than the method's own
  class, without a method of the same name and parameters.

## Transformation

- The method becomes static with the instance as its first parameter; uses of
  instance members go through it (`_total` becomes `order._total`).
- The method moves to the target; private members of the old class it uses
  become `internal`.
- Every call passes the instance: `order.Describe("x")` becomes
  `Receipts.Describe(order, "x")`, or `Order.Describe(order, "x")` when a
  stub is kept.

## Preserved

- The result of every call, and the method's comments and documentation.

## Limitations

- With a stub, callers of the old instance method still change to call the
  static stub, because making the method static changes its signature.
- Private members the method uses become `internal`.

## Error codes

| Code | Meaning |
|---|---|
| `already-static` | the method is already static; Move Static Method moves it |
| `polymorphic-method` | the method is virtual, abstract, an override or an interface implementation |
| `not-a-class` | the method belongs to a struct or interface |
| `uses-base` | the method calls through `base` |
| `method-group` | the method is used as a method group |
| `conditional-access` | a call uses null-conditional access |
| `name-conflict` | the instance parameter's name is already used in the method |
| `same-type` | the target is the method's own class |
| `member-exists` | the target already has a method with the same name and parameters |
| `target-not-class` | the target is not a class or struct |
| `uses-protected-member` | the method uses a protected member of its class |

A refusal in either step leaves every file as it was, and names the step that
refused.
