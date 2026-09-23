# Convert Anonymous Type to Class

Replaces an anonymous type with a named class that behaves the same, and
constructs that class wherever the containing member creates the anonymous
type.

## Arguments

| Argument | Meaning |
|---|---|
| `name` | the name of the new class |

The target is a caret on the `new` of an anonymous object creation.

## Precondition

- The caret is on an anonymous object creation.
- Every property type can be named: none is itself an anonymous type or uses
  a type parameter.
- No type named `name` is already visible where the class would be declared.

## Transformation

- A `sealed` class named `name` is declared right after the type containing
  the creation, `internal` at namespace level or `private` when nested, with:
  - a constructor taking each property, camel case, in order;
  - a get-only property for each anonymous property, with the same type and
    nullable annotation;
  - `Equals(object)`, `GetHashCode` and `ToString` matching the anonymous
    type's: equal when every property is equal by its default comparer, and
    printed as `{ Name = value, ... }`.
- Every creation of the same anonymous type in the containing member becomes
  `new Name(...)` with the property values in order, explicit or projected.
- `using System;` and `using System.Collections.Generic;` are added when the
  file does not already import them.

## Preserved

- Property access, equality (so `Distinct`, `GroupBy` and dictionary lookups
  behave the same), hashing into the same buckets, `ToString` output, and
  `==`, which compares references for both.

## Limitations

- Creations of the same anonymous type in other members are left anonymous,
  so where they meet the converted one the code no longer compiles and the
  conversion is refused.
- Comments inside the anonymous initializer are not kept; the arguments are
  written on one line.
- Hash codes are equal for equal instances but not the same numbers.

## Error codes

| Code | Meaning |
|---|---|
| `no-anonymous-type` | the caret is not on an anonymous object creation |
| `unnamable-property-type` | a property's type is anonymous or uses a type parameter |
| `name-conflict` | a type named `name` is already visible there |
