using System.Collections.Generic;

namespace Shop;

public sealed class Sku
{
    public Sku(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public static List<Sku> Defaults() => new() { new("A1"), new("B2") };
}
