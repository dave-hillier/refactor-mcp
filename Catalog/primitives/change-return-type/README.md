# Change Return Type

Changes the declared return type of a method, and of every override,
interface member and implementation that must match it, when the method's
body and every caller stay valid.

## Arguments

| Argument | Meaning |
|---|---|
| `type` | the new return type, as written in C#, such as `long`, `IReadOnlyList<T>` or `string?` |

The target is the method, by symbol. Targeting an override or an
implementation changes the whole family.

## Precondition

- The type resolves where the method is declared. It may use the method's own
  type parameters.
- The method's body still compiles: every `return` gives a value the new type
  accepts, and an async method's type is one it can return.
- Every caller still compiles: a result stored, returned, passed or used as a
  receiver still fits.
- Every call around a reference binds as it did before. A result of a new type
  can make the call it is passed to choose another overload, or infer other
  type arguments, and still compile.
- In a nullable context, no caller gets a new nullable warning, such as
  dereferencing a result that may now be null.
- Every member of the family is declared in the solution.

## Transformation

The return type of each declaration in the family is replaced by the new
type. Nothing else changes: callers keep their code, and `var` locals take the
new type.

## Preserved

- The value every call returns and which methods callers go on to call with
  it.
- Documentation comments, attributes, modifiers and comments next to the
  return type.

## Limitations

- Operators are not checked: widening `int` to `long` changes `int` arithmetic
  on the result to `long` arithmetic, which can differ on overflow.
- Delegates created from the method are checked only by compiling.
- Properties, indexers and delegate types are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `unknown-type` | the type does not resolve |
| `return-value-incompatible` | the method's own body does not compile with the new type |
| `breaks-callers` | a caller does not compile with the new type |
| `changes-overload-resolution` | a call around a reference would bind to a different method |
| `introduces-nullable-warnings` | a caller would get a nullable warning |
| `external-member` | the method overrides or implements a member outside the solution |
