# Inline Local Variable

Replaces every use of a local with the expression it was initialised with,
and removes the declaration. The reverse of Extract Local Variable.

## Target

The local, by a caret on its declaration or on any use of it. In a later step
of a composite, the containing member's symbol with `arguments.local` naming
the local.

## Precondition

- The local is declared by a local declaration statement in a block or switch
  section, with an initializer, and is not a `using` declaration or a `ref`
  local.
- The local is never written after its declaration: not assigned,
  incremented, or passed by `out` or `ref`.
- The local is used at least once.
- When it is used more than once, the initializer has no side effects: no
  call, object creation, assignment, increment or `await`.
- No local, parameter or field the initializer reads is assigned between the
  declaration and a use. A use inside a loop that does not contain the
  declaration counts the whole loop as between.

## Transformation

- Each use is replaced by the initializer, parenthesised only where
  precedence requires it.
- When the declaration converted the value to the local's type, the
  conversion is kept as a cast wherever removing it would change the meaning,
  so `double ratio = count;` inlines as `(double)count`.
- A target-typed `new()` or an array initializer names the local's type, since
  it no longer has a declaration to take it from.
- The declaration is removed. When the statement declares other locals too,
  only this one is removed from it.

## Preserved

- Behaviour: each use sees the value the local held.
- Comments above the declaration stay in place, above the statement that
  followed it.

## Limitations

- A single use of an initializer with side effects moves those side effects
  to the point of use, past the statements in between. Only variables the
  initializer reads directly are checked for assignments in between, not
  state a call inside it might read.
- Locals declared by `for`, `using`, `foreach` or pattern statements are not
  covered.

## Error codes

| Code | Meaning |
|---|---|
| `no-initializer` | the local has no value to inline |
| `assigned-after-declaration` | the local is written after its declaration |
| `initializer-has-side-effects` | the initializer has side effects and the local is used more than once |
| `initializer-inputs-change` | a variable the initializer reads is assigned before a use |
| `never-used` | the local is never read; Safe Delete Local removes it |
| `using-declaration` | the local is a `using` declaration and would no longer be disposed |
| `not-a-local` | the target is not a local variable |
