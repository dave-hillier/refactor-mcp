# Convert to Block Namespace

Turns a file's `namespace Shop;` declaration into a `namespace Shop { ... }`
block around everything after it, indenting that code a level. The reverse of
Convert to File-Scoped Namespace.

## Precondition

- The file has a file-scoped namespace declaration.

## Transformation

- The declaration becomes `namespace Name` with the opening brace on the next
  line, and the closing brace ends the file.
- Every line after the declaration gains one level of indentation, the file's
  own unit or four spaces when nothing is indented. This includes comments,
  documentation comments, indented directives and using directives that
  followed the declaration, which move inside the block.
- Blank lines between the declaration and the first code are dropped, and
  blank lines stay empty rather than gaining indentation.

## Preserved

- Everything before the declaration, such as a header comment and using
  directives.
- Lines inside a string literal that spans lines, including raw string
  literals, which are part of its value and keep their indentation.
- Directives written at column zero, such as `#if DEBUG`.

## Limitations

- A comment on the same line as the declaration's semicolon is dropped.

## Error codes

| Code | Meaning |
|---|---|
| `no-file-scoped-namespace` | the file has no file-scoped namespace declaration |
