# Extract Class

Splits a class that does two jobs: some of its fields, properties and methods
move into a new class, and the old class keeps a field holding an instance of
it and reaches the moved members through that field.

## Recipe

1. `create-type` the new class: `{ "name": "Address", "file": "Address.cs" }`.
2. `introduce-field` of the new type on the class, which holds a new instance:
   `"target": { "symbol": "T:Shop.Customer" }, "arguments": { "type": "Address", "name": "_address" }`.
3. `move-field` (or `move-property`) each field through it:
   `"target": { "symbol": "F:Shop.Customer._street" }, "arguments": { "via": "_address" }`.
   A static field or constant moves with `"to": "Address"` instead.
4. `move-instance-method` each method through it:
   `"target": { "symbol": "M:Shop.Customer.FormatAddress" }, "arguments": { "via": "_address" }`,
   or `move-static-method` with `"to": "Address"` for a static one.

Fields and properties move before methods, so a moved method finds the data it
uses already in the new class and reads it there rather than through the old
class.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | yes | The new class's name |
| `members` | yes | The fields, properties and methods to move, by name; a method name moves every overload |
| `field` | no | The name of the field holding the new class; defaults to `_` and the class name in camel case |
| `file` | no | The new class's file, relative to the solution; defaults to `<name>.cs` beside the class |
| `stub` | no | `true` (the default) leaves a delegating method for each moved method; `false` updates its callers |

The target is the class, by symbol: `"target": { "symbol": "T:Shop.Customer" }`.

## Precondition

- The target is a class, and no type of the new name exists in its namespace.
- Every named member exists and is a field, property or ordinary method.
- The class has no member with the field's name.
- Each move meets the precondition of Move Field, Move Property, Move Instance
  Method or Move Static Method: for example a method is not virtual and uses no
  protected member, and without stubs every caller can reach the field.

## Transformation

- The new class is public, empty apart from what moves into it, in the class's
  namespace.
- The class gains `private readonly <Name> <field> = new <Name>();` after its
  fields.
- Each member moves as its primitive moves it: uses of a moved field go
  through the new field (`_street` becomes `_address._street`), private
  members become `internal`, and a moved method's uses of what stayed behind
  go through a parameter of the old class.

## Preserved

- Behaviour of every use of the moved members, and of the class's public
  surface when stubs are kept.
- Comments above moved members, which move with them.

## Limitations

- The field always holds a new instance created with the class; it is not
  passed in through a constructor.
- Moved private members become `internal` rather than staying private.
- Code that copies the class field by field, such as a serializer, sees the
  moved state inside the new object rather than on the class.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-class` | the target is a struct, interface or other type |
| `no-members` | no members were named |
| `member-not-found` | a named member does not exist in the class |
| `member-not-movable` | a named member is not a field, property or ordinary method |
| `type-already-exists` | a type of the new name exists (step 1) |
| `name-conflict` | the class already has a member with the field's name (step 2) |
| `polymorphic-method` | a method to move is virtual, abstract, an override or an interface implementation |
| `uses-protected-member` | a method to move uses a protected member of the class |
| `via-not-accessible` | without stubs, a caller of a moved method cannot reach the field |

A refusal in any step leaves every file as it was, and names the step that
refused.
