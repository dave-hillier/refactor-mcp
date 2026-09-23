# Convert to Async

Makes a method that blocks on tasks async: it awaits them instead and returns
a task. Its callers await it and become async in turn, repeating up to
callers that cannot be async, which go on blocking on the task.

## Recipe

1. `change-return-type` on the method to `Task`, or `Task<T>` for a method
   returning `T`.
2. Mark the method `async` and replace each blocking wait in it with `await`.
3. For each caller, await the call and repeat from step 1 on the caller, up
   to a caller that cannot be async, where the call blocks.

```json
"steps": [
  { "refactoring": "change-return-type", "target": { "symbol": "M:Stock.Inventory.Available(System.String)" }, "arguments": { "type": "Task<int>" } }
]
```

No primitive in the catalog does step 2, and Change Return Type refuses step
1 on its own, since the method's body still returns an `int`. The recipe case
records that and is marked unimplemented. The dedicated implementation does
all three steps as one change.

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
