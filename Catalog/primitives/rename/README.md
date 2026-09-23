# Rename

Gives a symbol a new name and updates every reference to it across the
solution.

## Precondition

- The new name is a C# identifier. A keyword is only accepted escaped with
  `@`.
- The rename leaves the solution compiling: the new name does not clash with a
  member, local or type already declared where the symbol is.
- When the symbol is a top-level type whose file is named after it, no file
  with the new name already exists beside it.

## Transformation

- The declaration and every reference are renamed, in every project that sees
  the symbol: calls, member accesses, object initializers, named arguments,
  `nameof` and `<see cref="..."/>` in documentation comments.
- Renaming a virtual or abstract member renames its overrides and base calls;
  renaming an interface member renames its implicit and explicit
  implementations. Renaming any of them renames the whole family.
- Renaming a type renames its constructors. Renaming a generic type renames its
  constructed uses.
- Renaming a namespace renames its last part in declarations, using directives
  and qualified names.
- A top-level type declared in a file named after it, such as `Customer.cs` for
  `Customer`, has that file renamed to match. A file named after something
  else keeps its name.
- A reference the rename would capture, such as a call that an existing
  overload with the new name would bind to, is kept on the renamed symbol by
  casting or qualifying it.

## Preserved

- What every reference refers to.
- Comments, blank lines and layout: only the identifiers change.

## Limitations

- Strings and comments other than documentation references are not searched,
  so a name written in a string literal or a plain comment keeps the old name.
- Partial types split across files named `Order.cs` and `Order.Lines.cs` have
  only the file named exactly after the type renamed.

## Error codes

| Code | Meaning |
|---|---|
| `name-conflict` | the new name clashes with existing code, so the result would not compile |
| `invalid-name` | the new name is not a C# identifier |
| `file-exists` | the type's file would be renamed onto a file that already exists |
