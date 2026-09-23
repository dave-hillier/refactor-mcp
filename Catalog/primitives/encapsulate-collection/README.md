# Encapsulate Collection

Stops callers changing a type's list behind its back: the list is exposed as
a read-only view, and the type gains `Add` and `Remove` methods for the
changes it allows.

## Precondition

- The target is a private field of type `List<T>`.
- A property that exposes it, if there is one, only has a getter; a setter
  could replace the list.
- The element name, from the `name` argument or the property name made
  singular, gives `Add<Element>` and `Remove<Element>` names that are free, as
  is the property name when a property has to be created.
- After the change, every use of the property still compiles. Callers that
  read, count, index or enumerate the list are unaffected, and calls to
  `Add` or `Remove` through the property are redirected. Any other change,
  such as `Clear()` or passing the list where a `List<T>` is expected, is
  refused and nothing is changed.

## Transformation

- The property whose getter returns the field becomes an
  `IReadOnlyList<T>` returning `_field.AsReadOnly()`, keeping its form,
  documentation and position. When no property exposes the field, one named
  after it (`_tags` gives `Tags`) is added after the type's fields.
- `public void Add<Element>(T element)` and
  `public bool Remove<Element>(T element)` are added after the property,
  delegating to the list. `Remove` returns whether an element was removed, as
  `List<T>.Remove` does.
- The element name drops a plural ending: `Tags` gives `Tag` and `Entries`
  gives `Entry`. Irregular plurals need the `name` argument.
- `order.Tags.Add(x)` becomes `order.AddTag(x)` and `order.Tags.Remove(x)`
  becomes `order.RemoveTag(x)`, in any file and inside the type.
- Code inside the type that uses the field directly is unchanged.

## Preserved

- The contents of the list after every call, and every value callers read.
- Nullable annotations on the element type.

## Limitations

- Only `List<T>` is supported; sets, dictionaries and arrays are refused.
- The view is a live read-only wrapper, not a copy, so callers see later
  changes.
- Code outside the solution that changed the list through the property stops
  compiling.

## Error codes

| Code | Meaning |
|---|---|
| `field-not-private` | the field is visible outside the type |
| `unsupported-collection-type` | the field is not a `List<T>` |
| `property-has-setter` | the exposing property can replace the list |
| `name-conflict` | a member name the refactoring needs is taken |
| `unsupported-use` | code changes the list through the property in a way other than `Add` or `Remove` |
