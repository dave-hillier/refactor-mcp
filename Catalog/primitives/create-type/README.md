# Create Type

Creates an empty class, interface, record or struct, either in a new file or
at the end of an existing one. Composite recipes such as Extract Class and
Extract Superclass start with it, so it takes no target, only arguments.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | yes | The type's name, with type parameters when generic: `Address`, `Page<T>` |
| `file` | yes | The file, relative to the solution: created when missing, appended to when present |
| `kind` | no | `class` (the default), `interface`, `record` or `struct` |
| `namespace` | no | The namespace to declare the type in |
| `baseType` | no | The base class, or for an interface its base interface. Any kind may name an interface here |

```json
{ "refactoring": "create-type", "arguments": { "name": "Address", "file": "Address.cs" } }
```

## Precondition

- `name` is an identifier, optionally followed by type parameter names.
- No type with that name and arity already exists in the namespace.
- `baseType`, when given, names a type the project can see, and the kind can
  derive from it: a class from a class that is neither sealed nor static, a
  record from a record, and a struct or interface only from interfaces.

## Transformation

- The type is `public` and empty, with its braces on their own lines.
- Without `namespace`, an existing file's type goes in that file's namespace,
  and a new file's in the namespace most files in the same folder declare, or
  failing that most files in the project; the global namespace when they
  declare none.
- A new file follows the project's namespace style: file-scoped when most of
  its files use file-scoped namespaces, a block otherwise.
- In an existing file the type follows the last member of its namespace,
  separated by a blank line.
- A new file joins the project whose folder contains it.
- When `baseType` only binds with a using directive, the directive is added.

## Preserved

- Every existing file other than the one named, and in that file everything
  already there, comments and usings included.

## Limitations

- The type is always `public`, and nested types cannot be created.
- A base type named by a simple name that matches several accessible types
  is refused as not found rather than asking which one was meant.

## Error codes

| Code | Meaning |
|---|---|
| `invalid-name` | `name` is not an identifier with optional type parameters |
| `invalid-kind` | `kind` is not `class`, `interface`, `record` or `struct` |
| `type-already-exists` | the namespace already has a type of that name and arity |
| `type-not-found` | `baseType` names no type the project can see |
| `invalid-base-type` | the kind of type cannot derive from `baseType` |
