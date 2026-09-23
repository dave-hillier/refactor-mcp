# Replace Type Code with Enum

Replaces a set of int or string constants that name kinds of something, such
as `Employee.Engineer = 0` and `Employee.Manager = 1`, with an enum, and
retypes every declaration that carries one of those codes.

This is a generator: it adds a type, and its `after/` pins one chosen design
(the enum's placement, naming and values described below) rather than the
only correct answer. With int constants the program behaves as before except
where a code is formatted or converted: `ToString()` now gives the member's
name. With string constants the values themselves disappear, so anything that
printed, stored or parsed the strings changes.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | yes | The enum's name, such as `EmployeeType` |
| `constants` | yes | The names of the constants, as an array, in the order the enum lists them |

The target is the type declaring the constants, by symbol:
`"target": { "symbol": "T:Staff.Employee" }`.

## Precondition

- Every named constant is a `const` field of the type.
- The constants are all `int` or all `string`.
- The type's namespace has no type named `name`, and the type's folder has
  no file named `<name>.cs`.
- The result compiles: a code that is used as a number or a string, such as
  `_type * 10` or `_type == "E"`, cannot become an enum.

## Transformation

- A public enum named `name` is created in `<name>.cs` beside the type's
  file, in the type's namespace and in the same namespace style (file-scoped
  or block). Its members have the constants' names, in the order given, each
  with the comments that were above its constant.
- Int values are written only when they are not 0, 1, 2 and so on in order;
  otherwise every member is given its old value. String values are dropped.
- The constants are removed from the type.
- Every reference to a constant, anywhere in the solution, becomes the enum
  member, such as `Employee.Manager` or `Manager` becoming
  `EmployeeType.Manager`, with a using added where the enum's namespace is
  not in scope.
- A field, property, parameter, local or method return type of the code's
  type is retyped to the enum when a code flows into or out of it: a
  constant is assigned to it, used to initialise it, passed to it, compared
  with it by `==` or `!=`, used as a label or pattern in a `switch` or `is`
  on it, or returned from it. The same then applies, until nothing changes,
  to declarations that exchange values with one already retyped. A nullable
  `string?` becomes a nullable enum, so null checks keep compiling. Locals
  declared with `var` keep `var`.

## Preserved

- The members, their order and layout, apart from the removed constants.
- Comments next to the retyped declarations and the rewritten references.
- Which branch each code takes in comparisons and switches.

## Limitations

- Only the flows listed above are followed. A code stored in a collection,
  passed to a method outside the solution, or converted explicitly is not
  followed, and if that leaves an error the refactoring refuses rather than
  inserting casts.
- A member that overrides or implements another is retyped on its own; if
  its base or interface cannot follow, the refactoring refuses.
- Nullable int codes (`int?`) are not recognised as carriers.

## Error codes

| Code | Meaning |
|---|---|
| `constant-not-found` | the type has no constant with a given name |
| `mixed-types` | the constants mix int and string values |
| `not-a-type-code` | a constant is neither an int nor a string |
| `type-already-exists` | the namespace already has a type, or the folder a file, with the enum's name |
| `breaks-compilation` | the result would not compile, typically because a code is used as a number or string |
