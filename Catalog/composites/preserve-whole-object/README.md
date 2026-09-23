# Preserve Whole Object

Replaces parameters that every call fills from members of one object with a
single parameter taking the object, and reads the members in the body.

## Recipe

1. `change-signature` on the method: the new list replaces the parameters
   with one of the object's type, `value` is the object the call reads from,
   and `replacements` turns each use of a removed parameter into a read of
   the member it came from.

```json
"steps": [
  {
    "refactoring": "change-signature",
    "target": { "symbol": "M:Heating.HeatingPlan.WithinRange(System.Int32,System.Int32)" },
    "arguments": {
      "parameters": [ { "name": "range", "type": "TempRange", "value": "DaysTempRange" } ],
      "replacements": { "low": "range.Low", "high": "range.High" }
    }
  }
]
```

The plan's second step, rewriting parameter uses as member access, is
Change Signature's `replacements`, so the recipe is one step. Change Signature
passes one `value` to every call, so the recipe only describes calls that
name the object the same way; the dedicated implementation passes each call
the object that call read from.

## Arguments

| Argument | Meaning |
|---|---|
| `parameters` | the parameters every call fills from members of one object |
| `parameterName` | the name of the parameter that takes the object |

The target is the method or constructor, by symbol.

## Precondition

- The method is called at least once.
- At every call, each argument for a listed parameter reads a field or
  property of the same object, written as a name or a chain of member
  accesses, so evaluating it again has no side effects.
- Every call fills each parameter from the same member, and passes objects
  of the same type.
- No other parameter has the new parameter's name, and the listed
  parameters are never assigned in the body.
- Change Signature's precondition holds, and the result compiles.

## Transformation

- The object's parameter takes the place of the first listed parameter,
  with the type the calls pass; the others are removed.
- Each use of a removed parameter, in every body of the method's family,
  reads the member it was filled from.
- Each call passes the object instead of the values it read from it.

## Preserved

- The values the body sees, as long as the members return the same value
  when read again in the body.
- Other parameters and their arguments, with their comments and layout.

## Limitations

- The members are read when the body uses them rather than before the call,
  so a member that changes in between gives the new value.
- Values read from `this` without a receiver, or computed from members, are
  refused rather than recognised as coming from the object.
- The method gains a dependency on the object's type, which may not suit a
  method in another layer.

## Error codes

| Code | Meaning |
|---|---|
| `not-from-one-object` | a call passes a value that is not a member of an object |
| `several-objects` | a call reads its values from more than one object |
| `members-differ` | calls fill a parameter from different members |
| `types-differ` | calls pass objects of different types |
| `no-calls` | nothing calls the method |
| `unknown-parameter` | a listed name is not a parameter of the method |
| `duplicate-parameter` | another parameter has the new parameter's name |
| `replaced-parameter-assigned` | a listed parameter is assigned in the body |
| `method-group-reference` | the method is used as a method group |
| `external-member` | the method overrides or implements a member outside the solution |
