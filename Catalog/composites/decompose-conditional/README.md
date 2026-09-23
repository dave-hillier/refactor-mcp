# Decompose Conditional

Extracts the condition of an `if` statement into a method that names it, and
each branch into a method that names what it does, so the statement reads as
a sentence.

## Recipe

Extract Method three times, on the else branch, the then branch and the
condition. Each new method is placed straight after the containing method, so
extracting from the bottom up leaves them in the order the statement reads.

1. `extract-method` on the statements of the else branch, with `name` the
   else method's name.
2. `extract-method` on the statements of the then branch.
3. `extract-method` on the condition. A selection covering exactly one
   expression becomes a method returning its value, here a `bool`.

```json
"steps": [
  { "refactoring": "extract-method", "target": { "file": "Tariff.cs", "selection": "marker" }, "arguments": { "name": "SummerCharge" } },
  { "refactoring": "extract-method", "target": { "file": "Tariff.cs", "range": "26:17-26:70" }, "arguments": { "name": "WinterCharge" } },
  { "refactoring": "extract-method", "target": { "file": "Tariff.cs", "range": "24:17-24:57" }, "arguments": { "name": "NotSummer" } }
]
```

Only the first step can be marked, so the later steps select by range, which
describes the file as the steps before left it. The plan orders the steps
condition first; extracting the branches first gives the same code with the
methods in reading order.

## Arguments

| Argument | Meaning |
|---|---|
| `conditionName` | the name of the method that evaluates the condition |
| `thenName` | the name of the method for the statements run when the condition holds |
| `elseName` | the name of the method for the else branch; left out, the else branch stays as it is |

The target is the `if` statement, by a caret on its `if` keyword.

## Precondition

- The caret is on the `if` keyword of an `if` statement in a class's
  block-bodied method.
- With `elseName`, the statement has an else branch that is not another `if`
  statement.
- No branch is empty.
- Each part meets Extract Method's precondition: a branch assigns no local
  that is read after it, declares none used after it, and returns early only
  with a value.
- The class has no member with any of the names.

## Transformation

- The condition becomes a call of a `private bool` method returning it.
- The statements of each branch become a call of a `private` method holding
  them, which returns the branch's value when the branch returns one. A branch
  without braces is replaced by its call.
- Each method takes the locals and parameters its part reads, in the order it
  first reads them, and is `static` when the containing method is.
- The methods follow the containing method: the condition, the then branch,
  then the else branch.

## Preserved

- Which branch runs, and what it does, for every input.
- Comments inside a branch move with its statements; a comment leading a
  branch's first statement stays above the call.

## Limitations

- An `else if` chain is decomposed one `if` at a time; the else branch of a
  chain is refused rather than extracted whole.
- A branch that assigns a local read after the statement is refused rather
  than turned into a method returning the value.
- Conditionals in constructors, accessors, local functions and lambdas are
  not covered.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-if` | the caret is not on an `if` statement |
| `no-else-branch` | `elseName` is given but the statement has no else branch |
| `else-if-branch` | the else branch is another `if` statement |
| `empty-branch` | a branch has no statements to extract |
| `assigned-local-used-after` | a branch assigns a local read after it |
| `declared-local-used-after` | a branch declares a local used after it |
| `returns-early` | a branch returns from a `void` method before its end |
| `name-conflict` | the class already has a member with one of the names |
| `not-in-method` | the statement is not in a method |
