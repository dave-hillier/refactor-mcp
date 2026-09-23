# Move Multiple Methods

Moves several methods of a class to another type in one operation: each
instance method through a field, property or parameter of the target type,
each static method to the target type by name.

## Recipe

For each method, callees before their callers:

- `move-instance-method` for an instance method:
  `"target": { "symbol": "M:Shop.Customer.Street" }, "arguments": { "via": "_address" }`;
- `move-static-method` for a static method:
  `"target": { "symbol": "M:Shop.Order.Round(System.Decimal)" }, "arguments": { "to": "Shop.Pricing" }`.

Every step passes the same `stub` choice. Moving callees first matters without
stubs: the callee's move rewrites the caller's call to go through the via
(`_address.Street()`), and the caller's move then turns that into a call on
`this` in the target. Methods that call each other in a cycle move in the
order they were named.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `methods` | yes | The methods to move, by name; a name moves every overload |
| `via` | one of `via` and `to` | The field or property to move instance methods through; static methods move to its type |
| `to` | one of `via` and `to` | The target type: instance methods move through the one field, property or parameter of that type, static methods to it |
| `stub` | no | `true` (the default) leaves a delegating method for each moved method; `false` updates the callers |

The target is the class declaring the methods, by symbol: `"target": { "symbol": "T:Shop.Customer" }`.

## Precondition

- Every named method exists in the class.
- Each move meets the precondition of Move Instance Method or Move Static
  Method, checked against the code as the earlier moves left it.

## Transformation

- Each method moves as its primitive moves it: uses of the via become `this`,
  uses of what stays behind go through a parameter of the old class, private
  members become `internal`, and static members the method uses are qualified
  by their type.
- With stubs, each old method delegates to the moved one. A moved method that
  calls another moved method reaches it through the other's stub
  (`customer.Street()`).
- Without stubs, every call is rewritten to reach the moved method.

## Preserved

- The behaviour of every call.
- Comments and documentation of the moved methods.

## Limitations

- With stubs, calls between moved methods still go through the old class's
  stubs rather than straight to the moved methods, as the recipe leaves them.
- A move that refuses part way through is reported by its step number in the
  callee-first order, not the order the methods were named in.

## Error codes

| Code | Meaning |
|---|---|
| `method-not-found` | a named method does not exist in the class |
| `via-not-found` | the class has no field or property with the via's name |
| `no-reference-to-target` | no field, property or parameter has the `to` type |
| `polymorphic-method` | a method is virtual, abstract, an override or an interface implementation |
| `uses-protected-member` | a method uses a protected member of the class |
| `via-not-accessible` | without stubs, a caller cannot reach the via |
| `member-exists` | the target already has a method with that name and parameters |

A refusal in any step leaves every file as it was, and names the step that
refused.
