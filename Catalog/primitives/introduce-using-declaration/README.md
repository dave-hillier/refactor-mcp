# Introduce Using Declaration

Turns a `using` statement whose block runs to the end of its enclosing block
into a C# 8 using declaration followed by the block's statements:

```csharp
using (var reader = new StreamReader(path))
{
    return reader.ReadToEnd();
}
```

becomes

```csharp
using var reader = new StreamReader(path);
return reader.ReadToEnd();
```

## Target

The `using` statement, by a caret anywhere in it. Usings stacked without
braces between them, `using (a) using (b) { ... }`, are converted together.

## Precondition

- The project's language version is C# 8 or later.
- The `using` declares its resources, `using (var x = ...)`, rather than
  disposing an expression.
- It is the last statement of a block, so the resources are disposed at the
  same point afterwards. It is not directly in a switch section, nor the body
  of another statement without braces, where a declaration is not allowed.
- Neither the resources nor the locals declared directly in its block share a
  name with a local declared elsewhere in the enclosing block, whose scope
  they would now overlap.

## Transformation

- Each `using (declaration)` becomes `using declaration;`, keeping `await` and
  the declaration's type and every declarator.
- The statements of the innermost block follow, moved one level out.

## Preserved

- Behaviour: the resources are disposed at the end of the enclosing block, in
  reverse order, as before.
- Comments above the `using`, after its header and inside its block.

## Limitations

- A using statement followed by other statements is refused rather than
  wrapped in a block of its own.
- Comments on the block's own braces are not kept.
- The refactoring changes one statement, so there are no references in other
  files to update.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-using-statement` | the caret is not on a `using` statement |
| `no-variable` | the `using` disposes an expression and declares no variable |
| `not-last-statement` | statements follow the `using` in its block and would run before disposal |
| `in-switch-section` | the `using` is directly in a switch section |
| `not-in-block` | the `using` is the body of another statement without braces |
| `name-conflict` | a moved name is already declared elsewhere in the enclosing block |
| `language-version` | the project's language version is before C# 8 |
