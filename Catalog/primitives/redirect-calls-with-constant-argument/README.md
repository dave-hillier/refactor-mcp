# Redirect Calls with Constant Argument

Makes calls that pass a constant for a parameter call, directly, the method
the original runs for that value. When `SetValue(string name, int value)`
calls `SetHeight(value)` for `"height"`, `box.SetValue("height", 10)` becomes
`box.SetHeight(10)`. Replace Parameter with Explicit Methods uses it once the
branches have methods of their own.

## Target

The method whose calls are redirected, by symbol, with:

| Argument | Meaning |
|---|---|
| `parameter` | the parameter the calls pass the constant for |
| `value` | the constant, written as C#, such as `"\"height\""` or `"Zone.Europe"` |
| `method` | the method the original calls for that value |

## Precondition

- The target is an ordinary, non-generic method with a block body. It is not
  virtual, abstract, an override or an interface implementation, so a call
  of it runs its body.
- The value is a constant of the parameter's type.
- The class has a method with the given name.
- With the parameter holding the value, the method does nothing but call
  that method and return what it returns. Following the value from the
  start of the body through `if` statements that compare the parameter with
  constants using built-in `==` or `!=`, `else` branches and `switch`
  statements on the parameter with constant `case` labels, the statements
  reached are `return M(...);`, or for a void method `M(...); return;`, or
  `M(...);` as the method's last statement.
- That call has no receiver or `this`, and passes some of the method's own
  parameters unchanged, in any order, each to a parameter of the same type
  and without `ref`, `out`, `in` or `params`. For a method returning a value,
  `M` returns the same type.
- The redirected calls compile, so the method is accessible wherever a call
  is.

## Transformation

- Each call of the method, in any file or project, whose argument for the
  parameter is a constant equal to the value becomes a call of the named
  method on the same receiver, passing the call's arguments for the
  parameters the method passes on, in its order.
- The arguments keep their comments. A named argument keeps its name, changed
  to the name of the parameter it now goes to.
- Calls passing another value or a variable, and calls of other overloads,
  are left alone.

## Preserved

- What each redirected call does: it runs the call the method would have
  made for the value, with the same argument values.
- The order arguments are evaluated in. A call is left alone when it would
  drop, repeat or reorder an argument that is anything but a constant, a
  local, a parameter, `this` or a field of this object, since that argument
  would no longer be evaluated as it was.

## Limitations

- A call that leaves out an optional argument, passes a `params` array in
  expanded form, or passes an argument by reference is left alone.
- A call where the named method would bind to another method, such as one a
  derived class hides it with, is left alone.
- A dispatch that compares the parameter in any other way, such as with
  `Equals`, patterns or combined conditions, ends the walk, and the method is
  refused as doing more than the call.
- The original method stays, even when no call of it is left.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-method` | the target is not an ordinary method with a block body |
| `polymorphic-method` | the method is virtual, an override or an interface implementation |
| `generic-method` | the method is generic |
| `unknown-parameter` | the method has no parameter of that name |
| `not-a-constant` | the value is not a constant of the parameter's type |
| `unknown-method` | the class has no method of the given name |
| `does-more-than-call` | for the value, the method does more than call the named method with its parameters |
| `does-not-compile` | a redirected call would not compile, such as when the named method is not accessible there |
