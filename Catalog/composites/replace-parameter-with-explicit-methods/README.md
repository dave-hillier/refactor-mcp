# Replace Parameter with Explicit Methods

Gives each value a method dispatches on a method of its own, and makes calls
that pass the value as a constant call that method directly. `SetValue("height", 10)`
becomes `SetHeight(10)`.

## Recipe

1. `extract-method` on the statements of each value's branch, with `name`
   the explicit method's name. Extracting the last branch first leaves the
   methods in the order of their values. A `return;` or `break;` ending the
   branch stays in the branch.
2. `change-accessibility` on each new method, giving it the dispatching
   method's accessibility.
3. `redirect-calls-with-constant-argument` on the dispatching method for each
   value, with `parameter`, the `value` and the new `method`: each call that
   passes the value as a constant calls the value's method.
4. `safe-delete-member` on the dispatching method once nothing calls it.

```json
"steps": [
  { "refactoring": "extract-method", "target": { "file": "Box.cs", "selection": "marker" }, "arguments": { "name": "SetWidth" } },
  { "refactoring": "extract-method", "target": { "file": "Box.cs", "range": "14:17-14:33" }, "arguments": { "name": "SetHeight" } },
  { "refactoring": "change-accessibility", "target": { "symbol": "M:Shapes.Box.SetHeight(System.Int32)" }, "arguments": { "accessibility": "public" } },
  { "refactoring": "change-accessibility", "target": { "symbol": "M:Shapes.Box.SetWidth(System.Int32)" }, "arguments": { "accessibility": "public" } },
  { "refactoring": "redirect-calls-with-constant-argument", "target": { "symbol": "M:Shapes.Box.SetValue(System.String,System.Int32)" }, "arguments": { "parameter": "name", "value": "\"height\"", "method": "SetHeight" } },
  { "refactoring": "redirect-calls-with-constant-argument", "target": { "symbol": "M:Shapes.Box.SetValue(System.String,System.Int32)" }, "arguments": { "parameter": "name", "value": "\"width\"", "method": "SetWidth" } }
]
```

In the recipe case a call passing a variable remains, so step 4 does not
apply. The dedicated implementation does all four steps as one change.

## Arguments

| Argument | Meaning |
|---|---|
| `parameter` | the parameter the method dispatches on |
| `methods` | the values to give methods of their own, each `{ "value": "\"height\"", "name": "SetHeight" }`, the value written as C# |

The target is the method, by symbol.

## Precondition

- The method is a block-bodied, non-virtual method of a class that neither
  overrides nor implements another.
- Its body starts by choosing what to do from the parameter: a `switch` on
  it with one constant `case` label per section, or a run of `if`
  statements and `else if` chains comparing it with `==` to constants.
- Each named value has a branch, which is not empty.
- Each branch ends the method: it returns or throws, or nothing follows the
  dispatch.
- Each branch meets Extract Method's precondition, and the class has no
  member with the new names.

## Transformation

- Each named value's branch becomes a method with the given name and the
  dispatching method's accessibility, taking the parameters the branch reads
  and returning what it returns. The branch now calls it.
- Each call that passes one of the named values as a constant, and whose
  arguments have no side effects, calls that value's method on the same
  receiver, passing the arguments for its parameters.
- The dispatching method is deleted once no reference to it remains.

## Preserved

- What each call does: a call passing a named value runs that value's
  branch, as the dispatch would have chosen it.
- Calls that pass a variable, or another value, still reach the dispatching
  method, which runs the same code through the new methods.

## Limitations

- The dedicated implementation keeps any call whose arguments have side
  effects calling the dispatching method, since the explicit method may not
  take all of them. The recipe's redirect step is finer: it keeps only calls
  where an argument it would drop or reorder has side effects.
- Values are matched by their constant value; a call passing a variable that
  happens to hold a value is not redirected.
- A dispatch that does other work first, or a branch that runs on into
  later statements, is refused.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-dispatch` | the body does not start by choosing from the parameter compared with constants |
| `unknown-value` | the method has no branch for a named value |
| `falls-through` | a named value's branch runs on into the rest of the method |
| `empty-branch` | a named value's branch has no statements |
| `no-values` | no value is named |
| `polymorphic-method` | the method is virtual, an override or an interface implementation |
| `not-in-class` | the method is not a block-bodied method of a class |
| `unknown-parameter` | the method has no parameter of that name |
| `assigned-local-used-after` | a branch assigns a local read after it |
| `name-conflict` | the class already has a member with one of the names |
