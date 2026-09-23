# Feature Flag Wrapping

Replaces an `if` on a feature flag with strategies: each branch moves into
the `Apply` method of its own class, and a property named after the flag
checks it and returns the strategy to apply. The flag then decides between
two objects in one place instead of guarding code inline, and the old path
can later be deleted by deleting a class.

This is a generator: it adds an interface, two classes and a property. The
`after/` fixtures pin one design, described below, rather than the only
correct answer.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `flag` | yes | The flag's name, as passed to `IsEnabled`, such as `NewCheckout` |

The target is the file holding the check: `"target": { "file": "Checkout.cs" }`.

## Precondition

- The file has exactly one `if` whose condition is `x.IsEnabled("Flag")`,
  with the flag as a string literal.
- `x` is a field, property or type the class can read, such as a
  `_flags` field or a static `Features` class, not a local or parameter.
- The branches do not leave the method (`return`, `break`, `continue`,
  `goto`, `yield`), do not use members of the class or its base classes, nor
  local functions, and do not assign a local or parameter declared outside
  them.
- No type named `I<Flag>Strategy`, `<Flag>Strategy` or `No<Flag>Strategy` is
  in scope, and the class has no member named `<Flag>`.
- The result compiles.

## Transformation

- `internal interface I<Flag>Strategy` declares `void Apply(...)`.
- `internal sealed class <Flag>Strategy` implements it with the `if` branch;
  `internal sealed class No<Flag>Strategy` with the `else` branch, or an empty
  body when there is none. An `else if` chain moves whole into the disabled
  strategy.
- The three types follow the outermost type declaring the check, in the same
  namespace, in that order.
- `Apply` takes the parameters and locals the branches read, in the order the
  method declares them, typed with the nullability they have at the check: a
  `string?` known not to be null there is passed as `string`.
- The class gains `private I<Flag>Strategy <Flag> => x.IsEnabled("Flag") ?
  new <Flag>Strategy() : new No<Flag>Strategy();`, static when the method
  holding the check is, placed after that method.
- The `if` becomes `<Flag>.Apply(...);`, keeping the comments above it. A
  comment trailing a branch moves with the branch.

## Preserved

- When the flag is checked: each call still checks it once, at the same
  point, so turning the flag on or off takes effect as before.
- What each branch does.

## Behaviour changes

- Each call allocates a strategy object.

## Limitations

- Negated checks (`!x.IsEnabled(...)`), flags named by a constant, and checks
  combined with other conditions are not recognised.
- Branches that use the class's members are refused rather than given access
  to them through a parameter.
- Branches that `await` would need an asynchronous `Apply` and are refused as
  not compiling.

## Error codes

| Code | Meaning |
|---|---|
| `flag-not-found` | the file has no `if` checking the flag |
| `ambiguous-flag-check` | the flag is checked by more than one `if` in the file |
| `flag-source-not-member` | the flags are read from a local or parameter |
| `branch-leaves-early` | a branch returns, breaks, continues, jumps or yields out |
| `branch-uses-members` | a branch uses a member of the class or a local function |
| `branch-assigns-outer-variable` | a branch assigns a variable declared outside it |
| `type-already-exists` | a strategy type's name is taken |
| `member-exists` | the class already has a member named after the flag |
