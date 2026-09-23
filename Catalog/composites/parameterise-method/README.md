# Parameterise Method

Replaces several methods that do the same thing with different literal
values by one method that takes those values as parameters, and makes every
call pass the values of the method it called.

## Recipe

1. `introduce-parameter` on each literal of the first method that differs
   between the methods, in the order they appear. Its calls now pass the
   literal.
2. `rename` the first method to the new name.
3. For each other method, `extract-method` on its body with `name` the new
   name. Its body is the parameterised method's body with other literals, so
   Extract Method makes it a call of that method passing them.
4. `inline-method` on the other method, so its callers call the
   parameterised method directly.

```json
"steps": [
  { "refactoring": "introduce-parameter", "target": { "file": "Employee.cs", "selection": "marker" }, "arguments": { "name": "factor" } },
  { "refactoring": "rename", "target": { "symbol": "M:Payroll.Employee.TenPercentRaise(System.Decimal)" }, "arguments": { "name": "Raise" } },
  { "refactoring": "extract-method", "target": { "file": "Employee.cs", "range": "19:13-19:30" }, "arguments": { "name": "Raise" } },
  { "refactoring": "inline-method", "target": { "symbol": "M:Payroll.Employee.FivePercentRaise" } }
]
```

The plan's recipe changes the first method's signature with Change
Signature; Introduce Parameter does that and replaces the literal in one
step. Its "redirect the similar methods" is Extract Method named after the
parameterised method, which turns code matching an existing method's body
into a call of it.

## Arguments

| Argument | Meaning |
|---|---|
| `methods` | the similar methods, by name; the first becomes the parameterised method |
| `name` | the name of the parameterised method |
| `parameterNames` | a name for each literal that differs between the methods, in the order the literals appear |

The target is the file declaring the methods: `"target": { "file": "Employee.cs" }`.

## Precondition

- At least two methods are named, each a block-bodied method of the same
  class with the same return type, parameters and static modifier, and none
  of them generic.
- Their bodies are the same token for token, except for literals; a literal
  that differs has the same type in every method.
- There is one parameter name for each position where a literal differs.
- Each step's precondition holds: the parameter names are free in the first
  method, and the other methods are only called, never virtual, overridden or
  used as method groups.

## Transformation

- The first method takes a parameter for each differing literal, after its
  required parameters, with the literal's type, and is renamed. Its body uses
  the parameters where the literals were.
- Each call of any of the methods calls the parameterised method, passing the
  literals of the method it called.
- The other methods are deleted.

## Preserved

- What each call does: the parameterised method runs the same code with the
  values the called method had.
- Other members of the class and their callers.

## Limitations

- Only literals can differ. Methods that differ in a name, an operator or
  a constant are refused.
- A literal that appears more than once gets one parameter per appearance.
- The methods must be declared in one file.

## Error codes

| Code | Meaning |
|---|---|
| `too-few-methods` | fewer than two methods are named |
| `signatures-differ` | the methods differ in class, return type, parameters or static modifier |
| `bodies-differ` | the bodies differ in more than literals of the same type |
| `same-body` | no literal differs, so there is nothing to make a parameter |
| `parameter-count` | the number of parameter names differs from the number of differing literals |
| `no-block-body` | a method has no block body |
| `name-conflict` | a parameter name is already used in the first method |
| `polymorphic-method` | a similar method is virtual, an override or an interface implementation |
| `method-group-reference` | a similar method is used without being called |
