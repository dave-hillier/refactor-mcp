# Use Named Arguments

Names the arguments of one call after the parameters they are passed for.

## Arguments

None. The target is a caret anywhere in the call: on the method's name, in its
argument list, or on `new` for an object creation. With calls nested inside
each other, the innermost call around the caret is the one changed.

## Precondition

- The caret is inside a method call, an object creation or a constructor
  initializer that binds to a single method.
- The call has at least one argument that is not named yet and is not passed
  to a params array.
- With the names written out, the call still binds to the same method. Named
  arguments can make another overload applicable, which would make the call
  ambiguous or change which method it calls.
- The result compiles.

## Transformation

- Each positional argument gets `name:` in front, where `name` is the
  parameter it is passed for.
- Arguments already named are left as they are, in the order written.
- Arguments passed to a params array in expanded form stay positional, since
  they cannot be named; the named arguments before them are in position, which
  C# allows.
- The receiver of an extension method called in reduced form is not an
  argument and is left alone.

## Preserved

- Which method the call binds to, and the value passed for each parameter.
  Arguments stay in the order they were written, so they are evaluated in the
  same order.
- Layout, and comments around each argument. A comment before an argument
  stays before it, ahead of the new name.

## Limitations

- Only the one call at the caret changes; there is no option to name the
  arguments of every call of a method.
- Indexer arguments and attribute arguments are not covered.

## Error codes

| Code | Meaning |
|---|---|
| `no-call-at-caret` | the caret is not inside a call |
| `no-arguments` | the call has no arguments |
| `already-named` | every argument that can be named already is |
| `changes-overload` | with names, the call would bind to another method or none |
