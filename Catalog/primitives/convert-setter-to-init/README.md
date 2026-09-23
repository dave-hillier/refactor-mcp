# Convert Setter to Init-Only

Replaces a property's `set` accessor with an `init` accessor, so the property
can only be set while the object is being created.

## Precondition

- The property has a `set` accessor.
- It is not virtual, abstract or an override, since every property in the
  hierarchy would have to change together.
- Every assignment of the property, in any file, is one an init accessor
  allows: in an object initialiser, in a `with` expression, or in a
  constructor or init accessor of the declaring type or a type derived from
  it, through `this` or `base`. Compound assignments and increments after
  construction are refused like any other write.

## Transformation

- The `set` keyword becomes `init`. The accessor keeps its modifiers, such as
  `protected`, its attributes and its body.
- Everything else about the property, including its initialiser and
  documentation, is unchanged.

## Preserved

- Every assignment the code makes, since each is one an init accessor
  accepts.

## Limitations

- Requires C# 9 or later.
- Code outside the solution that sets the property after construction stops
  compiling.

## Error codes

| Code | Meaning |
|---|---|
| `no-setter` | the property has no `set` accessor |
| `in-hierarchy` | the property is virtual, abstract or an override |
| `assigned-after-construction` | some code sets the property after construction |
