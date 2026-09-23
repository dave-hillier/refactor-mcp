# Make Method Async

Makes a method that blocks on tasks async: it awaits them instead and returns
a task. Its callers get the task, and block on it where they used to get the
value, so only the method itself changes shape. Convert to Async repeats this
on each caller up to a boundary.

## Target

The method, by symbol. There are no arguments.

## Precondition

- The method is an ordinary method with a body, block or expression, that
  does not already return a task, is not an iterator, does not return by
  reference and has no `ref`, `out` or `in` parameters.
- It is not virtual, abstract, an override or an interface implementation,
  whose signature it shares with other methods.
- It blocks on at least one task outside any lambda, local function or
  `lock`: `task.Result`, `task.Wait()` or `task.GetAwaiter().GetResult()`.
- It is only called, never used as a method group, where a method returning
  a task would no longer fit the delegate.
- The result compiles.

## Transformation

- The method is marked `async` and returns `Task<T>` in place of `T`, or
  `Task` in place of `void`. A `using System.Threading.Tasks;` directive is
  added where the file needs one.
- Each blocking wait in the method becomes `await` on the task,
  parenthesised only where precedence needs it.
- A call of the method in the method itself or in another `async` method is
  awaited, unless it is in a lambda, local function, query or `lock`.
- Every other call blocks on the task with `.GetAwaiter().GetResult()`, so
  the caller keeps its signature and still gets the value.

## Preserved

- The values computed and the order calls are made in: a caller that blocks
  gets the value the method used to return, once the method has finished.
- Comments and layout around the changed calls and declaration.
- Waits inside lambdas and local functions, which may run later, keep
  blocking.

## Limitations

- Awaiting rethrows a task's exception as it is, where `.Result` and
  `.Wait()` wrapped it in an `AggregateException`; a caller catching
  `AggregateException` sees a different exception.
- Awaiting resumes on the captured synchronization context. A caller that
  blocks on that context's thread, as the callers left blocking do, can
  deadlock where the original wait did not, if the awaited task completed
  without needing the context.
- Method names keep no `Async` suffix; Rename adds one.

## Error codes

| Code | Meaning |
|---|---|
| `nothing-to-await` | the method does not block on a task |
| `already-async` | the method already returns a task |
| `not-a-method` | the target is not an ordinary method |
| `polymorphic-method` | the method is virtual, an override or an interface implementation |
| `ref-parameters` | the method has `ref`, `out` or `in` parameters |
| `iterator` | the method is an iterator |
| `method-group-reference` | the method is used as a method group |
