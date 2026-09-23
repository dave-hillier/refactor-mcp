# Cleanup Usings

Removes the using directives in a file that nothing in it needs.

## Precondition

- None. A file whose directives are all used is left unchanged.

## Transformation

- Each directive the compiler reports as unnecessary is removed: ordinary,
  `static` and alias directives alike, at the top of the file or inside a
  namespace.
- A directive needed only for extension methods, such as `using System.Linq;`
  for `Where`, is used and stays.
- A header comment above the first directive stays at the top of the file.
- When every directive of a file or namespace goes, so does the blank line
  that followed them.

## Preserved

- Everything else in the file exactly as written: nothing is reformatted.
- The order of the directives that remain.

## Limitations

- Directives are not sorted, and missing ones are not added.
- Only the one file is cleaned; other files keep their unnecessary directives.

## Error codes

None.
