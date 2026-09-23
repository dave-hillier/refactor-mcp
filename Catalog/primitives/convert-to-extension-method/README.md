# Convert to Extension Method

Makes a method callable as an extension method on the type of its first
parameter.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the method to convert, such as `M:Shop.Text.Shout(System.String)` |
| `to` | optional, for an instance method: the static class to put the extension in (default `<Type>Extensions`) |

## Precondition

For a static method, the usual case:

- It is not already an extension method.
- It is declared in a static, non-generic, top-level class.
- It has at least one parameter, and the first is not `out`, `params` or a
  pointer.

For an instance method, no precondition beyond being an ordinary method of a
class.

## Transformation

- A static method gains `this` on its first parameter. Each call written as
  `Text.Shout(value, ...)` or, inside the class, `Shout(value, ...)` becomes
  `value.Shout(...)`, with the receiver parenthesized where precedence needs
  it. Explicit type arguments are kept.
- A call keeps its static form when the extension form would not bind the same
  way: its first argument is a `null` or `default` literal, is named or passed
  by reference, converts to the parameter only by a user-defined or numeric
  conversion, or the calling file does not see the class's namespace.
- An instance method moves to a static class beside its type, as an extension
  method whose first parameter is the instance, with uses of the instance's
  members qualified by that parameter. The original method stays, delegating
  to the extension, so callers are unchanged.

## Preserved

- The behaviour of every call.
- Nullable annotations on the first parameter.

## Limitations

- For an instance method, members the method uses must be accessible from the
  new static class; private members are not made accessible.
- For an instance method the extension parameter is named after the type in
  camel case and typed without type arguments, so generic types are not
  supported.

## Error codes

| Code | Meaning |
|---|---|
| `already-extension` | the method is already an extension method |
| `not-static-class` | the static method's class is not static |
| `nested-or-generic-class` | the static method's class is nested or generic |
| `no-parameters` | the method has no parameters |
| `invalid-first-parameter` | the first parameter is `out`, `params` or a pointer |
