namespace Shop;

public sealed record Discount(decimal Amount, string Code) : Adjustment(Amount);
