# Add Null Checks

Adds an argument null guard at the start of a method or constructor for each
reference-type parameter that is meant never to be null.

This is a generator: it changes behaviour, because a call that passed null
now throws `ArgumentNullException` at the start of the method instead of
failing later or not at all. The `after/` fixtures pin one design, described
below, rather than the only correct answer.

The target is the method or constructor, by symbol:
`"target": { "symbol": "M:Shop.Order.#ctor(Shop.Customer,System.String)" }`.

## Precondition

- The method or constructor has a body, block or expression.
- At least one parameter needs a guard: a reference type (including a type
  parameter constrained to a class) that is not `out`, not annotated as
  nullable, does not default to `null`, and is not already guarded.

## Transformation

- Each parameter needing one gets `ArgumentNullException.ThrowIfNull(name);`,
  in parameter order, at the top of the body, followed by a blank line before
  the first existing statement.
- A parameter counts as already guarded when the body calls
  `ArgumentNullException.ThrowIfNull` on it, throws in an `if` whose condition
  is `p == null`, `null == p` or `p is null`, or uses `p ?? throw`.
- An expression body becomes a block: the expression is returned, or becomes
  a statement when the method returns nothing (void, a constructor, or an
  async method returning `Task` or `ValueTask`).
- `using System;` is added when `ArgumentNullException` does not bind where
  the guards go.

## Preserved

- The existing statements, with the comments and blank lines around them; a
  comment above the first statement stays with it, below the guards.
- Behaviour for every call that passes non-null arguments.

## Limitations

- In a constructor the guards run after the `base(...)` or `this(...)` call,
  which may already have used the parameter.
- In an iterator or async method the guard runs when iteration or the task
  starts, not at the call, as for any statement in such a body.
- Unconstrained type parameters are not guarded, since they may be value
  types; nor are parameters annotated nullable, which are declared to accept
  null.
- Existing guards in other shapes, such as a helper method, are not
  recognised, so their parameters are guarded again.
- Local functions, lambdas, property setters and indexers are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `no-body` | the method is abstract, extern, partial without a body, or an interface member |
| `nothing-to-guard` | no parameter is a reference type left unguarded |
