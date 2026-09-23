# Replace Method with Method Object

Turns a long method whose locals get in the way of Extract Method into an
object of its own. The instance, each parameter and each local become fields
of a new class, so the body can then be split into methods that share them
without passing them around. The old method creates one of these objects for
each call and runs it.

## Recipe

The plan's recipe, "Create Type, Convert Local to Field and move parameters
to fields, Move Instance Method", cannot be carried out with primitives: no
primitive turns a parameter into a field set by a constructor, or makes the
old method create a new object on each call. The recipe that the primitives
can carry out holds the object in a field of the old class instead:

1. `create-type` the class: `{ "name": "PriceCalculation", "file": "PriceCalculation.cs" }`.
2. `introduce-field` of the class on the method's class, holding a new instance:
   `"target": { "symbol": "T:Shop.Order" }, "arguments": { "type": "PriceCalculation", "name": "_priceCalculation" }`.
3. `move-instance-method` the method through it:
   `"target": { "symbol": "M:Shop.Order.Price(System.Int32,System.Decimal)" }, "arguments": { "via": "_priceCalculation" }`.
4. `convert-local-to-field` each local of the moved method:
   `"target": { "symbol": "M:Shop.PriceCalculation.Price(Shop.Order,System.Int32,System.Decimal)" }, "arguments": { "local": "basePrice" }`.

This preserves the behaviour of calls that do not overlap, but every call on
one instance shares the object, so recursive or concurrent calls share its
fields, and the parameters stay parameters. The dedicated implementation does
what the refactoring intends instead, and its cases describe that result
rather than the recipe's.

## Arguments

| Argument | Required | Meaning |
|---|---|---|
| `name` | yes | The new class's name |
| `method` | no | The name of the new class's method that runs the body; defaults to `Compute` |
| `file` | no | The new class's file, relative to the solution; defaults to `<name>.cs` beside the method's file |

The target is the method, by symbol.

## Precondition

- The method has one body: it is not abstract, extern or partial.
- Neither the method nor any class containing it is generic.
- An instance method belongs to a class, not a struct, whose copy the object
  would otherwise work on.
- No parameter is `ref`, `out` or `in`.
- The body does not call through `base`, and uses no protected member.
- No type has the new class's name in the namespace, and its file does not
  exist.
- No two parameters or converted locals share a name, no other name the body
  declares, such as a lambda parameter, is the name of one of the new fields,
  and no parameter has the name the constructor gives the instance.

## Transformation

- The new class is in the method's namespace, public when the method's class
  is, internal otherwise. It has, in order:
  - a `private readonly` field for the instance, named `_` and the class name
    in camel case, when the body uses the instance;
  - a `private` field for each parameter, named `_` and the parameter's name,
    `readonly` unless the body assigns the parameter;
  - a `private` field for each local declared by a plain declaration
    statement of the method (not `const`, `using` or `ref`, and not captured
    by a lambda or local function), nullable in a nullable context when its
    type is a reference type, since it holds nothing until the body sets it;
  - a public constructor taking the instance and the parameters, assigning
    each field;
  - a public method with the old return type, `async` when the old method
    was, holding the old body, block or expression.
- In the body, parameters and converted locals become their fields,
  declarations of converted locals become assignments (or disappear when they
  have no value, leaving their comments), `this` and the instance's members go
  through the instance field (`_order._discountRate`), and static members of
  the old class are qualified by it (`Tree.Deeper`). A recursive call reaches
  the old method through the instance, creating a new object.
- The old method keeps its signature and documentation, loses `async`, and
  its body becomes `return new PriceCalculation(this, quantity, itemPrice).Compute();`,
  without `return` for a `void` method, or the same expression for an
  expression-bodied one.
- Private members of the old class the body uses become `internal`, and the
  old file drops usings only the body needed.

## Preserved

- The result and effects of every call: each call gets its own object, so
  calls never share the fields.
- Comments inside the body, which move with it.

## Limitations

- Private members become `internal` rather than being passed in.
- Locals declared by `for`, `foreach`, `using`, patterns and `out` variables,
  and locals a lambda captures, stay locals.
- An `async` or iterator method's body runs in the new method, so the old
  method returns the new method's task or sequence directly.

## Error codes

| Code | Meaning |
|---|---|
| `type-already-exists` | a type has the new class's name |
| `partial-method` | the method is partial |
| `method-type-parameter` | the method is generic |
| `generic-type` | the method's class, or a class containing it, is generic |
| `not-a-class` | an instance method belongs to a struct |
| `by-reference-parameter` | a parameter is `ref`, `out` or `in` |
| `uses-base` | the body calls through `base` |
| `uses-protected-member` | the body uses a protected member |
| `repeated-local-name` | two parameters or locals to become fields share a name |
| `name-conflict` | a name the body declares would hide one of the new fields |
| `instance-parameter-conflict` | a parameter already has the name the constructor gives the instance |
