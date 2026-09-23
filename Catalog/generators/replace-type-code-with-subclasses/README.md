# Replace Type Code with Subclasses

Replaces an enum field that says what kind of object an instance is with a
subclass per kind. Each object's class then carries its kind, so Replace
Conditional with Polymorphism can move the behaviour that switches on it into
the subclasses.

This is a generator: it adds types and changes how objects are created, and
its `after/` pins one chosen design, described below, rather than the only
correct answer. Behaviour changes in two ways: `GetType()` returns the
subclass rather than the original class, and asking the factory for a value
the enum does not define throws `ArgumentOutOfRangeException` where the
constructor used to accept it.

## Arguments

None. The target is the field, by symbol:
`"target": { "symbol": "F:Staff.Employee._type" }`.

## Precondition

- The field's type is an enum. Run Replace Type Code with Enum first for an
  int or string code.
- The field's class is a class that can be derived from: not sealed, static
  or a record.
- No type in the class's namespace is named after an enum member.
- The field is assigned only in the constructor, not in methods, property
  setters or its declaration.
- The class has exactly one explicit constructor, which assigns the field
  from one of its parameters in a statement of its own and does not chain to
  another constructor with `this(...)`.
- The result compiles, which rules out, for example, an existing member with
  the property's name.

## Transformation

- The class becomes `abstract`.
- The field becomes an abstract get-only property, in the field's place and
  with its comments, named from the field without its leading underscore and
  with a capital first letter (`_type` becomes `Type`). It is `protected`
  when the field was private and otherwise keeps the field's accessibility.
  If the class already has a get-only property that just returns the field,
  that property becomes abstract instead and the field is removed.
- Every read of the field reads the property.
- The constructor loses the code parameter and the assignment, and becomes
  `protected`. A constructor left with no parameters and nothing to do is
  removed.
- A factory, `public static Base Create(...)`, takes the constructor's
  original parameters and returns `code switch` with an arm per enum member
  constructing its subclass, and a discard arm that throws
  `ArgumentOutOfRangeException`. It follows the constructor, or takes its
  place when the constructor was removed. `using System;` is added if needed.
- After the class, in the same file and namespace, comes one
  `sealed class <Member> : Base` per enum member, in the enum's order, with
  the class's accessibility, type parameters and constraints. It has a
  public constructor passing the remaining parameters to `base`, when there
  are any, and overrides the property with an expression body returning its
  member.
- Across the solution, `new Base(Code.Member, x)` becomes `new Member(x)`,
  and a construction whose code is not a constant becomes
  `Base.Create(code, x)`.

## Preserved

- Every read of the code gives the same value as before.
- The class's other members, their order and comments.
- The constructor's other parameters and statements, and the nullable
  annotations of its parameters, which the subclasses and factory repeat.

## Limitations

- A class that already has subclasses, or several constructors, is not
  supported.
- The subclasses are not moved to files of their own; Move Type to File does
  that.
- A target-typed `new(...)` of the class is not rewritten, so the result does
  not compile and the refactoring refuses.
- The conditionals on the code stay as they are; Replace Conditional with
  Polymorphism replaces them.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-enum` | the field's type is not an enum |
| `class-sealed` | the class is sealed, static or a record, so it cannot have subclasses |
| `type-already-exists` | the namespace already has a type named after an enum member |
| `code-changes` | the field is assigned outside the constructor, so an object's kind can change |
| `unsupported-constructor` | the class does not have exactly one constructor assigning the field from a parameter |
| `breaks-compilation` | the result would not compile |
