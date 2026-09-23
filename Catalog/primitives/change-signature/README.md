# Change Signature

Adds, removes and reorders the parameters of a method or constructor, and
updates every call in the solution to match. Overrides, overridden methods,
interface members and their implementations change together, since they must
keep the same parameters.

Many composites build on this primitive: Introduce Parameter Object, Preserve
Whole Object, Parameterise Method and Constructor Injection all change a
signature as one of their steps.

## Arguments

`parameters` is the complete new parameter list, in order. An existing
parameter is named; a new one also gives its type, and either the `value`
every existing call passes for it or a `default`, or both. A parameter of the
old signature that is not listed is removed.

```json
"arguments": {
  "parameters": [
    { "name": "b" },
    { "name": "a" },
    { "name": "scale", "type": "int", "value": "1" },
    { "name": "label", "type": "string?", "default": "null" }
  ]
}
```

| Field | Meaning |
|---|---|
| `name` | an existing parameter's name, or the new parameter's name |
| `type` | the type of a new parameter; given only for new parameters |
| `value` | a C# expression each existing call passes for a new parameter |
| `default` | a default value declared for a new parameter; with no `value`, existing calls leave it out |

`replacements` is optional. For a removed parameter the body still uses, it
gives the expression that takes the place of each use, usually one reading
the parameter's value from a new parameter:

```json
"arguments": {
  "parameters": [ { "name": "point", "type": "Point", "value": "new Point(3, 4)" } ],
  "replacements": { "x": "point.X", "y": "point.Y" }
}
```

The target is the method or constructor, by symbol. Targeting an override or
an implementation changes the whole family.

## Precondition

- Every listed name is either an existing parameter or comes with a type.
- A new parameter has a value for existing calls, a default, or both.
- No removed parameter is read or written in the body of any member of the
  family, unless `replacements` gives an expression for it. A replaced
  parameter is only read, never assigned, and only removed parameters are
  replaced.
- A params array stays last, and no required parameter follows an optional
  one.
- The `this` parameter of an extension method stays first.
- Every member of the family is declared in the solution; a method that
  implements or overrides a member from a referenced assembly cannot change.
- The method is only called, never converted to a delegate: a method group
  would no longer match its delegate type.
- The result compiles.

## Transformation

- Each declaration in the family gets the new parameter list. New parameters
  are declared with the given type and default.
- Each use of a replaced parameter, in every body of the family, becomes its
  expression, in parentheses unless it is a name or member access.
- Each call's arguments are rearranged into the new order, with the given
  value for each new parameter and nothing for a removed one. This covers
  invocations, `base` calls, object creation, target-typed `new`, and `this()`
  and `base()` constructor initializers.
- Named arguments keep their names. Once a call leaves an optional parameter
  to its default, every argument after it is named.
- The arguments a call passes to a params array in expanded form stay
  together, in order, at the end.
- A call to an extension method in reduced form keeps its receiver.
- References in `nameof` and documentation comments are left alone.

## Preserved

- Every call passes the same values to the same parameters, so the method sees
  what it saw before, apart from new parameters, which receive the given value.
- Layout: each position in a parameter or argument list keeps its line breaks
  and indentation, so a list laid out one per line stays that way.
- Comments travel with the parameter or argument they belong to, including a
  line comment after its comma.
- Nullable annotations on new and existing parameters.

## Limitations

- Arguments are evaluated in the new parameter order, so reordering calls
  whose arguments have side effects that depend on each other changes the
  order those side effects happen in.
- A removed parameter's argument is dropped even if evaluating it had side
  effects. Remove Unused Parameter refuses in that case.
- A replacement is taken on trust: behaviour is preserved only when, at every
  call, the expression gives the value the call passed for the parameter.
- `<param>` elements in documentation comments are not reordered, added or
  removed, and `cref` references with parameter lists are not updated.
- Delegates, indexers, operators, attribute constructors and primary
  constructors are not covered.
- A call that passes several values to a params array after an argument it
  leaves out cannot be named, and is refused.

## Error codes

| Code | Meaning |
|---|---|
| `removed-parameter-in-use` | a parameter left out of the new list is used in a body |
| `replaced-parameter-assigned` | a parameter given a replacement is assigned in a body |
| `replaced-parameter-kept` | a parameter given a replacement is still in the new list |
| `unknown-parameter` | a listed name is not a parameter and has no type to add it |
| `duplicate-parameter` | a new parameter has the name of an existing one |
| `missing-value` | a new parameter has neither a value for calls nor a default |
| `optional-before-required` | a required parameter would follow an optional one |
| `params-not-last` | a params array would not be last |
| `extension-this-moved` | the `this` parameter of an extension method would move |
| `method-group-reference` | the method is used as a method group |
| `external-member` | the method overrides or implements a member outside the solution |
