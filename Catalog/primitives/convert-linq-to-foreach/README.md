# Convert LINQ to Foreach

Turns a LINQ query of `Where` and `Select` ending in `ToList`, `Sum`, `Count`
or `Any` into a `foreach` loop that builds the same result. The reverse of
Convert Foreach to LINQ.

```csharp
var names = users.Where(user => user.IsActive).Select(user => user.Name).ToList();
```

becomes

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

## Target

The query, by a caret anywhere in the statement.

## Precondition

- The query is the whole initializer of a local declaration, or the whole
  expression of a `return`, in a block.
- It is a chain of `System.Linq.Enumerable` calls on a source: any number of
  `Where` and `Select`, then one of `ToList()`, `Sum()`, `Sum(selector)`,
  `Count()`, `Count(predicate)`, `Any()` or `Any(predicate)`.
- Every argument is a lambda with one parameter and an expression body, and is
  not `async`.
- A `Sum` is not of nullable values.
- The names the loop declares (the loop variable and a local for each `Select`
  followed by another lambda) are not already declared in scope.

## Transformation

- The loop is `foreach (var x in source)`, named after the first lambda's
  parameter, or after the singular of the source's name, or `item`.
- Each `Where` becomes an `if` around the rest of the body. A `Select`
  followed by another lambda becomes a local named after that lambda's
  parameter; a `Select` straight before `ToList` or `Sum()` becomes the value
  added. Later lambdas' parameters are renamed to the loop's current variable.
- The result accumulates in the declared local, keeping its written type. A
  `var` total, count or flag is declared with the result's type, since its
  starting value `0` or `false` would not say it.
  - `ToList` starts from `new List<T>()` and calls `Add`, adding
    `using System.Collections.Generic;` when the file does not have it.
  - `Sum` starts at `0` and adds with `+=`.
  - `Count` starts at `0` and increments.
  - `Any` starts at `false` and sets it to `true` and breaks on the first match.
- A returned query accumulates in a new local, `arguments.name` or `result`,
  `total` or `count`, returned after the loop; a returned `Any` returns `true`
  from the loop and `false` after it.

## Preserved

- Behaviour: each element runs through the conditions and projections in the
  query's order, and `Any` stops at the first match.
- Comments above the statement stay above the accumulator, and a comment
  ending its line stays on the declaration.

## Limitations

- Other operators (`OrderBy`, `First`, `ToArray`, query syntax and so on),
  method groups, and lambdas with a block body are refused.
- A `Select` whose values are only counted by `Count()` or `Any()` is refused.
- `+=` does not check for overflow as `Sum` does, and adds `float` values as
  `float` where `Sum` adds them as `double`.
- The refactoring changes one statement, so there are no references in other
  files to update.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-query` | the statement at the caret is not a LINQ query |
| `unsupported-query` | the query uses an operator, argument or lambda the loop form does not cover |
| `unsupported-statement` | the query is part of a larger expression rather than assigned to a local or returned |
| `name-conflict` | a name the loop would declare is already declared |
