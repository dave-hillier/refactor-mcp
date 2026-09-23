# Replace Conditional with Polymorphism

Replaces a method whose body chooses what to do by the kind of object it is
dealing with, with a method each subclass overrides. The conditional can test
either:

- **a type code**: a property of the method's own class that every subclass
  overrides to return a constant, as Replace Type Code with Subclasses
  leaves it (`switch (Type) { case EmployeeType.Engineer: ... }`); or
- **a parameter's type**: type patterns on one of the method's parameters,
  whose class is the hierarchy's base (`case Circle c:`, `Circle c =>`,
  `shape is Circle c`). The method may live in another class.

This is a generator: it restructures the hierarchy, and its `after/` pins one
chosen design, described below, rather than the only correct answer. Each
case runs exactly as before for its subclass. Behaviour changes where the
conditional had a default that only threw: the throw is dropped, because the
abstract method leaves no subclass without an implementation. For a
parameter's type, a null argument now throws `NullReferenceException` at the
delegating call instead of reaching the default.

## Arguments

None. The target is the method, by symbol:
`"target": { "symbol": "M:Shapes.Geometry.Area(Shapes.Shape)" }`.

## Precondition

- The method's body is one of:
  - a single `switch` statement;
  - a `switch` expression, as its expression body or its only `return`;
  - an `if` / `else if` chain, optionally ending in `else`; or a run of `if`
    statements without `else` whose branches all end in `return` or
    `throw`, followed by the default's statements.
- Every test is on the same property of the method's class, against
  constants, or on the same parameter of the method, against types.
- Each label maps to subclasses declared in the solution: a constant to the
  subclasses whose override returns it, a type to that subclass. No label
  has a `when` clause, and no subclass is chosen by two cases. A case does
  not leave the switch part way through with `break` or `goto`.
- When there is no default, or the default only throws, the base class is
  abstract and every concrete subclass has a case, directly or through a
  class between it and the base.
- For a parameter's type, the cases use nothing of the class declaring the
  method except static members reached through the class's name.
- The result compiles.

## Transformation

- Each case becomes `override` of the method at the end of its subclass,
  after a blank line, with the method's accessibility. A section with
  several labels becomes an override in each of their subclasses. A case
  that is a single `return e;` or a switch expression arm becomes an
  expression body, `=> e;`; otherwise the statements form a block body,
  without a trailing `break`, keeping their comments.
- The subclass's usings gain the namespaces the case needs.
- In a generic hierarchy, each override is written with its subclass's own
  type arguments: in `Shared<TValue> : Store<TValue>`, `T` becomes `TValue`.
- For a type code, the method is replaced in place by an `abstract`
  declaration when there is no default (or the default only throws), or by
  a `virtual` method with the default's code. Private members of the class
  that the moved cases use become `protected`.
- For a parameter's type, the parameter's class gains, at its end, an
  abstract or virtual method with the same name and the method's other
  parameters, with the method's accessibility (private becomes internal).
  In the moved code the parameter and the pattern variable become the
  instance: `c.Radius` becomes `Radius`, and `c` alone becomes `this`. The
  original method keeps its signature and delegates:
  `return shape.Area();`.
- A `void` method without a default gets an empty virtual method, since
  doing nothing was its default.
- Usings the change leaves unnecessary are removed.

## Preserved

- What each subclass computes, and the signature callers use: the original
  method still exists, as the base method or as the delegating method.
- Nullable annotations on the return type and parameters.
- Comments inside the moved cases and on the method.

## Limitations

- Only one conditional, making up the whole body, is replaced. A method
  that does other work around the switch needs Extract Method first.
- Conditions combining tests (`Type == A || Type == B`), relational or
  property patterns, and `case null` are not supported.
- For a type code the code must be a property; subclasses that return their
  code from a field or a computed expression are not recognised.
- Generic type arguments are translated only for direct subclasses of the
  base.
- The overrides are not placed near other members of the subclass; they go
  at its end.

## Error codes

| Code | Meaning |
|---|---|
| `no-conditional` | the body is not one conditional on a type code property or a parameter's type |
| `unsupported-case` | a label has a `when` clause, is not a constant or type, matches no subclass, repeats a subclass, or its case leaves the switch early |
| `subclass-without-case` | there is no default and a concrete subclass has no case |
| `base-not-abstract` | there is no default and the base class is not abstract |
| `uses-caller-members` | a case on a parameter's type uses a member of the class declaring the method |
| `breaks-compilation` | the result would not compile, for example because a subclass already declares the method |
