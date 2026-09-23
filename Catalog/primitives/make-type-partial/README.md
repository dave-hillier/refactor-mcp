# Make Type Partial

Adds the `partial` modifier to a class, struct, record or interface, so its
members can be spread over several declarations.

## Arguments

None. The target is the type, by symbol.

## Precondition

- The type is a class, struct, record, record struct or interface. Enums and
  delegates cannot be partial.
- The type is not already partial.

## Transformation

- `partial` is added as the last modifier, immediately before the `class`,
  `struct`, `record` or `interface` keyword, where C# requires it.
- A declaration with no modifiers gets `partial` in front of its keyword,
  after its attributes, documentation comment and any comment above it.
- A containing type is left alone: a nested type can be partial on its own.

## Preserved

- The type's meaning: a single partial declaration compiles to the same type.
- Attributes, comments, documentation, type parameters and constraints.

## Limitations

- No new part is created; adding members in another file is a separate step,
  such as Move Member to Another Partial File.

## Error codes

| Code | Meaning |
|---|---|
| `already-partial` | the type is already declared partial |
| `unsupported-type-kind` | the type is an enum or a delegate |
