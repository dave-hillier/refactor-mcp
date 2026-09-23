# Change Type

Changes the declared type of a local, parameter, field, property or method
return value, typically to a base type or interface its uses need ("use base
type where possible"). Introduce Interface for Dependency uses it after
Extract Interface.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `to` | yes | The new type, as it would be written in the declaring file: `IWriter`, `IEnumerable<Order>` |
| `parameter` | no | With a method target, the parameter to change instead of the return type |

Targets:

- a field, property or method return type by symbol:
  `"target": { "symbol": "F:Shop.Report._writer" }`;
- a parameter by its method's symbol and `parameter`:
  `"target": { "symbol": "M:Shop.Report.Print(Shop.FileWriter)" }, "arguments": { "parameter": "writer", "to": "IWriter" }`;
- a local by a caret on its name: `"target": { "file": "Report.cs", "caret": "marker" }`.

## Precondition

- `to` names a type the declaring file can see, or can see once its
  namespace is imported.
- Everything still compiles: every value assigned or returned converts to the
  new type, and every use of the variable, or of the method's result, needs
  only what the new type offers. This covers callers in other files,
  overrides and interface implementations.
- Every use still reaches the same code: a call passing the variable picks the
  same overload, and a member used through the new type is the one used
  before, or one the old member overrides or implicitly implements.

## Transformation

- The type is written in place. `var` is replaced by the new type.
- A nullable declaration stays nullable: `FileWriter?` becomes `IWriter?`.
- A variable declared alongside others gets a declaration of its own, after
  the original, with the new type.
- The declaring file gains a using directive when the new type needs one,
  and a qualified name its usings already cover is shortened.

## Preserved

- Behaviour: the checks above refuse any change that would make a use reach
  different code.
- Initializers, modifiers and comments of the declaration.

## Limitations

- The new type must be named; the most general type every use allows is not
  worked out.
- Uses through operators, conversions and pattern matching are left to the
  compile check, so a user-defined conversion that differs between the two
  types is not detected.

## Error codes

| Code | Meaning |
|---|---|
| `type-not-found` | `to` names no type the file can see |
| `parameter-not-found` | the method has no parameter of that name |
| `incompatible-use` | code using the variable or result would no longer compile |
| `changes-overload` | a use would reach a different overload or member |
