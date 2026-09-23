# Convert If Chain to Switch Statement

Turns an `if` / `else if` chain that compares one value with constants or
patterns into a `switch` statement on that value.

## Precondition

- The caret is on the first `if` of the chain.
- The chain has at least two cases, counting a final `else`.
- Every condition tests the same value, written the same way, in one of these
  forms:
  - `x == c` or `c == x`, where `c` is a constant, including `null` and enum
    members;
  - `x is T` or `x is pattern`;
  - several of those joined by `||`, none declaring a pattern variable;
  - one of those followed by `&& condition`.
- The tested value has no side effects: the chain evaluates it once per
  comparison, where a switch evaluates it once.
- No branch contains a `break` that belongs to an enclosing loop: in a switch
  it would leave the switch instead.
- A comparison does not use a user-defined `==` (other than `string`'s), which
  a constant pattern would not call, and does not compare with NaN, which `==`
  never matches but a constant pattern does.

## Transformation

- Each `if` becomes a switch section, in order:
  - `x == c` becomes `case c:`;
  - `x is T t` becomes `case T t:`, and `x is null` becomes `case null:`;
  - `a || b` becomes one label for each alternative;
  - `x is T t && condition` becomes `case T t when condition:`.
- The final `else` becomes `default:`.
- Each section runs its branch's statements, followed by `break;` unless they
  always jump away already.
- Without a final `else`, there is no `default` section, and an unmatched value
  runs on to the code after the switch, as it ran on after the chain.

## Preserved

- Behaviour: cases are tried in the order of the chain, and each branch still
  runs only when its condition is the first to hold.
- Comments before the chain, and comments inside each branch.

## Limitations

- Chains of separate `if` statements, each ending in a `return`, are not
  collected; only `else if` chains are.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `too-few-cases` | the chain has fewer than two cases |
| `different-values` | the conditions do not all test the same value |
| `unsupported-condition` | a condition is not a constant comparison or a pattern test that a case label can express |
| `side-effects` | the tested value has side effects |
| `contains-break` | a branch contains a `break` of an enclosing loop |
