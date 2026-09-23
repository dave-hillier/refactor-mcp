# Inline Method

Replaces every call of a method with the method's body, across the solution,
and deletes the method. The reverse of Extract Method.

## Target

The method, by symbol. Overloads are told apart by their signature.

## Precondition

- The method has a body, is not `virtual`, `abstract`, an override or an
  interface implementation, is not `async` or an iterator, and does not call
  itself.
- Every reference to the method is a call. A method group, such as one
  converted to a delegate, has no call to inline.
- A method returning a value has a single expression: an expression body, or
  a block holding only `return expression;`. Its calls are expressions whose
  value is used.
- A `void` method returns, if at all, only at its end. Each call is a
  statement of its own.
- Every member and type the method names is accessible at each call.
- Locals the inlined code declares, including locals for arguments, do not
  clash with names at the call site.
- A call passes no `ref`, `out` or `in` argument and no `params` array, and a
  call on an object other than `this` names it with a simple expression when
  the method reaches `this` more than once.

## Transformation

- A value-returning method's expression replaces the call, parenthesised only
  where precedence requires. A `void` method's statements replace the call
  statement, in a new block when the call was the body of an `if` or loop
  without braces.
- Each parameter is replaced by its argument, or by the parameter's default
  when the call leaves it out. An argument with side effects that the method
  reads more than once or not at all, or that the method assigns, is first
  evaluated into a local named after the parameter and declared with its
  type. When several arguments have side effects, each gets a local so they
  run in the order written.
- Type parameters are replaced by the call's type arguments, inferred or
  written.
- Members the method reaches through `this` are reached through the call's
  receiver. Types and static members are qualified as much as the call site
  needs, so a call in a file without the method's `using` directives still
  compiles.
- Calls nested in another call's arguments are inlined too.
- The method is deleted, with its comments.

## Preserved

- Behaviour at every call: arguments are evaluated once, in order, before
  the method's code that depends on them.
- Comments inside the method travel with its statements, below any comment
  above the call.
- Other overloads of the method.

## Limitations

- A value-returning method with more than one statement is refused rather
  than turned into statements at a call that is itself a statement.
- Extension methods the method calls need their namespace imported at the
  call site; no `using` directive is added.
- An argument without side effects is substituted even when it reads a field
  the method changes before reading the parameter.
- Only whole-method inlining is covered; inlining a single call site and
  keeping the method is not.

## Error codes

| Code | Meaning |
|---|---|
| `recursive-method` | the method calls itself |
| `polymorphic-method` | the method is virtual, an override or an interface implementation |
| `unsupported-method` | the method is async or an iterator |
| `method-group-reference` | the method is used without being called |
| `inaccessible-member` | the method uses a member or type a call site cannot access |
| `multiple-statements` | a value-returning method's body is more than a single return |
| `early-return` | a void method returns before its last statement |
| `name-conflict` | a local the inlined code declares clashes with a name at the call site |
| `unsupported-call` | a void method's call is not a statement of its own |
