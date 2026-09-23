# Split Declaration and Assignment

Turns a local declaration with an initializer into a declaration without one,
followed by an assignment of the same value: `var x = e;` becomes `T x;` and
`x = e;`. The reverse of Join Declaration and Assignment.

## Target

The local, by a caret on its declaration or on any use of it. In a later step
of a composite, the containing member's symbol with `arguments.local` naming
the local.

## Precondition

- The local is declared by a local declaration statement in a block or switch
  section, with an initializer.
- The statement declares only that local.
- The local is not `const`, not a `using` declaration and not a `ref` local.
- A `var` local's type can be named, so it is not of an anonymous type.

## Transformation

- The declaration keeps its written type, or `var` is replaced by the
  inferred type, written as briefly as the scope allows. In a nullable context
  the type carries the annotation `var` implied, so a reference type is
  written as nullable.
- The assignment follows the declaration directly.
- An array initializer, which is only valid in a declaration, becomes an
  array creation of the declared type.

## Preserved

- Behaviour: the value is computed at the same point.
- Comments above the declaration stay above it; a comment at the end of the
  line follows the value into the assignment.

## Limitations

- Locals declared by `for`, `using`, `foreach` or pattern statements are not
  covered.

## Error codes

| Code | Meaning |
|---|---|
| `no-initializer` | the declaration has no value to split off |
| `multiple-declarators` | the statement declares several locals, whose initializers would run out of order |
| `const-local` | the local is a constant, which cannot be assigned |
| `using-declaration` | the local is a `using` declaration, whose disposal is tied to its initializer |
| `anonymous-type` | the local's type is anonymous and cannot be written |
| `not-a-local` | the target is not a local variable |
