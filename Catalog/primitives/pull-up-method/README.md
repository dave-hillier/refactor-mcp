# Pull Up Method

Moves a method from a class into its base class, or declares it abstract in
the base class and makes the subclasses' versions override it.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `abstract` | no | `true` to declare the method abstract in the base class and leave the bodies in the subclasses |

The target is the method, by symbol, which tells overloads apart:
`"target": { "symbol": "M:Shop.Manager.Pay(System.Decimal)" }`.

## Precondition

- The class has a base class, declared in the solution.
- The method uses nothing the base class cannot see: no other member or
  nested type of the subclass, and no type parameter of the subclass the base
  class does not share. With `abstract`, only the signature has to meet this.
- Neither the base class nor a class above it has a method with the same
  parameters, or a non-method member, of that name.
- Every other subclass that declares a method with the same name and
  parameters is a direct subclass, and declares the same method with the
  same body. With `abstract`, the body may differ, but every other direct
  subclass that is not abstract must declare the method.
- With `abstract`, the base class is abstract.
- The result compiles.

## Transformation

- The method goes at the end of the base class, with its documentation and
  comments. A private method becomes protected.
- The subclass's type parameters are written as the base class's, and the
  base class's file gains the usings the method needs.
- Identical methods in the other subclasses are removed.
- With `abstract`, the base class gains an abstract declaration with the
  method's signature and accessibility, and the method in the subclass and
  each sibling's matching method become `override`s. The body and its
  documentation stay where they are.

## Preserved

- Every call to the method, which now binds to the inherited method or the
  override.
- The other overloads, which stay in the subclass.

## Limitations

- A call through `base.` inside the moved method keeps its text, so it now
  reaches the base class's own base; the compile check refuses it only when
  that no longer binds.
- Usings the subclass's file no longer needs are left in place.

## Error codes

| Code | Meaning |
|---|---|
| `no-base-class` | the class has no base class other than `object` |
| `base-not-in-source` | the base class is not declared in the solution |
| `member-exists-in-base` | the base class or a class above it already has the method |
| `conflicts-with-sibling` | another subclass has a different method with that signature |
| `uses-subclass-members` | the method uses something only the subclass has |
| `base-not-abstract` | `abstract` was asked for but the base class is not abstract |
| `sibling-lacks-implementation` | with `abstract`, another subclass does not declare the method |
| `breaks-compilation` | the result would not compile |
