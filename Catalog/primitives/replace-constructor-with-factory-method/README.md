# Replace Constructor with Factory Method

Adds a static factory method that calls a constructor, narrows the
constructor, and makes every creation in the solution call the factory.

## Arguments

| Argument | Meaning |
|---|---|
| `name` | the factory method's name; `Create` when omitted |
| `accessibility` | the constructor's new accessibility; `private` when omitted |

The target is the constructor, by symbol, which tells overloads apart.

## Precondition

- The constructor belongs to a class that is not abstract or static.
- The accessibility is one of the six C# accessibilities.
- The type has no member with the factory's name, other than methods whose
  parameters differ.
- No creation sets members with an object or collection initializer.
- Everything else that calls the constructor directly, such as a derived
  class's `base(...)` or a `new()` constraint, can still reach it with the new
  accessibility.

## Transformation

- The factory, `static`, with the constructor's former accessibility, its
  parameters (types, modifiers and defaults) and its type as the return type,
  goes after the constructor and returns `new Type(arguments)`.
- The constructor's accessibility becomes the requested one; its
  documentation and other modifiers stay.
- Every `new Type(...)` and target-typed `new(...)` calling the constructor,
  outside the factory, becomes `Type.Name(...)` with the same arguments,
  including their names, and the same trivia. Constructed generic types keep
  their type arguments, as in `Box<int>.Create(5)`.
- Calls of other overloads and `this(...)` or `base(...)` calls are left alone.

## Preserved

- Every creation still runs the same constructor with the same arguments.

## Limitations

- Creations through reflection, `Activator.CreateInstance` or a `new()`
  generic constraint are not rewritten; the last is refused by the compile
  check when the constructor becomes inaccessible.

## Error codes

| Code | Meaning |
|---|---|
| `invalid-accessibility` | the accessibility argument is not a C# accessibility |
| `abstract-type` | the constructor belongs to an abstract or static class |
| `name-conflict` | the type already has a member with the factory's name and parameters |
| `object-initializer` | a creation uses an object or collection initializer |
| `constructor-still-needed` | a derived class or constraint needs the constructor at its old accessibility |
