using System;
using System.Collections.Generic;
using Shop.Rules;

namespace Shop;

public sealed class Order : IValidatable
{
    public DateTime Placed { get; init; }

    public IEnumerable<string> Validate()
    {
        yield return "ok";
    }
}
