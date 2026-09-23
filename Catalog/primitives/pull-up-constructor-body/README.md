# Pull Up Constructor Body

Moves the statements at the start of a subclass constructor that only set up
the base class into a base constructor, and makes the subclass constructor
chain to it.

## Arguments

None. The target is the constructor, by symbol:
`"target": { "symbol": "M:Shop.Manager.#ctor(System.String,System.Int32)" }`.

## Precondition

- The class has a base class, declared in the solution.
- The constructor does not already pass arguments to a base constructor or
  chain to another of its own constructors. An empty `base()` is replaced.
- Its first statement can move. A statement can move when it assigns a field
  or non-virtual property the base class has, and reads only the
  constructor's parameters, static members, and fields and non-virtual
  properties the base class has. A statement that calls an instance method of
  the class could reach an override before the subclass has run its own
  initialisation, so it stays.
- When the base class already has a constructor taking the same parameter
  types, it does exactly what the moved statements do, and runs the base
  class's parameterless constructor first when the subclass did.
- The result compiles.

## Transformation

- The leading run of statements that can move is taken, stopping at the
  first that cannot, so the order in which things are assigned is kept.
- The base class gains a `protected` constructor taking the parameters those
  statements read, in the subclass constructor's order and with their names,
  after its fields and constructors. When the base class already has a
  constructor that does the same, it is reused.
- When the subclass constructor ran a base constructor with statements of
  its own, the parameterless one, the new base constructor chains to it with
  `this()` so those statements still run first.
- The subclass constructor calls `base(...)` with those parameters, on its
  own line when its body's brace is on its own line.
- When the base class had only its implicit parameterless constructor and
  something else relied on it (another subclass, another constructor, or code
  creating the base class), that constructor is declared explicitly, `public`
  or `protected` for an abstract class, as it was in effect.
- Comments inside the moved statements move with them.

## Preserved

- The values every field and property holds when construction finishes.
- The subclass's remaining statements and their comments.

## Limitations

- Only the one constructor is changed; other subclasses with the same
  statements can be pulled up in turn, and reuse the base constructor.
- Only assignments move. A statement that declares a local, or calls a
  method for its effect, ends the run even when it would be safe to move.

## Error codes

| Code | Meaning |
|---|---|
| `no-base-class` | the class has no base class other than `object` |
| `base-not-in-source` | the base class is not declared in the solution |
| `already-chains` | the constructor already passes arguments to `base(...)` or calls `this(...)` |
| `nothing-to-pull-up` | the constructor's first statement cannot move to the base class |
| `base-constructor-exists` | the base class has a constructor with the same parameters that does something else |
| `breaks-compilation` | the result would not compile |
