# Convert Class to Record

Turns a class whose state is fixed by its constructor into a record: a
positional record where the constructor only assigns its parameters to
get-only properties, otherwise the same members with `class` changed to
`record`.

## Arguments

None. The target is the class, by symbol.

## Precondition

A record changes three behaviours of a class: `Equals`, `GetHashCode` and
`==` compare values instead of references, and `ToString` lists the members
instead of printing the type name. The conversion is refused wherever the
solution could observe the difference.

- The type is a non-static class, and the project's language version is C# 9
  or later.
- The class neither derives from a class other than `object` nor is derived
  from, since records only derive from records.
- Its state is fixed at construction: every instance field is `readonly` and
  no instance property has a `set` accessor.
- It does not override `Equals(object)`, which a record generates.
- Nothing in the solution observes equality: no `==` or `!=` between
  instances (comparing with `null` is fine), no `Equals` or `GetHashCode` call
  on or with an instance, no use as a dictionary key or set element (a type
  argument for a `TKey` type parameter, or the element type of a hash set), and
  no call to `Contains`, `IndexOf`, `LastIndexOf`, `Remove`, `Distinct`,
  `Union`, `Intersect`, `Except`, `ToHashSet` or `SequenceEqual` over the type.
- Unless the class overrides `ToString`, nothing formats an instance: no
  `ToString` call, interpolation or string concatenation of one.

## Transformation

- `class` becomes `record` on every declaration, keeping modifiers, type
  parameters, base types and constraints.
- The record is positional when the class has one declaration and a single
  public constructor whose body only assigns each parameter to a public
  get-only auto-property of the same type, and neither the constructor nor
  those properties carry attributes or documentation. The parameter list,
  named after the properties and keeping default values, follows the name
  and type parameters; the constructor and the properties are removed, and a
  record left with no members ends with `;`.
- Named arguments at every call of the constructor in the solution are
  renamed to the property names.
- Otherwise the constructor and properties stay in the body.

## Preserved

- Construction, member access and every observable behaviour the
  precondition checks.

## Limitations

- Positional properties get `init` accessors, so object initializers and
  `with` can set them afterwards; nothing that compiled before changes.
- An instance passed as `object` to a method that formats or compares it,
  such as `string.Format` or a non-generic collection, is not detected.
- Records also gain `Deconstruct`, a copy constructor and `EqualityContract`;
  a class already declaring a conflicting member is refused by the compile
  check.

## Error codes

| Code | Meaning |
|---|---|
| `unsupported-type` | the type is a static class, a struct or an interface |
| `language-version` | the project's language version predates records |
| `class-hierarchy` | the class derives from, or is derived by, another class |
| `mutable-state` | an instance field is not readonly or a property has a setter |
| `declares-equality` | the class overrides `Equals(object)` |
| `equality-observed` | the solution compares, hashes or looks up instances |
| `to-string-observed` | the solution formats an instance and the class does not override `ToString` |
