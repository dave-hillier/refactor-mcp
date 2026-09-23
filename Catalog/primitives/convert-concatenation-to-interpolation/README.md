# Convert String Concatenation to Interpolation

Turns a chain of `+` string concatenations into one interpolated string:
`"Hello, " + name + "!"` becomes `$"Hello, {name}!"`.

## Target

The concatenation, by a caret anywhere in it. The whole chain the caret is in
is converted, through parentheses around nested concatenations.

## Precondition

- The expression is a `+` that concatenates strings.
- No operand of the chain is itself a numeric addition, as in
  `a + b + " total"`, where `a + b` adds numbers before the concatenation.
- There are no comments between the operands.

## Transformation

- String literals become text, keeping their escapes as written. Braces are
  doubled so they stay text.
- The result is a verbatim `$@"..."` when every string literal is verbatim.
  Otherwise it is a regular `$"..."`, and verbatim literals are escaped.
- Character literals become text.
- An interpolated string operand is merged in.
- Any other operand becomes an interpolation `{operand}`. Parentheses around it
  are dropped, except around a conditional, whose colon would start a format.
- `value.ToString("F2")` on a formattable value type becomes `{value:F2}`.
- The result is written on one line, in place of the chain.

## Preserved

- The resulting string: interpolation formats each value with the current
  culture, as concatenation does.
- Comments before and after the expression.

## Limitations

- A value of a type whose `IFormattable.ToString(null, provider)` differs from
  its `ToString()` is formatted by the former after conversion.
- Raw string literals are interpolated as values rather than merged as text.
- An expression split over several lines becomes one line, however long.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-concatenation` | the expression at the caret is not a string concatenation |
| `numeric-addition` | an operand of the chain adds numbers before the concatenation |
| `contains-comments` | there are comments between the operands, which the string could not keep |
