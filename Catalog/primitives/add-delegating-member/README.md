# Add Delegating Member

Adds to a class a member that forwards to the member of the same name of one
of its fields, or of its base class. Replace Inheritance with Delegation uses
it to give a class, while it still derives from its base class, its own
members for the inherited ones other code uses, so the class can later stop
deriving without its callers noticing.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `member` | yes | The member to forward to: its name, `this` for an indexer, or its documentation comment id (`M:Shop.Printer.Print(System.String)`) when overloads share the name |
| `via` | yes | The name of the field to forward through, or `base` for the base class |

The target is the class, by symbol: `"target": { "symbol": "T:Shop.Stack" }`.

## Precondition

- The target is a class or struct; with `base`, a class deriving from a class
  other than `object`. With a field name, the class declares that field.
- The field's type, or the base class and the classes it derives from, has an
  instance member of that name the class can reach through the field or
  `base`, and exactly one unless it is named by id.
- The member is a method, property, indexer or field. Events, static members,
  generic methods, methods with `ref`, `out`, `in`, `params` or optional
  parameters, and members returning by reference are refused.
- The class does not already declare a member with that name, other than a
  method overload with different parameter types.
- No existing reference changes meaning, other than in the ways below, and
  the result compiles.

## Transformation

- The new member has the forwarded member's name, type, parameters and
  accessibility. A method becomes an expression-bodied method
  (`public string Greeting(string salutation) => _customer.Greeting(salutation);`).
  A property or indexer becomes a property or indexer whose getter reads the
  member and, when the forwarded member has a setter as accessible as itself,
  whose setter writes it, with the accessors on one line. A field becomes a
  property, get-only when the field is `readonly`.
- It is placed after the class's fields and the members right after them that
  already forward through the same field or `base`, or first in the class
  when there are none, with a blank line on each side.
- When it hides an inherited member, as a member forwarding to `base` always
  does, it is declared `new`.
- Inside the class, uses of an inherited member the new one hides, written
  bare or with `this`, are written with `base` (`this[Count - 1]` becomes
  `this[base.Count - 1]` when forwarding `Count`), so they keep reaching the
  inherited member.
- Elsewhere, uses through the class of the member forwarded to `base` now
  reach the new member, which reaches the same member.

## Preserved

- What every existing reference reaches when it runs.
- Comments and blank lines around the neighbouring members.

## Limitations

- Implicit uses, such as a collection initializer calling an `Add` the new
  member hides, are not checked.
- The accessors of a forwarding property or indexer are written on one line.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-class` | the target is not a class or struct |
| `field-not-found` | the class has no field with the name `via` gives |
| `no-base-class` | `via` is `base` and the class derives only from `object` |
| `member-not-found` | the field's type or the base class has no such member the class can reach |
| `ambiguous-member` | several members share the name; name one by its documentation comment id |
| `unsupported-member` | the member is an event, static, generic, has `ref`, `out`, `in`, `params` or optional parameters, or returns by reference |
| `name-conflict` | the class already declares a member of that name |
| `changes-meaning` | a reference outside the class, or to a member the class declares, would reach the new member instead |
| `breaks-compilation` | the result would not compile, for example because the new member would expose a less accessible type |
