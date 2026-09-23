# Sync Namespace with Folder

Sets the namespace of a file to the one its location implies: the project's
root namespace followed by the folders between the project and the file.
`Billing/Taxes/Rate.cs` in a project whose root namespace is `Shop` gets the
namespace `Shop.Billing.Taxes`.

## Arguments

| Argument | Meaning |
|---|---|
| `target.file` | the file whose namespace to change |

## Precondition

- The file declares exactly one namespace, block or file-scoped.
- The namespace differs from the one its folder implies.
- The folder path gives a valid namespace name.
- No type in the file shares its name with a type already in the new
  namespace, and none is a partial type with parts in other files.

## Transformation

- The root namespace is the project's `RootNamespace`, which defaults to the
  project name. Folder names have spaces and hyphens replaced by underscores.
- The namespace declaration is renamed, and every type in it moves with it,
  with references across the solution updated as in Move Type to Namespace:
  qualified names are requalified, files using the types gain a `using`, and
  the file imports the namespaces its types relied on implicitly.

## Preserved

- The meaning of every name in the solution.
- The file's layout, comments and namespace form.

## Limitations

- Files declaring several namespaces, or none, are refused rather than
  restructured.
- References in documentation comments (`cref`) are not updated.

## Error codes

| Code | Meaning |
|---|---|
| `namespace-matches-folder` | the namespace already matches the folder |
| `multiple-namespaces` | the file declares more than one namespace |
| `no-namespace` | the file's types are in the global namespace |
| `type-exists` | the new namespace already has a type of the same name |
| `partial-type` | a type in the file has parts in other files |
| `invalid-namespace` | the folder path does not give a valid namespace name |
