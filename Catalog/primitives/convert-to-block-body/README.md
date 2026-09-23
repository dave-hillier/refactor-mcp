# Convert to Block Body

Replaces an expression body with a block holding the equivalent statement.
The reverse of Convert to Expression Body.

## Target

The declaration, by symbol: a method, constructor, operator, property,
indexer or accessor (`M:Sample.set_Name(System.String)` for a setter). A local
function, which has no symbol id, by a caret on its name.

## Precondition

- The declaration has an expression body.

## Transformation

- When the declaration returns a value, `=> expression;` becomes
  `{ return expression; }`: methods and local functions with a return type,
  operators, conversions and get accessors.
- When it does not, the expression becomes a statement of its own: `void`
  methods, constructors, finalizers, set, init, add and remove accessors, and
  `async` methods returning `Task` or `ValueTask`, which await rather than
  return.
- `=> throw expression;` becomes `{ throw expression; }`.
- An expression-bodied property or indexer gets an accessor list with a get
  accessor holding the block.
- The block is laid out on lines of its own. An expression spanning several
  lines keeps its line breaks, shifted to sit under the statement.

## Preserved

- Behaviour: the block means exactly what the expression body did.
- Comments above the declaration stay there; a comment after the semicolon
  stays at the end of the statement.

## Limitations

- An auto-property's accessors have no body to expand, and an abstract member
  none at all; both are refused.

## Error codes

| Code | Meaning |
|---|---|
| `already-block-body` | the declaration already has a block body |
| `no-body` | the declaration has no body, such as an abstract method or an auto-property |
| `not-a-member-with-body` | the target is not a method, property, accessor, constructor, operator or local function |
