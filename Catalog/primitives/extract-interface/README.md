# Extract Interface

Declares some or all of a class's public members in a new interface, in a new
file, and makes the class implement it. Introduce Interface for Dependency
uses it before changing the type of a field or parameter to the interface.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | yes | The interface's name, such as `IOrder` |
| `members` | no | The member names to declare, as an array. A method name takes every overload; `this` names the indexer. Omit it for every public instance member |
| `file` | no | The file for the interface, relative to the solution. Defaults to `<name>.cs` beside the class |

The target is the class, by symbol: `"target": { "symbol": "T:Shop.Order" }`.

## Precondition

- Every named member exists and is a public instance method, property,
  indexer or event, so the class can implement it implicitly.
- Without `members`, the class has at least one such member.
- No type with the interface's name and the class's arity exists in the
  class's namespace.
- The result compiles.

## Transformation

- The interface is public, in the class's namespace, following the namespace
  style of the class's file, and has the class's type parameters and
  constraints: `Repository<T> where T : class` gives
  `IRepository<T> where T : class`.
- Members appear in the class's order, one per line, each with its
  documentation comment. Methods keep their parameters, defaults and
  constraints; properties and indexers declare only the accessors callers can
  use, so `{ get; private set; }` becomes `{ get; }` and an expression body
  becomes `{ get; }`.
- The interface file has the usings its signatures need and no others.
- The interface is added after the class's existing base class and
  interfaces.

## Preserved

- The class's members and layout; only its base list changes.
- Every caller, which still uses the class.

## Limitations

- Existing parameters and fields of the class's type are not changed to the
  interface; Change Type does that.
- Attributes on members are not copied.
- An event field declaring several events at once cannot be chosen.

## Error codes

| Code | Meaning |
|---|---|
| `member-not-found` | a named member does not exist in the class |
| `member-not-eligible` | a named member is not a public instance member |
| `no-members` | no member names were given and the class has no public instance members |
| `type-already-exists` | the namespace already has a type with the interface's name |
| `breaks-compilation` | the result would not compile |
