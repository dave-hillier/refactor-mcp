namespace Shop;

public sealed class Sku
{
    public Sku(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public override bool Equals(object obj) => obj is Sku other && other.Code == Code;

    public override int GetHashCode() => Code.GetHashCode();
}
