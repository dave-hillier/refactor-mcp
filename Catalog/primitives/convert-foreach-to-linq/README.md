# Convert Foreach to LINQ

Turns a `foreach` loop that builds a list, a total, a count or a flag from a
sequence into a LINQ query in method syntax. The reverse of Convert LINQ to
Foreach.

```csharp
var names = new List<string>();
foreach (var user in users)
{
    if (user.IsActive)
    {
        names.Add(user.Name);
    }
}
```

becomes

```csharp
var names = users.Where(user => user.IsActive).Select(user => user.Name).ToList();
```

## Target

The loop, by a caret anywhere in it.

## Precondition

- The loop enumerates a generic sequence (an array or an `IEnumerable<T>`)
  into a single variable, without converting the elements to another type, and
  is not an `await foreach`.
- The body reads, from the outside in:
  - an `if` without an `else`, whose condition becomes a `Where`;
  - a local declared from the current element, when the element is not used
    after it, which becomes a `Select` whose lambda takes the local's name;
  - and finally one accumulation:
    - `list.Add(value)` on a `List<T>` of the value's type, which becomes
      `ToList` or `AddRange`;
    - `total += value` on an `int`, `long`, `float`, `double` or `decimal` of
      the value's type, which becomes `Sum`;
    - `count++`, `++count` or `count += 1` on an `int`, which becomes `Count`;
    - `found = true; break;` on a local `bool` declared `false` just before the
      loop, which becomes `Any`;
    - `return true;` with `return false;` straight after the loop, which
      becomes a returned `Any`.
- The expressions that move into lambdas do not assign, increment or await,
  and do not read the accumulator.

## Transformation

- When the statement just before the loop declares the accumulator with its
  starting value (an empty `new List<T>()`, zero or `false`), the query becomes
  that declaration's initializer and the declaration keeps its written type.
- Otherwise the query is added to the accumulator: `list.AddRange(query)`,
  `total += query.Sum(...)` or `count += query.Count(...)`.
- Each lambda's parameter takes the name of the loop variable or local it
  replaces. A `Select` of the current element itself is left out, as is the
  selector of a `Sum` of the elements themselves.
- A final `Where` before `Count` or `Any` becomes their predicate.
- `using System.Linq;` is added when the file does not already have it.

## Preserved

- Behaviour: LINQ runs each element through the whole query before the next,
  so conditions and projections run in the same order as in the loop, and
  `Any` stops at the first match as the loop's `break` or `return` did.
- Comments above the accumulator's declaration and above the loop, which join
  above the query.

## Limitations

- Comments inside the loop body are not carried into the query.
- `Sum` checks for overflow and `+=` does not, so a total that overflowed and
  wrapped in the loop throws `OverflowException` in the query. `Sum` of `float`
  values adds them as `double` before rounding the result.
- Bodies with `else`, `continue` guards, several accumulations, or a local
  whose element is still used after it are refused rather than converted with
  query syntax or anonymous types.
- The refactoring changes one loop, so there are no references in other files
  to update.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-foreach-loop` | the caret is not in a `foreach` loop |
| `not-queryable` | the loop enumerates a sequence that is not generic, which LINQ does not query without a cast |
| `unsupported-loop-body` | the body is not a filter, projection and accumulation of the kinds above |
| `side-effects` | the body does something besides the accumulation, or a moved expression assigns, increments or awaits |
| `early-exit` | the body leaves the loop early other than to report a match |
| `accumulator-used-in-body` | a condition or value reads the accumulator, which the query does not know until it ends |
