# Safe Delete Member

Deletes a method, property, indexer, field or event that nothing uses.

## Precondition

- The target is a method, property, field or event. A type is deleted with
  Safe Delete Type.
- Nothing refers to the member outside its own declaration, in any project of
  the solution. Recursive calls from inside the member do not count.
- The member does not override another: deleting an override changes what
  calls through the base class run.
- No other member overrides or implements it, and it does not implement an
  interface member: calls reach such members without naming them.

## Transformation

- The member is removed with its documentation comment and the comments
  directly above it.
- A field or event declared alongside others loses just its declarator:
  `private int _count, _spare;` becomes `private int _count;`.
- The blank line that separated the member goes with it, so the members either
  side stay one blank line apart, and a member that becomes first in its type
  follows the opening brace directly.

## Preserved

- Every other member, including other overloads of a method.
- Preprocessor directives around the member, such as `#region` and `#if`.

## Limitations

- A field or property initializer with side effects is deleted with the
  member, so the side effect no longer happens when the type is constructed.
- Uses by reflection, serialization or string names are not found.
- Constructors, operators and finalizers are not offered.

## Error codes

| Code | Meaning |
|---|---|
| `member-referenced` | code outside the member refers to it |
| `overrides-member` | the member overrides a base class member |
| `has-overrides` | a subclass overrides the member |
| `has-implementations` | the member is an interface member that a type implements |
| `implements-interface` | the member implements an interface member |
| `not-a-member` | the target is a type |
