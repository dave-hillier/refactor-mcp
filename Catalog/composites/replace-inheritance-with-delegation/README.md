# Replace Inheritance with Delegation

For a class that inherits from another only to reuse some of its members:
the class holds an instance of its old base class in a field, reaches what it
used to inherit through the field, forwards the inherited members other code
uses, and stops deriving from the old base class.

## Recipe

1. `introduce-field` of the base type on the class:
   `"target": { "symbol": "T:Shop.Stack" }, "arguments": { "type": "List<int>", "name": "_list" }`.
2. `add-delegating-member` forwarding to the base class, for each inherited
   member other code uses through the class:
   `"target": { "symbol": "T:Shop.Stack" }, "arguments": { "member": "Count", "via": "base" }`,
   then `"member": "this"` for the indexer. Each is declared `new`, and the
   class's own uses of the inherited member are written with `base`.
3. `replace-base-uses-with-field`, so the class's uses of its inherited
   members, those in the new forwarding members included, go through the
   field: `"target": { "symbol": "T:Shop.Stack" }, "arguments": { "field": "_list" }`.
4. `change-base-type` removing the base class, which also drops the `new`
   modifiers that no longer hide anything:
   `"target": { "symbol": "T:Shop.Stack" }`.

The plan's recipe, Introduce Field, delegate each used inherited member,
Change Base Type, has no single primitive for its middle step. It is split
here into forwarding members added while the base class is still there,
which changes nothing for their callers, and one step that moves every use
of the base class part onto the field at once, which is what keeps each step
behaviour-preserving.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `field` | no | The name of the field holding the old base class; defaults to `_` and the base class name in camel case |

The target is the class, by symbol: `"target": { "symbol": "T:Shop.Stack" }`.

## Precondition

- The target is a class, not static, not a record and not partial, with a
  base class other than `object`, which may be a library class.
- No member of the class or its bases has the field's name.
- The class overrides no member of the base class, since the base class would
  go on calling its own version.
- The class uses no protected member of the base class, which a field cannot
  reach.
- Inherited events, and inherited methods with type parameters or `ref`,
  `out`, `params` or optional parameters, are not used by other code.
- The result compiles: no code converts the class to its old base class or to
  an interface it only had through it, and no code relies on an extension
  method of the old base class.

## Transformation

- The class gains `private readonly List<int> _list = new List<int>();` as its
  first member, the type written as the base list wrote it. When a constructor
  passes arguments to the base class, the field has no initializer, and each
  constructor that does not chain to another assigns it first, passing on the
  arguments it passed to the base (`_catalogue = new Catalogue(title);`).
- Inside the class, uses of inherited members through an implicit `this`,
  `this` or `base` go through the field: `this[Count - 1]` becomes
  `_list[_list.Count - 1]`. Inherited static members are qualified by their
  class.
- Each inherited member that other code uses through the class, including its
  subclasses, is forwarded by a member of the class with the same name, as
  accessible as the inherited one, after the field: properties and fields as
  properties, `get`-only unless other code sets them, indexers likewise, and
  methods by expression-bodied methods.
- The base class is removed from the base list; interfaces stay.

## Preserved

- Every use of the class's own members and of the forwarded members, which
  reach the same code as before.
- Comments and interfaces on the declaration.

## Limitations

- Members of `object` the old base class overrides, such as `ToString`, are
  not forwarded, so their results can change.
- Forwarding members are expression-bodied, and their accessor lists are
  written on one line.
- A `new` modifier on a member that hid an inherited one is left in place.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-class` | the target is not a non-static class |
| `no-base-class` | the class derives only from `object` |
| `partial-type` | the class is declared in several parts |
| `name-conflict` | the class or a base already has a member with the field's name |
| `overrides-base-member` | the class overrides a member of the base class |
| `uses-protected-member` | the class uses a protected member of the base class |
| `unsupported-member` | other code uses an inherited event |
| `unsupported-method` | other code uses an inherited method with type parameters or `ref`, `out`, `params` or optional parameters |
| `breaks-compilation` | the result would not compile, for example because code converts the class to its old base |
