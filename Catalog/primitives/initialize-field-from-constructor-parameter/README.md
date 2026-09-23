# Initialize Field from Constructor Parameter

Assigns a field that nothing uses yet from a constructor parameter, at the
end of the constructor. Nothing reads the field, so the assignment changes
nothing the code does; it prepares the field for a later step, such as
Replace Expression with Field in Constructor Injection's recipe.

## Target

The constructor, by symbol, with:

| Argument | Meaning |
|---|---|
| `field` | the field to assign, declared in the constructor's class |
| `parameter` | the constructor parameter to assign it from |

## Precondition

- The target is an instance constructor with a block or expression body.
- The constructor has the parameter, and the class declares an instance
  field of that name.
- The parameter's type converts implicitly to the field's type.
- Nothing uses the field: no code in any file reads or writes it, and it has
  no initializer.
- The constructor does not return early, so the assignment runs whenever the
  constructor completes.

## Transformation

- `field = parameter;` is added as the constructor's last statement, before
  any comment that closes the body. It is written `this.field = parameter;`
  where a parameter or local of the constructor has the field's name.
- An expression-bodied constructor gets a block body holding its expression
  and then the assignment.

## Preserved

- Everything the code does: the field was never read, so giving it a value
  cannot be observed.
- Comments on the constructor's statements.

## Limitations

- Reflection that reads the field would see the new value.
- A field used anywhere is refused, even where the assignment would not
  change what the uses see; assign it by hand, or use Introduce Field for a
  new one first.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-constructor` | the target is not an instance constructor with a body |
| `unknown-parameter` | the constructor has no parameter of that name |
| `unknown-field` | the class declares no instance field of that name |
| `type-mismatch` | the field's type cannot hold the parameter |
| `field-in-use` | the field is read, written or initialised somewhere already |
| `returns-early` | the constructor contains a `return` statement |
