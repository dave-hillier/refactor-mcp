# Move Member to Another Partial File

Moves a member of a partial type into the part of that type declared in
another file, or into a new file holding a new part.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the member to move, such as `M:Shop.Order.Discount(System.Decimal)` |
| `file` | the file to move it to, relative to the project root of the case |

## Precondition

- The member's containing type is declared `partial`.
- The target file is not the member's own file.
- The target file, when it exists, declares a part of the same type.
- When the target file does not exist, the type is not nested.

## Transformation

- The member, with the comments above it, is removed from its part and
  appended to the end of the other part, separated by a blank line.
- A field declared alongside others (`decimal _total, _discount;`) is split out
  and moved on its own.
- A new file repeats the source file's usings and namespace form (block or
  file-scoped) and declares a part with the type's modifiers, keyword, name and
  type parameters, but without base types, constraints or attributes, which
  the original part keeps.
- The target file gains the usings the member needs; the source file drops
  those only the member needed.

## Preserved

- The type, its members and their meaning: parts of a partial type compile to
  one type.
- The member's body, comments and documentation.

## Limitations

- The member is always appended at the end of the target part, not placed
  among members of its kind.
- Partial methods, which have a declaration and an implementation, move one
  declaration at a time.

## Error codes

| Code | Meaning |
|---|---|
| `type-not-partial` | the containing type is not partial |
| `same-file` | the target file is the member's own file |
| `no-part-in-file` | the target file exists but declares no part of the type |
| `nested-type` | a new file was asked for a nested type's member |
