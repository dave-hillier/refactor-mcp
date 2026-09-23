# Remove Middle Man

Removes the members of a class that only pass requests on to another object it
holds, so that callers talk to that object directly. The reverse of Hide
Delegate.

## Recipe

1. Inline Method on each method or property that only delegates, which
   replaces every call or read with the delegating expression reached through
   the caller's receiver, then deletes the member:
   `{ "refactoring": "inline-method", "target": { "symbol": "M:Company.Person.GetManager" } }`,
   `{ "refactoring": "inline-method", "target": { "symbol": "P:Company.Person.Manager" } }`.

The plan's recipe ends with Safe Delete Member; Inline Method already deletes
the member once its uses are inlined, so that step has nothing left to do.

## Target and arguments

The class that delegates, by symbol: `"target": { "symbol": "T:Company.Person" }`.

| Argument | Required | Meaning |
|---|---|---|
| `via` | yes | the field or property holding the delegate, such as `Department` |

## Precondition

- The class has a field or property named `via`.
- At least one member of the class only delegates through it:
  - a method whose body is one call of a member of the delegate, passing the
    method's own parameters in order (`Department.Budget(year)`), or one read
    of a member of it (`Department.Manager`);
  - a get-only property that reads a member of the delegate or calls one of
    its methods without arguments.
- Wherever a delegating member is used, the delegate and the member it forwards
  to are accessible. A private delegate has to be exposed first, for example
  with Encapsulate Field.
- Every use of a delegating method is a call; a method group has no call to
  redirect. The other preconditions of Inline Method hold for each method.

## Transformation

- Each use of a delegating member is replaced by what it forwards to, reached
  through the same receiver: `person.GetManager()` becomes
  `person.Department.Manager`, `person.Budget(2024)` becomes
  `person.Department.Budget(2024)`, `person?.Manager` becomes
  `person?.Department.Manager`, and `Manager` inside the class becomes
  `Department.Manager`.
- The delegating members are deleted.

## Preserved

- The behaviour of every use: the delegate is read and its member used exactly
  as the delegating member did.
- Members of the class that do more than delegate, and members that callers
  reach through dispatch (virtual, abstract, overriding or implementing an
  interface), which are kept.

## Limitations

- Only members that forward their parameters unchanged and in order are
  recognised as delegating; one that adapts an argument is kept.
- Static members, indexers and events are not considered.
- A refusal from Inline Method on any delegating method refuses the whole
  change, so nothing is removed.

## Error codes

| Code | Meaning |
|---|---|
| `via-not-found` | the class has no field or property named `via` |
| `no-delegating-members` | no member of the class only delegates through `via` |
| `via-not-accessible` | the delegate or its member is not accessible where a delegating member is used |
| `method-group-reference` | a delegating method is used without being called |
