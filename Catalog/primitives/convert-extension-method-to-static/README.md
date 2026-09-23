# Convert Extension Method to Static Method

The reverse of Convert to Extension Method: an extension method becomes an
ordinary static method, and every extension-style call becomes a static call.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the extension method, such as `M:Shop.Text.Shout(System.String)` |

## Precondition

- The method is an extension method.
- No call uses it through null-conditional access (`value?.Shout()`), whose
  static form would need a null check.
- It is not used as a method group on a value (`value.Shout` passed as a
  delegate), which has no static equivalent.

## Transformation

- `this` is removed from the first parameter.
- Each call `value.Shout(...)` becomes `Text.Shout(value, ...)`, with the
  receiver as the first argument. The class name is as short as the calling
  file allows, and a call inside the class is left unqualified.
- Chained calls convert together: `raw.Trimmed().ToUpperInvariant()` becomes
  `Formatting.Trimmed(raw).ToUpperInvariant()`.
- Explicit type arguments are kept.
- Calls already written in static form are unchanged.

## Preserved

- The behaviour of every call, including evaluation order: the receiver is
  evaluated first, as the first argument.

## Limitations

- LINQ query syntax that binds to the method (a `Select` or `Where` extension)
  is not rewritten, and stops compiling.

## Error codes

| Code | Meaning |
|---|---|
| `not-extension` | the method is not an extension method |
| `conditional-access` | a call uses null-conditional access |
| `method-group` | the method is used as a method group on a value |
