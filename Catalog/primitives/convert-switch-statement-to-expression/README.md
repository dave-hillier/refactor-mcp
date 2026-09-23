# Convert Switch Statement to Switch Expression

Turns a `switch` statement whose every section produces a value into a
`switch` expression that is returned or assigned.

## Precondition

- The caret is on the `switch` statement, between `switch` and its opening
  brace.
- Every section, braced or not, is one of:
  - `return value;`
  - `variable = value; break;`, the same variable in every such section;
  - `throw exception;`
- The sections either all return or all assign; sections that throw may be
  mixed with either.
- A switch expression throws for a value no arm matches, where a switch
  statement does nothing. So the switch must have a `default` section, or,
  when its sections return, be followed directly by a `return` or `throw`
  that becomes the discard arm.

## Transformation

- The statement becomes `return x switch { ... };` or
  `variable = x switch { ... };`.
- Each section becomes an arm, in order, with the `default` arm last as `_`:
  - `case c:` becomes the constant pattern `c`;
  - `case pattern when condition:` keeps its pattern and `when` clause;
  - several labels become one pattern joined by `or`, unless one of them has a
    `when` clause or declares a variable, in which case each becomes its own
    arm with the same value;
  - `throw e;` becomes the throw expression `throw e`.
- The `return` or `throw` after a switch with no `default` becomes the `_` arm
  and is removed.
- Arms are written one per line, with a trailing comma after the last.

## Preserved

- Behaviour: patterns are tried in the same order, and the `default` section,
  wherever it was, only runs when no case matches, as `_` does last.
- Comments on a section's labels and statements are written above its arm; a
  comment at the end of the section's statement stays at the end of the arm.

## Limitations

- A declaration immediately before an assigning switch is not merged into it;
  combine them with Join Declaration and Assignment.
- A value spanning several lines keeps its line breaks and their original
  indentation.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-switch` | the caret is not on a `switch` statement |
| `unsupported-section` | a section does something other than return a value, assign one variable and break, or throw |
| `different-targets` | the sections assign different variables |
| `no-default` | there is no `default` section and no `return` after the switch to become one |
