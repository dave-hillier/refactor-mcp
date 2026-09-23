# Inline Type Alias

Writes the type an alias names in place of every use of the alias and removes
the alias's `using` directive. The reverse of Introduce Type Alias.

## Precondition

- The file declares a `using` alias with the given name.
- The alias names a type, not a namespace.

## Transformation

- Every use of the alias, in types and in expressions such as `Stamp.UtcNow`,
  becomes the type, written as briefly as the file's using directives allow.
- When the file does not import a namespace the type needs, a using directive
  for it is added, so `Stamp` becomes `DateTimeOffset` with `using System;`
  rather than a qualified name.
- The alias's directive is removed. A header comment above it stays at the top
  of the file.
- A `global using` alias is inlined in every file of its project. A file that
  held nothing but the directive is deleted.

## Preserved

- What every use of the alias refers to.
- A nullable annotation on a use: `Lookup?` becomes `Dictionary<string, string>?`.
- Comments and layout around the uses.

## Limitations

- A namespace alias is refused rather than replaced by the namespace's full
  name.
- An imported namespace that would make another name in the file ambiguous is
  refused because the result would not compile, rather than falling back to a
  qualified name.

## Error codes

| Code | Meaning |
|---|---|
| `alias-not-found` | the file declares no alias with that name |
| `not-a-type-alias` | the alias names a namespace |
