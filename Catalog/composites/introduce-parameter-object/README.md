# Introduce Parameter Object

Replaces a group of parameters that travel together with one parameter of a
new positional record holding them. The body reads the record's properties,
and every call builds the record from the arguments it passed.

## Recipe

1. `change-signature` on the method: the new list replaces the group with a
   tuple parameter whose element names are the grouped parameters' names,
   `value` builds the tuple at the call, and `replacements` turns each use of
   a grouped parameter into a read of its element.
2. `convert-tuple-to-named-type` on the method with `parameter` naming the
   tuple parameter. The tuple becomes the record, the element reads become
   property reads, and the tuples calls pass become `new` expressions.

```json
"steps": [
  {
    "refactoring": "change-signature",
    "target": { "symbol": "M:Bank.Account.TotalBetween(System.DateTime,System.DateTime)" },
    "arguments": {
      "parameters": [ { "name": "range", "type": "(DateTime start, DateTime end)", "value": "(from, to)" } ],
      "replacements": { "start": "range.start", "end": "range.end" }
    }
  },
  {
    "refactoring": "convert-tuple-to-named-type",
    "target": { "symbol": "M:Bank.Account.TotalBetween(System.ValueTuple{System.DateTime,System.DateTime})" },
    "arguments": { "name": "DateRange", "parameter": "range" }
  }
]
```

The plan's recipe starts with Create Type, but that primitive creates an
empty type, and no primitive gives it the properties and constructor a
parameter object needs. Convert Tuple to Named Type creates the record with
them and rewrites the calls, so the recipe passes through a tuple instead.
Change Signature passes one `value` to every call, so the recipe only
describes a method with a single caller; the dedicated implementation builds
each call's tuple from that call's own arguments.

## Arguments

| Argument | Meaning |
|---|---|
| `parameters` | the parameters to group, in the order the record declares them |
| `typeName` | the name of the new record |
| `parameterName` | the name of the parameter that replaces the group |
| `kind` | `struct` for a `readonly record struct` (the default), `class` for a `sealed record` |

The target is the method, by symbol.

## Precondition

- The target is an ordinary method, and at least two parameters are grouped.
- Each grouped parameter exists, is passed by value, is required, is not a
  params array and is not the `this` of an extension method.
- No parameter outside the group has the new parameter's name.
- No type named `typeName` is visible where the record would be declared.
- Change Signature's precondition holds: the method is only called, never
  converted to a delegate, and its family is declared in the solution.
- The result compiles.

## Transformation

- The new parameter takes the place of the first grouped parameter; the
  others are removed.
- The record is declared after the type containing the method, with that
  type's accessibility. Its properties are the grouped parameters' names in
  Pascal case, with their types, in the order given.
- Each use of a grouped parameter in the body, and in the bodies of
  overrides and implementations, reads the matching property.
- Each call passes `new TypeName(...)` built from the arguments it passed for
  the grouped parameters, keeping the names of other named arguments.

## Preserved

- The values the method sees: each property holds what the call passed for
  the parameter it replaces.
- Arguments are evaluated in the order they were written.
- Other parameters and their arguments, with their comments and layout.

## Limitations

- The record is declared in the method's file, not a file of its own.
- The new argument is written positionally even when the call named the
  grouped arguments.
- Constructors are not covered, since Convert Tuple to Named Type works on
  methods.

## Error codes

| Code | Meaning |
|---|---|
| `too-few-parameters` | fewer than two parameters are grouped |
| `unknown-parameter` | a listed name is not a parameter of the method |
| `unsupported-parameter` | a grouped parameter is ref, out, params, optional or an extension's this |
| `duplicate-parameter` | a parameter outside the group has the new parameter's name |
| `not-a-method` | the target is a constructor or other special method |
| `type-name-conflict` | a type named `typeName` is already visible |
| `method-group-reference` | the method is used as a method group |
| `external-member` | the method overrides or implements a member outside the solution |
