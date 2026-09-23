# Convert Primary Constructor to Constructor

Replaces a class or struct's primary constructor with an explicit
constructor, storing each captured parameter in a private field.

## Arguments

None. The target is the class or struct, by symbol.

## Precondition

- The type is a class or struct, not a record, with a primary constructor.
- For every parameter the members capture, the field `_name` is free: no
  member of the type already has that name.

## Transformation

- The parameter list leaves the type's header, and arguments given to the
  base type become a `base(...)` call.
- A parameter read or written by a member (captured) gets a private field
  named `_name`, `readonly` unless a member assigns the parameter. The fields
  are declared first, in parameter order, and every use of the parameter in a
  member reads the field.
- The constructor is public, takes the same parameters with their defaults
  and attributes, and goes after the fields. Its body assigns each captured
  parameter to its field, then runs, in declaration order, each field or
  property initializer that read a parameter, as an assignment; those
  initializers are removed. Initializers that read no parameter stay.
- The member after the fields is separated from them by a blank line.

## Preserved

- Construction, named arguments at call sites, and the value of every member.

## Limitations

- Initializers that read a parameter run in the constructor body, after the
  base constructor, rather than before it. This only shows when the base
  constructor calls a virtual member that reads them.
- The field name is not configurable.

## Error codes

| Code | Meaning |
|---|---|
| `unsupported-type` | the type is a record or has several declarations |
| `no-primary-constructor` | the type has no primary constructor |
| `name-conflict` | a member already has the name a captured parameter's field needs |
