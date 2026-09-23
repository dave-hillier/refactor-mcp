# Extract Local Variable

Also known as Introduce Variable. Declares a local holding a selected
expression just before the statement that contains it, and uses the local in
place of the expression. The reverse of Inline Local Variable.

## Target

The expression, by a selection. The argument `name` is the local's name.

## Precondition

- The selection is an expression inside a statement of a block-bodied member.
- The expression has a value.
- The expression is evaluated every time the statement runs, and only then:
  it is not in a loop condition or increment, the right of `&&`, `||` or
  `??`, a branch of `?:`, the part after `?.`, a switch expression arm, or a
  lambda.
- It reads no variable declared inside the statement outside the selection,
  such as the parameter of an enclosing lambda.
- `name` is not a local or parameter visible at the statement, and does not
  appear in the rest of the block, where the local would clash with or hide
  it.

## Transformation

- The local is declared with the expression's type, written as briefly as the
  scope allows, with its nullable annotation. An anonymous type is declared
  with `var`.
- The declaration goes directly before the statement. A statement that is the
  body of an `if`, loop or similar without braces is wrapped in a block to
  hold it.
- The selected expression is replaced by the local, dropping parentheses that
  only grouped it. When the expression has no side effects, identical
  expressions elsewhere in the same statement that are evaluated whenever it
  is are replaced as well. Other statements are not touched.

## Preserved

- Behaviour: the expression is evaluated once, at the same point in the
  statement's evaluation or earlier, and only where nothing it reads can have
  changed.
- Comments above the statement stay above the new declaration; a trailing
  comment stays on the statement.

## Limitations

- Moving the evaluation of an expression with side effects to before the
  statement runs it ahead of the parts of the statement that preceded it.
- Occurrences in other statements are not replaced, even when they would see
  the same value.
- Expression-bodied members are refused rather than converted to a block
  body.

## Error codes

| Code | Meaning |
|---|---|
| `expression-bodied-member` | the expression is in an expression-bodied member |
| `not-in-statement` | the expression is not inside a statement, such as a field initializer |
| `in-loop-condition` | the expression is in a loop condition or increment |
| `conditionally-evaluated` | the expression runs only on some paths, or later |
| `declared-in-statement` | the expression reads a variable declared inside the statement |
| `name-conflict` | `name` is already declared or used where the local would be |
| `void-expression` | the expression has no value |
| `not-an-expression` | the selection is not an expression |
