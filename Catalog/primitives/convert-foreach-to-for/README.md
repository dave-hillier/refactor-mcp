# Convert Foreach to For

Turns a `foreach` over an indexable collection into an index loop that reads
each element into the loop's variable: `foreach (var item in items)` becomes
`for (int i = 0; i < items.Count; i++)` with `var item = items[i];` as the first
statement of the body. The reverse of Convert For to Foreach.

## Target

The loop, by a caret anywhere in it. A caret inside nested loops picks the
innermost `foreach`.

## Precondition

- The collection has an `int` indexer and an `int` `Length` or `Count`, as
  arrays, strings, spans, `List<T>`, `IList<T>` and `IReadOnlyList<T>` do, and
  the indexer returns the elements the `foreach` yields.
- The collection is a local, parameter, field or property, possibly through
  `this`, since the loop's condition reads it on every iteration.
- The loop is not an `await foreach`.
- A given index name is not already declared or used in the loop's scope.

## Transformation

- The header becomes `for (int i = 0; i < collection.Length; i++)`, with `Count`
  for collections that have no `Length`.
- The loop's variable is declared as the body's first statement, with the type
  the `foreach` wrote, from `collection[i]`. When the `foreach` converted each
  element explicitly, as `foreach (int n in doubles)` does, the value is cast.
- A deconstructing `foreach (var (a, b) in pairs)` deconstructs
  `pairs[i]` instead.
- A body without braces gets a block.
- The index is `arguments.name`, or the first of `i`, `j`, `k` and `index` that
  is not declared or used in the loop's scope.

## Preserved

- Behaviour for collections not changed during the loop: the same elements in
  the same order, each in a fresh variable, so lambdas that capture it still
  see their own element.
- Comments above the loop, after its header and in its body.

## Limitations

- A collection computed by an expression is refused rather than stored in a
  local first.
- A body that changes the collection, which the `foreach` would have stopped
  with an exception, runs on over the changed collection.
- The refactoring changes one loop, so there are no references in other files
  to update.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-foreach-loop` | the caret is not in a `foreach` loop |
| `not-indexable` | the collection has no `int` indexer with a `Length` or `Count`, or the loop is an `await foreach` |
| `collection-not-simple` | the collection is computed, and the loop's condition would compute it on every iteration |
| `name-conflict` | the given index name is already declared or used in the loop's scope |
