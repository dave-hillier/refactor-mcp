# Inline Class

Moves every member of a class that is not pulling its weight into the one
class that holds an instance of it, and deletes it. The reverse of Extract
Class.

## Recipe

1. Move each member into the holding class with the `into` form of the move
   primitives, which rewrites its uses through the holder to be direct. A
   method moves before the members it uses, since until then its uses of
   them are inside the class being inlined, with no holder to go through; it
   reaches them through the holder meanwhile.
   `{ "refactoring": "move-instance-method", "target": { "symbol": "M:Shop.Address.Format" }, "arguments": { "into": "Customer" } }`,
   `{ "refactoring": "move-property", "target": { "symbol": "P:Shop.Address.Street" }, "arguments": { "into": "Customer" } }`,
   `{ "refactoring": "move-field", "target": { "symbol": "F:Shop.Address._country" }, "arguments": { "into": "Customer" } }`.
2. Change Accessibility back to `private` on each private member that a moved
   method made `internal` so it could reach it through the holder:
   `{ "refactoring": "change-accessibility", "target": { "symbol": "F:Shop.Customer._country" }, "arguments": { "accessibility": "private" } }`.
3. Safe Delete Member on the holder, now unused:
   `{ "refactoring": "safe-delete-member", "target": { "symbol": "F:Shop.Customer._address" } }`.
4. Safe Delete Type on the class:
   `{ "refactoring": "safe-delete-type", "target": { "symbol": "T:Shop.Address" } }`.

The moves place fields after the holding class's last field and other members
at its end in the order they move, so the result matches the dedicated
implementation when the members are moved in their declared order. A class
whose methods follow the members they use, as is usual, ends up with those
methods first.

## Target

The class to inline, by symbol: `"target": { "symbol": "T:Shop.Address" }`.

## Precondition

- The class is a plain class: not static, abstract, generic or partial; no
  base class other than `object` and no interfaces; no constructor, finalizer,
  static member or nested type.
- Exactly one instance field or get-only auto-property of another class (the
  holder) has the class's type, and it creates the object in its initializer
  with `new Address()` or `new()`, without arguments or an object initializer.
  So each instance of the holding class has exactly one, from the start, and
  it is never replaced.
- Nothing else names the class: no parameter, local, return type, base list,
  `typeof`, `nameof` or other `new`.
- Every use of the holder reaches a member of the class, as in
  `_address.Street`; it is not passed on, compared, assigned or reached
  through `?.`.
- No member of the holding class, or of a class it derives from, has the name
  of a member of the class.
- The result compiles.

## Transformation

- Each member of the class moves into the holding class, with its comments
  and modifiers: fields after the holding class's last field, other members
  at its end, in their order.
- Each use through the holder becomes direct: `_address.Street` becomes
  `Street`, or `this.Street` where a local or parameter of that name hides it;
  `this._address.Street` becomes `this.Street`; and `customer.Address.Street`
  in another class becomes `customer.Street`.
- The holder is removed, and the class is deleted, with its file when nothing
  else is left in it.
- The holding class's file gains the usings the moved members need.

## Preserved

- The behaviour of every use: each holding instance had exactly one instance
  of the class, whose state now lives in the holding instance itself.
- Initializers of the moved fields and properties, which run when the holding
  class is constructed, as the holder's `new` did.

## Limitations

- Members keep their accessibility, so a public member of the class becomes a
  public member of the holding class; narrow it with Change Accessibility.
- A class whose namespace holds nothing else leaves `using` directives for
  that namespace behind, which the compile check refuses.
- A class that is constructed with arguments or set up in a constructor is
  refused rather than having its set-up merged into the holding class.

## Error codes

| Code | Meaning |
|---|---|
| `unsupported-class` | the class is not a plain class that its members can leave |
| `type-referenced` | code other than the holder names the class |
| `no-holder` | no field or property holds an instance of the class |
| `several-holders` | several fields or properties hold an instance of the class |
| `holder-not-created` | the holder is static, or does not create the object in its initializer |
| `member-exists` | the holding class already has a member of the same name |
| `holder-escapes` | a use of the holder does something other than reach a member of the class |
