using System.Collections.Generic;

namespace Shop;

public sealed class Sku
{
    private Sku(string code)
    {
        Code = code;
    }

    public static Sku Parse(string code) => new Sku(code);

    public string Code { get; }

    public static List<Sku> Defaults() => new() { Sku.Parse("A1"), Sku.Parse("B2") };
}
