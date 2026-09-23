# Convert Record to Class

Turns a record into a class that behaves the same: a positional record's
constructor, properties and `Deconstruct` are written out, and the value
equality, `ToString` and `==` operators the compiler generated for the record
are generated as source.

## Arguments

None. The target is the record, by symbol.

## Precondition

- The type is a record class, not a `record struct`.
- The record neither derives from another record nor has records deriving
  from it; the equality contract of a record hierarchy has no plain class
  equivalent.
- No `with` expression copies an instance, since a class has no clone
  method for it to call.
- The record has one declaration.

## Transformation

- `record` becomes `class`, keeping the modifiers, type parameters, base
  types and constraints, and `IEquatable<T>` is added to the base types.
- A positional parameter list becomes a public constructor assigning each
  parameter to a public `{ get; init; }` property of the same name, and a
  `Deconstruct` method. The constructor parameters are camel case, and named
  arguments at every call site in the solution are renamed to match.
- After the existing members come, unless the record declares them itself:
  - `Equals(T other)`, comparing every instance field, including private
    fields and the backing fields of auto-properties, with
    `EqualityComparer<TField>.Default`; `virtual` and checking the runtime type
    when the record is not sealed.
  - `Equals(object)` calling it, and `GetHashCode` combining the same fields.
  - `ToString` in the record format, `Name { A = 1, B = 2 }`, listing the
    public fields and readable properties.
  - `==` and `!=`.
- `using System;` and `using System.Collections.Generic;` are added when the
  generated members need them and the file does not already import them.
- Parameters accepting `null` are annotated where nullable annotations are
  enabled.

## Preserved

- Construction, property access, deconstruction, equality, hashing into the
  same buckets, `ToString` output and `==`.

## Limitations

- The record's protected copy constructor and `EqualityContract` are not
  generated, which is why `with` expressions and record hierarchies are
  refused.
- Hash codes are equal for equal instances but not the same numbers the
  record produced.
- A declared `PrintMembers` is kept but no longer used by `ToString`.
- Attributes targeting the generated property (`[property: ...]`) on a
  positional parameter are not moved to the property.

## Error codes

| Code | Meaning |
|---|---|
| `record-struct` | the target is a record struct |
| `record-hierarchy` | the record derives from, or is derived by, another record |
| `with-expression` | a `with` expression copies an instance |
| `partial-type` | the record has more than one declaration |
