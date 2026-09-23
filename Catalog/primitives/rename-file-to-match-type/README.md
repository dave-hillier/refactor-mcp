# Rename File to Match Type

Renames a source file after the single top-level type it declares, so that
`Ledger.cs` holding `class Account` becomes `Account.cs`.

## Arguments

| Argument | Meaning |
|---|---|
| `target.file` | the file to rename |

## Precondition

- The file declares exactly one top-level type. Types nested inside it do not
  count, and the parts of a partial type in the same file count once.
- The file is not already named after the type.
- No other file with the new name exists in the same folder.

## Transformation

- The file is renamed to the type's name followed by `.cs`, in the same folder
  and project. A generic type's file takes the name without its type
  parameters: `Box<T>` lives in `Box.cs`.

## Preserved

- The file's text, byte for byte: comments, usings and layout are untouched.
- Every reference to the type, since no code changes.

## Limitations

- Generic arity is not written into the name, so `Box<T>` and `Box<T, U>`
  cannot both be renamed into one folder.
- Enums, delegates, records, structs and interfaces are named the same way as
  classes.

## Error codes

| Code | Meaning |
|---|---|
| `multiple-types` | the file declares more than one top-level type |
| `no-type` | the file declares no top-level type |
| `name-already-matches` | the file is already named after its type |
| `file-exists` | a file with the new name already exists in the folder |
