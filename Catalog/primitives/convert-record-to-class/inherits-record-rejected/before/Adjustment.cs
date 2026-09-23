namespace Shop;

public abstract record Adjustment(decimal Amount);

public sealed record Discount(decimal Amount, string Code) : Adjustment(Amount);
