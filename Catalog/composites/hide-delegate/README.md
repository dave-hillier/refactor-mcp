# Hide Delegate

Stops clients reaching through one object to another. When clients write
`person.Department.Manager`, the person gains a `Manager` member that forwards
to its department, and the clients write `person.Manager`, so they no longer
depend on the department.

## Recipe

For one client's chained call:

1. `extract-method` on the client's statement that calls through the delegate:
   `"target": { "file": "Payroll.cs", "selection": "marker" }, "arguments": { "name": "ApproveByManager" }`.
   The new method takes the server object (`person`) as a parameter.
2. `move-instance-method` the extracted method onto the server through that
   parameter, without a stub:
   `"target": { "symbol": "M:Staff.Payroll.ApproveByManager(Staff.Person,Staff.Invoice)" }, "arguments": { "via": "person", "stub": false }`.
   The call becomes `person.ApproveByManager(invoice)`.

The plan's third step, repointing the other callers, has no primitive: each
other client's chain would need its own Extract Method and would produce
another method rather than reuse the first. The recipe therefore hides one
chained statement, as an `internal` method since Extract Method makes it
private. The dedicated implementation hides the delegate's member itself and
repoints every client, so its cases describe a different result from the
recipe's.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `member` | yes | The delegate's field, property or method that clients reach, such as `Manager` |
| `name` | no | The forwarding member's name; defaults to `member` |

The target is the server's field or property holding the delegate, by symbol:
`"target": { "symbol": "P:Staff.Person.Department" }`.

## Precondition

- The target is an instance field or property.
- The delegate's type, or a base type, has an instance field, property or
  method with the member's name.
- A method to forward is not generic and has no `ref`, `out`, `params` or
  optional parameters.
- The server and its base classes have no member with the forwarding name.
- The result compiles, which fails when, for example, the forwarding member
  would be more accessible than the member's type.

## Transformation

- The server gains, right after the field or property holding the delegate, a
  member as accessible as that field or property: a read-only property
  `public Employee Manager => Department.Manager;` for a field or property,
  or `public string Describe(string prefix) => Department.Describe(prefix);`
  for each overload of a method.
- Every read of the member through the delegate on an explicit receiver,
  `x.Department.Manager`, becomes `x.Manager`, and every call
  `x.Department.Describe(a)` becomes `x.Describe(a)`, in every file.

## Preserved

- What every client reads and calls: the forwarding member reaches the same
  member of the same delegate.

## Limitations

- Assignments through the delegate, null-conditional access
  (`person.Department?.Manager`), method groups, and uses inside the server
  without a receiver are left as they were.
- The forwarding member is get-only; it does not forward a setter.

## Error codes

| Code | Meaning |
|---|---|
| `static-delegate` | the field or property holding the delegate is static |
| `member-not-found` | the delegate's type has no instance member of that name |
| `name-conflict` | the server already has a member with the forwarding name |
| `unsupported-member` | the method to forward is generic or has `ref`, `out`, `params` or optional parameters |
| `breaks-compilation` | the result would not compile |
