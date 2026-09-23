# Add Observer / Event

Declares an event on a method's class and raises it whenever the method
completes, so other objects can observe the change the method makes without
the class knowing about them.

This is a generator: it adds a public member and a call to whatever handlers
are subscribed, so the class's surface and, once something subscribes, its
behaviour change. The `after/` fixtures pin one design, described below,
rather than the only correct answer.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `event` | yes | The event's name, such as `Updated` |

The target is the method, by symbol: `"target": { "symbol": "M:Shop.Counter.Update(System.Int32)" }`.

## Precondition

- The method has a body and returns `void`.
- No parameter is `ref`, `out` or `in`; an `Action` cannot pass them.
- The class has no member with the event's name.

## Transformation

- The event is `public event Action<T1, ..., Tn> Name;`, with the method's
  parameter types in order as they are written, or `Action` for a method
  without parameters. It is `static` when the method is, and declared
  nullable (`Action<...>?`) where nullable annotations are enabled.
- The event is declared immediately before the method, taking the blank lines
  that preceded the method, with a blank line between the two. The method
  keeps its documentation comment.
- `Name?.Invoke(p1, ..., pn);` passes the method's parameters and is raised:
  - at the end of the body when the end is reachable, after any comment that
    sits before the closing brace;
  - before every `return` that leaves the method; a `return` that is the
    embedded statement of an `if`, `else` or loop is wrapped in a block with
    the call.
- An expression body becomes a block holding the expression as a statement.
- `using System;` is added when `Action` does not bind in the file.

## Preserved

- The method's statements and what they do; the event is raised after them.
- Returns inside lambdas and local functions, which leave something other
  than the method.
- Behaviour, while nothing subscribes to the event.

## Limitations

- The event is raised on normal completion only, not when the method throws.
- Handlers receive the parameters' values at the end of the method; a
  parameter the method reassigns is passed as reassigned.
- A custom `EventHandler<TEventArgs>` with an arguments class is not
  generated; the event is an `Action`.
- The event is always public, so a method whose parameter types are less
  accessible than its class is refused because the result would not compile.

## Error codes

| Code | Meaning |
|---|---|
| `no-body` | the method is abstract, extern, partial without a body, or an interface member |
| `not-void` | the method returns a value |
| `ref-parameter` | a parameter is `ref`, `out` or `in` |
| `member-exists` | the class already has a member with the event's name |
