namespace Shop;

public sealed class Sku
{
    public Sku(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public string Label() => $"Item {this}";
}
