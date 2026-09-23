# Use Pattern Matching

Replaces a type test followed by casts, or an `as` conversion followed by a
null check, with a declaration pattern in the `if` statement.

## Precondition

The caret is on an `if` statement whose condition is one of:

- A type test of a local or parameter, `x is T`, with casts `(T)x` in its
  branch. A field or property could change between the test and a cast, so
  only locals and parameters are converted, and `x` must not be assigned
  where the casts are.
- A negated type test, `!(x is T)` or `x is not T`, whose branch always leaves
  (returns, throws, breaks or continues), with casts `(T)x` in the statements
  after the `if`.
- A null check of a local declared by the statement just before the `if` and
  initialised with `x as T`:
  - `t != null` or `t is not null`, where `t` is only used in the condition and
    the branch, since the pattern variable is only assigned there;
  - `t == null` or `t is null`, whose branch always leaves and does not use
    `t`, so that the code after the `if` sees it assigned.

The pattern variable's name must not already be declared where it would be in
scope.

## Transformation

- A type test becomes `x is T name`, or `x is not T name` when negated, and
  every cast `(T)x` where `x` is known to be a `T` becomes `name`, dropping
  the parentheses around it.
- The name is the `name` argument when given; otherwise, when a local is
  initialised with nothing but the cast and never assigned, that local's name,
  and the declaration is removed; otherwise the type's name in camel case,
  numbered if taken.
- An `as` conversion and its null check become `x is T t` or `x is not T t`,
  keeping the local's name unless `name` is given, and the declaration is
  removed. Comments on the declaration move to the `if`.

## Preserved

- Behaviour: the pattern tests the same type as the cast or conversion, and
  the variable holds the value the casts would have produced.
- Uses of the variable in the rest of the method.

## Limitations

- Casts in an `else` branch, or in code outside the branch, are left as they
  are.
- Type tests in `switch` statements and conditional expressions are not
  converted.
- `x as T` where `T` is a nullable value type gives a pattern variable of the
  underlying type; uses such as `.Value` then no longer compile, and the
  change is refused.

## Arguments

| Argument | Meaning |
|---|---|
| `name` | optional name for the pattern variable |

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `no-type-check` | the condition is neither a type test nor a null check of an `as` conversion just before the `if` |
| `not-a-local` | the tested value is not a local or parameter |
| `no-cast` | there is no cast of the tested value to the tested type to replace |
| `variable-assigned` | the tested value is assigned where the casts are |
| `branch-falls-through` | the test is negated, or checks for null, and its branch does not always leave |
| `used-outside-if` | the local assigned with `as` is used where the pattern variable would not be assigned |
| `name-conflict` | the pattern variable's name is already declared |
