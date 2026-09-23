using System;

namespace Shop;

public sealed record Sku(string Code)
{
    public bool Equals(Sku other) => other is not null && string.Equals(Code, other.Code, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Code);

    public override string ToString() => Code;
}
