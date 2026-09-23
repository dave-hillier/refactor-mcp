using System;

namespace Shop;

public sealed class Sku : IEquatable<Sku>
{
    public Sku(string code)
    {
        Code = code;
    }

    public string Code { get; init; }

    public bool Equals(Sku other) => other is not null && string.Equals(Code, other.Code, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Code);

    public override string ToString() => Code;

    public void Deconstruct(out string code)
    {
        code = Code;
    }

    public override bool Equals(object obj) => Equals(obj as Sku);

    public static bool operator ==(Sku left, Sku right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Sku left, Sku right) => !(left == right);
}
