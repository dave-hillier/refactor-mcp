# Move Type to Namespace

Changes the namespace a top-level type is declared in, leaving it in its file,
and keeps every reference to it compiling across the solution.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the type to move, such as `T:Shop.Invoice` |
| `namespace` | the namespace to move it to, such as `Shop.Billing` |

## Precondition

- The type is top-level and declared in one place (not a partial type with
  several parts).
- `namespace` is a valid dotted name and differs from the current namespace.
- The new namespace has no type with the same name and arity.
- The type's namespace declaration is not nested inside another namespace
  block. A file-scoped namespace holds no other type.

## Transformation

- When the type is the only member of its namespace declaration, the
  declaration is renamed, block or file-scoped. Otherwise the type moves to a
  new namespace block of its own after the old one, in the same file, with the
  comments above it. A type in the global namespace is wrapped in a new block.
- A reference qualified by the old namespace, including `global::` names, using
  aliases and `using static`, is requalified with the new namespace.
- A file that uses the type by its simple name, from outside the new namespace
  and its children, gains a `using` for the new namespace.
- The moved type gains a `using` for each namespace it relied on without one:
  its old namespace and that namespace's parents, for the types and extension
  methods it uses from there.
- A `using` that only the moved type needed, and that is unnecessary after the
  move, is removed. Usings that were already unnecessary are left alone.
- References in other projects of the solution are updated the same way.

## Preserved

- The meaning of every name in the solution.
- The type's file, members, comments and layout.

## Limitations

- Usings declared inside the old namespace block are copied into a new block
  when the type is split out, even where the type does not need them.
- References in documentation comments (`cref`) are not updated.

## Error codes

| Code | Meaning |
|---|---|
| `namespace-unchanged` | the type is already in the namespace |
| `type-exists` | the new namespace already has a type of that name |
| `nested-type` | the type is nested inside another type |
| `invalid-namespace` | the namespace is not a valid dotted name |
| `partial-type` | the type is partial and declared in several places |
| `nested-namespace-block` | the type's namespace block is nested in another |
| `shares-file-scoped-namespace` | the type shares a file-scoped namespace with other types |
