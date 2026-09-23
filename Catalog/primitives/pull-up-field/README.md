# Pull Up Field

Moves a field from a class into its base class. The other subclasses that
declare the same field lose their copy, since they now inherit it.

## Arguments

None. The target is the field, by symbol:
`"target": { "symbol": "F:Shop.Manager._name" }`.

## Precondition

- The class has a base class, declared in the solution.
- The field's type and initializer use nothing the base class cannot see:
  no other member or nested type of the subclass, and no type parameter of
  the subclass that the base class does not share.
- Neither the base class nor any class above it has a member of that name.
- Every other subclass of the base that declares a member of that name
  declares the same field: a direct subclass, with the same type, initializer
  and static-ness. Anything else would hide the pulled-up field.
- The result compiles.

## Transformation

- The field goes after the last field of the base class, or first when it
  has none, keeping its documentation comment, initializer and other
  modifiers.
- A private field becomes protected, so the subclass can still use it.
- A declaration of several fields is split, and only the target moves.
- The subclass's type parameters are written as the base class's: a field
  `List<T>` in `Repository<T> : Store<T>` becomes `List<TItem>` in
  `Store<TItem>`.
- The base class's file gains the usings the field's type and initializer
  need.
- Identical fields in the other subclasses are removed.

## Preserved

- Every use of the field, in the subclass and elsewhere, which now binds to
  the inherited field.
- Comments around the field that belong to its neighbours.

## Limitations

- A field that other code reaches through a sibling's differently-accessible
  copy is refused by the compile check rather than reconciled.
- Usings the subclass's file no longer needs are left in place.

## Error codes

| Code | Meaning |
|---|---|
| `no-base-class` | the class has no base class other than `object` |
| `base-not-in-source` | the base class is not declared in the solution |
| `member-exists-in-base` | the base class or a class above it has a member of that name |
| `conflicts-with-sibling` | another subclass declares a different member of that name |
| `uses-subclass-members` | the field's type or initializer uses something only the subclass has |
| `breaks-compilation` | the result would not compile |
