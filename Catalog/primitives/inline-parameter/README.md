# Inline Parameter

When every call passes the same constant for a parameter, removes the
parameter and writes the constant where the body used it.

## Arguments

| Argument | Meaning |
|---|---|
| `parameter` | the name of the parameter to inline |

The target is the method or constructor, by symbol.

## Precondition

- The method is called at least once, and only called: it is not used as a
  method group.
- Every call passes a compile-time constant for the parameter, and all of them
  are the same value. A call that leaves an optional parameter out passes its
  default.
- The body never assigns the parameter, increments it or passes it by
  reference.
- The parameter is not `ref`, `out`, `in` or a params array.
- The method is not virtual, abstract or an override and implements no
  interface member, since the other members of its hierarchy have bodies of
  their own.
- The result compiles.

## Transformation

- Every use of the parameter in the body becomes the constant, written as the
  first call wrote it, parenthesised where precedence needs it.
- When the constant's type is not the parameter's type, it is cast, so an
  argument of `2` for a `double` parameter becomes `(double)2` and arithmetic
  keeps its meaning.
- The parameter is removed as by Change Signature, dropping its argument from
  every call.

## Preserved

- What the method computes for every call.
- Comments in the body and at the calls. A comment written inline before the
  removed argument goes with it.

## Limitations

- Only compile-time constants are inlined. A static readonly field every call
  passes is refused, as is any other expression.
- The constant is copied as written, so a named constant from another
  namespace may need a `using` in the method's file.
- The body can end up with expressions a person would fold, such as
  `"en-GB" ?? "en"` (`nullable-parameter`), and a constant condition can make
  code unreachable, which the compiler reports as a warning.

## Error codes

| Code | Meaning |
|---|---|
| `values-differ` | calls pass different values |
| `not-constant` | a call passes something other than a constant |
| `parameter-assigned` | the body writes to the parameter |
| `no-calls` | nothing calls the method |
| `part-of-hierarchy` | the method is virtual, abstract, an override or an interface implementation |
| `method-group-reference` | the method is used as a method group |
