# Replace Error Code with Exception

Makes a method that reports failure through its return value return nothing
and throw instead, and turns each caller that tested the value into a
`try`/`catch`.

This is a generator: it changes how failure travels, so the fixtures pin one
chosen design rather than the only correct answer.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `exception` | no | The exception type to throw and catch, by simple or qualified name. Defaults to `InvalidOperationException` |

The target is the method, by symbol:
`"target": { "symbol": "M:Shop.Account.Withdraw(System.Decimal)" }`.

## Precondition

- The method returns `int`, where `0` means success and any other value is an
  error code, or `bool`, where `true` means success.
- Every `return` returns a constant, and at least one returns a failure.
- The method is not virtual, abstract, an override or an interface
  implementation, since the other implementations would keep returning codes.
- The exception type exists, is accessible and derives from `Exception`.
- Every caller either ignores the result or tests it for success as the whole
  condition of an `if`: `M() != 0`, `M() == 0`, `0 != M()` for an `int`;
  `M()` or `!M()` for a `bool`.
- The result compiles.

## Transformation

- The return type becomes `void`.
- A success return becomes `return;`, which is dropped when it is the last
  statement of the body and carries no comment.
- A failure return becomes `throw new InvalidOperationException(...)`. When
  the exception type has a constructor taking a string, the message is
  `"<Method> returned error code <code>"` for an `int` and
  `"<Method> failed"` for a `bool`; otherwise the parameterless constructor is
  used.
- An expression-bodied method that always fails gets a block body that throws.
- A caller's `if` becomes:

  ```csharp
  try
  {
      <the call>;
      <the branch taken on success>
  }
  catch (InvalidOperationException)
  {
      <the branch taken on failure>
  }
  ```

  A missing branch leaves its block empty.
- `using System;` is added where the default exception is named.

## Behaviour changes

- A caller that ignored the result now sees the exception instead of carrying
  on. Such calls are left as they are.
- Error codes are no longer distinguishable by value, except through the
  exception's message.
- Statements of the success branch run inside the `try`, so an exception of
  the same type thrown there is caught by the failure branch.

## Preserved

- Which branch runs for each outcome of the call, for every caller the tool
  rewrites.
- Comments on the rewritten returns and on the statements of both branches.
- Other overloads of the method and their callers.

## Limitations

- Only `0` (or `true`) means success. A method for which some other value
  also means success, such as a positive count, has those returns turned into
  throws.
- Callers that store the result, return it, combine it with other conditions
  or pass the method as a delegate are refused.
- Each error code is not given its own exception type.

## Error codes

| Code | Meaning |
|---|---|
| `in-hierarchy` | the method is virtual, abstract, an override or an interface implementation |
| `unsupported-return-type` | the method returns neither `int` nor `bool` |
| `non-constant-return` | a `return` returns a value that is not a constant |
| `no-error-code` | the method never returns a failure |
| `exception-type-not-found` | no accessible exception type has the given name |
| `unsupported-caller` | a caller uses the result other than by testing it in an `if` |
| `breaks-compilation` | the result would not compile |
