# Convert Method to Local Function

Moves a private method used by only one member into that member as a local
function. The reverse of Convert Local Function to Method.

## Target

The method, by symbol. Overloads are told apart by their signature.

## Precondition

- The method is private, and not an extension, `extern` or `partial` method.
- It is used, and every use outside its own body is in one member: one
  method, constructor, operator or accessor. Uses inside the method, such as
  recursive calls, do not count.
- Every use is inside that member's block body, where a local function can be
  declared. A member with an expression body can be converted to a block body
  first.
- Every call is on the member's own instance: unqualified, through `this`, or
  through the type for a static method.
- The caller uses no other overload of the method, which the local function
  would hide.
- No local, parameter, local function or method type parameter of the caller
  has the method's name, the name of one of its type parameters, or the name
  of anything the method refers to from outside itself, such as a field it
  reads.

## Transformation

- The method becomes a local function at the end of the caller's body, after
  a blank line, with the same signature, type parameters, constraints and
  body. Its accessibility modifier is dropped; a static method becomes a
  `static` local function.
- `this.M()` and `Type.M()` become `M()`.
- The method is removed from its type. When the caller is in another file of
  a partial type, the local function goes there.

## Preserved

- Behaviour: every call reaches the same code with the same `this`.
- Comments above and inside the method move with it.
- Uses as a method group and in `nameof`.

## Limitations

- A caller with an expression body is refused rather than converted to a
  block body.
- Uses in a constructor initializer or a field initializer are outside any
  block and are refused.

## Error codes

| Code | Meaning |
|---|---|
| `not-private` | the method is not private, so code outside its type may use it |
| `unsupported-method` | the method is an extension, `extern` or `partial` method |
| `never-called` | nothing uses the method, so there is no member to move it into |
| `called-from-several-members` | the method is used from more than one member |
| `caller-expression-bodied` | a use is outside a block body, such as in an expression-bodied member |
| `called-on-another-instance` | a call is on an instance other than the caller's own |
| `overloaded-method` | the caller also uses another overload of the method |
| `name-conflict` | a name in the caller would hide the method or something it refers to |
