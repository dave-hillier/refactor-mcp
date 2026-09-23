# Introduce Generic Type Parameter

Replaces a concrete type used in a class or method with a new type parameter,
constrained as the uses need, and passes the old type wherever the class or
method is used, so every existing use means what it did.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `type` | yes | The concrete type to replace, as written in the class's file |
| `name` | yes | The new type parameter's name |

The target is the class or the method, by symbol:
`"target": { "symbol": "T:Shop.Box" }` or
`"target": { "symbol": "M:Shop.Ledger.Load" }`.

## Precondition

- `type` names a type, and the class or method uses it.
- `name` is an identifier nothing in scope already uses: not an existing type
  parameter, type or member.
- The class or method does not reach a static member through `type`, and
  does not create it with constructor arguments; a type parameter can do
  neither.
- Some constraint makes the result compile with every use reaching the same
  members as before.

## Transformation

- Every name of `type` in the class or method becomes the type parameter:
  in declarations, casts, `typeof`, type arguments and nullable annotations.
  `var` stays `var`, and `nameof(Invoice)` is left alone because its text is
  its value.
- The constraint is the loosest that works, tried in order: none, each
  interface the type implements, then the type itself when it is a class
  that is not sealed. `new()` is added when the code creates instances.
- A class gains the parameter after any it already has; its references to
  itself take the new parameter, and every other reference to it takes the old
  type: `Box` becomes `Box<Invoice>`, `Index<int>` becomes
  `Index<int, Invoice>`, and `Box.Empty()` becomes `Box<Invoice>.Empty()`.
- A method gains the parameter; a call that cannot infer it, because no
  parameter's type mentions the old type, passes the old type explicitly,
  spelling out any type arguments it used to infer.
- The constraint clause goes at the end of the header, before the body.

## Preserved

- Every use of the class or method, which instantiates it with the old type.
- What each use of a value of the old type reaches: another overload or a
  different member is refused, so the constraint is tightened or the
  refactoring declines.

## Limitations

- Calls between the class's own members are not rechecked for overloads,
  since their signatures change together.
- References in documentation comments are left as they are.
- Only a class's or method's own declaration is changed; partial classes with
  several declarations are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `type-not-found` | `type` names no type the file can see |
| `type-not-used` | the class or method does not use `type` |
| `invalid-name` | `name` is not an identifier |
| `name-conflict` | `name` is already a type parameter, type or member in scope |
| `uses-static-members` | the code reaches a static member through `type` |
| `no-valid-constraint` | no constraint keeps the code compiling and binding as before, or instances are created with arguments |
