# Replace Array with Object

Replaces an array whose elements mean different things, such as a name at
index 0 and a score at index 1, with a new class that has a named property
for each index.

This is a generator: it adds a type, so the fixtures pin one chosen design
rather than the only correct answer.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | yes | The new class's name |
| `members` | yes | The property names, one per index in order, as an array |

The target is a field, by symbol (`"symbol": "F:Shop.Result.Score"`), or a
field or local, by caret on its name in its declaration or a use.

## Precondition

- The variable is a single-dimensional array whose element type does not use
  a type parameter.
- The class name and every member name are valid identifiers, and the member
  names are distinct.
- The variable's namespace has no type with the class's name, and its folder
  no file with that name.
- Every use of the variable is an element access with a literal index, or an
  assignment of a new array.
- Every literal index has a member, and every array created for the variable
  has as many elements as there are members.
- The variable is declared on its own, not alongside other variables.
- The result compiles.

## Transformation

- The class is a `public class` in a new file named after it, beside the file
  declaring the variable, in that file's namespace and namespace style.
- It has one `public` auto-property with `get` and `set` per member, in index
  order, typed as the array's element type, with no blank lines between them.
  When nullable analysis is enabled and the element type is a non-nullable
  reference type, each property is initialised to `null!`, since an array's
  elements start null too.
- The variable's declared type becomes the class; `var` stays `var`.
- `new T[2]` becomes `new C()`.
- `new T[] { a, b }`, `new[] { a, b }`, `{ a, b }` and `[a, b]` become
  `new C { First = a, Second = b }`, keeping the initialiser's line breaks
  and comments.
- `x[1]` becomes `x.Second`.
- The new file imports what the element type needs.

## Behaviour changes

- The value is a reference to an object rather than an array: code that
  copied the array reference still shares it, as before, but nothing can
  index it or ask for its length.

## Preserved

- Every value stored and read, now through the named properties.
- Comments and layout of the initialisers and element accesses.

## Limitations

- An index must be an integer literal; a constant from elsewhere or a
  variable is refused.
- Uses as an array (`Length`, `foreach`, passing it to a method, returning it)
  are refused rather than adapted.
- Every property has the array's element type; narrowing them is left to
  Change Type.
- A parameter or property holding the array cannot be the target.

## Error codes

| Code | Meaning |
|---|---|
| `not-an-array` | the variable is not a single-dimensional array field or local |
| `generic-element-type` | the element type uses a type parameter |
| `invalid-name` | the class or a member name is not a valid identifier, or the names repeat |
| `type-already-exists` | the class's name is already taken |
| `unsupported-use` | the variable is used as an array, or assigned something other than a new array |
| `index-out-of-range` | a literal index has no member |
| `wrong-member-count` | an array created for the variable has a different number of elements than there are members |
| `breaks-compilation` | the result would not compile |
