# Merge Partial Declarations

Moves the members of every part of a partial type into one of its
declarations, deleting the parts and any file they leave empty.

## Arguments

None. The target is the type, by symbol, with `file` naming the file whose
declaration receives the members. Without `file`, the first declaration of
the type receives them.

## Precondition

- The type has at least two declarations.
- The `using` directives the moved members need can be added to the receiving
  file without making a name there ambiguous or redefining an alias.

## Transformation

- The receiving declaration keeps its members and gains the members of each
  other part after them, parts in the same file first, then the other files in
  path order, each part's members in their order.
- Modifiers stated on any part are combined: an accessibility missing from the
  receiving declaration is added first, other modifiers such as `sealed`,
  `static` or `abstract` before `partial`.
- Base types stated on any part are combined, a base class first.
- Constraint clauses and attributes stated on another part are brought over.
- Comments above another part go before its first member, and the comments
  and directives before its closing brace, such as `#endregion`, go before the
  receiving declaration's closing brace. Moved members are reindented.
- `partial` is dropped, unless the type declares a partial member, whose
  declaration and implementation must stay partial.
- The `using` directives of another part's file that its members need are
  added to the receiving file.
- A part is removed from its file. A file left without any type is deleted.

## Preserved

- The type's members, base types, modifiers, constraints and attributes.
- What every name in the moved members binds to.

## Limitations

- Documentation comments on parts other than the receiving one are dropped.
- `using` directives inside a namespace block are not brought over.
- Parts under different `#if` conditions are not detected.
- When a moved member's name would be ambiguous in the receiving file the
  merge is refused rather than the name qualified.

## Error codes

| Code | Meaning |
|---|---|
| `single-declaration` | the type has only one declaration, so there is nothing to merge |
| `conflicting-imports` | a using directive the moved members need would make a name ambiguous in the receiving file |
