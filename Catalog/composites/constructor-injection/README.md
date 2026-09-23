# Constructor Injection

Turns an object a method constructs for itself into a dependency the class
receives through its constructor and keeps in a field. Every construction of
the class passes a new instance, built as the method built it.

The tool is `inject-constructor-dependency`. The older
`convert-to-constructor-injection` tool moves a method parameter to the
constructor instead; callers lose the value they passed, so it does not
preserve behaviour and is not this refactoring.

## Recipe

1. `change-signature` on the constructor, adding a parameter of the
   dependency's type whose `value` at each construction is the object the
   method constructed.
2. `introduce-field` on the class, adding a field of the dependency's type.
3. `initialize-field-from-constructor-parameter` on the constructor, with the
   new `field` and `parameter`.
4. `make-field-readonly` on the field.
5. `replace-expression-with-field` on the method, replacing the local's
   construction with the field. Every construction of the class now passes
   the same construction for the parameter the field is assigned from.
6. `inline-local-variable` on the local, which now holds the field.

```json
"steps": [
  {
    "refactoring": "change-signature",
    "target": { "symbol": "M:Shop.OrderService.#ctor(System.String)" },
    "arguments": { "parameters": [ { "name": "prefix" }, { "name": "mailer", "type": "Mailer", "value": "new Mail.Mailer(\"smtp.example.com\")" } ] }
  },
  { "refactoring": "introduce-field", "target": { "symbol": "T:Shop.OrderService" }, "arguments": { "type": "Mailer", "name": "_mailer" } },
  { "refactoring": "initialize-field-from-constructor-parameter", "target": { "symbol": "M:Shop.OrderService.#ctor(System.String,Shop.Mail.Mailer)" }, "arguments": { "field": "_mailer", "parameter": "mailer" } },
  { "refactoring": "make-field-readonly", "target": { "symbol": "F:Shop.OrderService._mailer" } },
  { "refactoring": "replace-expression-with-field", "target": { "symbol": "M:Shop.OrderService.Place(System.String)" }, "arguments": { "expression": "new Mailer(\"smtp.example.com\")", "field": "_mailer" } },
  { "refactoring": "inline-local-variable", "target": { "symbol": "M:Shop.OrderService.Place(System.String)" }, "arguments": { "local": "mailer" } }
]
```

The plan's recipe has no step making the field readonly; Replace Expression
with Field needs it, so that only the constructor sets the field. The recipe
covers a class with one constructor; the dedicated implementation also gives
a class without one a constructor, and does every step as one change.

## Arguments

| Argument | Meaning |
|---|---|
| `parameter` | the constructor parameter's name; the local's name when left out |
| `field` | the field's name; the local's name with a leading underscore when left out |

The target is the local holding the constructed object, by a caret on its
declaration or any use, or in a later step by the method's symbol with
`arguments.local` naming it.

## Precondition

- The local is declared on its own, in a block of an instance method of a
  class without a primary constructor, and initialised with `new` and no
  object initializer. It is never assigned again.
- The construction's arguments read no local, parameter or instance member
  and have no side effects, so every caller of the constructor can pass the
  same construction.
- The class has at most one instance constructor, and it does not call
  another with `this(...)`.
- The class has no member with the field's name, and the constructor no
  parameter with the parameter's name.
- The result compiles: a derived class that relies on the constructor without
  passing the dependency is refused.

## Transformation

- A `private readonly` field of the local's type is added after the class's
  other fields.
- The constructor takes the dependency as a parameter after its required
  parameters and assigns it to the field at its end. A class without a
  constructor of its own gets one after the field, `public`, or `protected`
  for an abstract class.
- Every construction of the class, including `base(...)` calls and
  target-typed `new`, passes the construction the method made, its type
  qualified as each file needs.
- The method uses the field wherever it used the local, and the declaration
  is removed; comments above it stay in place.

## Preserved

- What the method does with the object, as long as the object behaves the
  same when it is shared.

## Limitations

- One object now serves every call of the method on an instance, where each
  call used to construct its own. That is the point of the refactoring, but
  an object that keeps state between uses behaves differently, and this is
  not checked.
- The object is constructed when the class is, rather than when the method
  runs, so a constructor with side effects runs earlier and even when the
  method is never called.
- Classes with several constructors are refused rather than choosing one.

## Error codes

| Code | Meaning |
|---|---|
| `not-a-construction` | the local is not initialised by constructing an object |
| `not-in-instance-method` | the local is not in an instance method of a class without a primary constructor |
| `not-declared-alone` | the local is not declared on its own in a block |
| `assigned-after-declaration` | the local is assigned after its declaration |
| `argument-depends-on-method` | an argument of the construction reads the method's state |
| `object-initializer` | the object is constructed with an initializer |
| `several-constructors` | the class has several constructors |
| `chained-constructor` | the constructor calls another with `this(...)` |
| `name-conflict` | the class already has a member with the field's name |
| `duplicate-parameter` | the constructor already has a parameter with the parameter's name |
| `not-a-local` | the target is not a local variable |
