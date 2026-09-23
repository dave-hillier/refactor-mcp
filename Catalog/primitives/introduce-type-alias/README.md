# Introduce Type Alias

Declares a `using` alias for a type written in a file, such as
`using StockIndex = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>;`,
and writes the alias wherever the file names that type.

## Precondition

- The caret is on the name of a class, struct, interface, enum or delegate,
  constructed or not.
- The type is not built from a type parameter, which an alias cannot name.
- The alias name is a C# identifier and means nothing where the type is used:
  no type, namespace, member, local or alias the file can see has that name.

## Transformation

- The alias is added after the file's other using directives. In a file
  without any, it goes at the top, below any header comment, followed by a
  blank line.
- The alias names the type fully qualified, since the file's other using
  directives do not apply to it, with type arguments written as C# keywords
  where there is one.
- Every name of the type in the file, in declarations, object creations and
  static member accesses, is replaced with the alias, whether it was written
  short or qualified. Other constructions of the same generic type, such as
  `Dictionary<string, int>` beside `Dictionary<string, List<int>>`, are left
  alone.

## Preserved

- What every name in the file refers to.
- A nullable annotation on a use, which stays on the use: `Dictionary<string, string>?`
  becomes `Lookup?`.
- Every other file, even where it names the same type: an alias is local to
  its file.

## Limitations

- Nullable annotations inside the type arguments are not carried into the
  alias; a use whose type arguments differ only in annotations is replaced
  too.
- Only named types can be aliased. Arrays, tuples and pointers, which C# 12
  allows as alias targets, are not offered.
- The alias is added to the compilation unit even when the file declares its
  other usings inside a namespace.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-type` | the caret is not on the name of a type |
| `uses-type-parameter` | the type is built from a type parameter |
| `name-conflict` | the alias name already means something where the type is used |
| `invalid-name` | the alias name is not a C# identifier |
