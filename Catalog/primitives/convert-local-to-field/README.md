# Convert Local to Field

Promotes a local variable to a private field of the containing type. The
declaration becomes an assignment to the field and every use refers to the
field. A step of Replace Method with Method Object.

## Target

The local, by a caret on its declaration or on any use of it. In a later step
of a composite, the containing member's symbol with `arguments.local` naming
the local. The optional argument `name` is the field's name; it defaults to
the local's.

## Precondition

- The local is declared by a local declaration statement in a block or switch
  section that declares only it, and is not a `using` declaration or a `ref`
  local.
- Its type can be written as a field's: it is not anonymous and does not use
  a type parameter of the method.
- The type and its bases have no member with the field's name, and no other
  local or parameter of the member has it, which would hide the field.

## Transformation

- A `private` field of the local's type is added after the type's last field,
  or as its first member, followed by a blank line, when it has none. The
  field is `static` when the member declaring the local is static.
- In a nullable context a reference type field is nullable, since it holds
  nothing until the member first assigns it.
- A declaration with an initializer becomes an assignment to the field at the
  same place. A declaration without one is removed, and the comments above it
  stay in place.
- A `const` local becomes a `private const` field with its value, and its
  declaration is removed.
- Every use of the local refers to the field.

## Preserved

- Behaviour of a single, non-reentrant call of the member.
- Comments above the declaration.

## Limitations

- The field outlives the call, so recursion, re-entrancy and concurrent calls
  now share one value. This is the point of the refactoring, but it is not
  checked.
- Locals declared by `for`, `using`, `foreach` or pattern statements are not
  covered.

## Error codes

| Code | Meaning |
|---|---|
| `name-conflict` | the type or a base already has a member with the field's name |
| `hidden-by-local` | another local or parameter of the member has the field's name |
| `uses-method-type-parameter` | the local's type uses a type parameter of the method |
| `using-declaration` | the local is a `using` declaration, disposed when the member ends |
| `multiple-declarators` | the statement declares several locals |
| `anonymous-type` | the local's type is anonymous and cannot be written |
| `not-a-local` | the target is not a local variable |
