# Convert Local Function to Method

Moves a local function out of its member to a private method of the
containing type. Variables it captures become parameters. The reverse of
Convert Method to Local Function.

## Target

The local function, by a caret on its name or on a call to it. In a later
step of a composite, the containing member's symbol with `arguments.function`
naming the local function. `arguments.name` names the method, and defaults to
the local function's name.

## Precondition

- The local function calls no other local function of its member, which the
  method could not reach.
- When it captures variables or type parameters of its member, every use of
  it is a call. A method group would have nothing to pass the captured values
  with.
- An `async` or iterator local function does not assign a captured variable,
  which would have to be passed by `ref`.
- The type has no member with the method's name, inherited or declared.

## Transformation

- The method is declared after the member that held the local function,
  after a blank line, with the local function's signature, modifiers and
  body. It is `private`, and `static` when the member is static or the local
  function reaches no instance member, through `this`, `base` or a simple
  name.
- Each local or parameter of the member that the local function reads becomes
  a parameter of the method, in declaration order, after its own parameters,
  and is passed by name at every call. A variable the local function assigns
  is a `ref` parameter, passed with `ref`.
- Each type parameter of the enclosing method that the local function uses,
  directly or through a captured variable's type, becomes a type parameter
  of the method, after its own, with its constraint. Calls pass type
  arguments explicitly only when one of them cannot be inferred from the
  parameters.
- Every call, including recursive calls, uses the method's name.
- In a nullable context, a captured parameter keeps its annotation. A local
  declared with `var` is passed as not nullable where its value is known not
  to be null.

## Preserved

- Behaviour: the method sees the values the captured variables hold at each
  call, and assignments to them are made through `ref`.
- Comments above and inside the local function move with it.

## Limitations

- Another local of the member with the method's name would hide the method at
  the calls; this is not checked.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-local-function` | the target is not a local function or a call to one |
| `name-conflict` | the type already has a member with the method's name |
| `used-as-delegate` | the local function captures variables and is used without being called |
| `calls-local-function` | the local function calls another local function of its member |
| `ref-in-async` | an async or iterator local function assigns a captured variable |
