# Convert For to Foreach

Turns an index loop that only reads each element of a collection in turn into
a `foreach` over the collection: `for (int i = 0; i < items.Count; i++)` with
`items[i]` in the body becomes `foreach (T item in items)` with `item`. The
reverse of Convert Foreach to For.

## Target

The loop, by a caret anywhere in it. A caret inside nested loops picks the
innermost `for`.

## Precondition

- The loop declares one `int` index starting at `0`, runs while it is less
  than the collection's `Length` or `Count`, and increments it by one with
  `i++`, `++i` or `i += 1`.
- The collection is a local, parameter, field or property, possibly through
  `this`, so evaluating it once gives the same collection.
- The index is used in the body only to read `collection[i]`, and not inside a
  lambda or local function, which would see the index change.
- The body does not modify the collection: it does not assign it, assign its
  elements or members, or call a method on it that returns nothing or is known
  to change it (`Remove`, `Pop`, `Dequeue` and the `Try` forms).
- The collection can be enumerated with `foreach` and yields the same elements
  it indexes.
- The element's name is not already declared or used in the loop's scope.

## Transformation

- The loop becomes `foreach (T name in collection)` and each `collection[i]` in
  the body becomes `name`.
- The element is declared with `var` when the index was, otherwise with the
  element's type, written as briefly as the scope allows. In a nullable context
  the type keeps its annotation.
- The name is `arguments.name`, or the singular of the collection's name
  (`orders` gives `order`, `entries` gives `entry`, a leading underscore is
  dropped), or `item` when the collection's name is not a plural or its
  singular is taken.

## Preserved

- Behaviour: the same elements are read in the same order.
- The body's statements, braces or their absence, and comments, including a
  comment after the loop's header.

## Limitations

- Loops that count down, start elsewhere, step by more than one or stop before
  the end are refused rather than converted with `Skip`, `Take` or `Reverse`.
- Modifications through another reference to the same collection, or by a
  method that returns a value, are not detected.
- A struct element whose members the body assigns is refused by the compile
  check rather than by a precondition, since a `foreach` variable is read-only.
- The refactoring changes one loop, so there are no references in other files
  to update.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-for-loop` | the caret is not in a `for` loop |
| `unsupported-loop-shape` | the loop does not count an index from 0 up to the collection's `Length` or `Count` by one |
| `index-used-otherwise` | the index is used other than to read the collection's element |
| `collection-modified` | the body changes the collection or its elements |
| `not-enumerable` | the collection cannot be enumerated with `foreach` to give the elements it indexes |
| `name-conflict` | the element's name is already declared or used in the loop's scope |
