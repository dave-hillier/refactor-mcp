# Create Adapter

Generates an adapter: a class that implements an interface a client expects
by wrapping an existing class, the adaptee, and forwarding each interface
member to the adaptee member that does the same job.

This is a generator. The fixtures pin the one design described below; other
adapters, such as one that inherits from the adaptee, would also be correct.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `interface` | yes | The interface to implement, as the adaptee's file would name it, such as `ILogger` or `IRepository<Order>`. A name the file does not import is looked up across the solution and its references |
| `name` | yes | The adapter's name |
| `map` | no | Interface member names mapped to adaptee member names, as an object: `{ "Log": "Write" }` |

The target is the adaptee class, by symbol: `"target": { "symbol": "T:Shop.LegacyLogger" }`.

## Precondition

- `interface` names an interface.
- Every key of `map` names a member of the interface, and every value names
  an instance member of the adaptee (or a base class) that the adapter can
  reach.
- Each mapped member has a compatible counterpart: see below.
- No type in the adaptee's namespace has the adapter's name, and no file with
  that name exists beside the adaptee's file.
- The result compiles.

## Matching members

An interface member forwards to the adaptee member it is mapped to, or, when
it is not mapped, to an adaptee member of the same name. A candidate is
compatible when it is the same kind of member and:

- a method has as many parameters with the same `ref`, `out` or `in`, each
  interface parameter type converts implicitly to the adaptee's, and the
  adaptee's return type converts implicitly to the interface's (any result
  may be discarded when the interface returns `void`);
- a property has a usable getter whose type converts implicitly to the
  interface's when the interface reads it, and a usable setter taking the
  interface's type when the interface writes it; an indexer also needs
  compatible parameters;
- an event has the same delegate type.

Among overloads, one whose types match exactly is preferred to one that
needs conversions. An unmapped member with no compatible counterpart is
generated throwing `NotImplementedException`; a mapped one is refused.

## Generated shape

- `public class <name> : <interface>` in a new file `<name>.cs` beside the
  adaptee's file, in the adaptee's namespace and namespace style, with the
  usings its signatures need.
- A `private readonly` field `_adaptee` of the adaptee's type, and a public
  constructor taking `adaptee` that assigns it.
- One public member per interface member, the interface's own first and then
  those of each interface it inherits, in declaration order, with the
  interface's parameter names and nullable annotations. Each has an
  expression body forwarding to its counterpart,
  `public void Log(string message) => _adaptee.Write(message);`, or
  `=> throw new NotImplementedException();` when it has none. Property and
  event accessors are laid out one to a line.

## Behaviour

No existing behaviour changes: nothing refers to the adapter yet. The
adapter's members throw where the adaptee has no counterpart, which a caller
of the interface would observe once it is given an adapter.

## Preserved

- Every existing file, including the interface's comments, regions and
  layout.

## Limitations

- Only instance members of the adaptee are considered; static ones are not.
- Indexers and events are matched by kind, not by name, and cannot be mapped.
- A counterpart must take the interface member's parameters in the same
  order; reordering or computing arguments is left to editing the adapter.
- A property is never mapped to a method, nor a method to a property.
- Nullability differences between the interface and the adaptee are not
  checked; the compiler reports them as warnings.

## Error codes

| Code | Meaning |
|---|---|
| `interface-not-found` | no type has the interface's name |
| `not-an-interface` | the named type is a class, struct or other type rather than an interface |
| `member-not-found` | a mapped name is not a member of the interface, or not a member of the adaptee |
| `incompatible-signature` | a mapped adaptee member cannot stand in for its interface member |
| `type-already-exists` | the namespace already has a type, or the folder a file, with the adapter's name |
