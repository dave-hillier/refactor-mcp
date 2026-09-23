# Push Down Field

Moves a field from a class into the direct subclasses that need it: those
whose code uses it, or whose callers reach it through a reference of the
subclass's type. When nothing uses the field, every direct subclass gets a
copy, so the declaration is never lost.

## Arguments

None. The target is the field, by symbol:
`"target": { "symbol": "F:Shop.Employee.Quota" }`.

## Precondition

- The class has at least one subclass declared in the solution.
- The class does not use the field itself.
- No code reaches the field through a reference of the class's type, or of a
  type above it, since such a reference would no longer have the field.
- The field's type and initializer use nothing private to the class.
- No receiving subclass already has a member of that name.
- The result compiles.

## Transformation

- Each receiving subclass gets the field after its last field, or first when
  it has none, with its documentation comment, initializer and modifiers.
- A use by a class further down counts as a use by the direct subclass it
  derives from.
- In a sealed subclass a protected field becomes private.
- The class's type parameters are written as the subclass's type arguments:
  `List<TItem>` in `Store<TItem>` becomes `List<Order>` in
  `OrderStore : Store<Order>`. Each subclass's file gains the usings the
  field needs.
- A declaration of several fields is split, and only the target moves.

## Preserved

- Every use of the field, which now binds to the subclass's copy.
- Comments around the field that belong to its neighbours.

## Limitations

- The field only moves one level: a field that a class further down uses
  goes to the direct subclass above that class, not to the class itself.
- Usings the class's file no longer needs are left in place.

## Error codes

| Code | Meaning |
|---|---|
| `no-subclasses` | no class in the solution derives from the field's class |
| `used-by-base` | the field's class uses the field itself |
| `used-through-base` | code reaches the field through a reference of the field's class |
| `uses-base-private-members` | the field's type or initializer uses something private to its class |
| `member-exists-in-subclass` | a receiving subclass already has a member of that name |
| `breaks-compilation` | the result would not compile |
