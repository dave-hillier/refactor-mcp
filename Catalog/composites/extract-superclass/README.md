# Extract Superclass

Gives a class a new base class and moves some of its fields and methods up
into it, so that other classes can later share them by deriving from it too.

## Recipe

1. `create-type` the superclass, deriving from the class's current base class
   if it has one: `{ "name": "Employee", "file": "Employee.cs", "baseType": "Person" }`.
2. `change-base-type` the class to it:
   `"target": { "symbol": "T:Staff.Manager" }, "arguments": { "to": "Employee" }`.
3. `pull-up-field` each field: `"target": { "symbol": "F:Staff.Manager._name" }`.
4. `pull-up-method` each method: `"target": { "symbol": "M:Staff.Manager.Badge" }`.

The superclass takes over the old base class, so every inherited member stays
inherited and Change Base Type has nothing to refuse. Fields go up before
methods, so a method using a pulled-up field finds it in the superclass.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | yes | The superclass's name |
| `members` | no | The fields and methods to pull up, by name; a method name pulls up every overload. Without it the superclass starts empty |
| `file` | no | The superclass's file, relative to the solution; defaults to `<name>.cs` beside the class |

The target is the class, by symbol: `"target": { "symbol": "T:Staff.Manager" }`.

## Precondition

- The target is a class, not static and not a record.
- No type of the superclass's name exists in the class's namespace.
- Every named member exists and is a field or ordinary method.
- Each member meets the precondition of Pull Up Field or Pull Up Method: it
  uses nothing that stays behind in the class, and the result compiles.

## Transformation

- The superclass is public, in the class's namespace, and derives from the
  class's old base class, written as the class wrote it.
- The class derives from the superclass instead, keeping its interfaces.
- Each member moves up as its primitive moves it: a private member becomes
  protected, and documentation comments go with it.

## Preserved

- Every use of the class and its members, which now reach the pulled-up
  members by inheritance.
- The class's interfaces and the comments around its declaration.

## Limitations

- Only one class is given the superclass. Another class with the same members
  can be moved under it with Change Base Type, after which pulling a member up
  from either removes the identical copy from the other.
- Properties are not pulled up, because there is no Pull Up Property.
- A generic class's type parameters are not given to the superclass.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-class` | the target is a struct, interface, record or static class |
| `member-not-found` | a named member does not exist in the class |
| `member-not-movable` | a named member is not a field or ordinary method |
| `type-already-exists` | a type of the superclass's name exists (step 1) |
| `uses-subclass-members` | a member uses something that stays in the class |
| `member-exists-in-base` | the old base class already has a member of that name |
| `breaks-compilation` | pulling a member up would not compile |

A refusal in any step leaves every file as it was, and names the step that
refused.
