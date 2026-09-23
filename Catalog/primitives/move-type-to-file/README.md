# Move Type to File

Moves a top-level type out of a file that declares several types into a new
file of its own, named after the type, in the same folder and project.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the type to move, such as `T:Shop.Receipt` |

## Precondition

- The type is declared directly in a namespace or at the top of the file, not
  nested inside another type.
- The file declares at least one other top-level type. Moving the only type is
  a rename of the file, which Rename File to Match Type does.
- No file with the type's name exists beside the original.

## Transformation

- The type, with the comments and documentation above it, is removed from the
  original file and written to `<Type>.cs`. A generic type's file is named
  without its type parameters.
- The new file repeats the original's header (comments and directives such as
  `#nullable enable` above the first using), its namespace declarations, block
  or file-scoped and nested as they were, and the usings the type needs. Usings
  it does not need are left out.
- The original file drops the usings that only the moved type needed. Usings it
  did not need before the move are left alone.
- Assembly attributes stay in the original file.

## Preserved

- Every reference to the type, which keeps its name and namespace.
- Comments above the moved type travel with it; comments and `#region` blocks
  around the types that stay are left where they were.

## Limitations

- A `#region` that opens directly above the moved type stays in the original
  file, and the new file has no region.
- When the moved type is the last in its namespace and a directive such as
  `#region` sits directly above it, the directive is lost from the original
  file.
- Partial types move one part at a time, the part declared in the file.

## Error codes

| Code | Meaning |
|---|---|
| `only-type-in-file` | the type is the only top-level type in its file |
| `file-exists` | a file with the type's name already exists beside the original |
| `nested-type` | the type is nested inside another type |
