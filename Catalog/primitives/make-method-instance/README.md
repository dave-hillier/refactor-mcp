# Make Method Instance

The reverse of Make Method Static with `pass: instance`: a static method that
takes an instance of its own type becomes an instance method on that
parameter.

## Arguments

| Argument | Meaning |
|---|---|
| `target.symbol` | the method, such as `M:Shop.Order.Discounted(Shop.Order,System.Decimal)` |
| `parameter` | optional: the parameter to become the instance (default: the first of the method's own type) |

## Precondition

- The method is static and has a parameter of its own containing type, passed
  by value. An extension method's `this` parameter does not count.
- The method does not assign that parameter.
- Every call passes the parameter explicitly and not as `null` or `default`,
  since calling an instance method on null throws before the method runs.
- The method is not used as a method group.

## Transformation

- `static` is removed and the parameter is dropped.
- `order.Member` becomes `Member`, or `this.Member` where a local or parameter
  of the same name would otherwise hide it. Other uses of the parameter become
  `this`.
- Each call `Order.Discounted(order, rate)` becomes `order.Discounted(rate)`.
  A receiver that does not bind tightly enough is parenthesized:
  `(current ?? new Order()).Label("Now: ")`. A call passing `this` becomes an
  unqualified call.

## Preserved

- The result of every call, and the method's comments and documentation.

## Limitations

- The instance argument is now evaluated before the other arguments. When it
  was not the first parameter and the arguments have side effects, their order
  changes.

## Error codes

| Code | Meaning |
|---|---|
| `not-static` | the method is not static |
| `no-parameter-of-type` | no parameter has the method's own type |
| `by-reference-parameter` | the parameter is `ref`, `in` or `out` |
| `parameter-assigned` | the method assigns the parameter |
| `null-argument` | a call passes `null` or `default` for the parameter |
| `method-group` | the method is used as a method group |
