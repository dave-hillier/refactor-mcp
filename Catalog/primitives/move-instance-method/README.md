# Move Instance Method

Moves an instance method onto the type of one of its class's fields or
properties, or of one of its own parameters. That field, property or parameter
(the "via") becomes `this` in the method's new home.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the method, such as `M:Shop.Customer.Label` |
| `via` | the field, property or parameter to move through, such as `_address` |
| `to` | instead of `via`: the target type's name; the one field, property or parameter of that type is used |
| `stub` | `true` (the default) leaves a delegating method behind; `false` removes it and updates every call |

Composite recipes use the `via` form, for example
`{ "refactoring": "move-instance-method", "target": { "symbol": "M:Shop.Customer.FormatAddress" }, "arguments": { "via": "_address" } }`.

## Precondition

- The method is an ordinary instance method, not virtual, abstract, an
  override or an interface implementation, and does not call through `base`
  or call itself.
- The via exists, and with `to`, exactly one field, property or parameter has
  the target type. The via's type is a class or struct the solution declares,
  is not the method's own class, and is not a constructed generic type.
- The method does not assign the via.
- The target has no member of the same name, other than methods with
  different parameters.
- The method uses no protected member of its class.
- When the method still needs its class's instance, the target's project can
  see the class's project.

Without a stub, additionally:

- Every caller can access a via field or property.
- The method is only called, not used as a method group or through
  null-conditional access.
- A call whose receiver would be evaluated twice (because the instance is also
  passed) or dropped (because a parameter becomes the receiver) is made on a
  simple name, not an arbitrary expression.
- No call passes `null` for a via parameter.

## Transformation

- The method is appended to the target type. Uses of the via become `this`:
  `_address.Street` becomes `Street`, or `this.Street` where a local of that
  name hides it. A via parameter is dropped from the parameter list.
- When the method uses other members of its class, it takes the instance as a
  new first parameter named after the class in camel case (`Customer
  customer`), and those uses go through it: `Name` becomes `customer.Name`,
  `this` becomes `customer`. Static members of the class are qualified by it.
- Private members of the class the moved method still uses become `internal`;
  a private method moves as `internal`.
- Types the method names are imported into the target file as needed, and the
  source file drops usings only the method needed.
- With a stub, the original method keeps its signature and documentation
  comment and calls the moved one: `return _address.Label(this);`. Ordinary
  comments above the method move with it.
- Without a stub, the original is removed and calls are rewritten:
  `customer.Label()` becomes `customer.Address.Label(customer)`, and a call
  `warehouse.Track(parcel, "x")` through a via parameter becomes
  `parcel.Track("x")`.

## Preserved

- The behaviour of every call.
- The method's body, comments and documentation.

## Limitations

- Private members are raised to `internal` rather than exposed through a
  narrower mechanism.
- A method whose target type is a constructed generic (`Repository<Order>`) is
  refused rather than rewritten against the type's parameters.
- The method is always appended at the end of the target type.

## Error codes

| Code | Meaning |
|---|---|
| `method-is-static` | the method is static; Move Static Method moves it |
| `via-not-found` | no field, property or parameter has the via's name |
| `no-reference-to-target` | no field, property or parameter has the `to` type |
| `ambiguous-target` | several fields, properties or parameters have the `to` type |
| `target-not-in-source` | the via's type is not declared in the solution |
| `target-not-class` | the via's type is not a class or struct |
| `generic-target` | the via's type is a constructed generic type |
| `same-type` | the via's type is the method's own class |
| `member-exists` | the target already has a member with that name and parameters |
| `polymorphic-method` | the method is virtual, abstract, an override or implements an interface |
| `uses-base` | the method calls through `base` |
| `recursive-method` | the method calls itself |
| `uses-protected-member` | the method uses a protected member of its class |
| `via-assigned` | the method assigns the via |
| `via-not-accessible` | without a stub, a caller cannot access the via |
| `method-group` | without a stub, the method is used as a method group |
| `conditional-access` | without a stub, a call uses null-conditional access |
| `complex-receiver` | without a stub, a call's receiver is not a simple name |
| `null-argument` | without a stub, a call passes `null` for the via parameter |
| `name-conflict` | the new instance parameter's name is already taken |
| `target-cannot-see-source` | the target's project cannot see the method's class |
