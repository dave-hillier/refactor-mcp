# Remove Delegating Member

Removes a member that only forwards, through `base`, to the inherited member
it hides or overrides, so its callers reach the inherited member directly.
Replace Delegation with Inheritance uses it once a class's forwarding members
reach their targets through `base`.

## Arguments

None. The target is the member, by symbol:
`"target": { "symbol": "M:Staff.Employee.LastName" }`, or for an indexer
`"target": { "symbol": "P:Shop.Stack.Item(System.Int32)" }`.

## Precondition

- The member is a method, property or indexer whose whole body reaches the
  inherited member of the same name through `base`: a method whose body is
  `base.M(a, b)`, passing its own parameters in order and by the same
  `ref` kind; a property whose getter reads `base.P` and whose setter, if
  any, assigns `base.P = value`; an indexer that does the same with
  `base[i]`. Expression and block bodies are both recognised.
- The inherited member has the same signature: the same return or property
  type, and parameters with the same names, types, modifiers and default
  values, so no call binds or behaves differently.
- The inherited member, and its setter where the member has one, is at least
  as accessible as the member.
- The member is not a new virtual member that subclasses override.
- The result compiles.

## Transformation

- The member is removed with its comments and documentation.
- Inside the class, uses of the inherited member written with `base` become
  plain uses (`base.LastName()` becomes `LastName()`, `base[i]` becomes
  `this[i]`), unless a subclass overrides it, where `base` is needed to keep
  reaching the inherited implementation.

## Preserved

- Every call of the removed member, which now reaches the inherited member it
  forwarded to.
- Comments and blank lines around the other members.

## Limitations

- Generic methods and members with attributes or modifiers on their
  accessors are not recognised as forwarding.
- A member forwarding to a field rather than `base` is not removed; Replace
  Field Uses with Base turns such a field into the instance first.

## Error codes

| Code | Meaning |
|---|---|
| `not-delegating` | the member does more than reach the inherited member of the same name through `base` |
| `signature-differs` | the inherited member's signature differs, for example in a parameter's name or default value |
| `inherited-less-accessible` | the inherited member or its setter is less accessible than the member |
| `member-overridden` | the member is virtual and a subclass overrides it |
