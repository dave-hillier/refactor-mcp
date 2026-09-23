# Separate Query from Modifier

Splits a method that both changes state and returns a value into a modifier
that makes the change and a query that returns the value, and makes every
caller call both. Afterwards asking for the value no longer changes anything.

## Recipe

1. `extract-method` on the statements that change state, with `name` the
   modifier's name.
2. `extract-method` on the returned expression, with `name` the query's name.
   The method now calls the modifier and returns the query.
3. `change-accessibility` on the query, then on the modifier, giving each the
   method's accessibility so its callers can reach them.
4. `inline-method` on the method. Each call becomes a call of the modifier
   followed by the call's statement with the query in its place.

```json
"steps": [
  { "refactoring": "extract-method", "target": { "file": "Account.cs", "selection": "marker" }, "arguments": { "name": "Debit" } },
  { "refactoring": "extract-method", "target": { "file": "Account.cs", "range": "16:20-16:28" }, "arguments": { "name": "Balance" } },
  { "refactoring": "change-accessibility", "target": { "symbol": "M:Bank.Account.Balance" }, "arguments": { "accessibility": "public" } },
  { "refactoring": "change-accessibility", "target": { "symbol": "M:Bank.Account.Debit(System.Decimal)" }, "arguments": { "accessibility": "public" } },
  { "refactoring": "inline-method", "target": { "symbol": "M:Bank.Account.Withdraw(System.Decimal)" } }
]
```

The plan's "redirect callers" is Inline Method on the method once it only
calls the two parts. The recipe needs the query last, as in a method that
changes state and then returns what it changed; for a method that stores its
value in a local first, inlining keeps that local at each call, where the
dedicated implementation calls the query into the caller's own variable.
Inline Method keeps a discarded result of a call, so a discarded call keeps a
call of the query; the dedicated implementation knows the query has no side
effects and drops it.

## Arguments

| Argument | Meaning |
|---|---|
| `queryName` | the name of the method that returns the value |
| `modifierName` | the name of the method that changes state |

The target is the method, by symbol.

## Precondition

- The method returns a value from a block body, and returns only at its end.
  It is not virtual, abstract, an override, an interface implementation,
  async or an iterator, and is declared in a class.
- The body has one of two shapes: statements that change state followed by
  `return value;`, or `var result = value;`, statements that change state,
  and `return result;`, where those statements do not read `result`.
- The returned value has no side effects: no call, object creation,
  assignment, increment or `await`.
- Each part meets Extract Method's precondition, and the class has no member
  with either name.
- Every reference to the method is a call that starts its statement: the
  whole statement, the initializer of a local declaring nothing else, the
  value assigned to a simple target, or, when the value is computed before the
  change, not the value returned.
- Each call's receiver is a simple expression and its arguments have no side
  effects, since both are evaluated once for each part.

## Transformation

- The modifier is a method of the method's accessibility holding the
  statements that change state, returning nothing. The query is a method of
  the same accessibility returning the value. Each takes the parameters it
  reads. The query is placed first, where the method was.
- Each call becomes a call of each part, on the same receiver with the
  arguments it passed for their parameters, in the order the method ran them.
  A call whose value was discarded only calls the modifier. A call that was
  the body of an `if` or loop without braces gets a block.
- The method is deleted.

## Preserved

- The state changes and the value each caller receives, in the same order.
- Comments above a call's statement stay above the first of its new
  statements.

## Limitations

- A method whose query and modifier are interleaved, or that returns from
  several places, is refused rather than untangled.
- A call inside a larger expression is refused rather than given a local for
  the value.
- Overloads and overrides are not separated together.

## Error codes

| Code | Meaning |
|---|---|
| `returns-nothing` | the method returns nothing, so there is no query |
| `no-modifier` | the method has no statements that change state before it returns |
| `query-has-side-effects` | the returned value has side effects of its own |
| `modifier-reads-result` | the statements that change state read the local the method returns |
| `returns-early` | the method returns before its last statement |
| `polymorphic-method` | the method is virtual, an override or an interface implementation |
| `expression-bodied-member` | the method is expression-bodied |
| `call-in-expression` | a call is part of a larger expression |
| `returned-before-modifier` | a call returns a value the query must compute before the modifier runs |
| `receiver-has-side-effects` | a call is made on an expression that would be evaluated twice |
| `argument-has-side-effects` | a call passes an argument with side effects |
| `method-group-reference` | the method is used as a method group |
| `name-conflict` | the class already has a member with one of the names |
