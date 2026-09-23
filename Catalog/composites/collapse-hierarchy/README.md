# Collapse Hierarchy

Merges a class and its base class into one when they are no longer different
enough to be kept apart: a subclass into its base class, or a base class into
its only subclass.

## Recipe

Merging a subclass into its base class:

1. Pull Up Field and Pull Up Method for each member of the subclass:
   `{ "refactoring": "pull-up-field", "target": { "symbol": "F:Staff.Salesman._commission" } }`,
   `{ "refactoring": "pull-up-method", "target": { "symbol": "M:Staff.Salesman.Bonus(System.Decimal)" } }`.
2. Change Type on each declaration that names the subclass, to the base class:
   `{ "refactoring": "change-type", "target": { "symbol": "M:Staff.Payroll.Pay(Staff.Salesman,System.Decimal)" }, "arguments": { "parameter": "salesman", "to": "Employee" } }`.
3. Safe Delete Type on the subclass:
   `{ "refactoring": "safe-delete-type", "target": { "symbol": "T:Staff.Salesman" } }`.

Merging a base class into its subclass uses Push Down Field and Push Down
Method instead, then Change Base Type on the subclass to the base class's own
base.

The primitives have limits the dedicated implementation does not: Pull Up
makes a private member protected, which the merged class does not need;
properties and events have no pull-up primitive; and `new Salesman()` has no
declaration that Change Type could retarget. The recipe case uses public and
protected fields and methods, and a class only named by a parameter.

## Target and arguments

The class to remove, by symbol: `"target": { "symbol": "T:Staff.Salesman" }`.

| Argument | Required | Meaning |
|---|---|---|
| `into` | no | the class to merge into: the base class (the default), or the only direct subclass |

## Precondition

For both directions:

- Both classes are classes declared in the solution, and neither is static,
  generic or partial.
- The class removed declares no constructor.
- Nothing names the class removed in `typeof` or `nameof`, whose results
  would change.
- The result compiles.

Merging a subclass into its base class, additionally:

- The subclass is not abstract and has no subclasses of its own.
- None of its members overrides another: moved up, it would replace what the
  base class does for its own instances.
- No member of the base class, of a class above it, or of another subclass
  shares a name with a member of the subclass.
- Nothing tests for the subclass or casts to it (`is`, `as`, a cast or a
  pattern), since every instance of the base class would then pass.

Merging a base class into its subclass, additionally:

- `into` is the base class's only direct subclass.
- The base class is never created itself, so every instance of it is already
  an instance of the subclass.
- The subclass does not call the base class's members through `base`.
- Every member of the subclass that shares a name with a member of the base
  class overrides it.

## Transformation

- The members of the class removed move into the class kept, with their
  documentation, comments and modifiers: fields after the last field, other
  members at the end. Private members stay private.
- Merging into the subclass, abstract members of the base class are dropped,
  and so are virtual ones the subclass overrides. The subclass's overrides of
  them lose `override` (and `sealed`), becoming `virtual` when a class further
  down overrides them in turn; an override of a member the base class itself
  overrode stays an override.
- Merging into the subclass, the subclass's base class becomes the removed
  class's own base, or none.
- The interfaces the class removed implements are added to the class kept.
- Every reference to the class removed, in any file, names the class kept:
  declarations, `new`, type arguments and base lists. Usings are added where
  the name needs them.
- The class removed is deleted, with its file when nothing else is left in it.

## Preserved

- The behaviour of every member and every call: merging up, the base class's
  instances and other subclasses gain members they do not use; merging down,
  every instance was already of the subclass.

## Limitations

- Constructors are not merged; a class that declares one is refused.
- Field initializers of members merged up run for every instance of the base
  class and its other subclasses, not only for instances of the subclass.
- A generic class, or a subclass of a constructed generic class, is refused
  rather than having its type parameters substituted.

## Error codes

| Code | Meaning |
|---|---|
| `unsupported-class` | a class is not a class, or is static, generic or partial |
| `no-base-class` | `into` is omitted and the class has no base class in the solution |
| `not-related` | `into` is neither the base class nor a direct subclass |
| `several-subclasses` | the base class has other subclasses besides `into` |
| `has-subclasses` | the subclass merged up has subclasses of its own |
| `declares-constructor` | the class removed declares a constructor |
| `overrides-member` | a member of the subclass merged up overrides another |
| `member-exists` | a member's name is already used in the class kept, above it or in another subclass |
| `type-tested` | code tests for, casts to, or takes `typeof` or `nameof` of the class removed |
| `base-instantiated` | the base class merged down is created itself |
| `uses-base` | the subclass calls the base class's version of a member through `base` |
