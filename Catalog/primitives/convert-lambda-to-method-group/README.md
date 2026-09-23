# Convert Lambda to Method Group

Replaces a lambda that only passes its parameters on to a method, such as
`n => Format(n)`, with the method group `Format`. The reverse of Convert
Method Group to Lambda.

## Target

The lambda, by a caret anywhere inside it.

## Precondition

- The lambda, or anonymous method, is not `async`, and its body is a single
  call: an expression body, or a block holding only that call as a statement
  or a `return`.
- The call passes exactly the lambda's parameters, in order, each by its
  simple name, without names and with the ref kind each was declared with.
- The call is of a method, not of a delegate, and the expression naming the
  method does not use the lambda's parameters, so `name => name.Trim()` is
  not a candidate.
- The receiver, if any, has the same value whenever the lambda could run: no
  receiver, `this`, `base`, a type, a readonly field of such a receiver, or a
  local or value parameter never assigned after its declaration. A lambda
  evaluates its receiver on every call; a method group evaluates it once.
- As a method group, the expression binds to the same method, converts to
  the same delegate type, and leaves the overload of any call it is passed
  to unchanged, with no new compile errors. A lambda can convert its
  arguments, as from `int` to `long`; a method group cannot.

## Transformation

- The lambda is replaced by the expression naming the method, with its
  receiver and any type arguments as written in the call:
  `(a, b) => Add(a, b)` becomes `Add`, `x => _formatter.Format(x)` becomes
  `_formatter.Format`.

## Preserved

- Behaviour: the delegate calls the same method with the same arguments.
- Comments before and after the lambda.

## Limitations

- A receiver that is null throws when the method group is created, where the
  lambda would have thrown only when called.
- A readonly field is treated as stable even while a constructor is still
  assigning it.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-lambda` | the caret is not inside a lambda |
| `not-a-forwarding-call` | the body is not a single call passing the parameters in order |
| `unstable-receiver` | the receiver could change between creating the delegate and calling it |
| `resolution-changes` | the method group would bind to a different method, delegate type or overload |
