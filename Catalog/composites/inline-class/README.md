# Inline Class

Moves every member of a class that is not pulling its weight into the one
class that holds an instance of it, and deletes it. The reverse of Extract
Class.

## Recipe

The plan's recipe is Move Field and Move Instance Method into the using class,
then Safe Delete Type. The move primitives move a member along a field or
parameter of its own class, and the class being inlined has no reference back
to the class holding it, so they cannot move its members there. The recipe
that the primitives can express covers a class without state:

1. Make Method Static on each method, which no longer needs the instance:
   `{ "refactoring": "make-method-static", "target": { "symbol": "M:Shop.PriceCalculator.Discounted(System.Decimal)" } }`.
2. Move Static Method into the holding class, without a stub:
   `{ "refactoring": "move-static-method", "target": { "symbol": "M:Shop.PriceCalculator.Discounted(System.Decimal)" }, "arguments": { "to": "Order", "stub": false } }`.
3. Safe Delete Member on the field that held the instance, now unused:
   `{ "refactoring": "safe-delete-member", "target": { "symbol": "F:Shop.Order._calculator" } }`.
4. Safe Delete Type on the class:
   `{ "refactoring": "safe-delete-type", "target": { "symbol": "T:Shop.PriceCalculator" } }`.

The recipe leaves the moved methods static. The dedicated implementation keeps
them as instance methods, and also moves fields and properties.

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
