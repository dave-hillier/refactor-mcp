# Convert to Expression Body

Replaces a block body holding a single statement with an expression body. The
reverse of Convert to Block Body.

## Target

The declaration, by symbol: a method, constructor, operator, property,
indexer or accessor (`M:Sample.set_Name(System.String)` for a setter). A local
function, which has no symbol id, by a caret on its name.

## Precondition

- The declaration has a block body, not an expression body and not none, as
  an abstract member or an auto-property has.
- The block holds exactly one statement: `return expression;`, an expression
  statement, or `throw expression;`.
- The block contains no preprocessor directive.
- A property or indexer has a get accessor and nothing else, with no
  modifiers or attributes on it. A property with a setter as well is
  converted one accessor at a time.

## Transformation

- `{ return expression; }` and `{ expression; }` become `=> expression;`.
- `{ throw expression; }` becomes `=> throw expression;`.
- A property or indexer whose only accessor is a get accessor becomes an
  expression-bodied property or indexer: `public string Name => _name;`.
- The expression follows the header on the same line. An expression spanning
  several lines keeps its line breaks, shifted to follow the header.
- A constructor initializer and a generic constraint clause stay before the
  arrow.

## Preserved

- Behaviour: an expression body means exactly what the single statement did.
- Comments inside the block move to lines of their own above the declaration,
  below any documentation comment; a comment after the statement stays at the
  end of the line.

## Limitations

- A comment between `return` and its expression, or after the header before
  the block, is not kept.
- Accessors with attributes or modifiers of their own keep their property's
  accessor list; only the accessor itself can be converted.

## Error codes

| Code | Meaning |
|---|---|
| `not-single-statement` | the body is not a single return, expression or throw statement |
| `several-accessors` | the property has more than a plain get accessor |
| `already-expression-bodied` | the declaration already has an expression body |
| `directive-in-body` | the body contains a preprocessor directive |
| `no-body` | the declaration has no body, such as an abstract method |
| `not-a-member-with-body` | the target is not a method, property, accessor, constructor, operator or local function |
