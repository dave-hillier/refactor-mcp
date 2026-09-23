# Change Base Type

Sets, replaces or removes the base class of a class. Extract Superclass and
Replace Inheritance with Delegation use it to move a class under a new parent
or off an old one.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `to` | no | The new base class, as it would be written in the class's file. Omit it to remove the base class |

The target is the class, by symbol: `"target": { "symbol": "T:Shop.Manager" }`.

## Precondition

- The target is a class; structs, interfaces and enums have no base class.
- `to` names a class that is neither sealed nor static, and that does not
  already derive from the target.
- Removing needs a base class to remove.
- Nothing relies on the old base in a way the new one does not support: the
  class's own members, its constructors' `base(...)` calls, its overrides, and
  code elsewhere that uses inherited members or converts the class to the old
  base must all still compile.

## Transformation

- An existing base class is replaced in place; otherwise the new base class
  goes first in the base list, ahead of any interfaces.
- Removing the base class keeps the interfaces, and drops the colon when the
  base class was all the list held.
- When the new base class only binds with a using directive, the directive is
  added, and a qualified name the usings already cover is shortened.
- A `new` modifier on a member of the class that no longer hides anything,
  such as a forwarding member once the base class it hid is removed, is
  dropped.

## Preserved

- The interfaces the class implements and their order.
- Comments and line breaks around the declaration.
- Every constructor chain and override, which is why a change that would break
  one is refused rather than repaired.

## Limitations

- Nothing is adapted to the new base: members the class used from the old base
  are not recreated, and `base(...)` calls are not rewritten. Composites that
  need that pull members up or introduce delegation first.
- Records are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-class` | the target is a struct, interface or other non-class type |
| `no-base-class` | there is no base class to remove |
| `type-not-found` | `to` names no type the class's file can see |
| `invalid-base-type` | `to` is sealed, static, an interface or not a class |
| `circular-base` | `to` already derives from the target |
| `base-members-in-use` | the class or code using it needs a member, constructor or overridden method only the old base has |
| `base-conversion-in-use` | code converts the class to the old base |
