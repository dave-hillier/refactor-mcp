# Push Down Method

Moves a method from a class into the direct subclasses that need it: those
whose code calls it, or whose callers reach it through a reference of the
subclass's type. When nothing calls it, every direct subclass gets a copy.
An abstract method is removed instead, and each subclass's implementation
stays as an ordinary method.

## Arguments

None. The target is the method, by symbol, which tells overloads apart:
`"target": { "symbol": "M:Shop.Employee.Report(System.Int32)" }`.

## Precondition

- The class has at least one subclass declared in the solution.
- The method does not override one from further up: callers reach an
  override through the type that declared the original, without naming the
  class.
- A virtual method is not overridden anywhere, since the subclasses would
  then disagree about what the moved method does.
- The class does not call the method itself, and no code calls it through a
  reference of the class's type or of a type above it.
- The method uses nothing private to the class.
- No receiving subclass already has a method with the same parameters, or a
  non-method member, of that name.
- The result compiles.

## Transformation

- Each receiving subclass gets the method at its end, with its
  documentation, comments and modifiers.
- In a sealed subclass a protected method becomes private and a virtual one
  stops being virtual.
- The class's type parameters are written as the subclass's type arguments,
  and each subclass's file gains the usings the method needs.
- For an abstract method, the declaration is removed and each override loses
  its `override` (and `sealed`) modifier, becoming `virtual` when a class
  further down overrides it in turn.

## Preserved

- Every call to the method, which now binds to the subclass's copy.
- The other overloads, which stay in the class.

## Limitations

- Each receiving subclass gets its own copy of the body; later changes to one
  do not reach the others.
- An interface the class implements through the method is refused by the
  compile check rather than moved to the subclasses.
- Usings the class's file no longer needs are left in place.

## Error codes

| Code | Meaning |
|---|---|
| `no-subclasses` | no class in the solution derives from the method's class |
| `method-is-override` | the method overrides one from a class further up |
| `method-is-overridden` | the method is virtual and a subclass overrides it |
| `used-by-base` | the method's class calls it itself |
| `used-through-base` | code calls the method through a reference of the method's class |
| `uses-base-private-members` | the method uses something private to its class |
| `member-exists-in-subclass` | a receiving subclass already has the method |
| `breaks-compilation` | the result would not compile |
