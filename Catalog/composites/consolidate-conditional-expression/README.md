# Consolidate Conditional Expression

Combines a run of conditions that lead to the same code into one condition,
and optionally gives it a name by extracting it into a method.

## Recipe

1. Merge the conditions that share a body into one `if`, one condition per
   step, on the same `if`, which stays where it was:
   - Merge Sibling Ifs for `if` statements or `else if` branches that follow
     it, joining with `||`:
     `{ "refactoring": "merge-sibling-ifs", "target": { "file": "Disability.cs", "caret": "marker" } }`
   - Merge Nested If for an `if` nested in it, joining with `&&`:
     `{ "refactoring": "merge-nested-if", "target": { "file": "Shipping.cs", "caret": "marker" } }`
2. With `name`, Extract Method on the combined condition, selected exactly,
   which extracts it into a method returning its value:
   `{ "refactoring": "extract-method", "target": { "file": "Disability.cs", "range": "12:17-12:70" }, "arguments": { "name": "IsNotEligible" } }`

A later step cannot use a marker, so it gives the caret as a `range` whose
start is the caret, such as `"range": "12:13-12:13"`, and the condition as
the range it covers once the conditions are merged.

## Target and arguments

The first `if`, by caret: `"target": { "file": "Disability.cs", "caret": "marker" }`.

| Argument | Required | Meaning |
|---|---|---|
| `name` | no | the name of a private method to extract the combined condition into |

## Precondition

- The caret is on an `if` statement that has one of these to combine with:
  - A nested `if`: its body is another `if` and nothing else, and neither has
    an `else`. Deeper nesting of the same shape is combined too.
  - `else if` branches directly after it with the same body.
  - `if` statements directly after it, with no `else`, with the same body.
    That body must always jump away (return, throw, break or continue): the
    second `if` then only runs when the first condition was false, exactly as
    `||` evaluates it. A body that can fall through would run once for each
    condition that holds.
- Conditions joined with `||` declare no pattern or `out` variable, which
  would be unassigned when an earlier condition holds.
- With `name`: the name is a valid identifier that no member of the class
  has, and no condition declares or assigns a variable.

## Transformation

- Nested conditions are joined with `&&`, outer first; the innermost body
  becomes the body.
- `else if` branches with the same body are joined with `||` in order, and the
  chain continues with the `else` of the last one.
- Consecutive `if` statements with the same body are joined with `||` in order
  and the later ones are removed.
- Operands are parenthesised only where precedence requires it.
- With `name`, the combined condition becomes the body of
  `private bool Name(...) { return condition; }`, placed after the member
  holding the `if`, and the `if` calls it. The method takes the locals and
  parameters the conditions read, in the order they are first read, with
  their types, and is `static` when the member holding the `if` is.

## Preserved

- Behaviour: the conditions are evaluated in the same order and short-circuit
  as before, and the body runs exactly when it did.
- Comments above the first `if` stay above it; comments above the `if`
  statements it absorbs follow them.
- Pattern variables declared by an outer condition stay in scope in the
  conditions after it and in the body.

## Limitations

- Only a run that starts at the caret is combined; a run that starts at a
  later `if` needs the caret there.
- Bodies must be the same code; bodies that differ only in comments or layout
  count as the same, and the first one is kept.
- The extracted method is not generic, so a condition that uses a type
  parameter of the containing method is refused by the compile check.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `nothing-to-consolidate` | there is no nested `if`, `else if` or following `if` with the same body |
| `body-falls-through` | the following `if` statements share a body that can fall through |
| `declares-variable` | a condition joined with `||`, or extracted, declares or assigns a variable |
| `invalid-name` | `name` is not a valid identifier |
| `name-conflict` | the class already has a member named `name` |
