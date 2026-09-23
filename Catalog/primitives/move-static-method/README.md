# Move Static Method

Moves a static method to another type, named by the caller, creating that type
as a static class when it does not exist.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the method, such as `M:Shop.Order.Vat(System.Decimal)` |
| `to` | the target type: a simple name, or a namespace-qualified one |
| `stub` | `true` (the default) leaves a delegating method behind; `false` removes it and updates every use |
| `file` | optional: the file for a target type that has to be created (default `<Type>.cs` beside the source) |

## Precondition

- The method is static and not a partial method.
- The target type, when it exists, is a class or struct declared in the
  solution, is not the method's own type, and has no method with the same name
  and parameters.
- `to` names one type; a new type's name is a valid identifier and its file
  does not exist.
- The target's project can see the old type's project.
- Without a stub, no use is through null-conditional access or an object
  initializer.

## Transformation

- The method is appended to the target type, keeping its comments.
- Static members of the old type that the method uses unqualified are
  qualified by it (`Rate` becomes `Order.Rate`), and private ones become
  `internal`. A private method moves as `internal`.
- Types and extension methods the method uses are imported into the target
  file as needed; the old file drops usings only the method needed.
- A new target type is a static class in the old type's namespace, in the same
  namespace form, as accessible as the old type.
- With a stub, the old method keeps its signature and documentation comment
  and calls the moved one: `=> TaxRules.Vat(net);`.
- Without a stub, every use, including calls inside the old type and method
  groups (`amounts.Select(Order.Vat)`), names the new type, which files in
  other namespaces import. Explicit type arguments are kept.

## Preserved

- The behaviour of every call.
- Generic type parameters and their constraints.

## Limitations

- `using static` imports of the old type are not updated.
- The method is always appended at the end of the target type.

## Error codes

| Code | Meaning |
|---|---|
| `method-not-static` | the method is an instance method; Move Instance Method moves it |
| `same-type` | the target is the method's own type |
| `member-exists` | the target already has a method with that name and parameters |
| `target-not-in-source` | the target type is not declared in the solution |
| `target-not-class` | the target type is not a class or struct |
| `uses-protected-member` | the method uses a protected member of its type |
| `conditional-access` | without a stub, a use goes through null-conditional access |
| `target-cannot-see-source` | the target's project cannot see the old type |
