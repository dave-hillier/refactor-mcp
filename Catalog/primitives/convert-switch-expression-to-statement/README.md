# Convert Switch Expression to Switch Statement

Turns a `switch` expression that is the whole value of a `return`, an
assignment or a local's initialiser into a `switch` statement.

## Precondition

- The caret is on the `switch` expression, on its governing expression or its
  `switch` keyword.
- The switch expression is exactly one of:
  - the value of a `return` statement;
  - the right-hand side of a simple assignment used as a statement;
  - the initialiser of a local declared on its own, without `using` or
    `const`, whose type can be named.

## Transformation

- Each arm becomes a section, in order:
  - a constant pattern `c` becomes `case c:`;
  - the alternatives of an `or` pattern become separate labels, unless the
    arm has a `when` clause or declares a variable;
  - any other pattern, with its `when` clause, becomes a pattern label;
  - `_` becomes `default:`.
- A section returns the arm's value, or assigns it and breaks. A throw
  expression becomes a `throw` statement.
- A local initialised by the switch expression is declared first, with its
  type written out in place of `var`, and assigned in each section.
- A switch expression with no discard arm throws `SwitchExpressionException`
  for an unmatched value, so a `default` section throwing it is added, with
  the value when it is a simple name. This keeps a switch that is exhaustive
  without a discard, such as one on `bool`, from falling off the end.

## Preserved

- Behaviour, including the exception for an unmatched value.
- Comments above an arm move above its section.

## Limitations

- Switch expressions nested in larger expressions, passed as arguments, or
  used as expression bodies are refused; extract them to a local first.
- The added `SwitchExpressionException` carries the unmatched value only when
  the governing expression is a simple name or member access, to avoid
  evaluating it twice.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-switch` | the caret is not on a `switch` expression |
| `unsupported-context` | the switch expression is not the whole value of a return, an assignment statement or a single local's initialiser |
| `anonymous-type` | the local it initialises has an anonymous type, which cannot be declared separately |
