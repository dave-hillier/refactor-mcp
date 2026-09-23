# Convert Tuple to Named Type

Replaces a tuple in a method's signature, its return type or one of its
parameters, with a named positional record, and updates the method's body and
its callers to construct and read it.

## Arguments

| Argument | Meaning |
|---|---|
| `name` | the name of the new type |
| `parameter` | the parameter whose tuple type is replaced; the return type when omitted |
| `kind` | `struct` for a `readonly record struct` (the default), `class` for a `sealed record` |

The target is the method, by symbol.

## Precondition

- The return type, or the named parameter's type, is a tuple, and every
  element has a name.
- No type named `name` is already visible where the type would be declared.
- The project's language version supports the kind: C# 10 for a record
  struct, C# 9 for a record class.
- Every use of the tuple that the refactoring does not rewrite still compiles
  with the named type. Values that flow on as tuples, such as a result stored
  in a variable declared with the tuple type, are refused.

## Transformation

- A positional record named `name` is declared after the type containing the
  method, with that type's accessibility. Its properties are the element
  names in Pascal case, with the element types. Type parameters the elements
  use become the record's type parameters, in order of first use.
- The tuple type in the signature becomes the named type.
- Tuple literals the method returns, or that callers pass for the parameter,
  become `new Name(...)`, dropping element names.
- Element accesses become property accesses, `min` or `Item1` becoming
  `Min`, on the method's result, on a `var` local initialized from it, and on
  the parameter in the body.
- Deconstruction keeps working through the record's `Deconstruct`.

## Preserved

- The values passed and returned, element access, deconstruction and value
  equality.

## Limitations

- `ToString` prints the record's format, `MinMax { Min = 1, Max = 2 }`,
  rather than the tuple's `(1, 2)`.
- The properties are read-only; code that assigned an element no longer
  compiles and the change is refused.
- With `class`, `default` of the type is `null` rather than a tuple of
  defaults.
- Only the one signature changes; overrides and interface implementations
  that share it are refused by the compile check.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-tuple` | the return type or parameter is not a tuple |
| `unnamed-elements` | a tuple element has no name |
| `name-conflict` | a type named `name` is already visible there |
| `language-version` | the project's language version predates the record kind |
| `used-as-tuple` | a use of the tuple would not compile with the named type |
