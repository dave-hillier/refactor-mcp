# Replace Expression with Field

Replaces an expression in a member with a readonly field that holds an
equivalent value: every constructor assigns the field from a parameter, and
every construction of the class passes an equivalent expression for that
parameter. Constructor Injection's recipe uses it to replace the object a
method constructs with the one the class receives.

## Target

Either the expression, by a selection, with `arguments.field` naming the
field; or, in a later step of a composite, the member by symbol with:

| Argument | Meaning |
|---|---|
| `expression` | the expression as C#; every occurrence in the member is replaced |
| `field` | the field to use in its place |

## Precondition

- The selection is exactly one expression, or the member contains the
  expression.
- The class containing the expression declares an instance field of that
  name, and it is `readonly`, so only constructors can set it.
- The field's type is the expression's type.
- The expression is in an instance method, property, indexer or event of
  that class, which runs only once the object is constructed: not in a
  constructor, initializer, `init` accessor or static member. The member is
  not an override, which a base constructor could call before the field is
  assigned.
- The expression is a fixed value, the same wherever it is evaluated: a
  constant, `typeof`, a static readonly field, or a construction with `new`
  and no initializer whose arguments are fixed values. It reads no local,
  parameter or instance state and calls no method.
- The field has no initializer, and the class declares its constructors.
  Each constructor that does not call `this(...)` assigns the field exactly
  once, as a statement of its body, from a required parameter of the field's
  type that it never writes; it has no `return` statement and does not use
  the object before the assignment. A constructor calling `this(...)` does
  not assign the field, and no `init` accessor does.
- Every construction that runs one of those constructors, with `new`,
  target-typed `new`, `this(...)` or `base(...)`, in any file or project,
  passes for the parameter an expression equivalent to the one replaced:
  equal constants of one type, or constructions of the same constructor with
  equivalent arguments, however the names in them are qualified.

## Transformation

- The expression is replaced with the field, written `this.field` where a
  local or parameter hides it.
- Comments before and after the expression stay in place.

## Preserved

- The value the member works with: the field holds a value equivalent to the
  one the expression computed.

## Limitations

- A construction is evaluated once, when the object is constructed, rather
  than each time the member runs, and one object is shared by every run of
  the member. An object that keeps state between uses, or whose identity
  matters, behaves differently; this is not checked. Constants are not
  affected.
- The construction runs at the caller, before the constructor, even if the
  member never runs, so a constructor with side effects runs earlier.
- Constructions by reflection or deserialization are not seen.
- An expression using the class's type parameters is compared as written,
  so constructions of a closed type, such as `new Formatter<int>()` for
  `new Formatter<T>()`, are refused as different.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-expression` | the selection is not exactly one expression |
| `expression-not-found` | the member does not contain the expression |
| `unknown-field` | the class declares no instance field of that name |
| `field-not-readonly` | the field is not `readonly` |
| `type-mismatch` | the field's type is not the expression's type |
| `not-in-instance-member` | the expression is not in an instance member that runs after construction |
| `not-a-fixed-value` | the expression is not built only from constants and constructions |
| `field-not-from-parameter` | some constructor does not assign the field once from a parameter, or something else assigns it |
| `used-during-construction` | the member could run before the field is assigned |
| `different-value-passed` | a construction passes a different value for the parameter |
