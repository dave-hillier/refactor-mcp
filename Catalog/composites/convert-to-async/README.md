# Convert to Async

Makes a method that blocks on tasks async: it awaits them instead and returns
a task. Its callers await it and become async in turn, repeating up to
callers that cannot be async, which go on blocking on the task.

## Recipe

1. `make-method-async` on the method. It awaits the tasks the method blocked
   on and returns a task; every caller blocks on that task with
   `.GetAwaiter().GetResult()`.
2. `make-method-async` on each caller that can be async, which now blocks on
   the task, so it awaits it and its own callers block in turn. Repeat up to
   callers that cannot be async, such as constructors, which keep blocking.

```json
"steps": [
  { "refactoring": "make-method-async", "target": { "symbol": "M:Stock.Inventory.Available(System.String)" } },
  { "refactoring": "make-method-async", "target": { "symbol": "M:Stock.Inventory.InStock(System.String)" } },
  { "refactoring": "make-method-async", "target": { "symbol": "M:Stock.Report.Summary(Stock.Inventory)" } }
]
```

The plan's first step, Change Return Type to `Task`, cannot run on its own:
the method's body still returns the value, not a task. Make Method Async
changes the return type, adds `async` and awaits the waits as one step, and
leaves the callers blocking so each step compiles and preserves behaviour.
The dedicated implementation finds the callers that can be async itself and
converts them all as one change.

## Arguments

None. The target is the method, by symbol.

## Precondition

- The method is an ordinary method with a body that does not already return
  a task, is not an iterator, does not return by reference and has no `ref`,
  `out` or `in` parameters.
- It is not virtual, abstract, an override or an interface implementation,
  whose signature it shares with other methods.
- It blocks on at least one task outside any lambda or `lock`:
  `task.Result`, `task.Wait()` or `task.GetAwaiter().GetResult()`.
- It is only called, never used as a method group, where a method returning
  a task would no longer fit the delegate.
- The result compiles.

## Transformation

- The method is marked `async` and returns `Task<T>` in place of `T`, or
  `Task` in place of `void`. A `using System.Threading.Tasks;` directive is
  added where the file needs one.
- Each blocking wait in the method becomes `await` on the task.
- Each call of a method made async is awaited when it is in a method that
  can be async: one that meets the precondition above, apart from blocking on
  a task, or one that is already async. That method is made async the same
  way, and its callers in turn.
- A call in a constructor, accessor, lambda, local function, query or `lock`,
  or in a method that cannot be async, blocks with
  `.GetAwaiter().GetResult()`.

## Preserved

- The values computed and the order calls are made in.
- Comments and layout around the changed calls and declarations.

## Limitations

- Awaiting rethrows a task's exception as it is, where `.Result` and
  `.Wait()` wrapped it in an `AggregateException`; a caller catching
  `AggregateException` sees a different exception.
- Awaiting resumes on the captured synchronization context, which can change
  which thread code after the await runs on.
- Method names keep no `Async` suffix; Rename adds one.
- A caller whose signature is shared, such as an override, blocks rather than
  changing its whole hierarchy.

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
