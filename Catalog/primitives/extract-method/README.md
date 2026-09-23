# Extract Method

Moves a run of statements out of a method body into a new private method, and
replaces them with a call to it.

## Precondition

- The selection lies inside a block-bodied method and covers at least one
  whole statement. Statements the selection touches are extracted whole.
- No local declared inside the selection is used after it. Such a local would
  be left without a declaration.

## Transformation

- The new method is placed after the containing method, with the given name,
  and is `private`. It is `static` when the containing method is.
- Parameters and locals the statements read are passed as parameters, in the
  order they are first used.
- When the statements return from the containing method on some paths, the new
  method returns a nullable result and the call site returns it when present,
  otherwise execution continues after the call.

## Preserved

- The behaviour of the containing method on every path.
- Comments and blank lines inside the extracted statements.

## Limitations

- Expression-bodied methods are refused rather than converted to a block body.
- Locals assigned inside the selection and read afterwards are not returned as
  out values; the extraction is refused instead.
- Extraction from constructors, accessors and local functions is not covered.
- The extracted method is never made `static`, so extracting from a static
  member produces code that does not compile (`from-static-method`).
- A comment above the first selected statement moves into the new method, and
  the blank line after the selection is dropped (`preserves-comments`).

## Error codes

| Code | Meaning |
|---|---|
| `expression-bodied-member` | the selection is in an expression-bodied method |
| `declared-local-used-after` | the selection declares a local used after it |
| `not-in-method` | the selection is not inside a method |
| `no-statements-selected` | the selection covers no whole statement |
