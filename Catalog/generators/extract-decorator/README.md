# Extract Decorator

Generates a decorator for an interface: a class that implements the
interface, wraps another implementation of it, and forwards every member to
the wrapped instance. Behaviour is then added by editing the decorator's
members, and callers opt in by wrapping an instance.

This is a generator. Many decorators would be correct; the fixtures pin the
one design described below rather than a unique answer.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | no | The decorator's name. Defaults to the interface's name without its leading `I`, followed by `Decorator`: `IGreeter` gives `GreeterDecorator` |

The target is the interface, or a class that implements exactly one
interface, by symbol: `"target": { "symbol": "T:Shop.IGreeter" }`.

## Precondition

- The target is an interface, or declares exactly one interface in its base
  list. Interfaces it only inherits through a base class do not count.
- No type in the target's namespace has the decorator's name, and no file
  with the decorator's name exists beside the target's file.
- The result compiles.

## Generated shape

- `public class <name> : <interface>` in a new file `<name>.cs` beside the
  target's file, in the target's namespace, written in the namespace style
  (block or file-scoped) of the target's file, with the usings its signatures
  need.
- A `private readonly` field `_inner` of the interface type, and a public
  constructor taking `inner` that assigns it.
- One public member per interface member, the interface's own first and then
  those of each interface it inherits, in declaration order. Each forwards to
  `_inner` with an expression body:
  - methods pass their arguments with the same `ref`, `out` or `in`, restate
    `params`, default values, type parameters and constraints, and pass type
    arguments explicitly: `_inner.Project<TResult>(item, map)`;
  - properties and indexers declare the accessors the interface declares; a
    get-only property is expression-bodied, one with a setter has
    `get => ...;` and `set => ...;` on separate lines;
  - events have `add` and `remove` accessors that subscribe to and
    unsubscribe from the wrapped instance's event.
- A member an earlier member already implements, because it has the same
  signature and type, is not repeated. One whose type differs, such as a
  property a derived interface hides with `new`, is implemented explicitly
  through a cast: `object IReader.Current => ((IReader)_inner).Current;`.
- Nullable annotations in the interface's signatures are kept.
- A generic interface gives a generic decorator with the same type parameters
  and constraints. A class implementing a constructed interface, such as
  `IRepository<Order>`, gives a non-generic decorator of that interface.
- The decorator's members carry no comments; the interface's documentation
  applies to them.

## Behaviour

Nothing changes. No existing code refers to the decorator, and a decorator
that only forwards behaves exactly as the instance it wraps.

## Preserved

- Every existing file, including the interface's comments, regions and
  layout.

## Limitations

- A class implementing several interfaces is refused; extract one interface
  that covers what is to be decorated, or decorate the interface itself.
- Static interface members are not implemented, so an interface with static
  abstract members cannot be decorated.
- Explicitly implemented members do not restate constraints, which C# does
  not allow there.
- Callers are not changed to use the decorator.

## Error codes

| Code | Meaning |
|---|---|
| `no-interface` | the class implements no interface |
| `ambiguous-interface` | the class implements several interfaces |
| `type-already-exists` | the namespace already has a type, or the folder a file, with the decorator's name |
