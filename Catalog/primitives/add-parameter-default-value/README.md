# Add Default Value to Parameter

Makes a parameter optional by giving it a default value, and optionally drops
the arguments that pass that same value.

## Arguments

| Argument | Meaning |
|---|---|
| `parameter` | the name of the parameter |
| `value` | the default, a C# constant expression |
| `removeFromCallSites` | when `true`, drop arguments that pass the default; `false` if left out |

The target is the method or constructor, by symbol.

## Precondition

- The parameter has no default yet. Changing an existing default would change
  what calls that leave it out pass.
- It is not `ref`, `out`, `in`, `params` or the `this` of an extension method.
- Every parameter after it is optional or a params array.
- The value is a compile-time constant the parameter's type can take.
- Every member of the method's family is declared in the solution.
- The result compiles.

## Transformation

- The parameter gets `= value` on the method and on every override, interface
  member and implementation related to it, so a call through any of them sees
  the same default.
- With `removeFromCallSites`, every argument whose constant value equals the
  default is dropped, whether positional or named. When a dropped argument is
  followed by another, the one after it is named.

## Preserved

- What every call passes: a dropped argument passed the default.
- A comment after the parameter's name stays after the default.

## Limitations

- Only arguments that are constants are compared with the default; a
  variable that happens to hold the default is kept.
- Numbers of different types count as equal when their values are, which is
  what they become once converted to the parameter's type.

## Error codes

| Code | Meaning |
|---|---|
| `already-optional` | the parameter already has a default |
| `ref-or-out-parameter` | the parameter is `ref`, `out`, `in`, `params` or `this` |
| `later-parameter-required` | a parameter after it has no default |
| `not-constant` | the value is not a compile-time constant |
| `incompatible-value` | the value cannot be converted to the parameter's type |
