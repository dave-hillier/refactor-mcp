# Convert to File-Scoped Namespace

Turns a file's `namespace Shop { ... }` block into a `namespace Shop;`
declaration, taking a level of indentation off the code inside.

## Precondition

- The project uses C# 10 or later.
- The file has exactly one namespace declaration, a block, with no namespace
  nested inside it.
- Nothing is declared outside the namespace, since a file-scoped namespace
  would take it in.

## Transformation

- The namespace line becomes `namespace Name;`, followed by a blank line, and
  the braces go. An opening brace on the namespace line is handled the same.
- Every line inside the block loses one level of indentation, the file's own
  unit such as four spaces or a tab. This includes comments, documentation
  comments, indented directives such as `#region`, and using directives that
  were inside the namespace, which now follow the declaration.

## Preserved

- Everything before the namespace, such as a header comment and using
  directives.
- Lines inside a string literal that spans lines, which are part of its value
  and keep their indentation.
- Directives written at column zero, such as `#if DEBUG`.

## Limitations

- A comment between the namespace name and its opening brace, or after the
  closing brace on the same line, is dropped.

## Error codes

| Code | Meaning |
|---|---|
| `no-block-namespace` | the file has no namespace block, or its namespace is already file-scoped |
| `several-namespaces` | the file declares more than one namespace, side by side or nested |
| `members-outside-namespace` | the file declares code outside the namespace |
| `language-version` | the project uses a language version before C# 10 |
