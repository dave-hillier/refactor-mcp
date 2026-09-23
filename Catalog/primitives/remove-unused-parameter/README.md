# Remove Unused Parameter

Removes a parameter that no body reads, together with the argument every call
passes for it.

## Arguments

| Argument | Meaning |
|---|---|
| `parameter` | the name of the parameter to remove |

The target is the method or constructor, by symbol.

## Precondition

- No member of the method's family uses the parameter: not the method, not an
  override, not an implementation of the same interface member.
- No call passes an argument for it that may have side effects: a call, an
  object creation, an assignment, an increment or an await. Dropping such an
  argument would drop the work it does.
- The method is only called, never used as a method group.
- Every member of the family is declared in the solution.
- The result compiles.

## Transformation

The parameter is removed as by Change Signature: from the method, every
override and interface member related to it, and every call. Named arguments
keep their names.

## Preserved

- The behaviour of every call.
- Layout of the remaining parameters and arguments. A line comment after the
  comma of the element that becomes last moves after the closing parenthesis.

## Limitations

- Reading a property is assumed to have no side effects, so an argument that
  only reads properties is dropped.
- `<param>` documentation for the parameter is left in place.

## Error codes

| Code | Meaning |
|---|---|
| `parameter-in-use` | the method or another member of its family uses the parameter |
| `argument-has-side-effects` | a call's argument may have side effects |
| `method-group-reference` | the method is used as a method group |
| `external-member` | the method overrides or implements a member outside the solution |
