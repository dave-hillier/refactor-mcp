# Change Accessibility

Changes the declared accessibility of a type or member, refusing when that
would break a reference, an override or an interface implementation, or
quietly change what a call binds to.

## Arguments

| Argument | Meaning |
|---|---|
| `accessibility` | `public`, `internal`, `protected`, `private`, `protected internal` or `private protected` |

The target is the type or member, by symbol.

## Precondition

- The accessibility is one of the six C# accessibilities, and is valid for the
  declaration: a top-level type cannot be `private` or `protected`, and an
  explicit interface implementation takes none.
- Every reference can still see the declaration.
- An override keeps the accessibility of the member it overrides, so neither
  a virtual member with overrides nor an override can change alone.
- A member implementing an interface member implicitly stays `public`.
- Accessibility stays consistent: a type used in the signature of a member
  cannot become less accessible than that member.
- The declaration is referenced exactly as often as before. Accessibility
  takes part in overload resolution, so hiding an overload can send its calls
  to another one, and exposing one can take calls from another, with the code
  still compiling.
- A field is the only one its declaration declares.

## Transformation

- The accessibility keywords are replaced where they stood, and the other
  modifiers stay in their order.
- A declaration with no accessibility written gets one in front of its other
  modifiers, after its attributes and documentation comment.
- Every part of a partial type that states an accessibility changes; parts
  that state none are left to take it from the others.

## Preserved

- What every reference binds to.
- Documentation comments, attributes, and comments between modifiers.

## Limitations

- Accessor accessibility, such as a `private set`, is not covered.
- Members of interfaces are not covered.
- An override family is refused rather than changed together.
- References from other assemblies outside the solution, such as those that
  see `internal` members through `InternalsVisibleTo` or consume a published
  package, are not considered.

## Error codes

| Code | Meaning |
|---|---|
| `invalid-accessibility` | the argument is not a C# accessibility |
| `breaks-references` | a reference could no longer see the declaration |
| `breaks-override` | an override and the member it overrides would disagree |
| `breaks-interface-implementation` | an implicit interface implementation would not be public |
| `inconsistent-accessibility` | a member's signature would use a less accessible type |
| `changes-binding` | a call would bind to a different member |
| `shared-declaration` | the field is declared together with others |
