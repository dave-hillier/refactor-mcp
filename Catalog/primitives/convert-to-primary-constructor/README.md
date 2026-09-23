# Convert to Primary Constructor

Replaces a constructor that only hands its parameters to fields and
properties with a C# 12 primary constructor. Private fields that merely hold a
parameter are removed and their uses read the parameter directly; other
members are initialized from it.

## Arguments

None. The target is the class or struct, by symbol.

## Precondition

- The type is a class or struct, not a record, and the project's language
  version is C# 12 or later.
- It has exactly one instance constructor, which is public, carries no
  attributes or documentation comment, and does not call `this(...)`.
- The constructor body only assigns parameters to fields or auto-properties
  of the type, one statement each, and every parameter is either assigned or
  passed to the base constructor.
- After the change, every use of a removed field reads the parameter; none
  would bind to a local, a parameter or a member of the same name.

## Transformation

- The constructor's parameter list, with defaults and modifiers, follows the
  type's name and type parameters. A `base(...)` call becomes the argument
  list of the base type in the base list. The constructor is removed.
- A field is removed and its uses, `_field` or `this._field`, become the
  parameter when it is private, has no attributes or comments, is assigned
  only in the constructor, is read only through `this`, and its parameter is
  assigned to nothing else and not passed to the base constructor.
- Every other assigned member keeps its declaration and gains the parameter
  as its initializer.

## Preserved

- Construction and the value of every member. A captured parameter is
  mutable where a `readonly` field was not, but nothing assigned it before.

## Limitations

- A class with several constructors is refused rather than having the others
  chain to the primary one.
- A captured parameter no longer has the field's name in debuggers and
  reflection.

## Error codes

| Code | Meaning |
|---|---|
| `unsupported-type` | the type is a record, an interface or already has a primary constructor |
| `language-version` | the project's language version is older than C# 12 |
| `several-constructors` | the type has more than one instance constructor, or none |
| `constructor-accessibility` | the constructor is not public |
| `constructor-annotated` | the constructor has attributes or a documentation comment |
| `constructor-has-logic` | the constructor does more than assign its parameters to fields and auto-properties |
| `parameter-shadowed` | a use of a removed field would bind to another symbol with the parameter's name |
